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


/// <summary>
/// 场景动画预览窗。左侧列出当前地图可预览物体，中间播放序列帧 / Spine 动画
/// 并沿角色路径做移动演示，右侧显示动画与路径的实时信息。
/// 只读地图数据；导入 Spine 或绑定动画时才会回写实例字段。
/// </summary>
public sealed partial class AnimationPreviewWindow : Window
{
    #region 常量与画刷

    private const int PreviewWidth = 1280;
    private const int PreviewHeight = 840;
    private const int PreviewMinWidth = 1000;
    private const int PreviewMinHeight = 660;

    /// <summary>顶栏高度。</summary>
    private const int HeaderHeight = 58;

    /// <summary>底部状态栏高度。</summary>
    private const int FooterHeight = 36;

    /// <summary>主体左右外边距。</summary>
    private const int BodyHorizontalMargin = 14;

    /// <summary>主体上下外边距。</summary>
    private const int BodyVerticalMargin = 10;

    /// <summary>左侧对象列表宽度。</summary>
    private const int ObjectPaneWidth = 300;

    /// <summary>右侧信息面板宽度。</summary>
    private const int InfoPaneWidth = 320;

    /// <summary>主体三列之间的间隔。</summary>
    private const int PaneGap = 10;

    /// <summary>顶栏输入控件统一高度。</summary>
    private const int ToolbarControlHeight = 30;

    private const int FilterWidth = 112;
    private const int SearchWidth = 226;

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
        Width = PreviewWidth;
        Height = PreviewHeight;
        MinWidth = PreviewMinWidth;
        MinHeight = PreviewMinHeight;
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

        _filterBox.ItemsSource = new[] { "全部", "仅动画", "仅 Spine", "有路径" };
        _filterBox.SelectedIndex = 0;
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

}

