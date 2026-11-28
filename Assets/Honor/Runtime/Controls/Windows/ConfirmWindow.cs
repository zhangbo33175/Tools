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
    /// <summary>主操作与「不保存」按钮的定宽。</summary>
    private const double ConfirmWidth = 104;

    /// <summary>取消按钮的定宽（文案更短，宽度收窄）。</summary>
    private const double CancelWidth = 84;

    /// <summary>确认窗按钮尺寸档：30 高、全局按钮圆角、左右内边距为 0（靠定宽居中）。</summary>
    private static readonly UiFactory.ActionSkin ConfirmSkin =
        new(30, UiMetrics.ButtonCornerRadius, 0);

    /// <summary>确认窗按钮配色：主操作青底白字，次操作浅青底；描边沿用全局按钮描边。</summary>
    private static readonly UiFactory.ActionPalette ConfirmPalette = new(
        UiTheme.Accent,
        UiTheme.White,
        null,
        UiTheme.DialogSecondary,
        UiTheme.TextPrimary,
        null);

    /// <summary>按强调档与定宽创建确认窗按钮。</summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="tone">强调档。</param>
    /// <param name="width">按钮定宽。</param>
    /// <param name="skin">尺寸档。</param>
    /// <returns>确认窗操作按钮。</returns>
    private static Button MakeAction(string text, UiFactory.ActionTone tone, double width, UiFactory.ActionSkin skin) =>
        UiFactory.ActionButton(text, tone, skin with { Width = width }, ConfirmPalette);

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

        var confirm = MakeAction(confirmText, UiFactory.ActionTone.Primary, ConfirmWidth, ConfirmSkin);
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

        var discard = MakeAction(discardText ?? string.Empty, UiFactory.ActionTone.Secondary, ConfirmWidth, ConfirmSkin);
        discard.IsVisible = threeWay;
        discard.Click += (_, _) => Close(ConfirmChoice.Discard);

        var cancel = MakeAction(cancelText, UiFactory.ActionTone.Neutral, CancelWidth, ConfirmSkin);
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

        WindowChromeTheme.Attach(this, closeAction: () =>
        {
            if (threeWay)
            {
                Close(ConfirmChoice.Cancel);
            }
            else
            {
                Close(false);
            }
        });
    }
}
