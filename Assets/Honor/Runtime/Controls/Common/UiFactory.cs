/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.cs
 * author:    云毅
 * created:   2026
 * descrip:   预览窗/面板共用的轻量控件工厂：统一按钮尺寸、圆角与主次样式，
 *            以及列表外框、卡片容器等重复结构。
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 预览窗/向导窗共用的轻量控件工厂。只负责视觉样式，不持有业务状态。
/// </summary>
public static class UiFactory
{
    #region 常量

    /// <summary>工具栏按钮高度。</summary>
    public const double ToolbarButtonHeight = 30;

    /// <summary>工具栏按钮字号。</summary>
    public const double ToolbarButtonFontSize = 12;

    /// <summary>工具栏按钮水平内边距。</summary>
    public const double ToolbarButtonPaddingX = 10;

    /// <summary>工具栏按钮圆角。</summary>
    public const double ToolbarButtonCornerRadius = 5;

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
        button.Width = width;
        button.Height = ToolbarButtonHeight;
        button.FontSize = ToolbarButtonFontSize;
        button.Padding = new Thickness(ToolbarButtonPaddingX, 0);
        button.CornerRadius = new CornerRadius(ToolbarButtonCornerRadius);
        button.VerticalAlignment = VerticalAlignment.Center;

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
    // ---TODO 重构：面板底部操作按钮与工具栏按钮统一由工厂生成，调用方只传文案、主色与差异内边距。
    public static Button CompactButton(
        string text,
        IBrush? background = null,
        IBrush? foreground = null,
        Thickness? padding = null)
    {
        var button = new Button
        {
            Content = text,
            Padding = padding ?? new Thickness(CompactButtonPaddingX, CompactButtonPaddingY),
        };
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

    #region 列表与卡片

    /// <summary>
    /// 创建「内容 + 右侧经典滚动条」的列表外框。
    /// 章节列表、物件列表、未配置资源列表和迷雾区域列表共用同一套结构。
    /// </summary>
    /// <param name="scroll">已配置好可见性的滚动视图。</param>
    /// <returns>带 1px 描边的列表外框。</returns>
    // ---TODO 重构：列表外框统一由控件工厂生成，面板不再各拼一套 Grid + ClassicScrollBar。
    public static Border ScrollListFrame(ScrollViewer scroll)
    {
        var area = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"*,{ClassicScrollBar.BarWidth}"),
        };
        area.Children.Add(scroll);
        var scrollBar = new ClassicScrollBar { Target = scroll };
        Grid.SetColumn(scrollBar, 1);
        area.Children.Add(scrollBar);

        return new Border
        {
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = area,
        };
    }

    /// <summary>创建预览窗/向导窗共用的卡片容器。</summary>
    /// <param name="child">卡片内容；可在返回后再挂载。</param>
    /// <param name="background">卡片底色；null 使用白卡片。</param>
    /// <param name="border">卡片描边；null 使用通用分隔线。</param>
    /// <param name="cornerRadius">圆角。</param>
    /// <param name="padding">内边距。</param>
    /// <returns>已应用主题底色与描边的卡片。</returns>
    // ---TODO 重构：预览窗/向导卡片统一由工厂生成，圆角与内边距只由调用方传差异值。
    public static Border PaneCard(
        Control? child = null,
        IBrush? background = null,
        IBrush? border = null,
        double cornerRadius = 8,
        double padding = 0) => new()
    {
        Background = background ?? UiTheme.Card,
        BorderBrush = border ?? UiTheme.Divider,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(cornerRadius),
        Padding = new Thickness(padding),
        Child = child,
    };

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
