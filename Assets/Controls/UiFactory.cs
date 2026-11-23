/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.cs
 * author:    云毅
 * created:   2026
 * descrip:   预览窗工具栏控件工厂：统一按钮尺寸、圆角与主次样式，
 *            避免各预览窗各写一套相同的按钮初始化代码。
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
}
