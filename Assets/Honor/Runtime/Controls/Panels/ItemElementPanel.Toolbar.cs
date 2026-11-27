/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Toolbar.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件面板工具条：标题、状态、保存/添加/导入/导出与统一按钮工厂
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 工具条

    private Control BuildToolbar()
    {
        var title = new TextBlock
        {
            Text = ToolbarTitle,
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ToolbarButtonSpacing,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        // 主行动（保存）：实心强调色 + 半粗字，与「资源整理 → 保存修改」同一套主按钮语言
        var save = BuildButton(SaveButtonText, SaveButtonWidth, UiTheme.Accent, UiTheme.White, async () => await SaveAsync());
        save.FontWeight = FontWeight.SemiBold;
        save.BorderBrush = UiTheme.Accent;
        buttons.Children.Add(save);
        buttons.Children.Add(BuildButton(AddButtonText, ActionButtonWidth, null, null, (_, _) => AddTemplate()));
        buttons.Children.Add(BuildButton(ImportButtonText, ActionButtonWidth, null, null, async () => await ImportCsvAsync()));
        buttons.Children.Add(BuildButton(ExportButtonText, ActionButtonWidth, null, null, async () => await ExportCsvAsync()));

        _statusLabel.Foreground = UiTheme.TextSecondary;
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        _statusLabel.Margin = ToolbarStatusMargin;
        _statusLabel.Text = ToolbarHint;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(_statusLabel, 1);
        grid.Children.Add(_statusLabel);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);

        return new Border
        {
            // 顶栏白底 + 底部细线，与迷雾 / 剧情页的页头统一，灰底会显得脏
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = ToolbarPadding,
            Child = grid,
        };
    }

    /// <summary>创建工具条按钮的统一外观；点击行为由调用方追加。</summary>
    /// <param name="text">按钮文字。</param>
    /// <param name="width">固定宽度。</param>
    /// <param name="background">底色；null 用主题默认。</param>
    /// <param name="foreground">文字色；null 用主题默认。</param>
    private static Button CreateButton(string text, double width, IBrush? background, IBrush? foreground)
    {
        var button = UiFactory.CompactButton(text, background, foreground, ToolbarButtonPadding);
        button.Width = width;
        return button;
    }

    private static Button BuildButton(string text, double width, IBrush? background, IBrush? foreground,
        EventHandler<RoutedEventArgs>? handler)
    {
        var button = CreateButton(text, width, background, foreground);
        if (handler is not null)
        {
            button.Click += handler;
        }

        return button;
    }

    private static Button BuildButton(string text, double width, IBrush? background, IBrush? foreground, Func<Task> action)
    {
        var button = CreateButton(text, width, background, foreground);
        button.Click += async (_, _) => await action();
        return button;
    }

    #endregion
}
