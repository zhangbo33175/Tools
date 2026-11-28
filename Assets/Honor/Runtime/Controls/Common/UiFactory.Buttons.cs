/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.Buttons.cs
 * author:    云毅
 * created:   2026
 * descrip:   按钮族：工具栏、紧凑、对话框操作按钮与两态开关的统一下发。
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
    #region 按钮骨架

    /// <summary>
    /// 按钮的骨架度量：内边距、尺寸、圆角、描边宽度、字号、字重与对齐。
    /// 工具栏、紧凑按钮与对话框操作按钮三族都只维护自己的预设，真正落到控件上的属性只有
    /// <see cref="ApplyButtonFrame"/> 这一处；颜色由各族自己的配色表负责，不在这份记录里。
    /// </summary>
    /// <param name="Padding">内边距（必填，避免误用零初始化构造）。</param>
    /// <param name="Width">定宽；null 表示按内容自适应。</param>
    /// <param name="MinWidth">最小宽度；null 表示不设。</param>
    /// <param name="Height">高度；null 表示沿用主题样式。</param>
    /// <param name="MinHeight">最小高度；null 表示不设。</param>
    /// <param name="CornerRadius">圆角；负数表示不覆盖。</param>
    /// <param name="BorderThickness">描边宽度；负数表示不覆盖。</param>
    /// <param name="FontSize">字号；null 表示沿用主题样式。</param>
    /// <param name="Weight">字重；null 表示沿用主题样式。</param>
    /// <param name="CenterContent">是否把内容在按钮内居中；false 表示不写这两条对齐属性。</param>
    /// <param name="Alignment">按钮自身在父容器里的纵向对齐。</param>
    public readonly record struct ButtonFrame(
        Thickness Padding,
        double? Width = null,
        double? MinWidth = null,
        double? Height = null,
        double? MinHeight = null,
        double CornerRadius = -1,
        double BorderThickness = -1,
        double? FontSize = null,
        FontWeight? Weight = null,
        bool CenterContent = true,
        VerticalAlignment Alignment = VerticalAlignment.Stretch);

    /// <summary>把骨架度量落到按钮上：只覆盖记录里给了值的项，其余保持控件原有值。</summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="frame">骨架度量。</param>
    /// <returns>同一个按钮。</returns>
    public static Button ApplyButtonFrame(Button button, ButtonFrame frame)
    {
        button.Padding = frame.Padding;

        if (frame.Width is { } width)
        {
            button.Width = width;
        }

        if (frame.MinWidth is { } minWidth)
        {
            button.MinWidth = minWidth;
        }

        if (frame.Height is { } height)
        {
            button.Height = height;
        }

        if (frame.MinHeight is { } minHeight)
        {
            button.MinHeight = minHeight;
        }

        if (frame.CornerRadius >= 0)
        {
            button.CornerRadius = new CornerRadius(frame.CornerRadius);
        }

        if (frame.BorderThickness >= 0)
        {
            button.BorderThickness = new Thickness(frame.BorderThickness);
        }

        if (frame.FontSize is { } fontSize)
        {
            button.FontSize = fontSize;
        }

        if (frame.Weight is { } weight)
        {
            button.FontWeight = weight;
        }

        if (frame.CenterContent)
        {
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }

        button.VerticalAlignment = frame.Alignment;
        return button;
    }

    #endregion

    #region 工具栏按钮

    /// <summary>创建预览窗工具栏按钮。</summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="width">按钮宽度。</param>
    /// <param name="primary">是否主按钮（蓝底白字）。</param>
    /// <returns>已应用统一样式的按钮。</returns>
    public static Button PreviewToolbarButton(string text, double width, bool primary = false)
    {
        var button = new Button();
        ApplyPreviewToolbarButton(button, text, width, primary);
        return button;
    }

    /// <summary>把统一工具栏样式应用到已有按钮。</summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="text">按钮文案。</param>
    /// <param name="width">按钮宽度。</param>
    /// <param name="primary">是否主按钮。</param>
    public static void ApplyPreviewToolbarButton(Button button, string text, double width, bool primary)
    {
        button.Content = text;
        ApplyButtonFrame(button, ToolbarFrame(width));

        if (primary)
        {
            button.Background = UiTheme.Accent;
            button.Foreground = UiTheme.White;
            button.BorderThickness = new Thickness(0);
            button.FontWeight = FontWeight.SemiBold;
            return;
        }

        button.Background = UiTheme.Card;
        button.Foreground = UiTheme.PreviewText;
        button.BorderBrush = UiTheme.Divider;
        button.BorderThickness = new Thickness(1);
        button.FontWeight = FontWeight.Normal;
    }

    /// <summary>工具栏按钮的骨架：定宽、26 高、5 圆角、内容不强行居中、在父容器里纵向居中。</summary>
    /// <param name="width">按钮宽度。</param>
    /// <returns>骨架度量。</returns>
    private static ButtonFrame ToolbarFrame(double width) => new(
        new Thickness(ToolbarButtonPaddingX, 0),
        Width: width,
        Height: ToolbarButtonHeight,
        CornerRadius: ToolbarButtonCornerRadius,
        FontSize: ToolbarButtonFontSize,
        CenterContent: false,
        Alignment: VerticalAlignment.Center);

    #endregion

    #region 紧凑按钮

    /// <summary>紧凑按钮水平内边距。</summary>
    public const double CompactButtonPaddingX = 10;

    /// <summary>紧凑按钮垂直内边距。</summary>
    public const double CompactButtonPaddingY = 4;

    /// <summary>创建面板底部/表格操作区的紧凑按钮。</summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="background">底色；null 使用按钮默认样式。</param>
    /// <param name="foreground">文字色；null 使用按钮默认样式。</param>
    /// <param name="padding">内边距；null 使用紧凑按钮默认值。</param>
    /// <returns>已应用统一内边距的按钮。</returns>
    public static Button CompactButton(
        string text,
        IBrush? background = null,
        IBrush? foreground = null,
        Thickness? padding = null)
    {
        var button = ApplyButtonFrame(
            new Button { Content = text },
            new ButtonFrame(
                padding ?? new Thickness(CompactButtonPaddingX, CompactButtonPaddingY),
                CenterContent: false));

        if (background is not null)
        {
            button.Background = background;
        }

        if (foreground is not null)
        {
            button.Foreground = foreground;
        }

        return button;
    }

    #endregion

    #region 开关态

    /// <summary>
    /// 把两态配色落到按钮上：只覆盖配色表里给出的项，
    /// 描边或字重为 null 时保持控件原有值。
    /// </summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="active">是否处于选中态。</param>
    /// <param name="visual">两态配色。</param>
    public static void ApplyToggle(Button button, bool active, ToggleVisual visual)
    {
        var state = active
            ? (visual.ActiveSurface, visual.ActiveText, visual.ActiveBorder, visual.ActiveWeight)
            : (visual.IdleSurface, visual.IdleText, visual.IdleBorder, visual.IdleWeight);

        button.Background = state.Item1;
        button.Foreground = state.Item2;
        if (state.Item3 is not null)
        {
            button.BorderBrush = state.Item3;
        }

        if (state.Item4 is { } weight)
        {
            button.FontWeight = weight;
        }
    }

    #endregion

    #region 对话框操作按钮

    /// <summary>对话框操作按钮的强调档。</summary>
    public enum ActionTone
    {
        /// <summary>主操作：实色底 + 白字 + 加粗。</summary>
        Primary,

        /// <summary>次操作：浅底 + 描边。</summary>
        Secondary,

        /// <summary>中性操作：不设底色，沿用全局按钮样式。</summary>
        Neutral,
    }

    /// <summary>
    /// 对话框操作按钮的尺寸档。各弹窗只维护自己的一份常量，
    /// 避免在每处按钮初始值里重复高度、圆角与内边距。
    /// </summary>
    /// <param name="Height">按钮高度。</param>
    /// <param name="CornerRadius">圆角；高度一半即胶囊。</param>
    /// <param name="PaddingX">左右内边距。</param>
    /// <param name="FontSize">字号；null 沿用全局样式。</param>
    /// <param name="MinWidth">最小宽度；0 表示不设。</param>
    /// <param name="Width">定宽；null 表示按内容自适应。</param>
    public readonly record struct ActionSkin(
        double Height,
        double CornerRadius,
        double PaddingX,
        double? FontSize = null,
        double MinWidth = 0,
        double? Width = null);

    /// <summary>
    /// 对话框操作按钮的配色：主操作与次操作两档。
    /// 描边传 null 表示沿用全局按钮描边，不做覆盖。
    /// </summary>
    /// <param name="PrimarySurface">主操作底色。</param>
    /// <param name="PrimaryText">主操作文字色。</param>
    /// <param name="PrimaryBorder">主操作描边。</param>
    /// <param name="SecondarySurface">次操作底色。</param>
    /// <param name="SecondaryText">次操作文字色。</param>
    /// <param name="SecondaryBorder">次操作描边。</param>
    public readonly record struct ActionPalette(
        IBrush PrimarySurface,
        IBrush PrimaryText,
        IBrush? PrimaryBorder,
        IBrush? SecondarySurface,
        IBrush? SecondaryText,
        IBrush? SecondaryBorder);

    /// <summary>
    /// 创建对话框底部/右上角操作按钮。确认窗、发布窗、收集物向导共用这一份构建逻辑，
    /// 差异只体现在尺寸档与配色上。
    /// </summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="tone">强调档。</param>
    /// <param name="skin">尺寸档。</param>
    /// <param name="palette">配色；null 表示不覆盖底色，全部沿用全局按钮样式（中性档常见）。</param>
    /// <returns>已应用统一布局与配色的按钮。</returns>
    public static Button ActionButton(string text, ActionTone tone, ActionSkin skin, ActionPalette? palette = null)
    {
        var button = ApplyButtonFrame(
            new Button { Content = text },
            new ButtonFrame(
                new Thickness(skin.PaddingX, 0),
                Width: skin.Width,
                MinWidth: skin.MinWidth > 0 ? skin.MinWidth : null,
                Height: skin.Height,
                MinHeight: skin.Height,
                CornerRadius: skin.CornerRadius,
                BorderThickness: 1,
                FontSize: skin.FontSize,
                Weight: tone == ActionTone.Primary ? FontWeight.SemiBold : FontWeight.Normal));

        if (palette is not { } colors)
        {
            return button;
        }

        switch (tone)
        {
            case ActionTone.Primary:
                button.Background = colors.PrimarySurface;
                button.Foreground = colors.PrimaryText;
                button.BorderBrush = colors.PrimaryBorder;
                break;

            case ActionTone.Secondary:
                button.Background = colors.SecondarySurface;
                button.Foreground = colors.SecondaryText;
                button.BorderBrush = colors.SecondaryBorder;
                break;
        }

        return button;
    }

    #endregion

}
