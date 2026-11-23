/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdSliceBaker.cs
 * author:    云毅
 * created:   2026
 * descrip:   PSD 图层自裁切片：导入时只在内存预览，点「导出切片」才落盘并按像素 + 旋转去重
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 一条自裁切片记录（与 PSD 图层一一对应）。
/// <para>
/// <see cref="Left"/>/<see cref="Top"/>/<see cref="Width"/>/<see cref="Height"/> 是「裁掉多余透明边之后」的
/// 最终画布矩形（已经叠进 <see cref="SourceLeft"/>/<see cref="SourceTop"/> 的偏移），导入时直接用这组值定位实例；
/// 原始图层框留在 <see cref="SourceLeft"/>…<see cref="SourceHeight"/> 里做核对。
/// </para>
/// <para>
/// <see cref="Rotation"/> 是「本层像素 = 复用文件顺时针转多少度」：0 表示同向，90 / 180 / 270 表示
/// 该层是已有切片旋转后的同一张图，落盘只留一份，实例用旋转字段还原。
/// </para>
/// </summary>
public sealed record PsdSliceRecord(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("layer")] string Layer = MapLayerKeys.Prop,
    [property: JsonPropertyName("band")] int Band = LayerBands.Standing,
    [property: JsonPropertyName("sourceLeft")] int SourceLeft = 0,
    [property: JsonPropertyName("sourceTop")] int SourceTop = 0,
    [property: JsonPropertyName("sourceWidth")] int SourceWidth = 0,
    [property: JsonPropertyName("sourceHeight")] int SourceHeight = 0,
    [property: JsonPropertyName("trimmed")] bool Trimmed = false,
    [property: JsonPropertyName("rotation")] int Rotation = 0);

/// <summary>切片集合：既用于「导出切片」落盘结果，也用于导入时的内存预览。</summary>
public sealed class PsdSliceSet
{
    /// <summary>图层序号 → 切片路径（预览时是「将来会落盘的绝对路径」，磁盘上并不存在）。</summary>
    public Dictionary<int, string> ByIndex { get; } = new();

    /// <summary>图层序号 → 切片记录（含分层、裁剪后的画布矩形、复用旋转与源框）。</summary>
    public Dictionary<int, PsdSliceRecord> Records { get; } = new();

    /// <summary>图层序号 → PNG 字节（只有导入预览会填，导出落盘时为空以省内存）。</summary>
    public Dictionary<int, byte[]> PngByIndex { get; } = new();

    /// <summary>整幅底图（导出成功或预览合成时非空）。</summary>
    public string? CompositePath { get; set; }

    /// <summary>整幅底图的 PNG 字节（导入预览用）。</summary>
    public byte[]? CompositePng { get; set; }

    /// <summary>底图像素宽（预览用）。</summary>
    public int CompositeWidth { get; set; }

    /// <summary>底图像素高（预览用）。</summary>
    public int CompositeHeight { get; set; }

    /// <summary>本次新烘焙的切片数。</summary>
    public int BakedCount { get; set; }

    /// <summary>命中缓存的切片数。</summary>
    public int CachedCount { get; set; }

    /// <summary>像素解码失败（压缩方式不支持等）的图层数。</summary>
    public int FailedCount { get; set; }

    /// <summary>整层全透明（裁完无像素）而跳过的图层数。</summary>
    public int SkippedCount { get; set; }

    /// <summary>与已出图切片像素完全一致（含旋转后一致）而复用的图层数。</summary>
    public int DuplicateCount { get; set; }

    /// <summary>其中靠旋转 90 / 180 / 270 判定为同一张图的图层数。</summary>
    public int RotationDuplicateCount { get; set; }

    /// <summary>本轮清掉的旧烘焙残留 PNG 数（旧清单里有、本轮不再保留的文件）。</summary>
    public int PrunedCount { get; set; }

    /// <summary>是否直接复用了上次的缓存清单（只有导出才可能命中）。</summary>
    public bool FromCache { get; set; }

    /// <summary>是否为「只进内存、不落盘」的导入预览。</summary>
    public bool PreviewOnly { get; set; }

    /// <summary>切片输出目录（预览时是目标目录，未创建）。</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>按图层序号取切片路径；没有自裁切片返回 null。</summary>
    /// <param name="index">图层序号。</param>
    /// <returns>切片绝对路径或 null。</param>
    public string? PathFor(int index) => ByIndex.TryGetValue(index, out var path) ? path : null;

    /// <summary>按图层序号取切片记录（分层 / 裁剪矩形）；没有返回 null。</summary>
    /// <param name="index">图层序号。</param>
    /// <returns>切片记录或 null。</returns>
    public PsdSliceRecord? RecordFor(int index) => Records.TryGetValue(index, out var record) ? record : null;
}

/// <summary>切片清单缓存文件（导出结果 + 判断「旧文件是不是本工具生成的」）。</summary>
internal sealed record PsdSliceManifest(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("length")] long Length,
    [property: JsonPropertyName("ticks")] long Ticks,
    [property: JsonPropertyName("slices")] List<PsdSliceRecord> Slices,
    [property: JsonPropertyName("version")] int Version = 0);

/// <summary>
/// 自裁切片烘焙器。Unity 侧 <c>psd_import.py</c> 按「图层名」复用切片，把同名不同尺寸的层
/// 全部指向同一张 PNG（如 <c>bg_road_7</c> 11 块 356×299 路面共用了 929×124 的胶囊），
/// 这里改为「按图层自己的像素与框裁切」。
/// <para>
/// 导入 PSD 时只调用 <see cref="BuildPreview"/>：逐层解码、裁透明边、按像素去重，产出 PNG 字节供画布直接显示，
/// 不建目录、不写文件。只有点「导出切片」才调用 <see cref="EnsureSlices"/> 真正落盘。
/// </para>
/// </summary>
public static class PsdSliceBaker
{
    #region 常量

    private const string ManifestFileName = "slices.json";

    /// <summary>整幅底图的文件名模板：Unity 侧 <c>bg_{PSD 名}.png</c>（1.psb → <c>bg_1.png</c>）。</summary>
    private const string CompositeNamePrefix = "bg_";

    /// <summary>底图烘焙时间戳文件名（与底图同目录）。</summary>
    private const string CompositeStampFileName = "bg_composite.json";

    /// <summary>
    /// 清单版本：5 = 八层目录 + 像素去重（含旋转 90/180/270）+ 裁透明边 + 导出时才落盘。
    /// 旧清单一律重烘，避免读到没有 rotation 字段的老记录。
    /// </summary>
    private const int ManifestVersion = 5;

    /// <summary>
    /// 八层资源目录：与 Unity <c>MapLayerCatalog.GetLayerDir</c> 的目录名逐字一致，
    /// 目录顺序按 <see cref="MapLayerKeys.AllKeys"/>（自底向上）。
    /// </summary>
    private static readonly string[] LayerFolders =
    {
        "Background", "Greenery", "Road", "Buildings", "Props", "Characters", "Foreground", "Bubbles",
    };

    #endregion

    #region 公开方法

    /// <summary>
    /// 导入预览：逐层自裁并去重（含旋转判定），把结果编码成 PNG 字节留在内存，<b>不建目录、不写文件</b>。
    /// 画布直接拿 <see cref="PsdSliceSet.PngByIndex"/> 出图，点「导出切片」时才真正落盘。
    /// </summary>
    /// <param name="document">已解析的 PSD 文档。</param>
    /// <param name="psdPath">PSD 源文件绝对路径。</param>
    /// <param name="levelDirectory">导出时会用到的关卡目录（这里只用于拼虚拟路径，不创建）。</param>
    /// <param name="resolveLayerKey">图层 → 八层键；返回 null 表示并入整幅底图。</param>
    /// <returns>只含内存 PNG 的切片集合。</returns>
    public static PsdSliceSet BuildPreview(
        PsdDocument document,
        string psdPath,
        string levelDirectory,
        Func<PsdLayerInfo, string?>? resolveLayerKey = null)
    {
        var set = new PsdSliceSet { Directory = levelDirectory, PreviewOnly = true };

        // 判重与落盘共用同一套账：尺寸对得上才去算旋转，绝大多数图直接走 0° 指纹
        var written = new Dictionary<string, WrittenSlice>(StringComparer.Ordinal);
        var bySize = new Dictionary<SizeKey, List<string>>();
        var usedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 预览时同一张图的 PNG 字节只编码一次，旋转复用的图层共用这份字节
        var pngByRelative = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in document.Layers)
        {
            if (layer.IsGroup || !layer.HasBounds)
            {
                continue;
            }

            var resolvedLayerKey = resolveLayerKey?.Invoke(layer);
            if (resolveLayerKey is not null && resolvedLayerKey is null)
            {
                set.SkippedCount++;
                continue;
            }

            var pixels = PsdReader.ReadLayerPixels(psdPath, layer, document.IsPsb);
            if (pixels is null)
            {
                set.FailedCount++;
                continue;
            }

            var trim = ComputeTrim(pixels.Rgba, pixels.Width, pixels.Height);
            if (trim.Width <= 0 || trim.Height <= 0)
            {
                set.SkippedCount++;
                continue;
            }

            var rgba = trim.IsWholeLayer(pixels.Width, pixels.Height)
                ? pixels.Rgba
                : CropPixels(pixels.Rgba, pixels.Width, trim.X, trim.Y, trim.Width, trim.Height);

            var layerKey = resolvedLayerKey ?? MapLayerKeys.Prop;

            var match = ResolveDuplicate(rgba, trim.Width, trim.Height, written, bySize);
            string relative;
            byte[] png;
            int rotation;
            if (match.HasValue)
            {
                relative = match.Value.Relative;
                png = pngByRelative[relative];
                rotation = match.Value.Rotation;
                set.DuplicateCount++;
                if (rotation != 0)
                {
                    set.RotationDuplicateCount++;
                }
            }
            else
            {
                var folder = MapLayerKeys.FolderName(layerKey);
                var fileName = BuildFileName(layer, folder, string.Empty, usedNames);
                relative = folder + "/" + fileName;
                png = PngWriter.EncodeRgba(trim.Width, trim.Height, rgba);
                rotation = 0;
                var hash0 = HashPixels(rgba, trim.Width, trim.Height);
                written[relative] = new WrittenSlice(relative, hash0);
                AddSize(bySize, trim.Width, trim.Height, relative);
                pngByRelative[relative] = png;
            }

            var record = new PsdSliceRecord(layer.Index, layer.Name, layer.GroupPath,
                layer.Left + trim.X, layer.Top + trim.Y, trim.Width, trim.Height, layer.Visible, relative,
                layerKey, LayerBands.Of(layerKey, layer.Name), layer.Left, layer.Top, pixels.Width, pixels.Height,
                !trim.IsWholeLayer(pixels.Width, pixels.Height), rotation);
            set.Records[layer.Index] = record;
            set.ByIndex[layer.Index] = Path.Combine(levelDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            set.PngByIndex[layer.Index] = png;
        }

        return set;
    }

    /// <summary>
    /// 导出切片：把预览过的图层真正写到
    /// <c>{资源根}\Chapter{章节}\Level{关卡}\&lt;八层目录&gt;\&lt;层名&gt;.png</c>，
    /// 相同像素（含旋转 90 / 180 / 270 后一致）只保留一张图，并写 <c>slices.json</c> 清单。
    /// </summary>
    /// <param name="document">已解析的 PSD 文档。</param>
    /// <param name="psdPath">PSD 源文件绝对路径。</param>
    /// <param name="levelDirectory">关卡资源根目录（<c>Chapter{n}\Level{n}</c>）。</param>
    /// <param name="resolveLayerKey">
    /// 图层 → 八层键的判定函数；返回 <c>null</c> 表示该层并入整幅底图、不出切片。
    /// </param>
    /// <param name="force">true 时忽略上次的 <c>slices.json</c> 缓存，强制重烘（「导出切片」按钮用 true）。</param>
    /// <returns>切片集合（含落盘统计与旋转去重数）。</returns>
    public static PsdSliceSet EnsureSlices(
        PsdDocument document,
        string psdPath,
        string levelDirectory,
        Func<PsdLayerInfo, string?>? resolveLayerKey = null,
        bool force = false)
    {
        var set = new PsdSliceSet { Directory = levelDirectory };
        Directory.CreateDirectory(levelDirectory);

        // 八层目录始终建出来：本张画稿没有前景 / 气泡层时也保留空目录，
        // 资源目录结构固定，下游（Unity 导入脚本 / 美术）不用猜「这一层是不是漏导了」
        foreach (var folder in LayerFolders)
        {
            Directory.CreateDirectory(Path.Combine(levelDirectory, folder));
        }

        var manifestPath = Path.Combine(levelDirectory, ManifestFileName);
        if (!force)
        {
            var cached = TryReadManifest(manifestPath, psdPath);
            if (cached is not null)
            {
                foreach (var record in cached.Slices)
                {
                    var path = Path.Combine(levelDirectory, record.File);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    set.ByIndex[record.Index] = path;
                    set.Records[record.Index] = record;
                    set.CachedCount++;
                }

                if (set.ByIndex.Count == cached.Slices.Count)
                {
                    set.FromCache = true;
                    var distinctFiles = cached.Slices.Select(record => record.File)
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    set.DuplicateCount = cached.Slices.Count - distinctFiles;
                    set.RotationDuplicateCount = cached.Slices.Count(record => record.Rotation != 0);
                    TryReuseComposite(set, levelDirectory, psdPath);
                    return set;
                }

                set.ByIndex.Clear();
                set.Records.Clear();
                set.CachedCount = 0;
                set.DuplicateCount = 0;
                set.RotationDuplicateCount = 0;
            }
        }

        // 旧清单里的文件：导出结束后清理「本轮不再保留」的残留，只删本工具自己产出的
        var previousFiles = TryReadAnyManifest(manifestPath)?.Slices
            .Select(record => record.File.Replace('/', Path.DirectorySeparatorChar))
            .ToList() ?? new List<string>();

        var records = new List<PsdSliceRecord>();
        var written = new Dictionary<string, WrittenSlice>(StringComparer.Ordinal);
        var bySize = new Dictionary<SizeKey, List<string>>();
        var usedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in document.Layers)
        {
            if (layer.IsGroup || !layer.HasBounds)
            {
                continue;
            }

            var resolvedLayerKey = resolveLayerKey?.Invoke(layer);
            if (resolveLayerKey is not null && resolvedLayerKey is null)
            {
                set.SkippedCount++;
                continue;
            }

            var pixels = PsdReader.ReadLayerPixels(psdPath, layer, document.IsPsb);
            if (pixels is null)
            {
                set.FailedCount++;
                continue;
            }

            var trim = ComputeTrim(pixels.Rgba, pixels.Width, pixels.Height);
            if (trim.Width <= 0 || trim.Height <= 0)
            {
                set.SkippedCount++;
                continue;
            }

            var rgba = trim.IsWholeLayer(pixels.Width, pixels.Height)
                ? pixels.Rgba
                : CropPixels(pixels.Rgba, pixels.Width, trim.X, trim.Y, trim.Width, trim.Height);

            var layerKey = resolvedLayerKey ?? MapLayerKeys.Prop;
            var match = ResolveDuplicate(rgba, trim.Width, trim.Height, written, bySize);
            string relative;
            int rotation;
            if (match.HasValue)
            {
                relative = match.Value.Relative;
                rotation = match.Value.Rotation;
                set.DuplicateCount++;
                if (rotation != 0)
                {
                    set.RotationDuplicateCount++;
                }
            }
            else
            {
                var folder = MapLayerKeys.FolderName(layerKey);
                var fileName = BuildFileName(layer, folder, string.Empty, usedNames);
                relative = folder + "/" + fileName;
                var layerDirectory = Path.Combine(levelDirectory, folder);
                Directory.CreateDirectory(layerDirectory);

                var target = Path.Combine(layerDirectory, fileName);
                // 导出按 force 走：同一轮里这张文件名还没被本层占用，直接写（同名旧图会被覆盖），
                // 保证点一次「导出切片」磁盘上就是当前 PSD 的像素
                PngWriter.WriteRgba(target, trim.Width, trim.Height, rgba);
                set.BakedCount++;

                var hash0 = HashPixels(rgba, trim.Width, trim.Height);
                written[relative] = new WrittenSlice(relative, hash0);
                AddSize(bySize, trim.Width, trim.Height, relative);
                rotation = 0;
            }

            var record = new PsdSliceRecord(layer.Index, layer.Name, layer.GroupPath,
                layer.Left + trim.X, layer.Top + trim.Y, trim.Width, trim.Height, layer.Visible, relative,
                layerKey, LayerBands.Of(layerKey, layer.Name), layer.Left, layer.Top, pixels.Width, pixels.Height,
                !trim.IsWholeLayer(pixels.Width, pixels.Height), rotation);
            records.Add(record);
            set.Records[layer.Index] = record;
            set.ByIndex[layer.Index] = Path.Combine(levelDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        WriteManifest(manifestPath, psdPath, records);
        set.PrunedCount = PruneStaleSlices(levelDirectory, records, previousFiles);
        return set;
    }

    /// <summary>
    /// 把指定图层合成为一张整幅底图（对应 Unity 侧 <c>psd.composite()</c> 烘焙的
    /// <c>Background\bg_{PSD 名}.png</c>，1.psb → <c>bg_1.png</c>）。图层像素自带变换，按 (left, top) 叠加即可。
    /// </summary>
    /// <param name="document">PSD 文档（提供画布尺寸）。</param>
    /// <param name="psdPath">PSD 源文件绝对路径（文件名用于底图命名）。</param>
    /// <param name="layers">参与合成的图层（自底向上顺序）。</param>
    /// <param name="levelDirectory">关卡资源根目录（<c>Chapter{n}\Level{n}</c>）。</param>
    /// <param name="force">为 true 时忽略已有底图强制重烘焙。</param>
    /// <returns>底图绝对路径；没有任何可用像素时返回 null。</returns>
    public static string? BakeComposite(
        PsdDocument document,
        string psdPath,
        IReadOnlyList<PsdLayerInfo> layers,
        string levelDirectory,
        bool force = false)
    {
        if (document.Width <= 0 || document.Height <= 0 || layers.Count == 0)
        {
            return null;
        }

        var backgroundDirectory = Path.Combine(levelDirectory, MapLayerKeys.FolderName(MapLayerKeys.Background));
        Directory.CreateDirectory(backgroundDirectory);

        var backgroundName = CompositeNamePrefix + Path.GetFileNameWithoutExtension(psdPath);
        var path = Path.Combine(backgroundDirectory, backgroundName + ".png");
        var stampPath = Path.Combine(backgroundDirectory, CompositeStampFileName);

        if (!force && File.Exists(path) && IsCompositeFresh(stampPath, psdPath, layers.Count))
        {
            return path;
        }

        var composite = BuildCompositeRgba(document, psdPath, layers);
        if (composite is null)
        {
            return null;
        }

        PngWriter.WriteRgba(path, composite.Value.Width, composite.Value.Height, composite.Value.Rgba);

        var stamp = JsonSerializer.Serialize(new CompositeStamp(psdPath, new FileInfo(psdPath).Length,
            File.GetLastWriteTimeUtc(psdPath).Ticks, layers.Count, composite.Value.Painted));
        File.WriteAllText(stampPath, stamp, new UTF8Encoding(false));
        return path;
    }

    /// <summary>导入预览用：把底图合成结果编码成 PNG 字节（不落盘）。</summary>
    /// <param name="document">PSD 文档。</param>
    /// <param name="psdPath">PSD 源文件绝对路径。</param>
    /// <param name="layers">参与合成的图层。</param>
    /// <returns>PNG 字节与画布尺寸；没有可用像素时返回 null。</returns>
    public static (byte[] Png, int Width, int Height)? BuildCompositePng(
        PsdDocument document,
        string psdPath,
        IReadOnlyList<PsdLayerInfo> layers)
    {
        var composite = BuildCompositeRgba(document, psdPath, layers);
        if (composite is null)
        {
            return null;
        }

        return (PngWriter.EncodeRgba(composite.Value.Width, composite.Value.Height, composite.Value.Rgba),
            composite.Value.Width, composite.Value.Height);
    }

    #endregion

    #region 去重

    /// <summary>已写 / 已生成过的一张切片：路径 + 0° 像素指纹。</summary>
    private readonly record struct WrittenSlice(string Relative, string Hash0);

    /// <summary>尺寸键（宽高对），用于「只有尺寸对得上才去算旋转」。</summary>
    private readonly record struct SizeKey(int Width, int Height);

    /// <summary>一次去重命中的结果。</summary>
    private readonly record struct DuplicateMatch(string Relative, int Rotation);

    private static void AddSize(Dictionary<SizeKey, List<string>> bySize, int width, int height, string relative)
    {
        var key = new SizeKey(width, height);
        if (!bySize.TryGetValue(key, out var list))
        {
            list = new List<string>();
            bySize[key] = list;
        }

        list.Add(relative);
    }

    /// <summary>
    /// 判重：先按 0° 指纹查同尺寸候选项；只有尺寸能对上（含宽高互换）才去算 90 / 180 / 270 的指纹，
    /// 命中即复用同一张 PNG，并记录「本层相对复用文件转多少度」。
    /// <para>
    /// 大多数唯一图只做一次哈希；同尺寸（如所有 84×205 的道具）也只在「候选集合」内多做三次旋转哈希，
    /// 不会把 578 层全部旋转四遍。
    /// </para>
    /// </summary>
    private static DuplicateMatch? ResolveDuplicate(
        byte[] rgba,
        int width,
        int height,
        IReadOnlyDictionary<string, WrittenSlice> written,
        IReadOnlyDictionary<SizeKey, List<string>> bySize)
    {
        var hash0 = HashPixels(rgba, width, height);
        var sameSize = bySize.TryGetValue(new SizeKey(width, height), out var straight) ? straight : null;
        var swappedSize = bySize.TryGetValue(new SizeKey(height, width), out var swapped) ? swapped : null;
        if (sameSize is null && swappedSize is null)
        {
            return null;
        }

        // 0° 同尺寸：最常见，直接比指纹
        foreach (var relative in sameSize ?? Enumerable.Empty<string>())
        {
            if (string.Equals(written[relative].Hash0, hash0, StringComparison.Ordinal))
            {
                return new DuplicateMatch(relative, 0);
            }
        }

        // 旋转候选：把本层转 90 / 180 / 270 再比指纹（尺寸互换只可能来自 90 / 270）
        var hashes = new[] { hash0, null, null, null };
        for (int turn = 1; turn < 4; turn++)
        {
            if ((turn == 1 || turn == 3) && swappedSize is null)
            {
                continue;
            }

            if (turn == 2 && sameSize is null)
            {
                continue;
            }

            var rotated = RotatePixels(rgba, width, height, turn);
            hashes[turn] = HashPixels(rotated.Rgba, rotated.Width, rotated.Height);
        }

        var candidates = new List<string>();
        if (sameSize is not null)
        {
            candidates.AddRange(sameSize);
        }

        if (swappedSize is not null)
        {
            candidates.AddRange(swappedSize);
        }

        foreach (var relative in candidates)
        {
            var slice = written[relative];
            for (int turn = 1; turn < 4; turn++)
            {
                if (hashes[turn] is null || !string.Equals(slice.Hash0, hashes[turn], StringComparison.Ordinal))
                {
                    continue;
                }

                // hashes[turn] 是「把本层转 turn 个 90° 后得到已落盘那张图」，
                // 反过来说：显示时要拿已落盘的图转 (4 - turn) 个 90° 才是本层的样子
                int rotation = ((4 - turn) % 4) * 90;
                return new DuplicateMatch(relative, rotation);
            }
        }

        return null;
    }

    /// <summary>
    /// 顺时针旋转 RGBA 缓冲：turn=1 → 90°，2 → 180°，3 → 270°（图像坐标系 y 向下，与引擎顺时针一致）。
    /// </summary>
    private static (byte[] Rgba, int Width, int Height) RotatePixels(byte[] rgba, int width, int height, int turn)
    {
        turn %= 4;
        if (turn == 0)
        {
            return (rgba, width, height);
        }

        if (turn == 2)
        {
            var flipped = new byte[rgba.Length];
            int pixels = width * height;
            for (int i = 0; i < pixels; i++)
            {
                int source = i * 4;
                int target = (pixels - 1 - i) * 4;
                flipped[target] = rgba[source];
                flipped[target + 1] = rgba[source + 1];
                flipped[target + 2] = rgba[source + 2];
                flipped[target + 3] = rgba[source + 3];
            }

            return (flipped, width, height);
        }

        int newWidth = height;
        int newHeight = width;
        var result = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int source = (y * width + x) * 4;
                // 顺时针 90°：(x, y) → (height - 1 - y, x)
                int targetX = turn == 1 ? height - 1 - y : y;
                int targetY = turn == 1 ? x : width - 1 - x;
                int target = (targetY * newWidth + targetX) * 4;
                result[target] = rgba[source];
                result[target + 1] = rgba[source + 1];
                result[target + 2] = rgba[source + 2];
                result[target + 3] = rgba[source + 3];
            }
        }

        return (result, newWidth, newHeight);
    }

    /// <summary>像素指纹：宽 + 高 + RGBA 逐字节的 SHA-256。</summary>
    private static string HashPixels(byte[] rgba, int width, int height)
    {
        var payload = new byte[8 + rgba.Length];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), width);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), height);
        Buffer.BlockCopy(rgba, 0, payload, 8, rgba.Length);
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    #endregion

    #region 内部实现

    private static (byte[] Rgba, int Width, int Height, int Painted)? BuildCompositeRgba(
        PsdDocument document,
        string psdPath,
        IReadOnlyList<PsdLayerInfo> layers)
    {
        if (document.Width <= 0 || document.Height <= 0 || layers.Count == 0)
        {
            return null;
        }

        int width = document.Width;
        int height = document.Height;
        var canvas = new byte[width * height * 4];
        int painted = 0;

        foreach (var layer in layers)
        {
            var pixels = PsdReader.ReadLayerPixels(psdPath, layer, document.IsPsb);
            if (pixels is null)
            {
                continue;
            }

            painted++;
            var source = pixels.Rgba;

            // 源图可能比画布大且带负偏移（整幅底图常见 -75/-78），取「源坐标 ∩ 画布」的有效行列
            int rowStart = Math.Max(0, -layer.Top);
            int rowEnd = Math.Min(pixels.Height, height - layer.Top);
            int columnStart = Math.Max(0, -layer.Left);
            int columnEnd = Math.Min(pixels.Width, width - layer.Left);

            for (int row = rowStart; row < rowEnd; row++)
            {
                int targetY = layer.Top + row;
                int sourceRow = row * pixels.Width * 4;

                for (int column = columnStart; column < columnEnd; column++)
                {
                    int targetX = layer.Left + column;
                    int s = sourceRow + column * 4;
                    int t = (targetY * width + targetX) * 4;
                    int alpha = source[s + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    if (alpha == 255)
                    {
                        canvas[t] = source[s];
                        canvas[t + 1] = source[s + 1];
                        canvas[t + 2] = source[s + 2];
                        canvas[t + 3] = 255;
                        continue;
                    }

                    // 源覆盖目标（straight alpha，浮点精度，避免多图层叠加累积灰阶误差）
                    float sourceAlpha = alpha / 255f;
                    float dstAlpha = canvas[t + 3] / 255f;
                    float outAlpha = sourceAlpha + dstAlpha * (1f - sourceAlpha);
                    if (outAlpha <= 0f)
                    {
                        continue;
                    }

                    canvas[t] = ToByte(Blend(source[s], canvas[t], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 1] = ToByte(Blend(source[s + 1], canvas[t + 1], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 2] = ToByte(Blend(source[s + 2], canvas[t + 2], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 3] = ToByte(outAlpha * 255f);
                }
            }
        }

        return painted == 0 ? null : (canvas, width, height, painted);
    }

    private static float Blend(byte source, byte target, float sourceAlpha, float dstAlpha, float outAlpha)
    {
        // src 覆盖在 dst 之上：结果 = (src × α + dst × dstA × (1-α)) / outA
        return (source * sourceAlpha + target * dstAlpha * (1f - sourceAlpha)) / outAlpha;
    }

    private static byte ToByte(float value)
    {
        if (value <= 0f)
        {
            return 0;
        }

        return value >= 255f ? (byte)255 : (byte)(value + 0.5f);
    }

    private static bool IsCompositeFresh(string stampPath, string psdPath, int layerCount)
    {
        try
        {
            if (!File.Exists(stampPath))
            {
                return false;
            }

            var stamp = JsonSerializer.Deserialize<CompositeStamp>(File.ReadAllText(stampPath));
            return stamp is not null
                && string.Equals(stamp.Source, psdPath, StringComparison.OrdinalIgnoreCase)
                && stamp.LayerCount == layerCount
                && stamp.Ticks == File.GetLastWriteTimeUtc(psdPath).Ticks;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryReuseComposite(PsdSliceSet set, string levelDirectory, string psdPath)
    {
        var backgroundDirectory = Path.Combine(levelDirectory, MapLayerKeys.FolderName(MapLayerKeys.Background));
        var path = Path.Combine(backgroundDirectory, CompositeNamePrefix + Path.GetFileNameWithoutExtension(psdPath) + ".png");
        if (File.Exists(path))
        {
            set.CompositePath = path;
        }
    }

    /// <summary>
    /// 切片文件名：沿用 Unity <c>psd_import.py</c> 的 <c>safe_file_name</c> 口径（空格换下划线、
    /// 「拷贝」换 copy、「组」换 group），扩展名 <c>.png</c>。同目录内同名但像素不同的图层
    /// 追加 <c>_{left}_{top}</c> 后缀，与 Unity「同名文件兜底」规则一致。
    /// </summary>
    private static string BuildFileName(
        PsdLayerInfo layer,
        string folder,
        string contentKey,
        IDictionary<string, string> usedNames)
    {
        var safeName = SafeName(layer.Name);
        var key = folder + "/" + safeName;

        // 同名但像素不同的图层（例如横 / 竖两种 bg_rock_1）不能共用同一个文件名：
        // 以前第二轮会直接覆盖第一轮，JSON 里两种尺寸都指向磁盘上最后写出的那张，
        // 画布只能把竖图塞进横框，看起来就是「拉伸变形 / 图不对」。
        // 像素完全相同的图层在 ResolveDuplicate 阶段已经复用，不会走到这里。
        if (usedNames.ContainsKey(key))
        {
            var candidate = $"{safeName}_{layer.Left}_{layer.Top}";
            var suffix = 2;
            while (usedNames.ContainsKey(folder + "/" + candidate))
            {
                candidate = $"{safeName}_{layer.Left}_{layer.Top}_{suffix++}";
            }

            safeName = candidate;
        }

        usedNames[folder + "/" + safeName] = contentKey;
        return safeName + ".png";
    }

    /// <summary>
    /// 清掉上一轮导出留下的残留：只删除「旧清单里出现过、本轮不再保留」的 PNG，
    /// 其它美术文件一律不动。旧清单就是本工具自己的账本，误删风险为零。
    /// </summary>
    /// <returns>删除的文件数。</returns>
    private static int PruneStaleSlices(string outDirectory, IReadOnlyList<PsdSliceRecord> records, IReadOnlyList<string> previousFiles)
    {
        if (previousFiles.Count == 0)
        {
            return 0;
        }

        var keep = new HashSet<string>(
            records.Select(record => record.File.Replace('/', Path.DirectorySeparatorChar)),
            StringComparer.OrdinalIgnoreCase);

        var removed = 0;
        foreach (var relative in previousFiles)
        {
            if (keep.Contains(relative))
            {
                continue;
            }

            var full = Path.Combine(outDirectory, relative);
            if (!File.Exists(full))
            {
                continue;
            }

            try
            {
                File.Delete(full);
                removed++;
            }
            catch (IOException)
            {
                // 被别的进程占用就留着，下次导出再清
            }
        }

        return removed;
    }

    /// <summary>求 alpha 非零像素的包围盒（扫描行的先决条件：只要还有未命中的行就继续找）。</summary>
    private static TrimBox ComputeTrim(byte[] rgba, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            var row = y * width * 4;
            int rowMin = -1, rowMax = -1;
            for (int x = 0; x < width; x++)
            {
                if (rgba[row + x * 4 + 3] == 0)
                {
                    continue;
                }

                if (rowMin < 0)
                {
                    rowMin = x;
                }

                rowMax = x;
            }

            if (rowMin < 0)
            {
                continue;
            }

            if (rowMin < minX)
            {
                minX = rowMin;
            }

            if (rowMax > maxX)
            {
                maxX = rowMax;
            }

            if (y < minY)
            {
                minY = y;
            }

            maxY = y;
        }

        if (maxX < 0)
        {
            return new TrimBox(0, 0, 0, 0);
        }

        return new TrimBox(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>按包围盒裁出新的 RGBA 缓冲。</summary>
    private static byte[] CropPixels(byte[] rgba, int sourceWidth, int x, int y, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
        {
            var sourceOffset = ((y + row) * sourceWidth + x) * 4;
            Buffer.BlockCopy(rgba, sourceOffset, result, row * width * 4, width * 4);
        }

        return result;
    }

    /// <summary>
    /// 层名清洗：与 Unity <c>psd_import.py → safe_file_name</c> 同一口径——
    /// 空格换下划线、「拷贝」换 copy、「组」换 group，再兜掉文件名非法字符。
    /// </summary>
    private static string SafeName(string name)
    {
        var unityLike = (name ?? "layer")
            .Replace(" ", "_")
            .Replace("拷贝", "copy")
            .Replace("组", "group");

        var builder = new StringBuilder(unityLike.Length);
        foreach (var ch in unityLike)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
        }

        var text = builder.ToString().Trim();
        return text.Length == 0 ? "layer" : text;
    }

    /// <summary>
    /// 读关卡 <c>slices.json</c> 里记录的 PSD 源路径与导出时的文件戳（长度 + UTC 修改时间）。
    /// 「更新地图」在重启后没有内存里的 PSD 文档时，靠这份基线判断源文件是否被改过。
    /// </summary>
    /// <param name="levelDirectory">关卡资源根目录（<c>Chapter{n}\Level{n}</c>）。</param>
    /// <param name="sourcePath">清单记录的 PSD / PSB 绝对路径。</param>
    /// <param name="length">导出时源文件长度。</param>
    /// <param name="ticks">导出时源文件 UTC 修改时间 ticks。</param>
    /// <returns>清单存在且记录了三项信息时 true。</returns>
    public static bool TryReadSourceStamp(
        string levelDirectory,
        out string sourcePath,
        out long length,
        out long ticks)
    {
        sourcePath = string.Empty;
        length = 0;
        ticks = 0;

        if (string.IsNullOrWhiteSpace(levelDirectory))
        {
            return false;
        }

        var manifest = TryReadAnyManifest(Path.Combine(levelDirectory, ManifestFileName));
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Source))
        {
            return false;
        }

        sourcePath = manifest.Source;
        length = manifest.Length;
        ticks = manifest.Ticks;
        return true;
    }

    /// <summary>读旧清单（不看版本、不看 PSD 是否变更），只用于清理残留。</summary>
    private static PsdSliceManifest? TryReadAnyManifest(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PsdSliceManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static PsdSliceManifest? TryReadManifest(string manifestPath, string psdPath)
    {
        var manifest = TryReadAnyManifest(manifestPath);
        if (manifest is null || manifest.Slices.Count == 0 || manifest.Version != ManifestVersion)
        {
            return null;
        }

        var info = new FileInfo(psdPath);
        return string.Equals(manifest.Source, psdPath, StringComparison.OrdinalIgnoreCase)
            && manifest.Length == info.Length
            && manifest.Ticks == File.GetLastWriteTimeUtc(psdPath).Ticks
            ? manifest
            : null;
    }

    private static void WriteManifest(string manifestPath, string psdPath, List<PsdSliceRecord> records)
    {
        var info = new FileInfo(psdPath);
        var manifest = new PsdSliceManifest(psdPath, info.Length, File.GetLastWriteTimeUtc(psdPath).Ticks, records, ManifestVersion);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), new UTF8Encoding(false));
    }

    #endregion

    /// <summary>底图烘焙的时间戳记录，PSD 或图层集合变化时自动重烘焙。</summary>
    private sealed record CompositeStamp(string Source, long Length, long Ticks, int LayerCount, int Painted);

    /// <summary>透明边包围盒（像素坐标 + 尺寸）。</summary>
    private readonly record struct TrimBox(int X, int Y, int Width, int Height)
    {
        /// <summary>是否整层都可见像素（没有可裁的透明边）。</summary>
        public bool IsWholeLayer(int sourceWidth, int sourceHeight) =>
            X == 0 && Y == 0 && Width == sourceWidth && Height == sourceHeight;
    }
}
