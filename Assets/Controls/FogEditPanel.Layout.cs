/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FogEditPanel.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   迷雾编辑面板布局：顶栏、笔刷设置、区域列表与内嵌画布
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class FogEditPanel : UserControl
{
    #region 布局

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = UiTheme.Form.SubtleSurface,
        };

        root.Children.Add(BuildToolbar());

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{SettingsWidth},8,*"),
            Margin = new Thickness(4, 4, 4, 4),
        };

        body.Children.Add(BuildSettingsPanel());

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("*,220"),
        };

        var canvasBorder = new Border
        {
            Background = UiTheme.MapView.FogNeutral,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _canvas,
        };

        right.Children.Add(canvasBorder);

        var listCard = BuildRegionCard();
        Grid.SetRow(listCard, 1);
        listCard.Margin = new Thickness(0, 4, 0, 0);
        right.Children.Add(listCard);

        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        Grid.SetRow(body, 1);
        root.Children.Add(body);
        return root;
    }

    private Control BuildToolbar()
    {
        var title = new TextBlock
        {
            Text = "迷雾编辑模式",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var undoButton = new Button { Content = "撤销一笔", Padding = new Thickness(10, 4) };
        undoButton.Click += (_, _) => UndoLastStroke();
        right.Children.Add(undoButton);

        var resetButton = new Button { Content = "重置迷雾", Padding = new Thickness(10, 4) };
        resetButton.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        right.Children.Add(resetButton);

        var addButton = new Button { Content = "添加迷雾区域", Padding = new Thickness(10, 4) };
        addButton.Click += (_, _) => AddRegion();
        right.Children.Add(addButton);

        var clearButton = new Button
        {
            Content = "清空迷雾",
            Padding = new Thickness(10, 4),
            Background = UiTheme.Error,
            Foreground = UiTheme.White,
        };
        clearButton.Click += (_, _) => ClearRegions();
        right.Children.Add(clearButton);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = UiTheme.Form.SubtleSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5),
            Child = grid,
        };
    }

    private Control BuildSettingsPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(6), Spacing = 6 };

        stack.Children.Add(new TextBlock
        {
            Text = "笔刷设置",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
        });

        var brushSlider = new Slider
        {
            Minimum = MinBrushSize,
            Maximum = MaxBrushSize,
            Value = DefaultBrushSize,
            TickFrequency = BrushTick,
            IsSnapToTickEnabled = false,
        };

        var brushRow = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*,44") };
        brushRow.Children.Add(new TextBlock
        {
            Text = "笔刷大小",
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0),
        });
        Grid.SetColumn(brushSlider, 1);
        brushRow.Children.Add(brushSlider);
        _brushValue.Text = ((int)DefaultBrushSize).ToString();
        _brushValue.Foreground = UiTheme.TextSecondary;
        _brushValue.VerticalAlignment = VerticalAlignment.Center;
        _brushValue.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_brushValue, 2);
        brushRow.Children.Add(_brushValue);
        stack.Children.Add(brushRow);

        brushSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _loading)
            {
                return;
            }

            _brushValue.Text = ((int)brushSlider.Value).ToString();
            _canvas.FogBrushSize = brushSlider.Value;
            if (_selectedRegion is not null)
            {
                _selectedRegion.BrushSize = brushSlider.Value;
            }
        };

        var opacitySlider = new Slider
        {
            Minimum = MinOpacity,
            Maximum = MaxOpacity,
            Value = DefaultOpacity,
            TickFrequency = OpacityTick,
        };

        var opacityRow = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*,44") };
        opacityRow.Children.Add(new TextBlock
        {
            Text = "迷雾透明度",
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0),
        });
        Grid.SetColumn(opacitySlider, 1);
        opacityRow.Children.Add(opacitySlider);
        _opacityValue.Text = DefaultOpacity.ToString("0.00");
        _opacityValue.Foreground = UiTheme.TextSecondary;
        _opacityValue.VerticalAlignment = VerticalAlignment.Center;
        _opacityValue.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_opacityValue, 2);
        opacityRow.Children.Add(_opacityValue);
        stack.Children.Add(opacityRow);

        opacitySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _loading)
            {
                return;
            }

            _opacityValue.Text = opacitySlider.Value.ToString("0.00");
            _canvas.FogOpacity = opacitySlider.Value;
            if (_selectedRegion is not null && _fog is not null)
            {
                _selectedRegion.Opacity = opacitySlider.Value;
                _canvas.Refresh();
                FogChanged?.Invoke(this, EventArgs.Empty);
            }
        };

        _drawButton.Content = "开始绘制";
        _drawButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _drawButton.Padding = new Thickness(8, 5);
        _drawButton.Click += (_, _) => ToggleDrawing();
        stack.Children.Add(_drawButton);

        var shortcuts = new Border
        {
            Background = UiTheme.Card,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = "左键拖拽：绘制迷雾", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "右键拖拽：擦除迷雾", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "右键拖动 / 中键 / 空格+左键：平移画布", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "滚轮：缩放画布", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                },
            },
        };
        stack.Children.Add(shortcuts);

        var stackHost = new ScrollViewer { Content = stack };

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = stackHost,
        };
    }

    private Control BuildRegionCard()
    {
        _regionHint.Text = "点击「开始绘制」后，在地图上左键拖拽绘制迷雾区域，右键拖拽擦除；\n" +
                           "点「添加迷雾区域」可先建一个空区域，再在其中涂抹。";
        _regionHint.Foreground = UiTheme.TextSecondary;
        _regionHint.TextWrapping = TextWrapping.Wrap;
        _regionHint.Margin = new Thickness(4, 4);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _regionHost,
        };

        _regionTitle.Text = "迷雾区域列表";
        _regionTitle.Foreground = UiTheme.AccentDark;
        _regionTitle.FontWeight = FontWeight.Bold;
        _regionTitle.Margin = new Thickness(0, 0, 0, 4);

        // 标题固定、列表占满剩余高度：用 StackPanel 会给列表无限高度，行数一多就冲出卡片
        var body = new Grid
        {
            Margin = new Thickness(8),
            RowSpacing = 4,
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(_regionTitle, 0);
        body.Children.Add(_regionTitle);

        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        _regionHost.Margin = new Thickness(4);
        scroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        var scrollArea = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14") };
        scrollArea.Children.Add(scroll);
        var scrollBar = new ClassicScrollBar { Target = scroll };
        Grid.SetColumn(scrollBar, 1);
        scrollArea.Children.Add(scrollBar);
        var listFrame = new Border
        {
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = scrollArea,
        };
        Grid.SetRow(listFrame, 1);
        body.Children.Add(listFrame);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = body,
            ClipToBounds = true,
        };
    }

    #endregion
}
