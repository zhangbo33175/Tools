/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdSliceBaker.Composite.cs
 * author:    云毅
 * created:   2026
 * descrip:   底图合成与像素处理：source-over 合成、透明边裁剪、旋转与安全命名
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
public static partial class PsdSliceBaker
{
    #region 底图合成与像素处理

    /// <summary>
    /// 把一组 PSD 图层按顺序合成到整幅 RGBA 画布（straight alpha、source-over）。
    /// <para>源图层允许超出画布或带负偏移，只取与画布相交的区域；返回 Painted 便于判断是否真的画上过像素。</para>
    /// </summary>
    /// <param name="document">PSD 文档尺寸。</param>
    /// <param name="psdPath">PSD 源文件路径。</param>
    /// <param name="layers">参与合成的图层（按底到顶顺序）。</param>
    /// <returns>合成画布、尺寸与实际绘制图层数；没有图层可画时为 null。</returns>
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

                    if (alpha == MaxChannelValue)
                    {
                        canvas[t] = source[s];
                        canvas[t + 1] = source[s + 1];
                        canvas[t + 2] = source[s + 2];
                        canvas[t + 3] = (byte)MaxChannelValue;
                        continue;
                    }

                    // 源覆盖目标（straight alpha，浮点精度，避免多图层叠加累积灰阶误差）
                    float sourceAlpha = alpha / ChannelScale;
                    float dstAlpha = canvas[t + 3] / ChannelScale;
                    float outAlpha = sourceAlpha + dstAlpha * (1f - sourceAlpha);
                    if (outAlpha <= 0f)
                    {
                        continue;
                    }

                    canvas[t] = ToByte(Blend(source[s], canvas[t], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 1] = ToByte(Blend(source[s + 1], canvas[t + 1], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 2] = ToByte(Blend(source[s + 2], canvas[t + 2], sourceAlpha, dstAlpha, outAlpha));
                    canvas[t + 3] = ToByte(outAlpha * ChannelScale);
                }
            }
        }

        return painted == 0 ? null : (canvas, width, height, painted);
    }

    /// <summary>
    /// source-over 单通道混合：结果 = (源 × α + 目标 × 目标α × (1-α)) / 输出α。
    /// </summary>
    private static float Blend(byte source, byte target, float sourceAlpha, float dstAlpha, float outAlpha)
    {
        // src 覆盖在 dst 之上：结果 = (src × α + dst × dstA × (1-α)) / outA
        return (source * sourceAlpha + target * dstAlpha * (1f - sourceAlpha)) / outAlpha;
    }

    /// <summary>浮点通道钳到 0–255 并四舍五入为字节。</summary>
    private static byte ToByte(float value)
    {
        if (value <= 0f)
        {
            return 0;
        }

        return value >= ChannelScale
            ? (byte)MaxChannelValue
            : (byte)(value + RoundingBias);
    }

    /// <summary>
    /// 判断整幅底图缓存是否仍可复用：源路径、图层数、源文件修改时间三者必须全部一致。
    /// 缓存戳缺失或解析失败时一律视为过期，宁可重新合成。
    /// </summary>
    /// <param name="stampPath">缓存戳文件路径。</param>
    /// <param name="psdPath">PSD 源文件路径。</param>
    /// <param name="layerCount">当前参与合成的图层数。</param>
    /// <returns>true = 缓存仍有效。</returns>
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

    /// <summary>
    /// 尝试复用已有整幅底图：按「Background 目录 + Composite_ + PSD 文件名」定位，存在时只记录路径，不重新编码。
    /// </summary>
    /// <param name="set">当前切片集合。</param>
    /// <param name="levelDirectory">关卡资源目录。</param>
    /// <param name="psdPath">PSD 源文件路径。</param>
    private static void TryReuseComposite(PsdSliceSet set, string levelDirectory, string psdPath)
    {
        var backgroundDirectory = Path.Combine(levelDirectory, MapLayerKeys.FolderName(MapLayerKeys.Background));
        var path = Path.Combine(backgroundDirectory, CompositeNamePrefix + Path.GetFileNameWithoutExtension(psdPath) + PngExtension);
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
        return safeName + PngExtension;
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
