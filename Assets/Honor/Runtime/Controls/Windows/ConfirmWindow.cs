/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfirmWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   通用确认弹窗（默认二选一，可选“不保存”三选一）
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>三选一确认结果：取消 / 保存后继续 / 不保存直接继续。</summary>
public enum ConfirmChoice
{
    Cancel = 0,
    Save = 1,
    Discard = 2,
}

/// <summary>
/// 轻量确认窗：一行说明 + 「确认 / 取消」两个按钮，结果通过 <c>ShowDialog&lt;bool&gt;</c> 返回。
/// 额外传入 <c>discardText</c> 时变成三选一，调用方改用 <c>ShowDialog&lt;ConfirmChoice&gt;</c>；
/// 只用于会丢掉当前编辑内容这类一次性确认，不承载表单。
/// </summary>
public sealed class ConfirmWindow : Window
{
    /// <summary>按文案构建确认窗。</summary>
    /// <param name="title">窗口标题。</param>
    /// <param name="message">说明文字（自动换行）。</param>
    /// <param name="confirmText">确认按钮文案。</param>
    /// <param name="cancelText">取消按钮文案。</param>
    /// <param name="discardText">可选的第三按钮文案（不保存 / 放弃修改）；null = 保持二选一。</param>
    public ConfirmWindow(
        string title,
        string message,
        string confirmText = "确定",
        string cancelText = "取消",
        string? discardText = null)
    {
        Title = title;
        Width = discardText is null ? 440 : 492;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.DialogSurface;

        var threeWay = discardText is not null;
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = UiTheme.TextPrimary,
        };

        var confirm = new Button
        {
            Content = confirmText,
            Width = 104,
            Height = 30,
            Background = UiTheme.Accent,
            Foreground = UiTheme.White,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        confirm.Click += (_, _) =>
        {
            if (threeWay)
            {
                Close(ConfirmChoice.Save);
            }
            else
            {
                Close(true);
            }
        };

        var discard = new Button
        {
            Content = discardText ?? string.Empty,
            Width = 104,
            Height = 30,
            IsVisible = threeWay,
            Background = UiTheme.DialogSecondary,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        discard.Click += (_, _) => Close(ConfirmChoice.Discard);

        var cancel = new Button
        {
            Content = cancelText,
            Width = 84,
            Height = 30,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        cancel.Click += (_, _) =>
        {
            if (threeWay)
            {
                Close(ConfirmChoice.Cancel);
            }
            else
            {
                Close(false);
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(confirm);
        if (threeWay)
        {
            buttons.Children.Add(discard);
        }

        buttons.Children.Add(cancel);

        Content = new Border
        {
            Padding = new Thickness(18, 16, 18, 14),
            Child = new StackPanel { Spacing = 16, Children = { text, buttons } },
        };
    }
}
