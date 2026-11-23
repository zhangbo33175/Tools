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
            Text = "物品元件编辑模式",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        buttons.Children.Add(BuildButton("保存", 70, UiTheme.Accent, UiTheme.White, async () => await SaveAsync()));
        buttons.Children.Add(BuildButton("添加元件", 86, null, null, (_, _) => AddTemplate()));
        buttons.Children.Add(BuildButton("导入配置", 86, null, null, async () => await ImportCsvAsync()));
        buttons.Children.Add(BuildButton("导出配置", 86, null, null, async () => await ExportCsvAsync()));

        _statusLabel.Foreground = UiTheme.TextSecondary;
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        _statusLabel.Margin = new Thickness(10, 0, 10, 0);
        _statusLabel.Text = "修改实时生效，点「保存」写盘并同步场景实例";

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(_statusLabel, 1);
        grid.Children.Add(_statusLabel);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);

        return new Border
        {
            Background = UiTheme.Form.SubtleSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5),
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
        var button = new Button
        {
            Content = text,
            Width = width,
            Padding = new Thickness(6, 4),
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
