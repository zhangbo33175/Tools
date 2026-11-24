/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteAnimationStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   序列帧动画配置持久化（output/Config/AnimationClips.json）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 动画片段配置仓库。文件格式刻意保持轻量：
/// <c>{ "Version": 1, "Clips": [ { "Name": "chef_wave", "Fps": 10, "Loop": true, "Frames": ["..."] } ] }</c>。
/// 导入时把帧图片复制到 <c>output\Config\Animations\&lt;片段名&gt;</c>，配置里记录相对路径，
/// 地图 JSON 仍只写 <c>anim</c> 名称，避免原目录移动后整段动画失效。
/// </summary>
public sealed class SpriteAnimationStore
{
    #region 字段

    private readonly JsonSerializerOptions _options = JsonFileOptions.Indented;

    private readonly string _configDirectory;

    #endregion

    #region 构造

    /// <summary>初始化仓库。</summary>
    /// <param name="configDirectory">配置目录（通常是 <c>output\Config</c>）。</param>
    public SpriteAnimationStore(string configDirectory)
    {
        _configDirectory = configDirectory;
        RootDirectory = System.IO.Path.Combine(configDirectory, "Animations");
        Path = System.IO.Path.Combine(configDirectory, "AnimationClips.json");
    }

    #endregion

    #region 属性

    /// <summary>动画配置文件路径。</summary>
    public string Path { get; }

    /// <summary>导入帧图片的根目录。</summary>
    public string RootDirectory { get; }

    #endregion

    #region 公开方法

    /// <summary>读取动画目录；文件缺失或损坏时返回空目录，不阻塞编辑器启动。</summary>
    /// <returns>动画目录。</returns>
    public SpriteAnimationCatalog Load()
    {
        var catalog = new SpriteAnimationCatalog();
        try
        {
            if (!File.Exists(Path))
            {
                return catalog;
            }

            var payload = JsonSerializer.Deserialize<AnimationClipsPayload>(
                FileSystemIo.ReadText(Path),
                _options);
            if (payload?.Clips is null)
            {
                return catalog;
            }

            foreach (var entry in payload.Clips)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Name) || entry.Frames is null)
                {
                    continue;
                }

                catalog.Register(new SpriteAnimationClip
                {
                    Name = entry.Name.Trim(),
                    Fps = Math.Clamp(entry.Fps, 0.1, 120),
                    Loop = entry.Loop,
                    Frames = entry.Frames.Select(Resolve).Where(path => path.Length > 0).ToList(),
                });
            }
        }
        catch (Exception)
        {
            // 配置损坏时按「没有动画」处理，画布继续显示静态贴图
        }

        return catalog;
    }

    /// <summary>
    /// 把用户目录里的序列帧复制进配置目录，按文件名顺序编号落盘。
    /// 复制后的绝对路径直接可用于画布播放，配置保存时会自动压成相对路径。
    /// </summary>
    /// <param name="frames">按播放顺序排列的源帧路径。</param>
    /// <param name="clipName">片段名，用于生成子目录。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>复制后的帧路径；失败返回空列表。</returns>
    public IReadOnlyList<string> ImportFrames(IReadOnlyList<string> frames, string clipName, out string error)
    {
        error = string.Empty;
        if (frames.Count == 0)
        {
            error = "序列帧目录里没有可导入的图片。";
            return Array.Empty<string>();
        }

        try
        {
            var folderName = EnsureUniqueFolderName(SanitizeName(clipName));
            var targetDirectory = System.IO.Path.Combine(RootDirectory, folderName);
            FileSystemIo.EnsureDirectory(targetDirectory);

            var imported = new List<string>(frames.Count);
            for (var index = 0; index < frames.Count; index++)
            {
                var source = frames[index];
                if (!File.Exists(source))
                {
                    error = $"序列帧文件不存在：{source}";
                    FileSystemIo.TryDeleteDirectory(targetDirectory);
                    return Array.Empty<string>();
                }

                var extension = System.IO.Path.GetExtension(source);
                if (!ImageFileTypes.IsSupported(source))
                {
                    extension = ".png";
                }

                var target = System.IO.Path.Combine(
                    targetDirectory,
                    $"frame_{index:0000}{extension.ToLowerInvariant()}");
                File.Copy(source, target, true);
                imported.Add(target);
            }

            return imported;
        }
        catch (Exception exception)
        {
            error = $"序列帧导入失败：{exception.Message}";
            return Array.Empty<string>();
        }
    }

    /// <summary>把整个动画目录写回配置文件。</summary>
    /// <param name="catalog">动画目录。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否保存成功。</returns>
    public bool TrySave(SpriteAnimationCatalog catalog, out string error)
    {
        try
        {
            FileSystemIo.EnsureParentDirectory(Path);

            var payload = new AnimationClipsPayload
            {
                Version = 1,
                Clips = new List<AnimationClipPayload>(),
            };
            foreach (var clip in catalog.Clips)
            {
                payload.Clips.Add(new AnimationClipPayload
                {
                    Name = clip.Name,
                    Fps = clip.Fps,
                    Loop = clip.Loop,
                    Frames = clip.Frames.Select(Relative).ToList(),
                });
            }

            FileSystemIo.WriteText(
                Path,
                JsonSerializer.Serialize(payload, _options));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>清理非法文件名字符。</summary>
    /// <param name="name">原始名字。</param>
    /// <returns>安全名字。</returns>
    private static string SanitizeName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Animation" : sanitized.Trim();
    }

    /// <summary>重名时追加序号，避免覆盖已导入的帧目录。</summary>
    /// <param name="baseName">基础名字。</param>
    /// <returns>唯一目录名。</returns>
    private string EnsureUniqueFolderName(string baseName)
    {
        if (!Directory.Exists(System.IO.Path.Combine(RootDirectory, baseName)))
        {
            return baseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseName}_{index}";
            if (!Directory.Exists(System.IO.Path.Combine(RootDirectory, candidate)))
            {
                return candidate;
            }
        }

        return $"{baseName}_{Guid.NewGuid():N}";
    }

    /// <summary>把绝对路径压成相对配置目录的路径。</summary>
    /// <param name="path">绝对路径。</param>
    /// <returns>相对路径；配置目录外保留绝对路径。</returns>
    private string Relative(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetFullPath(_configDirectory);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetRelativePath(root, full)
            : full;
    }

    /// <summary>把配置里的相对路径解析为绝对路径。</summary>
    /// <param name="path">相对或绝对路径。</param>
    /// <returns>绝对路径；空输入返回空串。</returns>
    private string Resolve(string path) => FileSystemIo.ResolvePath(_configDirectory, path);

    #endregion

    #region 载荷类型

    private sealed class AnimationClipsPayload
    {
        public int Version { get; set; } = 1;

        public List<AnimationClipPayload> Clips { get; set; } = new();
    }

    private sealed class AnimationClipPayload
    {
        public string Name { get; set; } = string.Empty;

        public double Fps { get; set; } = 10;

        public bool Loop { get; set; } = true;

        public List<string> Frames { get; set; } = new();
    }

    #endregion
}
