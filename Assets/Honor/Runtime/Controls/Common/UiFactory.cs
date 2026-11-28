/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.cs
 * author:    云毅
 * created:   2026
 * descrip:   控件工厂入口：度量常量与标记，其余分组按职责拆到分部文件。
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器控件工厂：按「按钮 / 卡片 / 表单 / 标签 / 标记」分文件维护，
/// 对外仍是同一个静态类，调用方只认 <c>UiFactory.xxx</c>。
/// </summary>
public static partial class UiFactory
{
    #region 常量

    /// <summary>工具栏按钮高度。</summary>
    public const double ToolbarButtonHeight = 30;

    /// <summary>工具栏按钮字号。</summary>
    public const double ToolbarButtonFontSize = UiMetrics.FontTitle;

    /// <summary>工具栏按钮水平内边距。</summary>
    public const double ToolbarButtonPaddingX = 10;

    /// <summary>工具栏按钮圆角。</summary>
    public const double ToolbarButtonCornerRadius = UiMetrics.ButtonCornerRadius;

    #endregion

    #region 分部

    // 按钮族见 UiFactory.Buttons.cs，卡片外壳见 UiFactory.Cards.cs，
    // 表单标签/字段行/提示文字见 UiFactory.Forms.cs，标签胶囊见 UiFactory.Chips.cs。

    #endregion

    #region 标记

    /// <summary>创建状态/类型标记圆点。</summary>
    /// <param name="brush">圆点颜色。</param>
    /// <param name="size">圆点直径。</param>
    /// <returns>垂直居中的圆点。</returns>
    // ---TODO 重构：列表、向导状态栏与预览标签共用同一份圆点几何。
    public static Border MarkerDot(IBrush brush, double size = 6) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2.0),
        Background = brush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    #endregion
}
