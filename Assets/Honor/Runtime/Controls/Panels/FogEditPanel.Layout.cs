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
            ColumnDefinitions = new ColumnDefinitions($"{SettingsWidth},{BodyColumnGap},*"),
            Margin = BodyMargin,
        };

        body.Children.Add(BuildSettingsPanel());

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions($"*,{RegionListHeight}"),
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
            Text = PanelTitle,
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ToolbarButtonSpacing,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                BuildToolbarButton(UndoButtonText, UndoLastStroke),
                BuildToolbarButton(ResetButtonText, () => ResetRequested?.Invoke(this, EventArgs.Empty)),
                BuildToolbarButton(AddRegionButtonText, AddRegion),
                BuildToolbarButton(ClearRegionsButtonText, ClearRegions, danger: true),
            },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = UiTheme.Form.SubtleSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = ToolbarPadding,
            Child = grid,
        };
    }

    /// <summary>构建工具栏按钮；危险操作使用错误色底与白色文字。</summary>
    /// <param name="content">按钮文案。</param>
    /// <param name="onClick">点击回调。</param>
    /// <param name="danger">是否按危险操作着色。</param>
    private Button BuildToolbarButton(string content, Action onClick, bool danger = false)
    {
        var button = UiFactory.CompactButton(
            content,
            danger ? UiTheme.Error : null,
            danger ? UiTheme.White : null,
            ToolbarButtonPadding);
        button.Click += (_, _) => onClick();
        return button;
    }

    private Control BuildSettingsPanel()
    {
        var stack = new StackPanel { Margin = SettingsMargin, Spacing = SettingsSpacing };
        stack.Children.Add(BuildSectionTitle(SettingsTitleText));

        var brushSlider = new Slider
        {
            Minimum = MinBrushSize,
            Maximum = MaxBrushSize,
            Value = DefaultBrushSize,
            TickFrequency = BrushTick,
            IsSnapToTickEnabled = false,
        };

        _brushValue.Text = ((int)DefaultBrushSize).ToString();
        stack.Children.Add(BuildSliderRow(BrushSizeLabel, brushSlider, _brushValue));

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

        _opacityValue.Text = DefaultOpacity.ToString("0.00");
        stack.Children.Add(BuildSliderRow(OpacityLabel, opacitySlider, _opacityValue));

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

        _drawButton.Content = StartDrawingText;
        _drawButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _drawButton.Padding = DrawButtonPadding;
        _drawButton.Click += (_, _) => ToggleDrawing();
        stack.Children.Add(_drawButton);

        stack.Children.Add(BuildShortcutCard());

        var stackHost = new ScrollViewer { Content = stack };

        return BuildPanelCard(stackHost, UiTheme.PanelSurface);
    }

    /// <summary>构建设置区小标题。</summary>
    /// <param name="text">标题文案。</param>
    private static TextBlock BuildSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
        };
    }

    /// <summary>构建统一的三列滑块行：右对齐标签、滑块、右对齐数值。</summary>
    /// <param name="label">左侧标签文案。</param>
    /// <param name="slider">滑块控件。</param>
    /// <param name="value">右侧数值标签。</param>
    private static Grid BuildSliderRow(string label, Slider slider, TextBlock value)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{SettingLabelWidth},*,{SettingValueWidth}"),
        };

        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = SettingLabelGap,
        });

        Grid.SetColumn(slider, 1);
        row.Children.Add(slider);

        value.Foreground = UiTheme.TextSecondary;
        value.VerticalAlignment = VerticalAlignment.Center;
        value.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(value, 2);
        row.Children.Add(value);
        return row;
    }

    /// <summary>构建左栏快捷键说明卡片，说明文本集中来自 <see cref="ShortcutHints"/>。</summary>
    private static Border BuildShortcutCard()
    {
        var panel = new StackPanel { Spacing = HintSpacing };
        foreach (var hint in ShortcutHints)
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                Foreground = UiTheme.TextSecondary,
                FontSize = HintFontSize,
            });
        }

        return new Border
        {
            Background = UiTheme.Card,
            CornerRadius = HintCornerRadius,
            Padding = HintPadding,
            Child = panel,
        };
    }

    /// <summary>构建带统一描边的面板卡片。</summary>
    /// <param name="child">卡片内容。</param>
    /// <param name="background">卡片背景。</param>
    /// <param name="clipToBounds">是否裁剪溢出内容。</param>
    private static Border BuildPanelCard(Control child, IBrush background, bool clipToBounds = false)
    {
        return new Border
        {
            Background = background,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = child,
            ClipToBounds = clipToBounds,
        };
    }

    private Control BuildRegionCard()
    {
        _regionHint.Text = RegionHintText;
        _regionHint.Foreground = UiTheme.TextSecondary;
        _regionHint.TextWrapping = TextWrapping.Wrap;
        _regionHint.Margin = RegionHintMargin;

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _regionHost,
        };

        _regionTitle.Text = RegionListTitle;
        _regionTitle.Foreground = UiTheme.AccentDark;
        _regionTitle.FontWeight = FontWeight.Bold;
        _regionTitle.Margin = RegionTitleMargin;

        // 标题固定、列表占满剩余高度：用 StackPanel 会给列表无限高度，行数一多就冲出卡片
        var body = new Grid
        {
            Margin = RegionBodyMargin,
            RowSpacing = RegionBodyRowSpacing,
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(_regionTitle, 0);
        body.Children.Add(_regionTitle);

        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧经典箭头滚动条
        _regionHost.Margin = RegionListMargin;
        scroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        var listFrame = UiFactory.ScrollListFrame(scroll);
        Grid.SetRow(listFrame, 1);
        body.Children.Add(listFrame);

        return BuildPanelCard(body, UiTheme.PanelSurface, clipToBounds: true);
    }

    #endregion
}
