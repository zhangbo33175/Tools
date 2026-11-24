/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewWindow.Panes.cs
 * author:    云毅
 * created:   2026
 * descrip:   动画预览窗三栏面板：物体列表、中心画布、右侧信息区与滚动条
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    #region 三栏面板

    private Control BuildObjectPane()
    {
        _objectList.Background = UiTheme.Transparent;
        _objectList.BorderThickness = new Thickness(0);
        _objectList.Margin = new Thickness(0, 6, 0, 8);
        _objectList.HorizontalAlignment = HorizontalAlignment.Stretch;
        _objectList.VerticalAlignment = VerticalAlignment.Stretch;
        _objectList.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(ConfigureListScrollBar, DispatcherPriority.Loaded);
        };
        _objectList.SelectionChanged += (_, _) =>
        {
            if (_refreshingList)
            {
                return;
            }

            if (_objectList.SelectedItem is ListBoxItem row && row.Tag is PlacedObject item)
            {
                HighlightRow(row);
                _canvas.Target = item;
                _canvas.RestartPlayback();
                _clock.Restart();
                _lastTick = 0;
                UpdateInfo();
            }
        };

        var title = new TextBlock
        {
            Text = "场景物体",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.PreviewText,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _listCountText.Text = "0";
        _listCountText.FontSize = 10;
        _listCountText.Foreground = UiTheme.Accent;
        _listCountText.VerticalAlignment = VerticalAlignment.Center;

        var countBadge = new Border
        {
            Background = UiTheme.PreviewAccentSoft,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _listCountText,
        };

        var titleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { title, countBadge },
        };
        Grid.SetColumn(countBadge, 1);

        var listGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { _objectList, _listScrollBar },
        };
        Grid.SetColumn(_listScrollBar, 1);

        var pane = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { titleRow, listGrid },
        };
        Grid.SetRow(listGrid, 1);
        return UiFactory.PaneCard(pane, padding: 10);
    }

    /// <summary>
    /// 隐藏 ListBox 主题自带的小滚动条，改用右侧 14px 的自绘滚动条；
    /// 自绘滑块至少 28px 高，任意位置按下都能拖动。
    /// </summary>

    private void ConfigureListScrollBar()
    {
        var scrollViewer = _objectList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _listScrollBar.Attach(scrollViewer);
    }

    /// <summary>中心画布卡。</summary>

    private Control BuildCanvasPane()
    {
        _canvas.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvas.VerticalAlignment = VerticalAlignment.Stretch;

        _canvasTitle.Text = "未选择物体";
        _canvasTitle.FontSize = 14;
        _canvasTitle.FontWeight = FontWeight.SemiBold;
        _canvasTitle.Foreground = UiTheme.PreviewText;
        _canvasTitle.TextTrimming = TextTrimming.CharacterEllipsis;

        _canvasSubtitle.Text = "从左侧列表选择一个物体";
        _canvasSubtitle.FontSize = 11;
        _canvasSubtitle.Foreground = UiTheme.PreviewHint;
        _canvasSubtitle.Margin = new Thickness(0, 1, 0, 0);
        _canvasSubtitle.TextTrimming = TextTrimming.CharacterEllipsis;

        _timeText.Text = "0.00s";
        _timeText.FontSize = 13;
        _timeText.FontWeight = FontWeight.SemiBold;
        _timeText.Foreground = UiTheme.Accent;
        _timeText.VerticalAlignment = VerticalAlignment.Center;

        _speedText.Text = FormatSpeed(1.0);
        _speedText.FontSize = 11;
        _speedText.Foreground = UiTheme.PreviewHint;
        _speedText.VerticalAlignment = VerticalAlignment.Center;

        _speedSlider.Minimum = MinPlaybackSpeed;
        _speedSlider.Maximum = MaxPlaybackSpeed;
        _speedSlider.Value = 1;
        _speedSlider.Width = 108;
        _speedSlider.Height = 24;
        _speedSlider.VerticalAlignment = VerticalAlignment.Center;
        _speedSlider.ValueChanged += value =>
        {
            _playbackSpeed = Math.Max(MinPlaybackSpeed, value);
            _speedText.Text = FormatSpeed(value);
        };

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 0, 0, 9),
        };
        top.Children.Add(new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _canvasTitle, _canvasSubtitle },
        });

        var playback = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _timeText, _speedText, _speedSlider },
        };
        Grid.SetColumn(playback, 1);
        top.Children.Add(playback);

        var canvasBorder = new Border
        {
            Background = UiTheme.PreviewView.Stage,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = _canvas,
        };

        var pane = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { top, canvasBorder },
        };
        Grid.SetRow(canvasBorder, 1);
        return UiFactory.PaneCard(pane, padding: 10);
    }

    /// <summary>右侧信息卡。</summary>

    private Control BuildInfoPane()
    {
        PrepareInfoValue(_nameValue);
        PrepareInfoValue(_idValue, monospace: true);
        PrepareInfoValue(_typeValue);
        PrepareInfoValue(_templateValue, monospace: true);
        PrepareInfoValue(_sourceValue);
        PrepareInfoValue(_assetValue);
        PrepareInfoValue(_clipValue);
        PrepareInfoValue(_durationValue);
        PrepareInfoValue(_stateValue);
        PrepareInfoValue(_pathValue);
        PrepareInfoValue(_pathLengthValue);
        PrepareInfoValue(_pathDurationValue);
        PrepareInfoValue(_moveSpeedValue);
        PrepareInfoValue(_boundsValue);
        PrepareInfoValue(_rotationValue);
        PrepareInfoValue(_stretchValue);

        var title = new TextBlock
        {
            Text = "动画信息",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.PreviewText,
            Margin = new Thickness(12, 10, 0, 0),
        };

        var sections = new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 12),
            Children =
            {
                BuildInfoSection("物体", new (string Label, TextBlock Value)[]
                {
                    ("名称", _nameValue),
                    ("ID", _idValue),
                    ("类型", _typeValue),
                    ("模板", _templateValue),
                }),
                MakeInfoDivider(),
                BuildInfoSection("动画", new (string Label, TextBlock Value)[]
                {
                    ("来源", _sourceValue),
                    ("资产 / 片段", _assetValue),
                    ("动画", _clipValue),
                    ("时长", _durationValue),
                    ("状态", _stateValue),
                }),
                MakeInfoDivider(),
                BuildInfoSection("移动", new (string Label, TextBlock Value)[]
                {
                    ("路径", _pathValue),
                    ("长度", _pathLengthValue),
                    ("单程", _pathDurationValue),
                    ("速度", _moveSpeedValue),
                }),
                MakeInfoDivider(),
                BuildInfoSection("外形", new (string Label, TextBlock Value)[]
                {
                    ("包围盒", _boundsValue),
                    ("旋转 / 翻转", _rotationValue),
                    ("拉伸", _stretchValue),
                }),
            },
        };

        var scroll = new ScrollViewer
        {
            Content = sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var pane = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { title, scroll },
        };
        Grid.SetRow(scroll, 1);
        return UiFactory.PaneCard(pane);
    }

    /// <summary>右侧信息分组：标题 + 标签值行。</summary>

    private Control BuildInfoSection(string title, (string Label, TextBlock Value)[] rows)
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.Accent,
            Margin = new Thickness(0, 0, 0, 2),
        });

        foreach (var (label, value) in rows)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("66,*"),
            };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = UiTheme.PreviewHint,
                Margin = new Thickness(0, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
            });
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            stack.Children.Add(row);
        }

        return stack;
    }


    private static void PrepareInfoValue(TextBlock value, bool monospace = false)
    {
        value.FontSize = monospace ? 11 : 12;
        value.Foreground = UiTheme.PreviewText;
        value.TextWrapping = TextWrapping.Wrap;
        value.VerticalAlignment = VerticalAlignment.Top;
        if (monospace)
        {
            value.FontFamily = new FontFamily("Consolas");
        }
    }


    private static Control MakeInfoDivider()
    {
        return new Border
        {
            Height = 1,
            Background = UiTheme.Divider,
            Margin = new Thickness(0, 12),
        };
    }

    /// <summary>底栏状态。</summary>

    #endregion
}
