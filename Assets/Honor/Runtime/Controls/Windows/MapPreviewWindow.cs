/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPreviewWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   整张地图的只读预览窗：播放全图动画、角色沿路径移动
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

#region 地图预览窗

/// <summary>
/// 地图预览窗。复用场景画布渲染整张地图，但切到只读预览模式：
/// 隐藏网格、选中框、占格、交通辅助线等编辑器图层，只保留底图、物件、
/// 序列帧 / Spine 动画以及角色沿 <see cref="PlacedObject.PathPoints"/> 的移动演示。
/// 预览只读取地图数据，不会修改坐标、动画名或路径点。
/// </summary>
public sealed class MapPreviewWindow : Window
{
    #region 常量与画刷


    #endregion

    #region 字段

    private readonly MapConfigData _config;
    private readonly AssetLibrary _assets;
    private readonly SpriteAnimationCatalog _spriteCatalog;
    private readonly SpineAnimationCatalog _spineCatalog;
    private readonly NineSliceCatalog? _nineSlices;

    private readonly MapCanvas _canvas = new();
    private readonly Button _playButton = new();
    private readonly TextBlock _timeText = new();
    private readonly TextBlock _zoomText = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _countText = new();
    private readonly Slider _speedSlider = new();
    private readonly TextBlock _speedText = new();
    private readonly CheckBox _showAnimationsBox = new();
    private readonly CheckBox _showPathsBox = new();
    private readonly DispatcherTimer _statusTimer;

    #endregion

    #region 构造

    /// <summary>构造地图预览窗。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="assets">贴图资源库。</param>
    /// <param name="spriteCatalog">序列帧动画目录。</param>
    /// <param name="spineCatalog">Spine 动画目录。</param>
    /// <param name="nineSlices">九宫格配置；为空时按普通拉伸绘制。</param>
    public MapPreviewWindow(
        MapConfigData config,
        AssetLibrary assets,
        SpriteAnimationCatalog spriteCatalog,
        SpineAnimationCatalog spineCatalog,
        NineSliceCatalog? nineSlices = null)
    {
        _config = config;
        _assets = assets;
        _spriteCatalog = spriteCatalog;
        _spineCatalog = spineCatalog;
        _nineSlices = nineSlices;

        Title = $"地图预览 - {config.SceneName}";
        Width = 1280;
        Height = 860;
        MinWidth = 1000;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PreviewPanel;

        Content = BuildLayout();
        ConfigureCanvas();

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();

        Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(_canvas.FitToView, DispatcherPriority.Loaded);
            RefreshStatus();
        };
        Closed += (_, _) =>
        {
            _statusTimer.Stop();
            _canvas.PreviewMode = false;
        };

        WindowChromeTheme.Attach(this);
    }

    #endregion

    #region 布局

    /// <summary>搭建顶栏 / 整图预览画布 / 底部状态栏。</summary>
    private Control BuildLayout() =>
        PreviewWindowChrome.BuildShell(BuildHeader(), BuildBody(), BuildFooter());

    /// <summary>顶栏：标题、播放控制、显示开关、速度与适应窗口。</summary>
    private Control BuildHeader()
    {
        var titlePanel = PreviewWindowChrome.BuildTitlePanel(
            "地图预览",
            "只读运行态 · 全图动画 + 物件路径移动");

        UiFactory.ApplyPreviewToolbarButton(_playButton, "暂停", 76, primary: true);
        _playButton.Click += (_, _) =>
        {
            _canvas.PreviewPaused = !_canvas.PreviewPaused;
            RefreshPlayButton();
            RefreshStatus();
        };

        var restart = UiFactory.PreviewToolbarButton("重新开始", 88);
        restart.Click += (_, _) =>
        {
            _canvas.PreviewPaused = false;
            _canvas.RestartPreview();
            RefreshPlayButton();
            RefreshStatus();
        };

        var fit = UiFactory.PreviewToolbarButton("适应窗口", 82);
        fit.Click += (_, _) => _canvas.FitToView();

        _showAnimationsBox.Content = "显示动画";
        _showAnimationsBox.FontSize = 12;
        _showAnimationsBox.IsChecked = true;
        _showAnimationsBox.VerticalAlignment = VerticalAlignment.Center;
        _showAnimationsBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.PreviewShowAnimations = _showAnimationsBox.IsChecked == true;
            _canvas.RefreshAnimations();
            RefreshStatus();
        };

        _showPathsBox.Content = "显示路径";
        _showPathsBox.FontSize = 12;
        _showPathsBox.IsChecked = true;
        _showPathsBox.VerticalAlignment = VerticalAlignment.Center;
        _showPathsBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.PreviewShowPaths = _showPathsBox.IsChecked == true;
            _canvas.RefreshAnimations();
            RefreshStatus();
        };

        _speedSlider.Minimum = 0.25;
        _speedSlider.Maximum = 4.0;
        _speedSlider.Value = 1.0;
        _speedSlider.Width = 138;
        _speedSlider.TickFrequency = 0.25;
        _speedSlider.IsSnapToTickEnabled = true;
        _speedSlider.VerticalAlignment = VerticalAlignment.Center;
        _speedSlider.ValueChanged += (_, _) =>
        {
            _canvas.PreviewPlaybackSpeed = _speedSlider.Value;
            _speedText.Text = $"{_speedSlider.Value:0.00}×";
        };

        _speedText.Text = "1.00×";
        _speedText.FontSize = 11;
        _speedText.Foreground = UiTheme.PreviewHint;
        _speedText.Width = 44;
        _speedText.VerticalAlignment = VerticalAlignment.Center;

        _timeText.Text = "00:00";
        _timeText.FontSize = 12;
        _timeText.Foreground = UiTheme.PreviewText;
        _timeText.FontFamily = new FontFamily("Consolas");
        _timeText.VerticalAlignment = VerticalAlignment.Center;

        _zoomText.Text = "100%";
        _zoomText.FontSize = 11;
        _zoomText.Foreground = UiTheme.PreviewHint;
        _zoomText.VerticalAlignment = VerticalAlignment.Center;

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _timeText,
                _playButton,
                restart,
                fit,
                _showAnimationsBox,
                _showPathsBox,
                new TextBlock { Text = "速度", FontSize = 11, Foreground = UiTheme.PreviewHint, VerticalAlignment = VerticalAlignment.Center },
                _speedSlider,
                _speedText,
                _zoomText,
            },
        };

        return PreviewWindowChrome.BuildHeader(titlePanel, null, right);
    }

    /// <summary>中心画布：包一层浅色描边卡片，和外框留 10px 间距。</summary>
    private Control BuildBody()
    {
        _canvas.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvas.VerticalAlignment = VerticalAlignment.Stretch;

        var surface = new Border
        {
            Background = UiTheme.PreviewView.Stage,
            BorderBrush = UiTheme.PreviewView.FrameBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            Margin = new Thickness(10, 8, 10, 8),
            Child = _canvas,
        };

        return new Grid { Children = { surface } };
    }

    /// <summary>底栏：当前状态与动画 / 路径计数。</summary>
    private Control BuildFooter() =>
        PreviewWindowChrome.BuildFooter(
            _statusText,
            _countText,
            "预览只读：滚轮缩放，右键拖动平移；不会修改地图数据。");

    #endregion

    #region 画布配置

    /// <summary>把场景画布切到只读预览模式，并绑定当前地图的资源与动画目录。</summary>
    private void ConfigureCanvas()
    {
        _canvas.PreviewMode = true;
        _canvas.ShowGrid = false;
        _canvas.ShowPathGrid = false;
        _canvas.ShowBoundingBox = false;
        _canvas.ShowImageBorder = false;
        _canvas.ShowMapLayer = true;
        _canvas.ShowClipOutsideMap = true;
        _canvas.PlacementEnabled = false;
        _canvas.PathDrawing = false;
        _canvas.FogDrawing = false;
        _canvas.PreviewShowAnimations = true;
        _canvas.PreviewShowPaths = true;

        _canvas.Assets = _assets;
        _canvas.Animations = _spriteCatalog;
        _canvas.SpineAnimations = _spineCatalog;
        _canvas.NineSlices = _nineSlices;
        _canvas.Config = _config;

        _canvas.ViewChanged += (_, _) =>
        {
            _zoomText.Text = $"{_canvas.ZoomPercent}%";
        };

        // 窗口刚显示时 Bounds 还是 0，适应窗口已通过 Opened + Dispatcher.Post 做一次；
        // 之后用户拉窗口，画布只重绘，不强行改变用户当前缩放。
    }

    #endregion

    #region 状态

    /// <summary>按预览时钟刷新时间、播放按钮、缩放与对象计数。</summary>
    private void RefreshStatus()
    {
        var seconds = _canvas.PreviewElapsedSeconds;
        _timeText.Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        _zoomText.Text = $"{_canvas.ZoomPercent}%";

        var animated = CountAnimatedObjects();
        var paths = _canvas.PreviewPathObjectCount;
        _countText.Text = $"物件 {_config.Objects.Count} · 动画 {animated} · 路径 {paths}";

        if (paths == 0)
        {
            _statusText.Text = animated > 0
                ? "当前地图没有带路径的物件；动画物件按各自配置循环播放。"
                : "当前地图没有可预览的动画帧或带路径物件；可直接查看地图静态画面。";
        }
        else
        {
            _statusText.Text = $"正在预览 {paths} 个路径物件：虚线为路径，黄点为当前脚底位置；滚轮缩放，右键拖动平移。";
        }
    }

    /// <summary>统计当前地图真正能播放的动画实例：命中序列帧或 Spine 资产才计入。</summary>
    /// <returns>动画实例数量。</returns>
    private int CountAnimatedObjects()
    {
        var count = 0;
        foreach (var item in _config.Objects)
        {
            if (!item.AnimEnabled)
            {
                continue;
            }

            if (string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase))
            {
                if (_spineCatalog.Find(item.SpineAsset) is not null)
                {
                    count++;
                }
            }
            else if (_spriteCatalog.Find(item.AnimName) is not null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>播放 / 暂停按钮按预览状态回显。</summary>
    private void RefreshPlayButton()
    {
        _playButton.Content = _canvas.PreviewPaused ? "播放" : "暂停";
        _playButton.Background = UiTheme.Accent;
    }

    #endregion
}

#endregion
