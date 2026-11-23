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
/// 自裁切片烘焙器。Unity 侧 <c>psd_import.py</c> 按「图层名」复用切片，把同名不同尺寸的层
/// 全部指向同一张 PNG（如 <c>bg_road_7</c> 11 块 356×299 路面共用了 929×124 的胶囊），
/// 这里改为「按图层自己的像素与框裁切」。
/// <para>
/// 导入 PSD 时只调用 <see cref="BuildPreview"/>：逐层解码、裁透明边、按像素去重，产出 PNG 字节供画布直接显示，
/// 不建目录、不写文件。只有点「导出切片」才调用 <see cref="EnsureSlices"/> 真正落盘。
/// </para>
/// </summary>
public static partial class PsdSliceBaker
{
    #region 常量

    private const string ManifestFileName = "slices.json";

    /// <summary>切片统一扩展名。</summary>
    private const string PngExtension = ".png";

    /// <summary>RGBA 通道最大值，同时作为 0–1 浮点与字节互转的标度。</summary>
    private const int MaxChannelValue = 255;

    /// <summary>通道浮点标度，避免多处重复写 255f。</summary>
    private const float ChannelScale = 255f;

    /// <summary>浮点转字节时的四舍五入偏置。</summary>
    private const float RoundingBias = 0.5f;

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
        var path = Path.Combine(backgroundDirectory, backgroundName + PngExtension);
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

}