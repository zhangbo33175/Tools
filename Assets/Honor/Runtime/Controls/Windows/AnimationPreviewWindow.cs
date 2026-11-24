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
/// 布局骨架位于 Layout partial，列表 / 信息面板位于 Panes partial，
/// 播放 / 导入 / 解析位于 Playback partial。
/// </summary>
public sealed partial class AnimationPreviewWindow : Window
{
    #region 常量与画刷

    /// <summary>窗口初始宽度。</summary>
    private const int PreviewWidth = 1280;

    /// <summary>窗口初始高度。</summary>
    private const int PreviewHeight = 840;

    /// <summary>窗口最小宽度，保证三栏信息不全挤在一起。</summary>
    private const int PreviewMinWidth = 1000;

    /// <summary>窗口最小高度，保证顶栏、画布与状态栏同时可见。</summary>
    private const int PreviewMinHeight = 660;

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

    /// <summary>筛选下拉框宽度。</summary>
    private const int FilterWidth = 112;

    /// <summary>搜索框宽度。</summary>
    private const int SearchWidth = 226;

    /// <summary>筛选「全部」索引。</summary>
    private const int FilterAll = 0;

    /// <summary>筛选「仅动画」索引：序列帧或 Spine 任一可用即命中。</summary>
    private const int FilterAnimated = 1;

    /// <summary>筛选「仅 Spine」索引。</summary>
    private const int FilterSpineOnly = 2;

    /// <summary>筛选「有路径」索引：至少两个路径点。</summary>
    private const int FilterWithPath = 3;

    /// <summary>右侧信息面板无值时的统一占位，空选中与无路径字段共用。</summary>
    private const string EmptyFieldText = "—";

    /// <summary>预览播放速度下限。</summary>
    private const double MinPlaybackSpeed = 0.25;

    /// <summary>预览播放速度上限。</summary>
    private const double MaxPlaybackSpeed = 3.0;

    /// <summary>速度文案格式：整数不带小数，非整数最多两位小数。</summary>
    private const string SpeedLabelFormat = "速度 {0:0.##}×";

    /// <summary>
    /// 筛选标签。数组顺序必须与 <see cref="FilterAll"/>…<see cref="FilterWithPath"/>
    /// 及列表筛选分支保持一致，避免新增筛选时索引错位。
    /// </summary>
    private static readonly string[] FilterLabels = { "全部", "仅动画", "仅 Spine", "有路径" };

    /// <summary>把倍速值格式化成状态栏统一文案。</summary>
    /// <param name="value">倍速。</param>
    /// <returns>如「速度 1×」「速度 1.5×」。</returns>
    private static string FormatSpeed(double value) =>
        string.Format(CultureInfo.InvariantCulture, SpeedLabelFormat, value);


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

}
