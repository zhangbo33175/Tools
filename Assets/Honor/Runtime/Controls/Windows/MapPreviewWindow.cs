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
using Avalonia.Controls.Shapes;
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
    private readonly CheckBox _trafficBox = new();
    private readonly CheckBox _trafficPlanBox = new();
    private readonly TextBlock _trafficText = new();
    private readonly StackPanel _trafficChips = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly List<TextBlock> _trafficChipTexts = new();
    private readonly List<Ellipse> _trafficChipDots = new();
    private int _trafficChipLightCount = -1;
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

        _statusTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            // 100ms：底栏倒计时和画布灯头最多差 0.1s，逐帧比对视频时不会出现两处秒数对不上
            Interval = TimeSpan.FromMilliseconds(100),
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

    /// <summary>搭建顶栏 / 整图预览画布 / 相位状态行 / 底部状态栏。</summary>
    private Control BuildLayout() =>
        PreviewWindowChrome.BuildShell(BuildHeader(), BuildBody(), BuildFooterBlock());

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
        _showAnimationsBox.FontSize = UiMetrics.FontTitle;
        _showAnimationsBox.IsChecked = true;
        _showAnimationsBox.VerticalAlignment = VerticalAlignment.Center;
        _showAnimationsBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.PreviewShowAnimations = _showAnimationsBox.IsChecked == true;
            _canvas.RefreshAnimations();
            RefreshStatus();
        };

        _showPathsBox.Content = "显示路径";
        _showPathsBox.FontSize = UiMetrics.FontTitle;
        _showPathsBox.IsChecked = true;
        _showPathsBox.VerticalAlignment = VerticalAlignment.Center;
        _showPathsBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.PreviewShowPaths = _showPathsBox.IsChecked == true;
            _canvas.RefreshAnimations();
            RefreshStatus();
        };

        _trafficBox.Content = "交通仿真";
        _trafficBox.FontSize = UiMetrics.FontTitle;
        _trafficBox.IsChecked = true;
        _trafficBox.VerticalAlignment = VerticalAlignment.Center;
        _trafficBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.TrafficPreviewEnabled = _trafficBox.IsChecked == true;
            // 仿真关掉时规划线也要一起收起来，否则会只剩一堆没人遵守的停点线
            _trafficPlanBox.IsEnabled = _canvas.TrafficPreviewEnabled;
            _canvas.RefreshAnimations();
            RefreshStatus();
        };

        _trafficPlanBox.Content = "交通规划";
        _trafficPlanBox.FontSize = UiMetrics.FontTitle;
        _trafficPlanBox.IsChecked = true;
        _trafficPlanBox.VerticalAlignment = VerticalAlignment.Center;
        _trafficPlanBox.IsCheckedChanged += (_, _) =>
        {
            _canvas.PreviewShowTrafficPlan = _trafficPlanBox.IsChecked == true;
            _canvas.RefreshAnimations();
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
        _speedText.FontSize = UiMetrics.FontBody;
        _speedText.Foreground = UiTheme.PreviewHint;
        _speedText.Width = 44;
        _speedText.VerticalAlignment = VerticalAlignment.Center;

        _timeText.Text = "00:00";
        _timeText.FontSize = UiMetrics.FontTitle;
        _timeText.Foreground = UiTheme.PreviewText;
        _timeText.FontFamily = UiFonts.Mono;
        _timeText.VerticalAlignment = VerticalAlignment.Center;

        _zoomText.Text = "100%";
        _zoomText.FontSize = UiMetrics.FontBody;
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
                _trafficBox,
                _trafficPlanBox,
                new TextBlock { Text = "速度", FontSize = UiMetrics.FontBody, Foreground = UiTheme.PreviewHint, VerticalAlignment = VerticalAlignment.Center },
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

        var surface = UiFactory.CanvasStage(
            _canvas,
            UiTheme.PreviewView.FrameBorder,
            cornerRadius: UiMetrics.SmallCornerRadius,
            margin: new Thickness(10, 8, 10, 8));

        return new Grid { Children = { surface } };
    }

    /// <summary>底栏：当前状态与动画 / 路径计数。</summary>
    private Control BuildFooter() =>
        PreviewWindowChrome.BuildFooter(
            _statusText,
            _countText,
            "预览只读：滚轮缩放，右键拖动平移；不会修改地图数据。");

    /// <summary>底栏区块：相位状态行 + 原有状态栏。相位行只在交通仿真开启时刷新文案。</summary>
    private Control BuildFooterBlock()
    {
        _trafficText.Text = "交通仿真准备中…";
        _trafficText.FontSize = UiMetrics.FontTitle;
        _trafficText.Foreground = UiTheme.InfoText;
        _trafficText.TextTrimming = TextTrimming.CharacterEllipsis;
        _trafficText.VerticalAlignment = VerticalAlignment.Center;

        var label = new TextBlock
        {
            Text = "交通仿真",
            FontSize = UiMetrics.FontBody,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.InfoText,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { label, _trafficChips, _trafficText },
        };

        var trafficBar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF6, 0xFB, 0xFD)),
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 6),
            Child = row,
        };

        return new StackPanel
        {
            Children =
            {
                trafficBar,
                BuildFooter(),
            },
        };
    }

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
        _canvas.TrafficPreviewEnabled = true;

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

        RefreshTrafficLine(paths);
    }

    /// <summary>刷新相位状态行：有交通数据时显示各灯相位与等待数量，没有时给出替代入口。</summary>
    /// <param name="paths">当前地图的路径物件数量。</param>
    private void RefreshTrafficLine(int paths)
    {
        if (_trafficBox.IsChecked != true)
        {
            ClearTrafficChips();
            _trafficText.Text = "交通仿真已关闭：角色按匀速沿路径移动。";
            _trafficText.Foreground = UiTheme.InfoText;
            return;
        }

        var sim = _canvas.TrafficSim;
        var summary = _canvas.TrafficSimulationStatus;
        if (sim is null || !sim.HasTrafficData)
        {
            ClearTrafficChips();
            _trafficText.Text = paths == 0
                ? "本关没有灯 / 横道 / 路径数据，暂无规则可看；可用工具栏「交通规则演示」查看示例路口。"
                : "本关没有红绿灯或横道数据，角色按匀速移动；可用工具栏「交通规则演示」查看示例路口。";
            _trafficText.Foreground = UiTheme.InfoText;
            return;
        }

        UpdateTrafficChips(sim);
        var vehicleCount = sim.Agents.Count(agent => agent.IsVehicle);
        var pedestrianCount = sim.Agents.Count - vehicleCount;
        var stopCount = sim.Agents.Sum(agent => agent.Stops.Count);
        _trafficText.Text =
            $"角色 {vehicleCount} 车 / {pedestrianCount} 人 · 停点 {stopCount} · " +
            $"等待 车 {sim.WaitingVehicleCount} · 人 {sim.WaitingPedestrianCount}";
        _trafficText.Foreground = UiTheme.PreviewText;
    }

    /// <summary>按灯列表一次性建出相位胶囊，之后每次刷新只改灯色与倒计时文本，避免状态条抖动。</summary>
    private void UpdateTrafficChips(TrafficSimulation sim)
    {
        if (_trafficChipLightCount != sim.Lights.Count)
        {
            _trafficChipLightCount = sim.Lights.Count;
            _trafficChips.Children.Clear();
            _trafficChipTexts.Clear();
            _trafficChipDots.Clear();

            foreach (var light in sim.Lights)
            {
                var dot = new Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var name = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(light.Name) ? light.Id : light.Name,
                    FontSize = UiMetrics.FontBody,
                    Foreground = UiTheme.PreviewText,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var phase = new TextBlock
                {
                    FontSize = UiMetrics.FontBody,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                _trafficChipDots.Add(dot);
                _trafficChipTexts.Add(phase);

                var chip = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xEB, 0xF2)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(9, 3),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = { dot, name, phase },
                    },
                };
                _trafficChips.Children.Add(chip);
            }
        }

        for (var i = 0; i < sim.Lights.Count && i < _trafficChipTexts.Count; i++)
        {
            var light = sim.Lights[i];
            var phase = sim.PhaseOf(light.Id);
            var color = phase switch
            {
                TrafficPhase.Red => Color.FromArgb(0xFF, 0xE5, 0x48, 0x4D),
                TrafficPhase.Yellow => Color.FromArgb(0xFF, 0xC5, 0x8A, 0x00),
                _ => Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32),
            };
            _trafficChipDots[i].Fill = new SolidColorBrush(color);
            _trafficChipTexts[i].Text =
                $"{TrafficSimulation.PhaseText(phase)} {sim.PhaseRemaining(light.Id):0.0}s";
            _trafficChipTexts[i].Foreground = new SolidColorBrush(color);
        }
    }

    /// <summary>清空相位胶囊并复位缓存，切换到无交通数据的地图时不会残留上一张图的灯。</summary>
    private void ClearTrafficChips()
    {
        if (_trafficChipLightCount < 0 && _trafficChips.Children.Count == 0)
        {
            return;
        }

        _trafficChipLightCount = -1;
        _trafficChips.Children.Clear();
        _trafficChipTexts.Clear();
        _trafficChipDots.Clear();
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
