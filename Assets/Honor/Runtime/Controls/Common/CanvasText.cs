/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CanvasText.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布文本布局工厂：统一文化方向、流方向、字体与字号入口
 ***************************************************************/

using System.Globalization;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 自绘画布共用的文本布局工厂。
/// <para>
/// 只负责把文本、字号、颜色和字体组合成 <see cref="FormattedText"/>，
/// 不参与坐标计算与绘制，避免各画布重复手写文化方向与流方向。
/// </para>
/// </summary>
public static class CanvasText
{
    #region 文本布局

    /// <summary>创建统一文化方向的文本布局。</summary>
    /// <param name="text">文本内容。</summary>
    /// <param name="size">字号。</param>
    /// <param name="brush">文字颜色。</param>
    /// <param name="typeface">字体；null 使用默认字体。</param>
    /// <returns>可直接测量与绘制的文本布局。</returns>
    // ---TODO 重构：自绘画布文本统一走此工厂，不再各自构造 FormattedText。
    public static FormattedText Create(
        string text,
        double size,
        IBrush brush,
        Typeface? typeface = null) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface ?? Typeface.Default,
            size,
            brush);

    #endregion
}
