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

    // ---TODO 视觉改版：已选槽位独立卡片、素材库分区与选中态角标。
    private Control BuildMaterialStep()
    {
        DetachFromParent(_slotPanel);
        DetachFromParent(_candidatePanel);
        DetachFromParent(_selectedCountText);

        var card = UiFactory.PaneCard(cornerRadius: 16, padding: 20);
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 14,
        };
        card.Child = root;

        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        var headStack = new StackPanel { Spacing = 3 };
        headStack.Children.Add(new TextBlock
        {
            Text = "第二步 · 选择素材",
            Foreground = UiTheme.WizardAccent,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "选择要作为收集物出现的素材",
            Foreground = UiTheme.PreviewText,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
        });
        headStack.Children.Add(new TextBlock
        {
            Text = $"最多 {MaxKinds} 种；已选内容会保留在左侧「收集物」切页。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
        });
        head.Children.Add(headStack);

        var countBadge = new Border
        {
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Avalonia.Thickness(12, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _selectedCountText.Foreground = UiTheme.WizardTitle;
        _selectedCountText.FontSize = 12;
        _selectedCountText.FontWeight = FontWeight.SemiBold;
        _selectedCountText.VerticalAlignment = VerticalAlignment.Center;
        countBadge.Child = _selectedCountText;
        Grid.SetColumn(countBadge, 1);
        head.Children.Add(countBadge);
        root.Children.Add(head);

        var slotCard = UiFactory.PaneCard(
            background: UiTheme.WizardAccentSoft,
            border: UiTheme.WizardAccent,
            cornerRadius: 12,
            padding: 12);
        var slotStack = new StackPanel { Spacing = 8 };
        slotStack.Children.Add(BuildSectionHead("已选素材", "点击缩略图右上角 × 可移除", UiTheme.WizardTitle, UiTheme.WizardInfoText));

        _slotPanel.Orientation = Orientation.Horizontal;
        var slotScroll = new ScrollViewer
        {
            Content = _slotPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 76,
        };
        slotStack.Children.Add(slotScroll);
        slotCard.Child = slotStack;
        Grid.SetRow(slotCard, 1);
        root.Children.Add(slotCard);

        var gridCard = UiFactory.PaneCard(
            background: UiTheme.WizardMapSurface,
            cornerRadius: 12,
            padding: 12);
        var gridLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 8,
        };
        gridLayout.Children.Add(BuildSectionHead("素材库", "点击卡片加入或取消选择", UiTheme.PreviewText, UiTheme.PreviewHint));

        _candidatePanel.Orientation = Orientation.Horizontal;
        var gridScroll = new ScrollViewer
        {
            Content = _candidatePanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Avalonia.Thickness(0, 2, 0, 0),
        };
        Grid.SetRow(gridScroll, 1);
        gridLayout.Children.Add(gridScroll);
        gridCard.Child = gridLayout;
        Grid.SetRow(gridCard, 2);
        root.Children.Add(gridCard);

        RefreshMaterialStep();
        return card;
    }

    /// <summary>构建素材步骤左侧标题 + 右侧说明的区块头。</summary>
    /// <param name="title">区块标题。</param>
    /// <param name="hint">右侧操作说明。</param>
    /// <param name="titleBrush">标题颜色。</param>
    /// <param name="hintBrush">说明颜色。</param>
    /// <returns>两列区块头。</returns>
    private static Control BuildSectionHead(string title, string hint, IBrush titleBrush, IBrush hintBrush)
    {
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        head.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = titleBrush,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        });
        var hintText = new TextBlock
        {
            Text = hint,
            Foreground = hintBrush,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(hintText, 1);
        head.Children.Add(hintText);
        return head;
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
            Foreground = UiTheme.White,
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
        var image = BuildPreview(template, 62);
        var name = new TextBlock
        {
            Text = template.TemplateName,
            Foreground = selected ? UiTheme.WizardTitle : UiTheme.PreviewText,
            FontSize = 11,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 84,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stack.Children.Add(image);
        stack.Children.Add(name);

        var check = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = UiTheme.WizardAccent,
            BorderBrush = UiTheme.White,
            BorderThickness = new Avalonia.Thickness(1),
            IsVisible = selected,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, 3, 3, 0),
            Child = new TextBlock
            {
                Text = "✓",
                Foreground = UiTheme.White,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var content = new Grid();
        content.Children.Add(stack);
        content.Children.Add(check);

        var button = new Button
        {
            Width = 100,
            MinHeight = 112,
            Background = selected ? UiTheme.WizardAccentSoft : UiTheme.Card,
            BorderBrush = selected ? UiTheme.WizardAccent : UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(11),
            Padding = new Avalonia.Thickness(6, 7),
            Margin = new Avalonia.Thickness(0, 0, 9, 9),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = content,
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
