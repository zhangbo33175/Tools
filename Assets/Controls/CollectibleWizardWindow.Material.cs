/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.Material.cs
 * author:    云毅
 * created:   2026
 * descrip:   收集物向导第二步：已选槽位、候选素材网格与缩略图构建
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
    #region 第二步：素材网格

    private Control BuildMaterialStep()
    {
        var card = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Avalonia.Thickness(14),
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 10,
        };
        card.Child = root;

        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        head.Children.Add(new TextBlock
        {
            Text = "步骤 2 / 2 · 选择收集物素材",
            Foreground = UiTheme.PreviewText,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
        });
        _selectedCountText.Foreground = UiTheme.WizardAccent;
        _selectedCountText.FontSize = 12;
        _selectedCountText.FontWeight = FontWeight.SemiBold;
        _selectedCountText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_selectedCountText, 1);
        head.Children.Add(_selectedCountText);
        root.Children.Add(head);

        _slotPanel.Orientation = Orientation.Horizontal;
        var slotScroll = new ScrollViewer
        {
            Content = _slotPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 82,
        };
        Grid.SetRow(slotScroll, 1);
        root.Children.Add(slotScroll);

        _candidatePanel.Orientation = Orientation.Horizontal;
        var gridScroll = new ScrollViewer
        {
            Content = _candidatePanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(gridScroll, 2);
        root.Children.Add(gridScroll);

        RefreshMaterialStep();
        return card;
    }

    private void RefreshMaterialStep()
    {
        _slotPanel.Children.Clear();
        _candidatePanel.Children.Clear();
        _selectedCountText.Text = $"已选 {_selected.Count}/{MaxKinds}";
        // 选中 / 移除素材只刷新网格，不会重建底部按钮；这里同步「开始制作」可用状态
        if (_startButton is not null)
        {
            _startButton.IsEnabled = _selected.Count > 0;
        }

        for (var i = 0; i < MaxKinds; i++)
        {
            _slotPanel.Children.Add(i < _selected.Count
                ? BuildSelectedSlot(_selected[i])
                : BuildEmptySlot());
        }

        foreach (var template in _candidates)
        {
            _candidatePanel.Children.Add(BuildCandidateTile(template));
        }
    }

    private Control BuildSelectedSlot(ObjectTemplate template)
    {
        var image = BuildPreview(template, 48);
        var remove = new Button
        {
            Content = "×",
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Avalonia.Thickness(0),
            FontSize = 12,
            Background = UiTheme.WizardDanger,
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, -2, -2, 0),
            CornerRadius = new CornerRadius(10),
        };
        ToolTip.SetTip(remove, "从已选移除");
        remove.Click += (_, _) =>
        {
            _selected.RemoveAll(item => item.TemplateId == template.TemplateId);
            RefreshMaterialStep();
        };

        var content = new Grid();
        content.Children.Add(image);
        content.Children.Add(remove);

        var border = new Border
        {
            Width = 64,
            Height = 64,
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Avalonia.Thickness(6),
            Margin = new Avalonia.Thickness(0, 0, 6, 0),
            Child = content,
        };
        ToolTip.SetTip(border, template.TemplateName);
        return border;
    }

    private static Control BuildEmptySlot() => new Border
    {
        Width = 64,
        Height = 64,
        Background = UiTheme.PreviewSlot,
        BorderBrush = UiTheme.WizardEmptySlotBorder,
        BorderThickness = new Avalonia.Thickness(1),
        CornerRadius = new CornerRadius(8),
        Margin = new Avalonia.Thickness(0, 0, 6, 0),
        Child = new TextBlock
        {
            Text = "＋",
            Foreground = UiTheme.WizardEmptySlotHint,
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private Control BuildCandidateTile(ObjectTemplate template)
    {
        var selected = _selected.Any(item => item.TemplateId == template.TemplateId);
        var image = BuildPreview(template, 58);
        var name = new TextBlock
        {
            Text = template.TemplateName,
            Foreground = UiTheme.PreviewText,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 74,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var stack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(image);
        stack.Children.Add(name);

        var button = new Button
        {
            Width = 88,
            MinHeight = 92,
            Background = selected ? UiTheme.WizardAccentSoft : UiTheme.Card,
            BorderBrush = selected ? UiTheme.WizardAccent : UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(9),
            Padding = new Avalonia.Thickness(6, 5),
            Margin = new Avalonia.Thickness(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = stack,
        };
        ToolTip.SetTip(button, selected ? "点击取消选择" : "点击加入已选");
        button.Click += (_, _) =>
        {
            var existing = _selected.FindIndex(item => item.TemplateId == template.TemplateId);
            if (existing >= 0)
            {
                _selected.RemoveAt(existing);
            }
            else if (_selected.Count >= MaxKinds)
            {
                _statusText.Text = $"最多选择 {MaxKinds} 种素材；先移除一个已选再添加。";
                return;
            }
            else
            {
                _selected.Add(template);
            }

            RefreshMaterialStep();
        };
        return button;
    }

    private Control BuildPreview(ObjectTemplate template, double size)
    {
        Bitmap? bitmap = _assets.Load(template.ImagePath);
        if (bitmap is not null)
        {
            return new Border
            {
                Width = size,
                Height = size,
                Background = UiTheme.WizardThumbSurface,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }

        return new Border
        {
            Width = size,
            Height = size,
            Background = UiTheme.WizardThumbFallback,
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                Text = "无图",
                Foreground = UiTheme.PreviewHint,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    #endregion
}
