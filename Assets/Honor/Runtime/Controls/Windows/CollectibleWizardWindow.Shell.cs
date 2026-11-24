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

    // ---TODO 视觉改版：统一顶部品牌区、动态步骤徽标与底栏卡片化样式。
    private Control BuildShell()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(24),
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(0, 0, 0, 16),
        };

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
        };
        brand.Children.Add(new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(14),
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(1),
            Child = new TextBlock
            {
                Text = "＋",
                Foreground = UiTheme.WizardTitle,
                FontSize = 24,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });

        var titleStack = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleStack.Children.Add(new TextBlock
        {
            Text = "添加收集物",
            Foreground = UiTheme.PreviewText,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "设定收集物规模，再从素材库挑选要在地图上出现的外观。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
        });
        brand.Children.Add(titleStack);
        header.Children.Add(brand);

        var stepBadge = new Border
        {
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Avalonia.Thickness(14, 7),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var badgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
        };
        badgeRow.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = UiTheme.WizardAccent,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _stepBadgeText.Foreground = UiTheme.WizardTitle;
        _stepBadgeText.FontSize = 12;
        _stepBadgeText.FontWeight = FontWeight.SemiBold;
        _stepBadgeText.VerticalAlignment = VerticalAlignment.Center;
        badgeRow.Children.Add(_stepBadgeText);
        stepBadge.Child = badgeRow;
        Grid.SetColumn(stepBadge, 1);
        header.Children.Add(stepBadge);
        root.Children.Add(header);

        _contentHost.Margin = new Avalonia.Thickness(0, 0, 0, 12);
        Grid.SetRow(_contentHost, 1);
        root.Children.Add(_contentHost);

        var footerContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        statusRow.Children.Add(UiFactory.MarkerDot(UiTheme.WizardAccent));
        _statusText.Foreground = UiTheme.PreviewHint;
        _statusText.FontSize = 12;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.TextWrapping = TextWrapping.Wrap;
        statusRow.Children.Add(_statusText);
        footerContent.Children.Add(statusRow);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(buttons, 1);
        footerContent.Children.Add(buttons);
        _footerButtons = buttons;

        var footer = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Avalonia.Thickness(14, 10),
            Child = footerContent,
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private readonly TextBlock _stepBadgeText = new();
    private StackPanel _footerButtons = null!;
    private Button? _startButton;

    #endregion

    #region 步骤切换

    /// <summary>
    /// 步骤间复用同一批输入控件时，先把它们从上一版视觉树摘下来。
    /// Avalonia 不允许一个控件同时挂在两个父级上；返回上一步 / 再进第二步都会重建容器。
    /// </summary>
    /// <param name="control">要复用的控件。</param>
    private static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl content when ReferenceEquals(content.Content, control):
                content.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
        }
    }

    private void ShowStep(int step)
    {
        _step = step;
        // ---TODO 视觉改版：步骤徽标随当前页实时同步，避免固定为「2 步完成」。
        _stepBadgeText.Text = $"第 {step + 1} 步 / 共 2 步";
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
            MinWidth = 102,
            Height = 36,
            Padding = new Avalonia.Thickness(18, 0),
            FontSize = 13,
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Avalonia.Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (primary)
        {
            button.Background = UiTheme.WizardAccent;
            button.Foreground = UiTheme.White;
            button.BorderBrush = UiTheme.WizardAccentDark;
            button.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            button.Background = UiTheme.Card;
            button.Foreground = UiTheme.TextPrimary;
            button.BorderBrush = UiTheme.Divider;
        }

        return button;
    }

    #endregion
}
