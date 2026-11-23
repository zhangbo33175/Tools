/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteAlphaProbe.cs
 * author:    云毅
 * created:   2026
 * descrip:   精灵透明像素采样：把归一化局部坐标换算成位图像素并读取 alpha
 ***************************************************************/

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MapEditorTool.Services;

/// <summary>
/// 精灵 alpha 采样器。命中测试只关心「点击处是否真的有像素」，这里按单像素读取，
/// 不为整张图生成 alpha 掩码，避免 4096×4096 地图素材额外占用一份内存。
/// </summary>
public static class SpriteAlphaProbe
{
    #region 常量

    /// <summary>单像素读取的字节数：BGRA / RGBA 都是 4 字节，alpha 固定在偏移 3。</summary>
    private const int PixelByteCount = 4;

    /// <summary>alpha 通道在 32 位像素中的字节偏移。</summary>
    private const int AlphaByteOffset = 3;

    #endregion

    #region 公开方法

    /// <summary>
    /// 读取精灵在归一化局部坐标处的 alpha，判断该点是否算作可见像素。
    /// </summary>
    /// <param name="sprite">已解码的精灵位图；为空时按「无图可判定」放行。</param>
    /// <param name="normalizedX">局部 X，0 为图片左边、1 为图片右边。</param>
    /// <param name="normalizedY">局部 Y，0 为图片上边、1 为图片下边。</param>
    /// <param name="alphaThreshold">alpha 达到该值才算不透明；用于过滤抗锯齿边缘的残影。</param>
    /// <returns>可见返回 true；透明、越界或读取失败返回 false。</returns>
    public static bool IsOpaque(
        Bitmap? sprite,
        double normalizedX,
        double normalizedY,
        byte alphaThreshold)
    {
        if (sprite is null)
        {
            // 没有贴图（色块回退 / 运行时切片尚未就绪）时不能把物件永久判成透明，
            // 否则用户会看到「点了有东西却选不中」；退回整图矩形口径更稳妥。
            return true;
        }

        if (!TryGetPixel(sprite.PixelSize, normalizedX, normalizedY, out var pixelX, out var pixelY))
        {
            return false;
        }

        var format = sprite.Format;
        if (format is null || format.Value.BitsPerPixel < 32 || format.Value == PixelFormat.Rgb32)
        {
            // 24/16 位图或明确无 alpha 的格式：整块像素都算不透明。
            return true;
        }

        var buffer = Marshal.AllocHGlobal(PixelByteCount);
        try
        {
            sprite.CopyPixels(new PixelRect(pixelX, pixelY, 1, 1), buffer, PixelByteCount, PixelByteCount);
            return Marshal.ReadByte(buffer, AlphaByteOffset) >= alphaThreshold;
        }
        catch (Exception)
        {
            // 平台后端不支持 CopyPixels 时按不透明处理，保证命中链路可用。
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    #endregion

    #region 坐标换算

    /// <summary>把 0–1 的局部坐标换算成位图内的整数像素坐标。</summary>
    /// <param name="size">位图像素尺寸。</param>
    /// <param name="normalizedX">局部 X。</param>
    /// <param name="normalizedY">局部 Y。</param>
    /// <param name="pixelX">像素列。</param>
    /// <param name="pixelY">像素行。</param>
    /// <returns>坐标有效返回 true。</returns>
    private static bool TryGetPixel(
        PixelSize size,
        double normalizedX,
        double normalizedY,
        out int pixelX,
        out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (size.Width <= 0 || size.Height <= 0
            || double.IsNaN(normalizedX) || double.IsNaN(normalizedY)
            || double.IsInfinity(normalizedX) || double.IsInfinity(normalizedY))
        {
            return false;
        }

        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
        {
            return false;
        }

        // 右边 / 下边正好为 1 时不能取到 width / height，钳到最后一个有效像素。
        pixelX = Math.Min(size.Width - 1, (int)Math.Floor(normalizedX * size.Width));
        pixelY = Math.Min(size.Height - 1, (int)Math.Floor(normalizedY * size.Height));
        return true;
    }

    #endregion
}
