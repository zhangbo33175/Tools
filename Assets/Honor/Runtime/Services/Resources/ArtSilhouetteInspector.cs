/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ArtSilhouetteInspector.cs
 * author:    云毅
 * created:   2026
 * descrip:   读取切片的轮廓/色彩，识别被错误放进背景组的立体植被
 ***************************************************************/

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MapEditorTool.Services;

/// <summary>
/// 一张切片的视觉轮廓统计。只在内存中缓存，不写入 JSON。
/// </summary>
/// <param name="IsStandingVegetation">是否像「有高度的绿色植被」：树、灌木、绿篱。</param>
/// <param name="GreenRatio">不透明采样点中绿色像素的占比（0–1）。</param>
/// <param name="ContactRatio">贴图底部 10% 高度内的不透明像素占比（0–1）。</param>
/// <param name="Aspect">有效内容高度 / 宽度；扁平的停车位标记、地毯接近 0。</param>
/// <param name="BoundingWidth">有效内容的像素宽。</param>
/// <param name="BoundingHeight">有效内容的像素高。</param>
/// <param name="OpaqueSamples">参与统计的不透明采样点数量。</param>
public sealed record ArtProfile(
    bool IsStandingVegetation,
    double GreenRatio,
    double ContactRatio,
    double Aspect,
    int BoundingWidth,
    int BoundingHeight,
    int OpaqueSamples)
{
    /// <summary>空 / 无法读取时的安全结果。</summary>
    public static ArtProfile None { get; } = new(false, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// 切片视觉分类器：只回答「这张图是不是立体植被」，不参与名字、图层键或层序判断。
/// <para>
/// 背景是 PSD 的合并组，切片命名经常和实际画面不一致。例如
/// <c>bg_rock_1_550_867.png</c> 实际是一棵绿色灌木（94% 绿色、底部接触率 54%），
/// 而同一模板的 <c>bg_rock_1.png</c> 是扁平石板（0% 绿色、底部接触率 94%）。
/// 名称 / 高宽比都会把这两者判反；这里改用像素统计做最后的语义兜底。
/// </para>
/// <para>
/// 判据同时要求：绿色占比足够高、有效内容不是扁条、底部有明显透明轮廓。
/// 因此平面的草地砖、停车位标记、散落扁石都不会被提层。
/// </para>
/// </summary>
public static class ArtSilhouetteInspector
{
    #region 常量

    /// <summary>最多采样点数；大图按步长抽样，避免刷新时扫描整张背景。</summary>
    private const long SampleBudget = 400_000;

    /// <summary>大于该像素数的整幅底图不做提层判断，避免无效解码成本。</summary>
    private const long MaxProfilePixels = 2_000_000;

    /// <summary>绿色像素占比下限。</summary>
    private const double GreenRatioThreshold = 0.22;

    /// <summary>有效内容高 / 宽下限；扁平石板约 0.33，灌木约 1.0。</summary>
    private const double MinimumAspect = 0.45;

    /// <summary>有效内容高度下限，过滤小草芽。</summary>
    private const int MinimumContentHeight = 40;

    /// <summary>贴图底部不透明占比上限；草地砖 / 停车位标记接近 1。</summary>
    private const double MaximumBottomContact = 0.95;

    /// <summary>alpha 低于该值视为透明边缘。</summary>
    private const byte AlphaThreshold = 8;

    #endregion

    #region 缓存与公开入口

    /// <summary>按位图对象缓存轮廓统计；<see cref="AssetLibrary"/> 已缓存位图，刷新时不会重复解码。</summary>
    private static readonly ConditionalWeakTable<Bitmap, ArtProfile> Profiles = new();

    /// <summary>读取切片轮廓统计。</summary>
    /// <param name="bitmap">已解码的切片位图。</param>
    /// <returns>轮廓统计；读取失败返回 <see cref="ArtProfile.None"/>。</returns>
    public static ArtProfile Inspect(Bitmap bitmap) => Profiles.GetValue(bitmap, Analyze);

    /// <summary>该像素尺寸是否值得做轮廓分析。</summary>
    /// <param name="width">图像宽。</param>
    /// <param name="height">图像高。</param>
    /// <returns>true = 不是空图，且不超过分析上限。</returns>
    public static bool IsProfileable(int width, int height)
        => width > 0 && height > 0 && (long)width * height <= MaxProfilePixels;

    /// <summary>是否是「有高度的绿色植被」。</summary>
    /// <param name="bitmap">切片位图；null 时返回 false。</param>
    /// <returns>true = 树 / 灌木 / 绿篱一类立体植被。</returns>
    public static bool IsStandingVegetation(Bitmap? bitmap)
        => bitmap is not null && Inspect(bitmap).IsStandingVegetation;

    #endregion

    #region 像素统计

    /// <summary>把位图转成 BGRA 缓冲区并统计绿色占比、有效包围盒与底部接触率。</summary>
    private static ArtProfile Analyze(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        if (!IsProfileable(size.Width, size.Height))
        {
            return ArtProfile.None;
        }

        try
        {
            using var target = new WriteableBitmap(size, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var framebuffer = target.Lock();
            bitmap.CopyPixels(framebuffer);

            var stride = framebuffer.RowBytes;
            if (stride <= 0)
            {
                return ArtProfile.None;
            }

            var pixels = new byte[stride * size.Height];
            Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
            return AnalyzeBytes(pixels, stride, size.Width, size.Height);
        }
        catch (Exception)
        {
            // 贴图损坏 / 平台未初始化时不阻断刷新，按「不是植被」处理。
            return ArtProfile.None;
        }
    }

    /// <summary>分析 BGRA 像素缓冲区。</summary>
    /// <param name="pixels">BGRA8888、预乘 alpha 的像素。</param>
    /// <param name="stride">每行字节数。</param>
    /// <param name="width">图像宽。</param>
    /// <param name="height">图像高。</param>
    private static ArtProfile AnalyzeBytes(byte[] pixels, int stride, int width, int height)
    {
        var area = (long)width * height;
        var step = area > SampleBudget
            ? (int)Math.Ceiling(Math.Sqrt(area / (double)SampleBudget))
            : 1;

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        var opaque = 0L;
        var green = 0L;

        for (var y = 0; y < height; y += step)
        {
            for (var x = 0; x < width; x += step)
            {
                var index = y * stride + x * 4;
                var alpha = pixels[index + 3];
                if (alpha <= AlphaThreshold)
                {
                    continue;
                }

                opaque++;
                var blue = pixels[index];
                var greenChannel = pixels[index + 1];
                var red = pixels[index + 2];
                if (greenChannel >= red + 12 && greenChannel >= blue + 8 && greenChannel >= 40)
                {
                    green++;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (opaque == 0 || maxX < minX || maxY < minY)
        {
            return ArtProfile.None;
        }

        var contentWidth = maxX - minX + 1;
        var contentHeight = maxY - minY + 1;
        var aspect = (double)contentHeight / contentWidth;

        // 只看内容包围盒底部 10%：草地砖 / 停车位标记几乎铺满底边；
        // 灌木、树有枝叶和透明空隙，底边接触率明显更低。
        var bandHeight = Math.Max(1, contentHeight / 10);
        var contactTotal = 0L;
        var contactOpaque = 0L;
        for (var y = Math.Max(minY, maxY - bandHeight + 1); y <= maxY; y += step)
        {
            for (var x = minX; x <= maxX; x += step)
            {
                contactTotal++;
                var index = y * stride + x * 4;
                if (pixels[index + 3] > AlphaThreshold)
                {
                    contactOpaque++;
                }
            }
        }

        var greenRatio = (double)green / opaque;
        var contactRatio = contactTotal == 0 ? 0 : (double)contactOpaque / contactTotal;
        var isStanding = greenRatio >= GreenRatioThreshold
            && aspect >= MinimumAspect
            && contentHeight >= MinimumContentHeight
            && contactRatio <= MaximumBottomContact;

        return new ArtProfile(
            isStanding,
            greenRatio,
            contactRatio,
            aspect,
            contentWidth,
            contentHeight,
            (int)Math.Min(int.MaxValue, opaque));
    }

    #endregion
}
