/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteLayout.cs
 * author:    云毅
 * created:   2026
 * descrip:   精灵绘制布局统一入口：等比适应、旋转后宽高互换与绕中心变换矩阵，
 *            场景画布和动画预览共用同一套 Unity ScaleToFit / flipX 口径。
 ***************************************************************/

using System;
using Avalonia;

namespace MapEditorTool.Services;

/// <summary>精灵在目标框内的垂直锚点。</summary>
public enum SpriteVerticalAnchor
{
    /// <summary>上下居中。</summary>
    Center,

    /// <summary>底部对齐，适合角色脚底锚点。</summary>
    Bottom,
}

/// <summary>
/// 精灵绘制布局计算。只做矩形与矩阵运算，不持有位图或地图数据。
/// </summary>
public static class SpriteLayout
{
    #region 等比适应

    /// <summary>
    /// 把源图等比缩放到目标框内，宽高比不变。未勾选缩放时使用 contain 口径；
    /// 目标或源尺寸无效时直接使用目标框。
    /// </summary>
    /// <param name="target">目标绘制框。</param>
    /// <param name="source">源图原始像素尺寸。</param>
    /// <param name="stretch">是否强制拉伸到目标框。</param>
    /// <param name="anchor">垂直锚点。</param>
    /// <returns>实际绘制矩形。</returns>
    public static Rect Fit(Rect target, Size source, bool stretch, SpriteVerticalAnchor anchor = SpriteVerticalAnchor.Center)
    {
        if (stretch || target.Width <= 0 || target.Height <= 0)
        {
            return target;
        }

        if (source.Width <= 0 || source.Height <= 0)
        {
            return target;
        }

        var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
        if (scale <= 0)
        {
            return target;
        }

        var width = source.Width * scale;
        var height = source.Height * scale;
        var y = anchor == SpriteVerticalAnchor.Bottom
            ? target.Y + target.Height - height
            : target.Y + (target.Height - height) / 2.0;

        return new Rect(
            target.X + (target.Width - width) / 2.0,
            y,
            width,
            height);
    }

    /// <summary>以位图像素尺寸调用 <see cref="Fit(Rect, Size, bool, SpriteVerticalAnchor)"/>。</summary>
    /// <param name="target">目标绘制框。</param>
    /// <param name="source">位图原始像素尺寸。</param>
    /// <param name="stretch">是否强制拉伸到目标框。</param>
    /// <param name="anchor">垂直锚点。</param>
    /// <returns>实际绘制矩形。</returns>
    public static Rect Fit(Rect target, PixelSize source, bool stretch, SpriteVerticalAnchor anchor = SpriteVerticalAnchor.Center) =>
        Fit(target, new Size(source.Width, source.Height), stretch, anchor);

    /// <summary>
    /// 旋转 90°/270° 且贴图与物件框宽高互换时，先在交换宽高的框内做 contain，
    /// 再由旋转矩阵摆回目标框，避免横图硬塞进竖框被压扁。
    /// </summary>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="pixelSize">贴图原始像素尺寸。</param>
    /// <param name="degrees">旋转角度，度。</param>
    /// <returns>实际绘制矩形。</returns>
    public static Rect FitForRotation(Rect bounds, Size pixelSize, double degrees)
    {
        var quarter = (int)Math.Round(Math.Abs(degrees) / 90.0) % 4;
        var swapped = quarter is 1 or 3
            && Math.Abs(pixelSize.Width - bounds.Width) > 1
            && Math.Abs(pixelSize.Height - bounds.Height) > 1;

        if (!swapped)
        {
            return Fit(bounds, pixelSize, stretch: false);
        }

        var center = bounds.Center;
        var swappedBounds = new Rect(
            center.X - bounds.Height / 2.0,
            center.Y - bounds.Width / 2.0,
            bounds.Height,
            bounds.Width);
        return Fit(swappedBounds, pixelSize, stretch: false);
    }

    #endregion

    #region 变换矩阵

    /// <summary>
    /// 绕包围盒中心旋转 + 水平镜像。镜像先于旋转施加，与 Unity 的
    /// T * R * S 同序；两者都无时返回单位矩阵。
    /// </summary>
    /// <param name="degrees">旋转角度，度。</param>
    /// <param name="flipX">是否水平镜像。</param>
    /// <param name="bounds">实例包围盒。</param>
    /// <returns>绘制矩阵。</returns>
    public static Matrix ContentTransform(double degrees, bool flipX, Rect bounds)
    {
        var rotated = Math.Abs(degrees) >= 0.01;
        if (!rotated && !flipX)
        {
            return Matrix.Identity;
        }

        var center = bounds.Center;
        var matrix = Matrix.CreateTranslation(-center.X, -center.Y);
        if (flipX)
        {
            matrix *= Matrix.CreateScale(-1, 1);
        }

        if (rotated)
        {
            matrix *= Matrix.CreateRotation(degrees * Math.PI / 180.0);
        }

        return matrix * Matrix.CreateTranslation(center.X, center.Y);
    }

    #endregion
}
