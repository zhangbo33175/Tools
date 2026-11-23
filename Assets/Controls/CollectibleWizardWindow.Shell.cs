/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.Shell.cs
 * author:    云毅
 * created:   2026
 * descrip:   收集物向导外壳：窗口骨架、步骤切换、底栏按钮与通用按钮样式
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class CollectibleWizardWindow : Window
{
    #region 外壳

    private Control BuildShell()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(18),
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
        };
        var titleStack = new StackPanel { Spacing = 3 };
        titleStack.Children.Add(new TextBlock
        {
            Text = "添加收集物",
            Foreground = UiTheme.PreviewText,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
        });
        var subtitle = new TextBlock
        {
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
        };
        titleStack.Children.Add(subtitle);
        header.Children.Add(titleStack);

        var stepBadge = new Border
        {
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Avalonia.Thickness(12, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        stepBadge.Child = new TextBlock
        {
            Text = "2 步完成",
            Foreground = UiTheme.WizardTitle,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        };
        Grid.SetColumn(stepBadge, 1);
        header.Children.Add(stepBadge);
        root.Children.Add(header);

        _contentHost.Margin = new Avalonia.Thickness(0, 2, 0, 8);
        Grid.SetRow(_contentHost, 1);
        root.Children.Add(_contentHost);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        _statusText.Foreground = UiTheme.PreviewHint;
        _statusText.FontSize = 12;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(_statusText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(buttons, 2);
        footer.Children.Add(buttons);
        _footerButtons = buttons;
        // footer 必须落在第三行，否则默认回到第 0 行，会和标题栏 / 步骤徽标叠在一起
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private StackPanel _footerButtons = null!;
    private Button? _startButton;

    #endregion

    #region 步骤切换

    private void ShowStep(int step)
    {
        _step = step;
        _contentHost.Content = step == 0 ? BuildQuantityStep() : BuildMaterialStep();
        RebuildFooter();
        _statusText.Text = step == 0
            ? "先定目标数量；层级可以之后在左侧收集物切页里继续调整。"
            : $"从物件表选择要作为收集物的素材，最多 {MaxKinds} 种；已选内容会保留在左侧切页。";
    }

    private void RebuildFooter()
    {
        _footerButtons.Children.Clear();
        if (_step == 0)
        {
            _startButton = null;
            var cancel = MakeButton("取消", false);
            cancel.Click += (_, _) => Close(false);
            _footerButtons.Children.Add(cancel);

            var next = MakeButton("下一步 →", true);
            next.Click += (_, _) =>
            {
                if (_candidates.Count == 0)
                {
                    _statusText.Text = "没有可选素材。";
                    return;
                }

                ShowStep(1);
            };
            _footerButtons.Children.Add(next);
        }
        else
        {
            var back = MakeButton("← 上一步", false);
            back.Click += (_, _) => ShowStep(0);
            _footerButtons.Children.Add(back);

            var cancel = MakeButton("取消", false);
            cancel.Click += (_, _) => Close(false);
            _footerButtons.Children.Add(cancel);

            var start = MakeButton("开始制作", true);
            _startButton = start;
            start.IsEnabled = _selected.Count > 0;
            start.Click += (_, _) =>
            {
                if (_selected.Count == 0)
                {
                    _statusText.Text = "至少选择一种收集物素材。";
                    return;
                }

                Close(true);
            };
            _footerButtons.Children.Add(start);
        }
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 96,
            Height = 34,
            Padding = new Avalonia.Thickness(16, 0),
            FontSize = 13,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (primary)
        {
            button.Background = UiTheme.WizardAccent;
            button.Foreground = Brushes.White;
            button.BorderBrush = UiTheme.WizardAccentDark;
            button.FontWeight = FontWeight.SemiBold;
        }

        return button;
    }

    #endregion
}
