/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapTextureResolver.cs
 * author:    云毅
 * created:   2026
 * descrip:   复刻 Unity MapAssetLoader.FindMapTextureByLayer 的三级目录命名查找
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 贴图命名关联：Unity 里物件的 <c>Name</c> 就是贴图资源名，按
/// 「本关目录 → 章节 Shared → 全局 Shared」三级依次找 <c>&lt;层目录&gt;/&lt;Name&gt;.png</c>，
/// 最后再兜旧版单数目录（Building / Character / Prop / Cat）。
/// <para>本工具烘焙出的目录与 Unity 完全同名同位，所以存档里 <c>image</c> 为空、
/// 或只写了资源名（Unity 导出的 JSON 就是这样）时，也能按这套规则找到图。</para>
/// </summary>
public static class MapTextureResolver
{
    #region 常量

    /// <summary>Unity 贴图根（相对工程根）。</summary>
    private const string TextureRootSuffix = "Assets/Res/Textures/Map";

    /// <summary>旧版单数目录名：Buildings → Building、Characters → Character / Cat、Props → Prop，其余同名。</summary>
    /// <param name="folder">八层目录名。</param>
    /// <returns>兜底目录名数组。</returns>
    private static string[] LegacyFolders(string folder) => folder switch
    {
        "Buildings" => new[] { "Building" },
        "Characters" => new[] { "Character", "Cat" },
        "Props" => new[] { "Prop" },
        _ => new[] { folder },
    };

    #endregion

    #region 查找

    /// <summary>
    /// 按「资源名 + 层键 + 章节 / 关卡」在 Unity 贴图目录里找 PNG。
    /// </summary>
    /// <param name="projectRoot">Unity 工程根（<c>resRoots.txt</c> 里的 <c>project=</c>）。</param>
    /// <param name="name">资源名（物件的 Name，可带扩展名或相对路径）。</param>
    /// <param name="layerKey">层键（八层任意一个，旧键会归一）。</param>
    /// <param name="chapterId">章节 ID（取数字部分，可为空）。</param>
    /// <param name="levelId">关卡 ID（取数字部分，可为空）。</param>
    /// <returns>命中的绝对路径；找不到返回 null。</returns>
    public static string? Resolve(string? projectRoot, string? name, string? layerKey, string? chapterId, string? levelId)
    {
        var stem = Stem(name);
        if (stem.Length == 0 || string.IsNullOrWhiteSpace(projectRoot))
        {
            return null;
        }

        var root = Path.Combine(projectRoot, TextureRootSuffix.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root))
        {
            return null;
        }

        var chapter = NumericOr(chapterId);
        var level = NumericOr(levelId);
        var folder = MapLayerKeys.FolderName(LayerOrder.Normalize(layerKey));

        // 与 Unity 同序：本关 → 章节 Shared → 全局 Shared
        foreach (var candidate in Candidates(root, chapter, level, folder, stem, legacy: false))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 旧版单数目录兜底（Unity 用 Building / Character / Prop / Cat）
        foreach (var candidate in Candidates(root, chapter, level, folder, stem, legacy: true))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>按「关卡 / 章节 Shared / 全局 Shared」拼候选路径。</summary>
    private static System.Collections.Generic.IEnumerable<string> Candidates(
        string root, string chapter, string level, string folder, string stem, bool legacy)
    {
        var folders = new[] { folder };
        if (legacy)
        {
            folders = LegacyFolders(folder);
        }

        foreach (var dir in folders)
        {
            if (chapter.Length > 0 && level.Length > 0)
            {
                yield return Path.Combine(root, $"Chapter{chapter}", $"Level{level}", dir, stem + ".png");
            }

            if (chapter.Length > 0)
            {
                yield return Path.Combine(root, $"Chapter{chapter}", "Shared", dir, stem + ".png");
            }

            yield return Path.Combine(root, "Shared", dir, stem + ".png");
        }
    }

    #endregion

    #region 辅助

    /// <summary>取资源名主干：去目录、去扩展名，并把 PSD 口径的空格 / 拷贝 / 组换成文件名口径。</summary>
    /// <param name="name">原始名。</param>
    /// <returns>主干；空值返回空串。</returns>
    private static string Stem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Replace('\\', '/');
        var fileName = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.Length == 0)
        {
            return string.Empty;
        }

        // 用户可能直接抄了 PSD 层名（带空格 / 「拷贝」「组」），这里按 Unity safe_file_name 同口径归一
        return stem.Replace(" ", "_").Replace("拷贝", "copy").Replace("组", "group");
    }

    /// <summary>取标识里的数字串，没有数字返回空串。</summary>
    private static string NumericOr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits;
    }

    #endregion
}
