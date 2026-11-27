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
            // 与其它页面统一：浅青底，不再用中性灰
            Background = UiTheme.PanelSurface,
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
            CornerRadius = new CornerRadius(PanelCardCornerRadius),
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
            // 顶栏改成白底 + 底部细线，跟其它页面的页头统一，灰底会显脏
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = ToolbarPadding,
            Child = grid,
        };
    }

    /// <summary>构建工具栏按钮；危险操作使用白底红字红描边，避免整条红块抢视觉。</summary>
    /// <param name="content">按钮文案。</param>
    /// <param name="onClick">点击回调。</param>
    /// <param name="danger">是否按危险操作着色。</param>
    private Button BuildToolbarButton(string content, Action onClick, bool danger = false)
    {
        var button = danger
            ? UiFactory.CompactButton(content, UiTheme.Card, UiTheme.Error, ToolbarButtonPadding)
            : UiFactory.CompactButton(content, null, null, ToolbarButtonPadding);
        if (danger)
        {
            button.BorderBrush = UiTheme.Error;
            button.FontWeight = FontWeight.SemiBold;
        }

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

        var fields = new StackPanel { Spacing = SettingBlockSpacing };
        fields.Children.Add(BuildSettingBlock(BrushSizeLabel, brushSlider, _brushValue));
        fields.Children.Add(BuildSettingBlock(OpacityLabel, opacitySlider, _opacityValue));
        stack.Children.Add(BuildSettingsCard(fields));

        _drawButton.Content = StartDrawingText;
        _drawButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _drawButton.Padding = DrawButtonPadding;
        _drawButton.Height = DrawButtonHeight;
        _drawButton.CornerRadius = new CornerRadius(DrawButtonCornerRadius);
        // 首帧也要是实心强调色：只在点击回调里换色的话，初始态会是一枚灰蓝的空按钮
        _drawButton.Background = UiTheme.Accent;
        _drawButton.BorderBrush = UiTheme.Accent;
        _drawButton.Foreground = UiTheme.White;
        _drawButton.FontWeight = FontWeight.SemiBold;
        _drawButton.Click += (_, _) => ToggleDrawing();
        stack.Children.Add(_drawButton);

        stack.Children.Add(BuildShortcutCard());

        var stackHost = new ScrollViewer { Content = stack };

        return BuildPanelCard(stackHost, UiTheme.Card);
    }

    /// <summary>构建设置区小标题：复用 <see cref="UiFactory.SectionHead"/>，字号取设置区标签档。</summary>
    /// <param name="text">标题文案。</param>
    private static Control BuildSectionTitle(string text) =>
        UiFactory.SectionHead(text, SettingLabelFontSize);

    /// <summary>白底圆角卡片装笔刷字段，和属性表单的分组卡保持同一套观感。</summary>
    /// <param name="child">卡片内容。</param>
    private static Border BuildSettingsCard(Control child)
    {
        // 与剧情页的分组块同一套：极浅青底 + 细描边，不再用「白卡套白卡」的双层边框
        return new Border
        {
            Background = UiTheme.Form.GroupHeader,
            BorderBrush = UiTheme.Form.GroupBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(SettingsCardCornerRadius),
            Padding = SettingsCardPadding,
            Child = child,
        };
    }

    /// <summary>构建单个设置项：标签 + 数值胶囊在上，滑块独占一行，窄栏里也不会挤成一团。</summary>
    /// <param name="label">设置项名称。</param>
    /// <param name="slider">滑块控件。</param>
    /// <param name="value">数值标签，塞进浅青胶囊里。</param>
    private static Control BuildSettingBlock(string label, Slider slider, TextBlock value)
    {
        value.Foreground = UiTheme.AccentDark;
        value.FontSize = SettingValueFontSize;
        value.FontWeight = FontWeight.SemiBold;
        value.VerticalAlignment = VerticalAlignment.Center;

        var badge = new Border
        {
            Background = UiTheme.PreviewAccentSoft,
            CornerRadius = SettingValueCornerRadius,
            Padding = SettingValuePadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = value,
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = UiTheme.TextPrimary,
            FontSize = SettingLabelFontSize,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);

        slider.Margin = SettingSliderMargin;

        var block = new StackPanel();
        block.Children.Add(header);
        block.Children.Add(slider);
        return block;
    }

    /// <summary>构建左栏快捷键说明卡片，说明文本集中来自 <see cref="ShortcutHints"/>。</summary>
    private static Border BuildShortcutCard()
    {
        var panel = new StackPanel { Spacing = HintSpacing };
        var title = BuildSectionTitle(ShortcutTitleText);
        title.Margin = HintTitleMargin;
        panel.Children.Add(title);

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = UiTheme.Divider,
            Margin = HintDividerMargin,
        });

        foreach (var hint in ShortcutHints)
        {
            // 「按键：说明」拆成左侧按键胶囊 + 右侧说明，扫读时按键一眼可辨
            var parts = hint.Split('：');
            var key = parts[0];
            var desc = parts.Length > 1 ? parts[1] : string.Empty;

            var chip = new Border
            {
                // 白卡上的胶囊要能一眼看出边界：浅青底 + 青灰描边，比纯白底更能立住「按键」
                Background = UiTheme.PreviewAccentSoft,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(ShortcutChipCornerRadius),
                Padding = ShortcutChipPadding,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = key,
                    Foreground = UiTheme.AccentDark,
                    FontSize = HintFontSize,
                    FontWeight = FontWeight.SemiBold,
                },
            };

            var text = new TextBlock
            {
                Text = desc,
                Foreground = UiTheme.TextSecondary,
                FontSize = HintFontSize,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = HintKeyGap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Children.Add(chip);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }

        return BuildSettingsCard(panel);
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
            // 圆角 8 与场景编辑 / 物品元件 / 剧情各页的卡片一致
            CornerRadius = new CornerRadius(PanelCardCornerRadius),
            Child = child,
            ClipToBounds = clipToBounds,
        };
    }

    private Control BuildRegionCard()
    {
        _regionHint.Text = RegionHintText;
        _regionHint.Foreground = UiTheme.TextSecondary;
        _regionHint.FontSize = HintFontSize;
        _regionHint.TextWrapping = TextWrapping.Wrap;
        _regionHint.TextAlignment = TextAlignment.Center;
        // 空列表时提示居中偏上，别吊在卡片左上角
        _regionHint.Margin = new Thickness(12, 30, 12, 0);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _regionHost,
        };

        _regionTitle.Text = RegionListTitle;
        _regionTitle.Foreground = UiTheme.AccentDark;
        _regionTitle.FontWeight = FontWeight.Bold;
        _regionTitle.VerticalAlignment = VerticalAlignment.Center;

        // 标题与小标题统一成「3px 青色竖条 + 加粗文字」，竖条由 BuildSectionTitle 那套语汇复用
        var titleBox = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = RegionTitleMargin,
            Children =
            {
                new Border
                {
                    Width = 3,
                    Height = 12,
                    CornerRadius = new CornerRadius(2),
                    Background = UiTheme.Accent,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                _regionTitle,
            },
        };

        // 标题固定、列表占满剩余高度：用 StackPanel 会给列表无限高度，行数一多就冲出卡片
        var body = new Grid
        {
            Margin = RegionBodyMargin,
            RowSpacing = RegionBodyRowSpacing,
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(titleBox, 0);
        body.Children.Add(titleBox);

        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧经典箭头滚动条
        _regionHost.Margin = RegionListMargin;
        scroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        var listFrame = UiFactory.ScrollListFrame(scroll);
        Grid.SetRow(listFrame, 1);
        body.Children.Add(listFrame);

        return BuildPanelCard(body, UiTheme.Card, clipToBounds: true);
    }

    #endregion
}
