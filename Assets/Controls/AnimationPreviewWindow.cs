/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景动画预览弹窗
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

#region 预览窗

/// <summary>
/// 场景动画预览窗。左侧列出当前地图可预览物体，中间播放序列帧 / Spine 动画
/// 并沿角色路径做移动演示，右侧显示动画与路径的实时信息。
/// 只读地图数据；导入 Spine 或绑定动画时才会回写实例字段。
/// </summary>
public sealed class AnimationPreviewWindow : Window
{
    #region 常量与画刷

    private static readonly IBrush BuildingBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x8B, 0x2B));
    private static readonly IBrush CharacterBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x6F, 0xD1));
    private static readonly IBrush PropBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x8E, 0xD4));

    #endregion

    #region 字段

    private readonly MapConfigData _config;
    private readonly AssetLibrary _assets;
    private readonly SpriteAnimationCatalog _spriteCatalog;
    private readonly SpineAnimationCatalog _spineCatalog;
    private readonly SpineAnimationStore _spineStore;
    private readonly Action? _onSceneChanged;

    private readonly AnimationPreviewCanvas _canvas = new();
    private readonly ListBox _objectList = new();
    private readonly ScrollRail _listScrollBar = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _countText = new();
    private readonly TextBlock _listCountText = new();
    private readonly TextBlock _canvasTitle = new();
    private readonly TextBlock _canvasSubtitle = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _filterBox = new();
    private readonly Button _playButton = new();
    private readonly TextBlock _timeText = new();
    private readonly TextBlock _speedText = new();
    private readonly PlaybackSpeedSlider _speedSlider = new();
    private readonly TextBlock _nameValue = new();
    private readonly TextBlock _idValue = new();
    private readonly TextBlock _typeValue = new();
    private readonly TextBlock _templateValue = new();
    private readonly TextBlock _sourceValue = new();
    private readonly TextBlock _assetValue = new();
    private readonly TextBlock _clipValue = new();
    private readonly TextBlock _durationValue = new();
    private readonly TextBlock _stateValue = new();
    private readonly TextBlock _pathValue = new();
    private readonly TextBlock _pathLengthValue = new();
    private readonly TextBlock _pathDurationValue = new();
    private readonly TextBlock _moveSpeedValue = new();
    private readonly TextBlock _boundsValue = new();
    private readonly TextBlock _rotationValue = new();
    private readonly TextBlock _stretchValue = new();
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer;
    private readonly List<PlacedObject> _allObjects = new();
    private readonly List<ListBoxItem> _visibleRows = new();
    private double _lastTick;
    private double _playbackSpeed = 1.0;
    private bool _playing = true;
    private bool _refreshingList;
    private ListBoxItem? _selectedRow;

    #endregion

    #region 构造

    /// <summary>构造预览窗。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="assets">贴图资源库。</param>
    /// <param name="spriteCatalog">序列帧目录。</param>
    /// <param name="spineCatalog">Spine 目录。</param>
    /// <param name="spineStore">Spine 导入持久化。</param>
    /// <param name="initialTarget">初始预览对象。</param>
    /// <param name="onSceneChanged">导入 / 绑定动画后的主窗口刷新回调。</param>
    public AnimationPreviewWindow(
        MapConfigData config,
        AssetLibrary assets,
        SpriteAnimationCatalog spriteCatalog,
        SpineAnimationCatalog spineCatalog,
        SpineAnimationStore spineStore,
        PlacedObject? initialTarget = null,
        Action? onSceneChanged = null)
    {
        _config = config;
        _assets = assets;
        _spriteCatalog = spriteCatalog;
        _spineCatalog = spineCatalog;
        _spineStore = spineStore;
        _onSceneChanged = onSceneChanged;

        Title = "场景动画预览";
        Width = 1280;
        Height = 840;
        MinWidth = 1000;
        MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PreviewPanel;

        _allObjects.AddRange(config.Objects);
        Content = BuildLayout();

        _canvas.SetScene(config, assets, spriteCatalog);
        _canvas.SetSpineCatalog(spineCatalog);

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += (_, _) => Tick();

        BuildObjectList();
        RebuildVisibleList();
        SelectInitial(initialTarget);
        _clock.Start();
        _lastTick = _clock.Elapsed.TotalSeconds;
        _timer.Start();
        UpdateInfo();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _clock.Stop();
        };
    }

    #endregion

    #region 布局

    /// <summary>搭建顶栏 / 左侧列表 / 中心画布 / 右侧信息四块。</summary>
    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("58,*,36"),
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

        _filterBox.ItemsSource = new[] { "全部", "仅动画", "仅 Spine", "有路径" };
        _filterBox.SelectedIndex = 0;
        _filterBox.Width = 112;
        _filterBox.Height = 30;
        _filterBox.FontSize = 12;
        _filterBox.VerticalAlignment = VerticalAlignment.Center;
        _filterBox.SelectionChanged += (_, _) => RebuildVisibleList();

        _searchBox.Width = 226;
        _searchBox.Height = 30;
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
            ColumnDefinitions = new ColumnDefinitions("300,10,*,10,320"),
            Margin = new Thickness(14, 10, 14, 10),
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
    private Control BuildObjectPane()
    {
        _objectList.Background = Brushes.Transparent;
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
        return new Border
        {
            Background = UiTheme.Card,
            CornerRadius = new CornerRadius(8),
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = pane,
        };
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

        _speedText.Text = "速度 1×";
        _speedText.FontSize = 11;
        _speedText.Foreground = UiTheme.PreviewHint;
        _speedText.VerticalAlignment = VerticalAlignment.Center;

        _speedSlider.Minimum = 0.25;
        _speedSlider.Maximum = 3;
        _speedSlider.Value = 1;
        _speedSlider.Width = 108;
        _speedSlider.Height = 24;
        _speedSlider.VerticalAlignment = VerticalAlignment.Center;
        _speedSlider.ValueChanged += value =>
        {
            _playbackSpeed = Math.Max(0.25, value);
            _speedText.Text = "速度 " + value.ToString("0.##", CultureInfo.InvariantCulture) + "×";
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

        return new Border
        {
            Background = UiTheme.Card,
            CornerRadius = new CornerRadius(8),
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = pane,
        };
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
        return new Border
        {
            Background = UiTheme.Card,
            CornerRadius = new CornerRadius(8),
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            Child = pane,
        };
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

    #region 列表与信息

    /// <summary>构建全部列表项；筛选时只重建可见项。</summary>
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
                1 => item.AnimEnabled && (ResolveSpine(item) is not null || ResolveSprite(item) is not null),
                2 => ResolveSpine(item) is not null,
                3 => item.PathPoints.Count >= 2,
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

        return item.PathPoints.Count >= 2 ? 2 : 3;
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
        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = TypeBrush(item.Type),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { dot, title },
        };
        Grid.SetColumn(title, 1);

        var meta = new TextBlock
        {
            Text = $"{TypeName(item.Type)} · {LayerName(item.LayerType)} · {item.Id}",
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
            tags.Children.Add(MakeTag("序列帧", new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x6E))));
        }

        if (item.PathPoints.Count >= 2)
        {
            tags.Children.Add(MakeTag($"{item.PathPoints.Count}P", new SolidColorBrush(Color.FromRgb(0x9A, 0x6B, 0xD6))));
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
                Foreground = Brushes.White,
            },
        };
    }

    /// <summary>给当前选中行加底色和左侧强调线，避免主题选中态过淡。</summary>
    private void HighlightRow(ListBoxItem? row)
    {
        if (_selectedRow is not null && !ReferenceEquals(_selectedRow, row))
        {
            _selectedRow.Background = Brushes.Transparent;
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
        UpdateSpeedAvailability(spine is not null || sprite is not null || target.PathPoints.Count >= 2);

        _nameValue.Text = target.Name;
        _idValue.Text = target.Id;
        _typeValue.Text = $"{TypeName(target.Type)} · {LayerName(target.LayerType)}";
        _templateValue.Text = string.IsNullOrWhiteSpace(target.TemplateId) ? "—" : target.TemplateId;

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
            _assetValue.Text = "—";
            _clipValue.Text = target.AnimEnabled
                ? $"{AnimationKinds.ToDisplay(target.AnimationKind)} · {target.AnimName}（未找到资源）"
                : $"{AnimationKinds.ToDisplay(target.AnimationKind)} · 未启用";
            _durationValue.Text = "—";
        }

        _stateValue.Text = $"循环 {(target.Loop ? "开" : "关")} · 启用 {(target.AnimEnabled ? "是" : "否")}";

        if (target.PathPoints.Count >= 2)
        {
            _pathValue.Text = $"{target.PathPoints.Count} 个点 · 循环移动";
            _pathLengthValue.Text = $"{pathLength:0} px";
            _pathDurationValue.Text = $"{pathDuration:0.0} s";
            _moveSpeedValue.Text = $"{_canvas.MovementPixelsPerSecond * Math.Max(0.01, target.Speed):0} px/s";
        }
        else if (target.PathPoints.Count == 1)
        {
            _pathValue.Text = "1 个点 · 原地播放";
            _pathLengthValue.Text = "—";
            _pathDurationValue.Text = "—";
            _moveSpeedValue.Text = "—";
        }
        else
        {
            _pathValue.Text = "未配置 · 原地播放";
            _pathLengthValue.Text = "—";
            _pathDurationValue.Text = "—";
            _moveSpeedValue.Text = "—";
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
            value.Text = "—";
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
            ? "速度 " + _speedSlider.Value.ToString("0.##", CultureInfo.InvariantCulture) + "×"
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
            if (_timeText.Text == "—")
            {
                _timeText.Text = $"{_canvas.TimeSeconds:0.00}s";
            }

            return;
        }

        // 静态物体没有可推进的内容，停掉时钟并清空时间，避免时间在走但画面不变。
        _playing = false;
        _playButton.Content = "播放";
        _clock.Stop();
        _timeText.Text = "—";
    }

    #endregion

    #region 播放与导入

    /// <summary>播放 / 暂停。</summary>
    private void TogglePlayback()
    {
        _playing = !_playing;
        _playButton.Content = _playing ? "暂停" : "播放";
        if (_playing)
        {
            _clock.Restart();
            _lastTick = 0;
        }
        else
        {
            _clock.Stop();
        }
    }

    /// <summary>定时器：推进动画、路径和右侧信息。</summary>
    private void Tick()
    {
        if (!_playing)
        {
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var delta = Math.Min(0.1, Math.Max(0, now - _lastTick));
        _lastTick = now;
        if (delta <= 0)
        {
            return;
        }

        _canvas.Advance(delta * _playbackSpeed);
        _timeText.Text = $"{_canvas.TimeSeconds:0.00}s";
        if (Math.Floor(_canvas.TimeSeconds * 10) % 2 == 0)
        {
            UpdateInfo();
        }
    }

    /// <summary>从预览窗导入 Spine 并绑定到当前物体。</summary>
    private async Task ImportSpineAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Spine 骨架（.json / .skel）",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Spine 骨架")
                {
                    Patterns = new[] { "*.json", "*.skel" },
                },
            },
        });
        if (files.Count == 0)
        {
            return;
        }

        var local = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(local))
        {
            return;
        }

        var atlas = Path.ChangeExtension(local, ".atlas");
        if (!File.Exists(atlas))
        {
            atlas = string.Empty;
        }

        var asset = _spineStore.Import(
            local,
            atlas,
            Path.GetFileNameWithoutExtension(local),
            string.Empty,
            out var error);
        if (asset is null)
        {
            _statusText.Text = $"Spine 导入失败：{error}";
            return;
        }

        _spineCatalog.Register(asset);
        try
        {
            _spineStore.Save(_spineCatalog);
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Spine 已载入，但写盘失败：{exception.Message}";
        }

        var target = _canvas.Target;
        if (target is not null)
        {
            target.AnimationKind = AnimationKinds.Spine;
            target.SpineAsset = asset.Name;
            target.SpineAnimation = asset.DefaultAnimation;
            target.AnimEnabled = true;
            _canvas.SetSpineCatalog(_spineCatalog);
            _canvas.Target = target;
            _canvas.RestartPlayback();
            _onSceneChanged?.Invoke();
        }

        RebuildVisibleList();
        UpdateInfo();
        _statusText.Text = $"已导入 Spine「{asset.Name}」，{asset.Animations.Count} 个动画。";
    }

    #endregion

    #region 辅助

    /// <summary>解析序列帧片段。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>片段或 null。</returns>
    private SpriteAnimationClip? ResolveSprite(PlacedObject item)
    {
        return item is { AnimEnabled: true }
            ? _spriteCatalog.Find(item.AnimName)
            : null;
    }

    /// <summary>解析 Spine 资产。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>资产或 null。</returns>
    private SpineAnimationAsset? ResolveSpine(PlacedObject item)
    {
        return item is { AnimEnabled: true }
            && string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            ? _spineCatalog.Find(item.SpineAsset)
            : null;
    }

    private static string TypeName(MapObjectType type) => type switch
    {
        MapObjectType.Building => "建筑",
        MapObjectType.Character => "人物",
        MapObjectType.Prop => "物件",
        _ => "物体",
    };

    private static IBrush TypeBrush(MapObjectType type) => type switch
    {
        MapObjectType.Building => BuildingBrush,
        MapObjectType.Character => CharacterBrush,
        _ => PropBrush,
    };

    private static string LayerName(string layer) => MapLayerKeys.DisplayName(layer);

    #endregion
}

#endregion
