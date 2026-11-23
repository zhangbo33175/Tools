/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewWindow.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   动画预览窗布局骨架：顶栏、主体、底栏三段与窗口根网格
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class AnimationPreviewWindow : Window
{
    #region 布局

    /// <summary>搭建顶栏 / 左侧列表 / 中心画布 / 右侧信息四块。</summary>
    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions($"{HeaderHeight},*,{FooterHeight}"),
            Background = UiTheme.PreviewPanel,
        };

        var header = BuildHeader();
        var body = BuildBody();
        var footer = BuildFooter();
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(footer);
        return root;
    }

    /// <summary>顶栏：标题、筛选、搜索、播放控制、导入。</summary>

    private Control BuildHeader()
    {
        var accent = new Border
        {
            Width = 3,
            Height = 30,
            CornerRadius = new CornerRadius(2),
            Background = UiTheme.Accent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var title = new TextBlock
        {
            Text = "场景动画预览",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.PreviewText,
        };
        var subtitle = new TextBlock
        {
            Text = "只读地图数据 · 动画与路径移动演示",
            FontSize = 11,
            Foreground = UiTheme.PreviewHint,
            Margin = new Thickness(0, 1, 0, 0),
        };

        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                accent,
                new StackPanel
                {
                    Spacing = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { title, subtitle },
                },
            },
        };

        _filterBox.ItemsSource = FilterLabels;
        _filterBox.SelectedIndex = FilterAll;
        _filterBox.Width = FilterWidth;
        _filterBox.Height = ToolbarControlHeight;
        _filterBox.FontSize = 12;
        _filterBox.VerticalAlignment = VerticalAlignment.Center;
        _filterBox.SelectionChanged += (_, _) => RebuildVisibleList();

        _searchBox.Width = SearchWidth;
        _searchBox.Height = ToolbarControlHeight;
        _searchBox.FontSize = 12;
        _searchBox.Padding = new Thickness(9, 0);
        _searchBox.PlaceholderText = "搜索名称 / ID / 动画 / 资产";
        _searchBox.TextChanged += (_, _) => RebuildVisibleList();

        UiFactory.ApplyPreviewToolbarButton(_playButton, "暂停", 80, primary: true);
        _playButton.Click += (_, _) => TogglePlayback();

        var restart = UiFactory.PreviewToolbarButton("重新开始", 86);
        restart.Click += (_, _) =>
        {
            _canvas.RestartPlayback();
            _clock.Restart();
            _lastTick = 0;
            UpdateInfo();
        };

        var importSpine = UiFactory.PreviewToolbarButton("导入 Spine", 96);
        importSpine.Click += async (_, _) => await ImportSpineAsync();

        var close = UiFactory.PreviewToolbarButton("关闭", 64);
        close.Click += (_, _) => Close();

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(18, 0, 0, 0),
            Children =
            {
                _filterBox,
                _searchBox,
            },
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _playButton,
                restart,
                importSpine,
                close,
            },
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(16, 0),
        };
        grid.Children.Add(titlePanel);
        Grid.SetColumn(filters, 1);
        grid.Children.Add(filters);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };
    }

    /// <summary>主体：左侧对象列表 + 中心画布 + 右侧信息。</summary>

    private Control BuildBody()
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{ObjectPaneWidth},{PaneGap},*,{PaneGap},{InfoPaneWidth}"),
            Margin = new Thickness(BodyHorizontalMargin, BodyVerticalMargin, BodyHorizontalMargin, BodyVerticalMargin),
        };

        var objectPane = BuildObjectPane();
        var canvasPane = BuildCanvasPane();
        var infoPane = BuildInfoPane();
        Grid.SetColumn(canvasPane, 2);
        Grid.SetColumn(infoPane, 4);
        body.Children.Add(objectPane);
        body.Children.Add(canvasPane);
        body.Children.Add(infoPane);
        return body;
    }

    /// <summary>左侧对象列表卡。</summary>

    private Control BuildFooter()
    {
        _statusText.FontSize = 11;
        _statusText.Foreground = UiTheme.PreviewHint;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.Text = "选择物体后显示动画与移动信息；Spine 资源可从当前窗口导入。";
        _statusText.TextTrimming = TextTrimming.CharacterEllipsis;

        _countText.FontSize = 11;
        _countText.FontWeight = FontWeight.SemiBold;
        _countText.Foreground = UiTheme.Accent;
        _countText.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(16, 0),
            Children = { _statusText, _countText },
        };
        Grid.SetColumn(_countText, 1);

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
    }

    #endregion
}
