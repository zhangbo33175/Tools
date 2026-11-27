/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StorySpeakerBook.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情说话人索引：按物件独有的数字 ID 取配置行与切片，供绑定与立绘解析共用
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 剧情可以绑定的地图物件：数字 ID 是物件的唯一标识，图片只是配置里挂的资源，
/// 因此绑定只认 <see cref="NumericId"/>，不再用实例短 ID 或资源名。
/// </summary>
/// <param name="NumericId">数字 ID：配置表行 ID；没有配置行时回退关卡切片序号。</param>
/// <param name="ObjectId">实例短 ID，仅用于识别是不是同一个实例。</param>
/// <param name="Name">物件名，下拉里给人看。</param>
/// <param name="ImagePath">立绘资源路径，来自物件的图片字段。</param>
/// <param name="FromConfig">true = 数字 ID 来自配置表；false = 回退切片序号。</param>
/// <param name="SceneIndex">同模板在场景里的位次，从 1 开始；单实例时为 1。</param>
/// <param name="SceneTotal">同模板实例总数。</param>
public sealed record StorySpeaker(
    string NumericId,
    string ObjectId,
    string Name,
    string ImagePath,
    bool FromConfig,
    int SceneIndex = 1,
    int SceneTotal = 1)
{
    /// <summary>同模板是否摆了多个实例：这时只报数字 ID 就分不清是哪一个人。</summary>
    public bool HasPeers => SceneTotal > 1 && SceneIndex > 0;

    /// <summary>下拉文案：<c>数字 ID-位次 · 物件名</c>，单实例时省略位次。</summary>
    public string Label => NumericId.Length == 0
        ? $"未登记 · {Name}"
        : HasPeers
            ? $"{NumericId}-{SceneIndex:00} · {Name}"
            : $"{NumericId} · {Name}";

    /// <summary>悬停说明：讲清数字 ID 从哪来，以及位次是什么意思。</summary>
    public string Tip => NumericId.Length == 0
        ? "这个物件既没有配置表行、也没有切片序号，先去资源整理页补齐再绑定"
        : (FromConfig
            ? $"数字 ID {NumericId} 来自物件配置表"
            : $"配置表没有这一行，数字 ID {NumericId} 回退到关卡切片序号")
          + (HasPeers ? $"；同模板摆了 {SceneTotal} 个，这是第 {SceneIndex} 个" : string.Empty);
}

/// <summary>关卡切片清单里的一条：文件名 + 左上角 + 序号。</summary>
/// <param name="File">图片相对路径，已统一成正斜杠。</param>
/// <param name="Left">切片左上 X。</param>
/// <param name="Top">切片左上 Y。</param>
/// <param name="Index">切片序号，就是回退的数字 ID。</param>
public readonly record struct StorySliceEntry(string File, double Left, double Top, int Index);

/// <summary>
/// 说话人索引服务。数字 ID 的口径与属性栏「ID」完全一致：配置表行优先，没有则回退切片序号；
/// 两边都没有时为「未登记」，不参与绑定。
/// </summary>
public static class StorySpeakerBook
{
    #region 缓存

    /// <summary>按 slices.json 路径缓存切片清单，文件时间戳变了才重读。</summary>
    private static readonly Dictionary<string, SliceCacheEntry> SliceCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>切片缓存条目。</summary>
    /// <param name="Stamp">文件最后写入时间。</param>
    /// <param name="Entries">切片清单。</param>
    private sealed record SliceCacheEntry(DateTime Stamp, List<StorySliceEntry> Entries);

    #endregion

    #region 建索引

    /// <summary>按当前地图建一份可绑定的角色索引；数字 ID 为空的排在最后，方便界面提示。</summary>
    /// <param name="config">当前地图。</param>
    /// <param name="table">物件配置表，可为空。</param>
    /// <param name="roots">资源搜索根，用于找关卡 <c>slices.json</c>。</param>
    /// <returns>角色索引，按数字 ID 升序。</returns>
    public static IReadOnlyList<StorySpeaker> Build(
        MapConfigData? config,
        ItemConfigSet? table,
        IEnumerable<string>? roots)
    {
        if (config is null)
        {
            return Array.Empty<StorySpeaker>();
        }

        var slices = LoadSlices(roots);
        var characters = config.Objects
            .Where(obj => obj.Type == MapObjectType.Character && !string.IsNullOrWhiteSpace(obj.Name))
            .ToList();

        // 先算出每个实例的数字 ID：位次要按数字 ID 分组，不能按模板 ID，否则不同配置行的同名资源会串组
        var numbered = new List<(PlacedObject Obj, string Num, bool FromConfig)>(characters.Count);
        foreach (var obj in characters)
        {
            var num = NumericIdOf(obj, table, slices, out var fromConfig);
            numbered.Add((obj, num, fromConfig));
        }

        // 组内按实例短 ID 排序定位次：实例还在，位次就不会因为增删别的实例而乱跳
        var slots = new Dictionary<string, SceneSlot>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in numbered.GroupBy(
                     entry => entry.Num.Length == 0 ? "~" + entry.Obj.Name : entry.Num,
                     StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(entry => entry.Obj.Id, StringComparer.Ordinal).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                slots[ordered[i].Obj.Id] = new SceneSlot(i + 1, ordered.Count);
            }
        }

        return numbered
            .Select(entry =>
            {
                var slot = slots.TryGetValue(entry.Obj.Id, out var found) ? found : new SceneSlot(1, 1);
                return new StorySpeaker(
                    entry.Num,
                    entry.Obj.Id,
                    entry.Obj.Name,
                    entry.Obj.ImagePath,
                    entry.FromConfig,
                    slot.Index,
                    slot.Total);
            })
            .OrderBy(speaker => speaker.NumericId.Length == 0 ? "~" + speaker.Name : speaker.NumericId.PadLeft(6, '0'))
            .ThenBy(speaker => speaker.SceneIndex)
            .ToList();
    }

    /// <summary>取一个物件的数字 ID：配置表行 ID 优先，没有则回退切片序号，都没有返回空串。</summary>
    /// <param name="item">物件实例。</param>
    /// <param name="table">物件配置表，可为空。</param>
    /// <param name="slices">关卡切片清单。</param>
    /// <param name="fromConfig">true = 命中配置表行。</param>
    /// <returns>数字 ID；两边都没命中返回空串。</returns>
    public static string NumericIdOf(
        PlacedObject item,
        ItemConfigSet? table,
        IReadOnlyList<StorySliceEntry> slices,
        out bool fromConfig)
    {
        fromConfig = false;

        // 配置表口径：先模板 ID，再图片文件名，与属性栏同序，避免两处对不上号
        foreach (var key in new[] { item.TemplateId, SliceFileName(item.ImagePath) })
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var id = table?.Find(key)?.Id?.Trim() ?? string.Empty;
            if (id.Length > 0 && id.All(char.IsDigit))
            {
                fromConfig = true;
                return id;
            }
        }

        var index = SliceIndexOf(item, slices);
        return index > 0 ? index.ToString() : string.Empty;
    }

    /// <summary>按图片路径 + 左上角找切片序号；同一张图只有一个切片时不再要求坐标。</summary>
    /// <param name="item">物件实例。</param>
    /// <param name="slices">切片清单。</param>
    /// <returns>切片序号；找不到返回 -1。</returns>
    private static int SliceIndexOf(PlacedObject item, IReadOnlyList<StorySliceEntry> slices)
    {
        if (slices.Count == 0)
        {
            return -1;
        }

        var file = NormalizeFile(item.ImagePath);
        if (file.Length == 0)
        {
            return -1;
        }

        foreach (var slice in slices)
        {
            if (string.Equals(slice.File, file, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(slice.Left - item.X) <= 0.5
                && Math.Abs(slice.Top - item.Y) <= 0.5)
            {
                return slice.Index;
            }
        }

        var sameFile = slices
            .Where(slice => string.Equals(slice.File, file, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameFile.Count == 1 ? sameFile[0].Index : -1;
    }

    #endregion

    #region 切片清单

    /// <summary>读取各资源根下的 <c>slices.json</c>；文件变了才重读，找不到就当没有切片。</summary>
    /// <param name="roots">资源搜索根。</param>
    /// <returns>合并后的切片清单。</returns>
    public static IReadOnlyList<StorySliceEntry> LoadSlices(IEnumerable<string>? roots)
    {
        if (roots is null)
        {
            return Array.Empty<StorySliceEntry>();
        }

        var merged = new List<StorySliceEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = System.IO.Path.Combine(root, "slices.json");
            if (!System.IO.File.Exists(path) || !seen.Add(path))
            {
                continue;
            }

            merged.AddRange(ReadSlices(path));
        }

        return merged;
    }

    /// <summary>读一份切片 JSON，按文件时间戳缓存。</summary>
    /// <param name="path">slices.json 路径。</param>
    /// <returns>切片清单。</returns>
    private static List<StorySliceEntry> ReadSlices(string path)
    {
        var stamp = System.IO.File.GetLastWriteTimeUtc(path);
        if (SliceCache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
        {
            return cached.Entries;
        }

        var entries = new List<StorySliceEntry>();
        try
        {
            using var document = JsonDocument.Parse(FileSystemIo.ReadText(path));
            if (document.RootElement.TryGetProperty("slices", out var slices) && slices.ValueKind == JsonValueKind.Array)
            {
                foreach (var slice in slices.EnumerateArray())
                {
                    if (!slice.TryGetProperty("index", out var indexNode)
                        || !slice.TryGetProperty("file", out var fileNode)
                        || !slice.TryGetProperty("left", out var leftNode)
                        || !slice.TryGetProperty("top", out var topNode))
                    {
                        continue;
                    }

                    var index = indexNode.GetInt32();
                    var file = NormalizeFile(fileNode.GetString());
                    if (index > 0 && file.Length > 0)
                    {
                        entries.Add(new StorySliceEntry(file, leftNode.GetDouble(), topNode.GetDouble(), index));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or JsonException or InvalidOperationException or FormatException)
        {
            entries.Clear();
        }

        SliceCache[path] = new SliceCacheEntry(stamp, entries);
        return entries;
    }

    /// <summary>统一切片路径分隔符：JSON 里的反斜杠与模型里的正斜杠要能对上。</summary>
    /// <param name="path">图片路径。</param>
    /// <returns>正斜杠路径。</returns>
    private static string NormalizeFile(string? path) => (path ?? string.Empty).Replace('\\', '/').Trim();

    /// <summary>取图片文件名（不含目录与扩展名），作为配置表的第二个查找键。</summary>
    /// <param name="path">图片路径。</param>
    /// <returns>资源名。</returns>
    private static string SliceFileName(string? path)
    {
        var file = NormalizeFile(path);
        if (file.Length == 0)
        {
            return string.Empty;
        }

        var name = file[(file.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    #endregion
}
