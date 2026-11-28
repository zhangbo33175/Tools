/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.Cards.cs
 * author:    云毅
 * created:   2026
 * descrip:   卡片与外壳：列表外框、面板卡片、分组块、竖条标题、页头页脚横条与画布舞台面。
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
    #region 网格落位

    /// <summary>
    /// 把控件落到网格的指定行、列（可跨列）并原样返回，省掉「SetRow / SetColumn / Children.Add」三行连写。
    /// 各面板骨架原本是逐控件手写这两条附加属性，同一份语汇重复了几十次。
    /// </summary>
    /// <typeparam name="T">控件类型。</typeparam>
    /// <param name="child">目标控件。</param>
    /// <param name="row">行号。</param>
    /// <param name="column">列号。</param>
    /// <param name="columnSpan">跨列数；1 表示不跨列。</param>
    /// <param name="rowSpan">跨行数；1 表示不跨行。</param>
    /// <returns>同一个控件，便于在 <c>Children.Add(...)</c> 里直接接住。</returns>
    public static T InCell<T>(T child, int row, int column, int columnSpan = 1, int rowSpan = 1) where T : Control
    {
        Grid.SetRow(child, row);
        Grid.SetColumn(child, column);
        if (columnSpan > 1)
        {
            Grid.SetColumnSpan(child, columnSpan);
        }

        if (rowSpan > 1)
        {
            Grid.SetRowSpan(child, rowSpan);
        }

        return child;
    }

    #endregion

    #region 列表与卡片

    /// <summary>卡片投影：与 App.axaml 的 <c>Theme.CardShadow</c> 同一档，保证 XAML 卡片与代码卡片同深度。</summary>
    private static readonly BoxShadows CardShadow =
        BoxShadows.Parse("0 1 2 0 #120E2A36, 0 4 12 0 #0A0E2A36");

    /// <summary>
    /// 创建「内容 + 右侧经典滚动条」的列表外框。
    /// 章节列表、物件列表、未配置资源列表和迷雾区域列表共用同一套结构。
    /// </summary>
    /// <param name="scroll">已配置好可见性的滚动视图。</param>
    /// <returns>带 1px 描边的列表外框。</returns>
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
    public static Border PaneCard(
        Control? child = null,
        IBrush? background = null,
        IBrush? border = null,
        double cornerRadius = UiMetrics.CardCornerRadius,
        double padding = 0) => new()
    {
        Background = background ?? UiTheme.Card,
        BorderBrush = border ?? UiTheme.Divider,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(cornerRadius),
        Padding = new Thickness(padding),
        Child = child,
    };

    /// <summary>
    /// 创建面板卡片：白底 + 面板描边 + 8 圆角。
    /// 物品元件、迷雾、资源整理各页的整块面板共用这一份定义。
    /// </summary>
    /// <param name="child">卡片内容。</param>
    /// <param name="background">底色；null 使用白卡片。</param>
    /// <param name="clipToBounds">是否裁剪溢出内容。</param>
    /// <param name="padding">内边距；null 为 0。</param>
    /// <returns>面板级卡片。</returns>
    public static Border PanelCard(
        Control? child = null,
        IBrush? background = null,
        bool clipToBounds = false,
        Thickness? padding = null) => new()
    {
        Background = background ?? UiTheme.Card,
        BorderBrush = UiTheme.PanelBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
        BoxShadow = CardShadow,
        ClipToBounds = clipToBounds,
        Padding = padding ?? default,
        Child = child,
    };

    /// <summary>
    /// 创建分组块：极浅青底 + 细描边 + 8 圆角，用于卡片内部的分区（如迷雾的笔刷设置、剧情的小节）。
    /// </summary>
    /// <param name="child">分组内容。</param>
    /// <param name="padding">内边距；null 使用 11,9,11,10。</param>
    /// <returns>分组块。</returns>
    public static Border GroupBlock(Control? child = null, Thickness? padding = null) => new()
    {
        Background = UiTheme.Form.GroupHeader,
        BorderBrush = UiTheme.Form.GroupBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
        Padding = padding ?? new Thickness(11, 9, 11, 10),
        Child = child,
    };

    /// <summary>
    /// 创建标题竖条：3px 宽的小青条。
    /// 栏标题、分组标题条与预览窗标题共用同一份宽度与倒角。
    /// </summary>
    /// <param name="brush">竖条颜色。</param>
    /// <param name="height">竖条高度；默认 12。</param>
    /// <param name="marginRight">右侧留白；默认 6，紧挨文字时给 0。</param>
    /// <returns>垂直居中的竖条。</returns>
    public static Border AccentBar(IBrush brush, double height = 12, double marginRight = 6) => new()
    {
        Width = UiMetrics.AccentBarWidth,
        Height = height,
        CornerRadius = new CornerRadius(UiMetrics.AccentBarCornerRadius),
        Background = brush,
        Margin = new Thickness(0, 0, marginRight, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>
    /// 创建栏标题：3px 青色竖条 + 加粗标题。
    /// 物品元件、迷雾、资源整理、剧情四页的栏标题与小节标题共用这一套语汇。
    /// </summary>
    /// <param name="text">标题文案。</param>
    /// <param name="fontSize">标题字号；默认 12。</param>
    /// <param name="margin">标题外边距。</param>
    /// <returns>水平排列的标题行。</returns>
    public static Control SectionHead(string text, double fontSize = 12, Thickness? margin = null) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = margin ?? default,
        Children =
        {
            AccentBar(UiTheme.Accent),
            new TextBlock
            {
                Text = text,
                Foreground = UiTheme.AccentDark,
                FontWeight = FontWeight.Bold,
                FontSize = fontSize,
                VerticalAlignment = VerticalAlignment.Center,
            },
        },
    };

    /// <summary>
    /// 创建页头横条：白底 + 底部 1px 分隔线。物品元件、迷雾、资源整理各页的工具条都走这一份，
    /// 不再各写一遍「白底 + 单边描边」。
    /// </summary>
    /// <param name="child">横条内容。</param>
    /// <param name="padding">内边距。</param>
    /// <param name="brush">分隔线色；null 用主题分隔线。</param>
    /// <param name="background">底色；null 用卡片白。</param>
    /// <returns>页头边框。</returns>
    public static Border HeaderBand(
        Control? child = null,
        Thickness? padding = null,
        IBrush? brush = null,
        IBrush? background = null) => new()
    {
        Background = background ?? UiTheme.Card,
        BorderBrush = brush ?? UiTheme.Divider,
        BorderThickness = new Thickness(0, 0, 0, 1),
        Padding = padding ?? default,
        Child = child,
    };

    /// <summary>
    /// 创建页脚横条：白底 + 顶部 1px 分隔线，用于状态栏 / 底部操作条。
    /// </summary>
    /// <param name="child">横条内容。</param>
    /// <param name="padding">内边距。</param>
    /// <param name="brush">分隔线色；null 用主题分隔线。</param>
    /// <param name="background">底色；null 用卡片白。</param>
    /// <returns>页脚边框。</returns>
    public static Border FooterBand(
        Control? child = null,
        Thickness? padding = null,
        IBrush? brush = null,
        IBrush? background = null) => new()
    {
        Background = background ?? UiTheme.Card,
        BorderBrush = brush ?? UiTheme.Divider,
        BorderThickness = new Thickness(0, 1, 0, 0),
        Padding = padding ?? default,
        Child = child,
    };

    /// <summary>
    /// 创建竖直滚动容器：横向滚动条一律禁用（编辑器里的长内容都是纵向流），
    /// 纵向默认按需出现；面板自带经典箭头滚动条时传 <paramref name="showScrollBar"/>=false 隐藏原生条。
    /// </summary>
    /// <param name="content">滚动内容。</param>
    /// <param name="showScrollBar">是否显示原生纵向滚动条。</param>
    /// <param name="padding">内边距；null 表示不设。</param>
    /// <returns>滚动容器。</returns>
    public static ScrollViewer VerticalScroll(Control content, bool showScrollBar = true, Thickness? padding = null) => new()
    {
        Content = content,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = showScrollBar
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        Padding = padding ?? default,
    };

    /// <summary>
    /// 创建列表条目行：透明底 + 底部 1px 分隔线。ListBox 模板行与浮窗里的行卡共用这一份，
    /// 避免每处各写一次单边描边。
    /// </summary>
    /// <param name="child">行内容。</param>
    /// <param name="padding">内边距。</param>
    /// <param name="line">分隔线色；null 用主题分隔线。</param>
    /// <returns>行边框。</returns>
    public static Border ListDividerRow(Control child, Thickness padding, IBrush? line = null) => new()
    {
        Padding = padding,
        Background = UiTheme.Transparent,
        BorderBrush = line ?? UiTheme.Divider,
        BorderThickness = new Thickness(0, 0, 0, 1),
        Child = child,
    };

    /// <summary>
    /// 创建画布舞台面：深色舞台底 + 可选描边 + 裁剪，预览类窗口的画布容器共用。
    /// </summary>
    /// <param name="child">画布内容。</param>
    /// <param name="border">描边色；null 不描边。</param>
    /// <param name="cornerRadius">圆角。</param>
    /// <param name="margin">外边距。</param>
    /// <returns>舞台面边框。</returns>
    public static Border CanvasStage(
        Control? child = null,
        IBrush? border = null,
        double cornerRadius = UiMetrics.InputCornerRadius,
        Thickness? margin = null) => new()
    {
        Background = UiTheme.PreviewView.Stage,
        BorderBrush = border,
        BorderThickness = border is null ? default : new Thickness(1),
        CornerRadius = new CornerRadius(cornerRadius),
        Margin = margin ?? default,
        ClipToBounds = true,
        Child = child,
    };

    #endregion

}
