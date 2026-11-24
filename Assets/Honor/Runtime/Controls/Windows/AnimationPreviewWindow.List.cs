/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewWindow.List.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景动画预览窗：对象列表、筛选、信息面板与初始选中
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

/// <summary>场景动画预览窗的列表与信息面板职责。</summary>
public sealed partial class AnimationPreviewWindow : Window
{
    #region 列表与信息

    /// <summary>
    /// 初始化列表数据源。这里只清空旧绑定，真正的行由
    /// <see cref="RebuildVisibleList"/> 按筛选条件构建。
    /// </summary>
    private void BuildObjectList()
    {
        _objectList.ItemsSource = null;
    }

    /// <summary>按筛选 / 搜索重建列表，保持当前选中项。</summary>
    private void RebuildVisibleList()
    {
        var selected = _canvas.Target;
        var filter = _filterBox.SelectedIndex;
        var keyword = (_searchBox.Text ?? string.Empty).Trim();

        var visible = _allObjects
            .Where(item => filter switch
            {
                FilterAnimated => item.AnimEnabled && (ResolveSpine(item) is not null || ResolveSprite(item) is not null),
                FilterSpineOnly => ResolveSpine(item) is not null,
                FilterWithPath => PathMath.HasPath(item.PathPoints),
                _ => true,
            })
            .Where(item => keyword.Length == 0
                || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.AnimName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.SpineAsset.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(AnimationRank)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _refreshingList = true;
        try
        {
            var items = new List<ListBoxItem>(visible.Count);
            foreach (var item in visible)
            {
                var row = new ListBoxItem
                {
                    Tag = item,
                    Content = BuildListRow(item),
                    Padding = new Thickness(7, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                };
                items.Add(row);
            }

            _visibleRows.Clear();
            _visibleRows.AddRange(items);
            _objectList.ItemsSource = items;
            HighlightRow(null);
            if (selected is not null)
            {
                for (var index = 0; index < items.Count; index++)
                {
                    if (ReferenceEquals(items[index].Tag, selected))
                    {
                        _objectList.SelectedItem = items[index];
                        HighlightRow(items[index]);
                        _objectList.ScrollIntoView(index);
                        break;
                    }
                }
            }
        }
        finally
        {
            _refreshingList = false;
        }

        _listCountText.Text = visible.Count.ToString(CultureInfo.InvariantCulture);
        _countText.Text = $"当前 {visible.Count} / {_allObjects.Count} 个物体";
    }

    /// <summary>列表排序优先级：Spine → 序列帧 → 有路径 → 静态。</summary>
    private int AnimationRank(PlacedObject item)
    {
        if (ResolveSpine(item) is not null)
        {
            return 0;
        }

        if (ResolveSprite(item) is not null)
        {
            return 1;
        }

        return PathMath.HasPath(item.PathPoints) ? 2 : 3;
    }

    /// <summary>列表行：类型色点 + 名称 + 类型 / 图层 / ID + 动画 / 路径标签。</summary>
    private Control BuildListRow(PlacedObject item)
    {
        var title = new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.PreviewText,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var dot = UiFactory.MarkerDot(UiTheme.ObjectTag.Of(item.Type));
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { dot, title },
        };
        Grid.SetColumn(title, 1);

        var meta = new TextBlock
        {
            Text = $"{ObjectTypeNames.Of(item.Type)} · {LayerName(item.LayerType)} · {item.Id}",
            FontSize = 10,
            Foreground = UiTheme.PreviewHint,
            Margin = new Thickness(12, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var row = new StackPanel { Spacing = 0, Children = { head, meta } };
        var tags = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(12, 3, 0, 0),
        };
        if (ResolveSpine(item) is not null)
        {
            tags.Children.Add(MakeTag("Spine", UiTheme.Accent));
        }
        else if (ResolveSprite(item) is not null)
        {
            tags.Children.Add(MakeTag("序列帧", UiTheme.AnimationTag.Sequence));
        }

        if (PathMath.HasPath(item.PathPoints))
        {
            tags.Children.Add(MakeTag($"{item.PathPoints.Count}P", UiTheme.AnimationTag.PathPoint));
        }

        if (tags.Children.Count > 0)
        {
            row.Children.Add(tags);
        }

        return row;
    }

    /// <summary>小标签。</summary>
    private static Control MakeTag(string text, IBrush brush)
    {
        return new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 9,
                Foreground = UiTheme.White,
            },
        };
    }

    /// <summary>给当前选中行加底色和左侧强调线，避免主题选中态过淡。</summary>
    private void HighlightRow(ListBoxItem? row)
    {
        if (_selectedRow is not null && !ReferenceEquals(_selectedRow, row))
        {
            _selectedRow.Background = UiTheme.Transparent;
            _selectedRow.BorderThickness = new Thickness(0);
        }

        _selectedRow = row;
        if (row is null)
        {
            return;
        }

        row.Background = UiTheme.PreviewAccentSoft;
        row.BorderBrush = UiTheme.Accent;
        row.BorderThickness = new Thickness(3, 0, 0, 0);
    }

    /// <summary>选中初始目标；没有显式目标时优先选第一个动画对象。</summary>
    /// <param name="initialTarget">调用方指定的对象。</param>
    private void SelectInitial(PlacedObject? initialTarget)
    {
        var target = initialTarget;
        if (target is null)
        {
            target = _allObjects.FirstOrDefault(item => ResolveSpine(item) is not null)
                ?? _allObjects.FirstOrDefault(item => ResolveSprite(item) is not null)
                ?? _allObjects.FirstOrDefault();
        }

        if (target is null)
        {
            return;
        }

        for (var index = 0; index < _visibleRows.Count; index++)
        {
            if (ReferenceEquals(_visibleRows[index].Tag, target))
            {
                _objectList.SelectedItem = _visibleRows[index];
                HighlightRow(_visibleRows[index]);
                _objectList.ScrollIntoView(index);
                var targetIndex = index;
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_canvas.Target, target) && targetIndex < _visibleRows.Count)
                    {
                        _objectList.ScrollIntoView(targetIndex);
                    }
                }, DispatcherPriority.Loaded);
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_canvas.Target, target) && targetIndex < _visibleRows.Count)
                    {
                        _objectList.ScrollIntoView(targetIndex);
                    }
                }, DispatcherPriority.Background);
                break;
            }
        }

        _canvas.Target = target;
        _canvas.RestartPlayback();
    }

    /// <summary>刷新右侧、画布标题与底栏信息。</summary>
    private void UpdateInfo()
    {
        var target = _canvas.Target;
        if (target is null)
        {
            SetInfoEmpty();
            _canvasTitle.Text = "未选择物体";
            _canvasSubtitle.Text = "从左侧列表选择一个物体";
            return;
        }

        var spine = ResolveSpine(target);
        var sprite = ResolveSprite(target);
        var pathLength = PathMath.Length(target.PathPoints);
        var pathDuration = _canvas.PathDuration(target);
        UpdateSpeedAvailability(spine is not null || sprite is not null || PathMath.HasPath(target.PathPoints));

        _nameValue.Text = target.Name;
        _idValue.Text = target.Id;
        _typeValue.Text = $"{ObjectTypeNames.Of(target.Type)} · {LayerName(target.LayerType)}";
        _templateValue.Text = string.IsNullOrWhiteSpace(target.TemplateId) ? EmptyFieldText : target.TemplateId;

        if (spine is not null)
        {
            _sourceValue.Text = "Spine";
            _assetValue.Text = spine.Name;
            _clipValue.Text = _canvas.CurrentSpineAnimation;
            _durationValue.Text = $"{_canvas.CurrentSpineDuration:0.00}s · 共 {spine.Animations.Count} 个动画";
        }
        else if (sprite is not null)
        {
            var frame = _canvas.CurrentFrameIndex(target);
            _sourceValue.Text = "序列帧";
            _assetValue.Text = sprite.Name;
            _clipValue.Text = $"{sprite.Frames.Count} 帧 · {sprite.Fps:0.##} FPS";
            _durationValue.Text = $"当前第 {frame + 1} 帧";
        }
        else
        {
            _sourceValue.Text = "静态贴图";
            _assetValue.Text = EmptyFieldText;
            _clipValue.Text = target.AnimEnabled
                ? $"{AnimationKinds.ToDisplay(target.AnimationKind)} · {target.AnimName}（未找到资源）"
                : $"{AnimationKinds.ToDisplay(target.AnimationKind)} · 未启用";
            _durationValue.Text = EmptyFieldText;
        }

        _stateValue.Text = $"循环 {(target.Loop ? "开" : "关")} · 启用 {(target.AnimEnabled ? "是" : "否")}";

        if (PathMath.HasPath(target.PathPoints))
        {
            _pathValue.Text = $"{target.PathPoints.Count} 个点 · 循环移动";
            SetPathMetrics(
                $"{pathLength:0} px",
                $"{pathDuration:0.0} s",
                $"{_canvas.MovementPixelsPerSecond * AnimationSpeeds.Clamp(target.Speed):0} px/s");
        }
        else if (target.PathPoints.Count == 1)
        {
            _pathValue.Text = "1 个点 · 原地播放";
            SetPathMetrics(EmptyFieldText, EmptyFieldText, EmptyFieldText);
        }
        else
        {
            _pathValue.Text = "未配置 · 原地播放";
            SetPathMetrics(EmptyFieldText, EmptyFieldText, EmptyFieldText);
        }

        _boundsValue.Text = $"{target.Bounds.Width:0} × {target.Bounds.Height:0}";
        _rotationValue.Text = $"{target.Rotation:0.##}° · {(target.FlipX ? "水平翻转" : "未翻转")}";
        _stretchValue.Text = target.Stretch ? "拉伸铺满" : "保持比例";

        _canvasTitle.Text = target.Name;
        _canvasSubtitle.Text = spine is not null
            ? $"Spine · {spine.Name} / {_canvas.CurrentSpineAnimation}"
            : sprite is not null
                ? $"序列帧 · {sprite.Name}"
                : "静态贴图预览";
    }

    /// <summary>填右下角路径三项指标；无有效路径时三项统一显示占位。</summary>
    /// <param name="lengthText">路径长度文本。</param>
    /// <param name="durationText">路径耗时文本。</param>
    /// <param name="speedText">移动速度文本。</param>
    private void SetPathMetrics(string lengthText, string durationText, string speedText)
    {
        _pathLengthValue.Text = lengthText;
        _pathDurationValue.Text = durationText;
        _moveSpeedValue.Text = speedText;
    }

    /// <summary>未选择物体时清空右侧字段。</summary>
    private void SetInfoEmpty()
    {
        foreach (var value in new[]
        {
            _nameValue, _idValue, _typeValue, _templateValue,
            _sourceValue, _assetValue, _clipValue, _durationValue, _stateValue,
            _pathValue, _pathLengthValue, _pathDurationValue, _moveSpeedValue,
            _boundsValue, _rotationValue, _stretchValue,
        })
        {
            value.Text = EmptyFieldText;
        }

        UpdateSpeedAvailability(false);
    }

    /// <summary>
    /// 只有存在动画或至少两个路径点时，速度才会产生可见效果；
    /// 静态物体上禁用滑条并明确显示「无动画」，避免用户误以为拖动失灵。
    /// </summary>
    private void UpdateSpeedAvailability(bool canAnimate)
    {
        _speedSlider.IsEnabled = canAnimate;
        _speedSlider.Opacity = canAnimate ? 1.0 : 0.45;
        _speedText.FontWeight = canAnimate ? FontWeight.SemiBold : FontWeight.Normal;
        _speedText.Foreground = canAnimate ? UiTheme.Accent : UiTheme.PreviewMuted;
        _speedText.Text = canAnimate
            ? FormatSpeed(_speedSlider.Value)
            : "无动画";
        ToolTip.SetTip(_speedSlider, canAnimate
            ? "拖动改变预览播放速度"
            : "当前物体没有动画或路径，速度不会产生可见变化");

        _playButton.IsEnabled = canAnimate;
        ToolTip.SetTip(_playButton, canAnimate
            ? "播放或暂停预览"
            : "当前物体没有动画或路径");
        if (canAnimate)
        {
            _playButton.IsEnabled = true;
            if (_timeText.Text == EmptyFieldText)
            {
                _timeText.Text = $"{_canvas.TimeSeconds:0.00}s";
            }

            return;
        }

        // 静态物体没有可推进的内容，停掉时钟并清空时间，避免时间在走但画面不变。
        _playing = false;
        _playButton.Content = "播放";
        _clock.Stop();
        _timeText.Text = EmptyFieldText;
    }

    #endregion
}

