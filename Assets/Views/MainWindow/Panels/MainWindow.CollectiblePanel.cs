/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.CollectiblePanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口收集物面板：统计格、素材行、数量刷新。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{
    private static TextBlock CreateCollectibleStatValue(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.CollectiblePanel.StatValueText,
            Margin = new Avalonia.Thickness(0, 1, 0, 0),
        };
    }

    private static Control BuildCollectibleStatCell(string label, TextBlock value, int column)
    {
        var panel = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = UiTheme.CollectiblePanel.StatLabelText,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(value);
        Grid.SetColumn(panel, column);
        return panel;
    }

    /// <summary>
    /// 收集物浮窗：头部是目标 / 已放 / 层级，下面每种素材一行。
    /// 行支持「点一下再点地图」和直接拖到地图两种摆放方式；随机生成一次补齐剩余数量。
    /// </summary>
    private void BuildCollectiblePanelList()
    {
        CollectiblePanelList.Children.Clear();
        _collectibleCountTexts.Clear();
        _collectibleGoalValue = null;
        _collectiblePlacedValue = null;
        _collectibleRemainValue = null;

        var plan = _config.Collectibles;
        var placed = _config.Objects.Count(item => item.IsCollectible);
        _collectibleGoalValue = CreateCollectibleStatValue(plan.Total.ToString(CultureInfo.InvariantCulture));
        _collectiblePlacedValue = CreateCollectibleStatValue(placed.ToString(CultureInfo.InvariantCulture));
        _collectibleRemainValue = CreateCollectibleStatValue(Math.Max(0, plan.Total - placed).ToString(CultureInfo.InvariantCulture));

        var stats = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,1,*,1,*"),
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
        };
        stats.Children.Add(BuildCollectibleStatCell("目标", _collectibleGoalValue, 0));
        var separator1 = new Border
        {
            Width = 1,
            Background = UiTheme.CollectiblePanel.StatSeparator,
            Margin = new Avalonia.Thickness(4, 8),
        };
        Grid.SetColumn(separator1, 1);
        stats.Children.Add(separator1);
        stats.Children.Add(BuildCollectibleStatCell("已放", _collectiblePlacedValue, 2));
        var separator2 = new Border
        {
            Width = 1,
            Background = UiTheme.CollectiblePanel.StatSeparator,
            Margin = new Avalonia.Thickness(4, 8),
        };
        Grid.SetColumn(separator2, 3);
        stats.Children.Add(separator2);
        stats.Children.Add(BuildCollectibleStatCell("剩余", _collectibleRemainValue, 4));

        CollectiblePanelList.Children.Add(new Border
        {
            Background = UiTheme.CollectiblePanel.SummarySurface,
            BorderBrush = UiTheme.CollectiblePanel.SummaryBorder,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(10, 7),
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
            Child = stats,
        });

        if (plan.Kinds.Count == 0)
        {
            CollectiblePanelList.Children.Add(new TextBlock
            {
                Text = "还没有选择收集物素材，点标题栏「重设」重新配置。",
                Foreground = UiTheme.TextSecondary,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(2, 4),
            });
            return;
        }

        var layerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Avalonia.Thickness(0, 0, 0, 5),
        };
        layerRow.Children.Add(new TextBlock
        {
            Text = "层级",
            Foreground = UiTheme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
        });
        var layerCombo = new ComboBox
        {
            ItemsSource = new[] { "物件下", "物件上" },
            SelectedIndex = CollectibleLayers.IsOver(plan.LayerMode) ? 1 : 0,
            FontSize = 11,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(layerCombo, 1);
        layerRow.Children.Add(layerCombo);
        layerCombo.SelectionChanged += (_, _) =>
        {
            var mode = layerCombo.SelectedIndex == 0 ? CollectibleLayers.UnderObjects : CollectibleLayers.OverObjects;
            plan.LayerMode = mode;
            foreach (var item in _config.Objects.Where(item => item.IsCollectible))
            {
                item.CollectibleOverObjects = CollectibleLayers.IsOver(mode);
            }

            Canvas.PendingCollectibleLayer = mode;
            Canvas.Refresh();
            SetMessage($"收集物层级已切换为「{CollectibleLayers.DisplayName(mode)}」：贴图与物件的前后关系立即生效。");
        };
        CollectiblePanelList.Children.Add(layerRow);

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
        };
        var randomButton = new Button
        {
            Content = $"随机生成剩余 {Math.Max(0, plan.Total - placed)}",
            FontSize = 11,
            Height = 28,
            Padding = new Avalonia.Thickness(6, 0),
            Margin = new Avalonia.Thickness(0, 0, 3, 0),
        };
        randomButton.Click += (_, _) => GenerateCollectiblesRandom();
        actions.Children.Add(randomButton);
        var clearButton = new Button
        {
            Content = "清空收集物",
            FontSize = 11,
            Height = 28,
            Padding = new Avalonia.Thickness(6, 0),
            Margin = new Avalonia.Thickness(3, 0, 0, 0),
        };
        Grid.SetColumn(clearButton, 1);
        clearButton.Click += (_, _) => ClearCollectibles();
        actions.Children.Add(clearButton);
        CollectiblePanelList.Children.Add(actions);

        CollectiblePanelList.Children.Add(new TextBlock
        {
            Text = $"素材（{plan.Kinds.Count} 种）",
            Foreground = UiTheme.CollectiblePanel.SectionText,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(1, 0, 0, 4),
        });

        foreach (var kind in plan.Kinds)
        {
            var template = kind.ToTemplate();
            var isSelected = Canvas.PendingCollectibleTemplate?.TemplateId == template.TemplateId;
            CollectiblePanelList.Children.Add(BuildCollectibleRow(template, isSelected));
        }

        RefreshCollectibleSummary();
    }

    /// <summary>收集物素材行：图标 + 名称 + 已放数量，支持点击选中和拖到画布落位。</summary>
    /// <param name="template">素材模板。</param>
    /// <param name="isSelected">是否是当前待摆放素材。</param>
    private Control BuildCollectibleRow(ObjectTemplate template, bool isSelected)
    {
        var swatch = BuildResourceIcon(template);
        var label = new TextBlock
        {
            Text = template.TemplateName,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(6, 0, 4, 0),
        };
        var count = new TextBlock
        {
            Text = "0",
            Foreground = UiTheme.CollectiblePanel.CountText,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 0, 8, 0),
        };
        _collectibleCountTexts[template.TemplateId] = count;

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        content.Children.Add(swatch);
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        Grid.SetColumn(count, 2);
        content.Children.Add(count);

        var row = new Button
        {
            Content = content,
            Height = 34,
            Padding = new Avalonia.Thickness(7, 0),
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = UiTheme.Card,
            BorderBrush = UiTheme.CollectiblePanel.RowBorder,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(row, "点击选中后到地图摆放；也可以直接拖到地图。");
        if (isSelected)
        {
            row.Background = UiTheme.RowSelectedBack;
            row.BorderBrush = UiTheme.RowSelectedBorder;
        }

        void SelectTemplate()
        {
            SetPathEditing(false, notify: false);
            _pendingTemplate = null;
            Canvas.PendingTemplate = null;
            Canvas.PendingCollectibleTemplate = template;
            Canvas.PendingCollectibleLayer = _config.Collectibles.LayerMode;
            BuildCollectiblePanelList();
            SetMessage($"已选择收集物：{template.TemplateName}，点击地图摆放；可直接点在物件上，层级跟随「{CollectibleLayers.DisplayName(_config.Collectibles.LayerMode)}」。");
        }

        row.Click += (_, _) =>
        {
            if (_collectibleDragMoved)
            {
                return;
            }

            SelectTemplate();
        };

        // Button 的类处理器会先把 PointerPressed 标成 Handled；这里用 Tunnel + handledEventsToo
        // 才能可靠拿到按下 / 移动 / 松开，否则拖拽永远启动不了。
        row.AddHandler(
            InputElement.PointerPressedEvent,
            (_, e) =>
            {
                if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                _collectibleDragTemplate = template;
                _collectibleDragStart = e.GetPosition(Canvas);
                _collectibleDragMoved = false;
                e.Pointer.Capture(row);
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        row.AddHandler(
            InputElement.PointerMovedEvent,
            (_, e) =>
            {
                if (_collectibleDragTemplate is null || !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                var point = e.GetPosition(Canvas);
                if (Math.Abs(point.X - _collectibleDragStart.X) < 6 && Math.Abs(point.Y - _collectibleDragStart.Y) < 6)
                {
                    return;
                }

                _collectibleDragMoved = true;
                SetMessage($"拖拽收集物：把「{template.TemplateName}」拖到地图上松开。");
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        row.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, e) =>
            {
                if (_collectibleDragTemplate is null)
                {
                    return;
                }

                var dragged = _collectibleDragMoved;
                var point = e.GetPosition(Canvas);
                e.Pointer.Capture(null);
                _collectibleDragTemplate = null;
                _collectibleDragMoved = false;

                if (!dragged)
                {
                    // 立即捕获指针后 Button 自己不会再发 Click，这里补回“轻点选中素材”的语义。
                    if (new Rect(row.Bounds.Size).Contains(e.GetPosition(row)))
                    {
                        SelectTemplate();
                        e.Handled = true;
                    }

                    return;
                }

                if (point.X < 0 || point.Y < 0 || point.X > Canvas.Bounds.Width || point.Y > Canvas.Bounds.Height)
                {
                    SetMessage("拖拽已取消：请把收集物拖到地图画布内再松开。");
                    e.Handled = true;
                    return;
                }

                SetPathEditing(false, notify: false);
                _pendingTemplate = null;
                Canvas.PendingTemplate = null;
                Canvas.PendingCollectibleTemplate = template;
                Canvas.PendingCollectibleLayer = _config.Collectibles.LayerMode;
                var placed = Canvas.PlaceCollectibleAt(template, point, _config.Collectibles.LayerMode);
                if (placed is null)
                {
                    SetMessage("摆放失败：画布没有地图数据，请先新建或打开一张地图。");
                    e.Handled = true;
                    return;
                }

                BuildCollectiblePanelList();
                SetMessage($"已手动摆放收集物：{template.TemplateName}（{CollectibleLayers.DisplayName(_config.Collectibles.LayerMode)}）。");
                e.Handled = true;
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        return row;
    }

    /// <summary>DocumentChanged 时只刷新收集物数量文字，不重建整页，避免拖动物件时反复重建列表。</summary>
    private void RefreshCollectibleSummary()
    {
        var plan = _config.Collectibles;
        var placed = _config.Objects.Count(item => item.IsCollectible);
        if (_collectibleGoalValue is not null)
        {
            _collectibleGoalValue.Text = plan.Total.ToString(CultureInfo.InvariantCulture);
        }

        if (_collectiblePlacedValue is not null)
        {
            _collectiblePlacedValue.Text = placed.ToString(CultureInfo.InvariantCulture);
        }

        if (_collectibleRemainValue is not null)
        {
            _collectibleRemainValue.Text = Math.Max(0, plan.Total - placed).ToString(CultureInfo.InvariantCulture);
        }

        foreach (var pair in _collectibleCountTexts)
        {
            pair.Value.Text = _config.Objects.Count(item => item.IsCollectible && item.TemplateId == pair.Key).ToString(CultureInfo.InvariantCulture);
        }
    }
}

