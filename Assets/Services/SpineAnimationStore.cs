/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineAnimationStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   Spine 动画库的导入、落盘与重新加载
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MapEditorTool.Services;

#region 持久化

/// <summary>
/// Spine 动画库。导入时把骨架、Atlas 与页贴图复制到
/// <c>output\Config\Spine\&lt;资产名&gt;</c>，再用旁路 JSON
/// <c>SpineAnimations.json</c> 记录相对路径，避免工程移动后失效。
/// </summary>
public sealed class SpineAnimationStore
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

    /// <summary>Spine 导出器常见的 Atlas 文件名后缀（正文也可以是 .atlas.txt）。</summary>
    private static readonly string[] AtlasExtensions = { ".atlas", ".atlas.txt" };

    private readonly string _configDirectory;

    /// <summary>创建存储。</summary>
    /// <param name="configDirectory">配置输出目录，通常是 <c>output\Config</c>。</param>
    public SpineAnimationStore(string configDirectory)
    {
        _configDirectory = configDirectory;
        RootDirectory = Path.Combine(configDirectory, "Spine");
        ConfigPath = Path.Combine(configDirectory, "SpineAnimations.json");
    }

    /// <summary>Spine 资产根目录。</summary>
    public string RootDirectory { get; }

    /// <summary>旁路配置路径。</summary>
    public string ConfigPath { get; }

    /// <summary>读取全部已登记资产；坏条目跳过并保留可诊断信息。</summary>
    /// <param name="catalog">目标目录。</param>
    /// <returns>加载失败的条目数量。</returns>
    public int LoadInto(SpineAnimationCatalog catalog)
    {
        catalog.Clear();
        if (!File.Exists(ConfigPath))
        {
            return 0;
        }

        SpineAnimationFilePayload? payload;
        try
        {
            var json = File.ReadAllText(ConfigPath);
            payload = JsonSerializer.Deserialize<SpineAnimationFilePayload>(json);
        }
        catch
        {
            return 0;
        }

        var failures = 0;
        foreach (var entry in payload?.Entries ?? new List<SpineAnimationEntryPayload>())
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var skeletonFile = Resolve(entry.SkeletonFile);
            var atlasFile = Resolve(entry.AtlasFile);
            var asset = SpineAnimationLoader.Load(entry.Name, skeletonFile, atlasFile, entry.DefaultAnimation, out _);
            if (asset == null)
            {
                failures++;
                continue;
            }

            catalog.Register(asset);
        }

        return failures;
    }

    /// <summary>把当前目录写回旁路 JSON。</summary>
    /// <param name="catalog">资产目录。</param>
    public void Save(SpineAnimationCatalog catalog)
    {
        Directory.CreateDirectory(_configDirectory);
        var payload = new SpineAnimationFilePayload
        {
            Version = 1,
            Entries = catalog.Assets.Select(asset => new SpineAnimationEntryPayload
            {
                Name = asset.Name,
                SkeletonFile = Relative(asset.SkeletonFile),
                AtlasFile = Relative(asset.AtlasFile),
                DefaultAnimation = asset.DefaultAnimation,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// 导入一组 Spine 文件：骨架 + 同名 Atlas，页贴图从 Atlas 目录一起复制。
    /// 不覆盖已存在资产，重名自动追加 _2/_3。
    /// </summary>
    /// <param name="skeletonPath">用户选择的 .json/.skel。</param>
    /// <param name="atlasPath">用户选择的 .atlas；为空时按同名自动查找。</param>
    /// <param name="assetName">资产名；为空时用文件名。</param>
    /// <param name="defaultAnimation">默认动画名。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>导入后的资产；失败返回 null。</returns>
    public SpineAnimationAsset? Import(
        string skeletonPath,
        string? atlasPath,
        string? assetName,
        string? defaultAnimation,
        out string error)
    {
        error = string.Empty;
        try
        {
            if (!File.Exists(skeletonPath))
            {
                error = $"骨架文件不存在：{skeletonPath}";
                return null;
            }

            var skeletonDirectory = Path.GetDirectoryName(skeletonPath) ?? string.Empty;
            var resolvedAtlas = ResolveAtlas(skeletonPath, atlasPath);
            if (resolvedAtlas == null)
            {
                error = "没有找到同名 .atlas / .atlas.txt；请把 Atlas 与骨架放在同一目录，或单独选择 Atlas。";
                return null;
            }

            var baseName = string.IsNullOrWhiteSpace(assetName)
                ? Path.GetFileNameWithoutExtension(skeletonPath)
                : assetName.Trim();
            var safeName = SanitizeName(baseName);
            var targetName = EnsureUniqueName(safeName);
            var targetDirectory = Path.Combine(RootDirectory, targetName);
            Directory.CreateDirectory(targetDirectory);

            var copiedSkeleton = Path.Combine(targetDirectory, Path.GetFileName(skeletonPath));
            File.Copy(skeletonPath, copiedSkeleton, true);

            var atlasDirectory = Path.GetDirectoryName(resolvedAtlas) ?? skeletonDirectory;
            var copiedAtlas = Path.Combine(targetDirectory, Path.GetFileName(resolvedAtlas));
            File.Copy(resolvedAtlas, copiedAtlas, true);

            foreach (var image in Directory.EnumerateFiles(atlasDirectory))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(image), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(image, Path.Combine(targetDirectory, Path.GetFileName(image)), true);
            }

            var asset = SpineAnimationLoader.Load(
                targetName,
                copiedSkeleton,
                copiedAtlas,
                defaultAnimation ?? string.Empty,
                out error);
            if (asset == null)
            {
                TryDeleteDirectory(targetDirectory);
            }

            return asset;
        }
        catch (Exception ex)
        {
            error = $"Spine 导入失败：{ex.Message}";
            return null;
        }
    }

    /// <summary>按同名规则查找 Atlas（兼容 Spine 导出的 .atlas 与 .atlas.txt）。</summary>
    /// <param name="skeletonPath">骨架文件。</param>
    /// <param name="atlasPath">显式 Atlas。</param>
    /// <returns>Atlas 路径；找不到返回 null。</returns>
    private static string? ResolveAtlas(string skeletonPath, string? atlasPath)
    {
        if (!string.IsNullOrWhiteSpace(atlasPath) && File.Exists(atlasPath))
        {
            return atlasPath;
        }

        var directory = Path.GetDirectoryName(skeletonPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(skeletonPath);
        foreach (var extension in AtlasExtensions)
        {
            var sameStem = Path.Combine(directory, stem + extension);
            if (File.Exists(sameStem))
            {
                return sameStem;
            }
        }

        return Directory.EnumerateFiles(directory, "*.atlas*")
            .Where(path => AtlasExtensions.Any(extension =>
                path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>清理非法文件名字符。</summary>
    /// <param name="name">原始名字。</param>
    /// <returns>安全名字。</param>
    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "SpineAsset" : sanitized.Trim();
    }

    /// <summary>重名时追加序号，避免覆盖用户已经导入的资产。</summary>
    /// <param name="baseName">基础名字。</param>
    /// <returns>唯一名字。</param>
    private string EnsureUniqueName(string baseName)
    {
        if (!Directory.Exists(Path.Combine(RootDirectory, baseName)))
        {
            return baseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseName}_{index}";
            if (!Directory.Exists(Path.Combine(RootDirectory, candidate)))
            {
                return candidate;
            }
        }

        return $"{baseName}_{Guid.NewGuid():N}";
    }

    /// <summary>把绝对路径压成相对配置目录的路径。</summary>
    /// <param name="path">绝对路径。</param>
    /// <returns>相对路径。</returns>
    private string Relative(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(_configDirectory);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, full)
            : full;
    }

    /// <summary>把配置里的相对路径解析为绝对路径。</summary>
    /// <param name="path">相对或绝对路径。</param>
    /// <returns>绝对路径。</returns>
    private string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_configDirectory, path));
    }

    /// <summary>删除导入失败留下的空目录。</summary>
    /// <param name="directory">目录。</param>
    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
            // 清理失败不覆盖原始导入错误
        }
    }
}

#endregion

#region 配置模型

internal sealed class SpineAnimationFilePayload
{
    public int Version { get; set; } = 1;

    public List<SpineAnimationEntryPayload> Entries { get; set; } = new();
}

internal sealed class SpineAnimationEntryPayload
{
    public string Name { get; set; } = string.Empty;

    public string SkeletonFile { get; set; } = string.Empty;

    public string AtlasFile { get; set; } = string.Empty;

    public string DefaultAnimation { get; set; } = string.Empty;
}

#endregion
