/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PreviewWindowChrome.cs
 * author:    云毅
 * created:   2026
 * descrip:   预览窗通用外壳：统一顶栏、标题区、底栏与三段式根网格
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 动画预览窗与地图预览窗共用的窗口外壳。
/// 高度、内边距、标题字号与三段式布局只在这里定义，窗口自身只提供标题文案和右侧动作区。
/// </summary>
public static class PreviewWindowChrome
{
    #region 尺寸与文案常量

    /// <summary>顶栏高度。</summary>
    public const int HeaderHeight = 58;

    /// <summary>底栏高度。</summary>
    public const int FooterHeight = 36;

    /// <summary>顶栏 / 底栏左右内边距。</summary>
    private const double HorizontalPadding = 16;

    /// <summary>标题左侧强调竖条的宽度。</summary>
    private const double AccentWidth = 3;

    /// <summary>标题左侧强调竖条的高度。</summary>
    private const double AccentHeight = 30;

    /// <summary>强调竖条的圆角。</summary>
    private const double AccentCornerRadius = 2;

    /// <summary>标题与强调竖条的间距。</summary>
    private const double TitleSpacing = 10;

    /// <summary>主标题字号。</summary>
    private const double TitleFontSize = 18;

    /// <summary>副标题与底栏字号。</summary>
    private const double HintFontSize = 11;

    /// <summary>副标题与主标题之间的紧凑间距。</summary>
    private const double SubtitleTopMargin = 1;

    #endregion

    #region 根布局

    /// <summary>构建「顶栏 / 主体 / 底栏」三段式窗口根网格。</summary>
    /// <param name="header">顶栏。</param>
    /// <param name="body">主体。</param>
    /// <param name="footer">底栏。</param>
    /// <returns>窗口根网格。</returns>
    public static Grid BuildShell(Control header, Control body, Control footer)
    {
        var root = new Grid
        {
            // ---TODO 重构：预览窗三段高度由外壳统一维护，窗口不再各写一份魔法数字。
            RowDefinitions = new RowDefinitions($"{HeaderHeight},*,{FooterHeight}"),
            Background = UiTheme.PreviewPanel,
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(footer);
        return root;
    }

    #endregion

    #region 顶栏与标题

    /// <summary>标题区：左侧强调竖条 + 主标题 + 副标题。</summary>
    /// <param name="title">主标题。</summary>
    /// <param name="subtitle">副标题。</param>
    /// <returns>标题面板。</returns>
    public static Control BuildTitlePanel(string title, string subtitle)
    {
        var accent = new Border
        {
            Width = AccentWidth,
            Height = AccentHeight,
            CornerRadius = new CornerRadius(AccentCornerRadius),
            Background = UiTheme.Accent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = TitleFontSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.PreviewText,
        };
        var subtitleBlock = new TextBlock
        {
            Text = subtitle,
            FontSize = HintFontSize,
            Foreground = UiTheme.PreviewHint,
            Margin = new Thickness(0, SubtitleTopMargin, 0, 0),
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = TitleSpacing,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                accent,
                new StackPanel
                {
                    Spacing = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { titleBlock, subtitleBlock },
                },
            },
        };
    }

    /// <summary>顶栏：左标题、中段可选内容、右动作区，统一边框与内边距。</summary>
    /// <param name="titlePanel">左侧标题区。</param>
    /// <param name="center">中间可选内容，如筛选与搜索。</param>
    /// <param name="right">右侧动作区。</param>
    /// <returns>顶栏边框。</returns>
    public static Control BuildHeader(Control titlePanel, Control? center, Control right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(HorizontalPadding, 0),
            Children = { titlePanel },
        };

        if (center is not null)
        {
            Grid.SetColumn(center, 1);
            grid.Children.Add(center);
        }

        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };
    }

    #endregion

    #region 底栏

    /// <summary>底栏：左侧状态文本、右侧计数文本，统一字号与上分隔线。</summary>
    /// <param name="statusText">状态文本块。</param>
    /// <param name="countText">计数文本块。</param>
    /// <param name="status">初始状态文案。</param>
    /// <returns>底栏边框。</returns>
    public static Control BuildFooter(TextBlock statusText, TextBlock countText, string status)
    {
        statusText.FontSize = HintFontSize;
        statusText.Foreground = UiTheme.PreviewHint;
        statusText.VerticalAlignment = VerticalAlignment.Center;
        statusText.Text = status;
        statusText.TextTrimming = TextTrimming.CharacterEllipsis;

        countText.FontSize = HintFontSize;
        countText.FontWeight = FontWeight.SemiBold;
        countText.Foreground = UiTheme.Accent;
        countText.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(HorizontalPadding, 0),
            Children = { statusText, countText },
        };
        Grid.SetColumn(countText, 1);

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
    }

    #endregion
}
