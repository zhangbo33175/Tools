/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficDemoWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则演示窗：示例路口 + 仿真播放，验证停线/相位/续走/行人过街
 ***************************************************************/

using System;
using System.Diagnostics;
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

#region 演示窗

/// <summary>
/// 交通规则演示窗。用一份内存示例数据（不落盘、不写当前地图）跑 <see cref="TrafficSimulation"/>，
/// 让「车停在停止线外 → 红灯等待 → 转绿起步」「行人路缘等待 → 绿灯过街」
/// 在编辑器里直接可见；相位、距离、速度全部来自与地图预览同一份仿真代码。
/// </summary>
public sealed class TrafficDemoWindow : Window
{
    #region 常量


    #endregion

    #region 字段

    private readonly TrafficDemoView _view = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _phaseText = new();
    private readonly TextBlock _countText = new();
    private readonly TextBlock _speedText = new();
    private readonly Slider _speedSlider = new();
    private readonly Button _playButton = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TrafficSimulation? _sim;
    private double _lastTick;
    private double _speed = 1.0;
    private bool _paused;

    #endregion

    #region 构造

    /// <summary>构造演示窗。</summary>
    public TrafficDemoWindow()
    {
        Title = "交通规则演示 - 示例路口";
        Width = 1180;
        Height = 800;
        MinWidth = 900;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PreviewPanel;

        Content = PreviewWindowChrome.BuildShell(BuildHeader(), BuildBody(), BuildFooter());

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += (_, _) => OnTick();

        Opened += (_, _) =>
        {
            _clock.Restart();
            _lastTick = 0;
            _timer.Start();
            RefreshStatus();
        };
        Closed += (_, _) => _timer.Stop();

        _sim = new TrafficSimulation(TrafficDemoScene.Build());
        RestartSimulation();
        WindowChromeTheme.Attach(this);
    }

    #endregion

    #region 布局

    /// <summary>顶栏：播放 / 重播 / 跳到变绿 / 速度。</summary>
    private Control BuildHeader()
    {
        var title = PreviewWindowChrome.BuildTitlePanel(
            "交通规则演示",
            "示例路口 · 车灯 / 人灯互补相位 · 与地图预览同一份仿真");

        UiFactory.ApplyPreviewToolbarButton(_playButton, "暂停", 76, primary: true);
        _playButton.Click += (_, _) =>
        {
            _paused = !_paused;
            _lastTick = _clock.Elapsed.TotalSeconds;
            RefreshPlayButton();
            RefreshStatus();
        };

        var restart = UiFactory.PreviewToolbarButton("重新开始", 88);
        restart.Click += (_, _) =>
        {
            _paused = false;
            RestartSimulation();
            RefreshPlayButton();
            RefreshStatus();
        };

        var skip = UiFactory.PreviewToolbarButton("跳到变绿", 88);
        skip.Click += (_, _) => SkipToGreen();

        _speedSlider.Minimum = 0.25;
        _speedSlider.Maximum = 2.0;
        _speedSlider.Value = 1.0;
        _speedSlider.Width = 132;
        _speedSlider.TickFrequency = 0.25;
        _speedSlider.IsSnapToTickEnabled = true;
        _speedSlider.VerticalAlignment = VerticalAlignment.Center;
        _speedSlider.ValueChanged += (_, _) =>
        {
            _speed = _speedSlider.Value;
            _speedText.Text = $"{_speed:0.00}×";
        };

        _speedText.Text = "1.00×";
        _speedText.FontSize = 11;
        _speedText.Foreground = UiTheme.PreviewHint;
        _speedText.Width = 44;
        _speedText.VerticalAlignment = VerticalAlignment.Center;

        _phaseText.Text = "等待启动";
        _phaseText.FontSize = 12;
        _phaseText.FontFamily = new FontFamily("Consolas");
        _phaseText.Foreground = UiTheme.PreviewText;
        _phaseText.VerticalAlignment = VerticalAlignment.Center;

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _phaseText,
                _playButton,
                restart,
                skip,
                new TextBlock { Text = "速度", FontSize = 11, Foreground = UiTheme.PreviewHint, VerticalAlignment = VerticalAlignment.Center },
                _speedSlider,
                _speedText,
            },
        };

        return PreviewWindowChrome.BuildHeader(title, null, right);
    }

    /// <summary>中心：示例场景画布。</summary>
    private Control BuildBody()
    {
        _view.HorizontalAlignment = HorizontalAlignment.Stretch;
        _view.VerticalAlignment = VerticalAlignment.Stretch;

        var surface = new Border
        {
            Background = UiTheme.PreviewView.Stage,
            BorderBrush = UiTheme.PreviewView.FrameBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            Margin = new Thickness(10, 8, 10, 8),
            Child = _view,
        };

        return new Grid { Children = { surface } };
    }

    /// <summary>底栏：状态与图例。</summary>
    private Control BuildFooter() =>
        PreviewWindowChrome.BuildFooter(
            _statusText,
            _countText,
            "示例数据不写入当前地图：车灯红6/绿8/黄2，人灯绿6/红8/黄2，两灯互补。");

    #endregion

    #region 时钟与操作

    private void OnTick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Max(0, now - _lastTick);
        _lastTick = now;

        if (_paused || _sim is null || dt <= 0)
        {
            return;
        }

        _sim.Advance(dt * _speed);
        _view.InvalidateVisual();
        RefreshStatus();
    }

    private void RestartSimulation()
    {
        _sim?.Reset();
        _view.Sim = _sim;
        _view.InvalidateVisual();
        _lastTick = _clock.Elapsed.TotalSeconds;
        RefreshStatus();
    }

    /// <summary>快进到车行灯转绿：按固定步长推进，让「等待 → 起步」可直接观察。</summary>
    private void SkipToGreen()
    {
        if (_sim is null)
        {
            return;
        }

        var guard = 0.0;
        while (_sim.PhaseOf(DemoIds.VehicleLight) != TrafficPhase.Green && guard < 30)
        {
            _sim.Advance(0.05);
            guard += 0.05;
        }

        _view.InvalidateVisual();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var sim = _sim;
        if (sim is null)
        {
            _statusText.Text = "示例场景准备中…";
            _countText.Text = string.Empty;
            return;
        }

        var waitingVehicles = sim.Agents.Count(agent => agent.Waiting && agent.IsVehicle);
        var waitingPeople = sim.Agents.Count(agent => agent.Waiting && !agent.IsVehicle);
        _statusText.Text = $"示例路口：车 200px/s、行人 80px/s 基准；" +
                           $"当前 车等待 {waitingVehicles} 台、人等待 {waitingPeople} 个。";
        _countText.Text = $"t {TimeSpan.FromSeconds(sim.Elapsed).ToString(@"mm\:ss", CultureInfo.InvariantCulture)}";
        _phaseText.Text = sim.StatusSummary();
    }

    private void RefreshPlayButton()
    {
        _playButton.Content = _paused ? "播放" : "暂停";
        _playButton.Background = UiTheme.Accent;
    }

    #endregion
}

#endregion

#region 示例数据

/// <summary>示例数据里的固定 ID，避免演示代码拼字符串。</summary>
internal static class DemoIds
{
    public const string VehicleLight = "demo_light_vehicle";
    public const string PedestrianLight = "demo_light_pedestrian";
    public const string Crosswalk = "demo_crosswalk";
}

/// <summary>示例路口数据：两车道 + 横向道路 + 一条斑马线 + 两组灯 + 两车两行人。</summary>
internal static class TrafficDemoScene
{
    public const double SceneWidth = 900.0;
    public const double SceneHeight = 640.0;
    public const double RoadLeft = 330.0;
    public const double RoadRight = 570.0;
    public const double CrossZoneTop = 250.0;
    public const double CrossZoneBottom = 400.0;
    public const double CrosswalkY = 320.0;
    public const double LaneUpX = 500.0;
    public const double LaneDownX = 400.0;

    /// <summary>构建一份内存地图：只用于演示，不写盘。</summary>
    /// <returns>含灯 / 横道 / 四名角色的配置。</summary>
    public static MapConfigData Build()
    {
        var config = new MapConfigData
        {
            SceneName = "示例路口（演示）",
            Width = (int)SceneWidth,
            Height = (int)SceneHeight,
        };

        var vehicleLight = new MapTrafficLightData
        {
            Id = DemoIds.VehicleLight,
            Name = "车行灯",
            PosX = 612,
            PosY = 268,
            Direction = TrafficDirections.Horizontal,
            Side = TrafficLightDefaults.Side,
            RedDuration = 6,
            GreenDuration = 8,
            YellowDuration = 2,
            OffsetPhase = 0,
            InitialPhase = "red",
            BoundCrosswalkId = DemoIds.Crosswalk,
        };

        var pedestrianLight = new MapTrafficLightData
        {
            Id = DemoIds.PedestrianLight,
            Name = "行人灯",
            PosX = 288,
            PosY = 268,
            Direction = TrafficDirections.Vertical,
            Side = TrafficLightDefaults.Side,
            RedDuration = 8,
            GreenDuration = 6,
            YellowDuration = 2,
            // 相位只认 OffsetPhase：行人灯从红 8s 之后切绿灯，演示一开始就是可过街状态
            OffsetPhase = 8,
            InitialPhase = "green",
            BoundCrosswalkId = DemoIds.Crosswalk,
        };

        config.TrafficLights.Add(vehicleLight);
        config.TrafficLights.Add(pedestrianLight);

        var crosswalk = new MapCrosswalkData
        {
            Id = DemoIds.Crosswalk,
            Name = "示例斑马线",
            PointA = new MapPathPointData { X = RoadLeft, Y = CrosswalkY },
            PointB = new MapPathPointData { X = RoadRight, Y = CrosswalkY },
            Width = 64,
            Direction = TrafficDirections.Horizontal,
            BoundLightId = DemoIds.VehicleLight,
        };
        config.Crosswalks.Add(crosswalk);

        config.Objects.Add(Vehicle(
            "demo_taxi",
            "黄色出租车",
            new[] { new Point(LaneUpX, 700), new Point(LaneUpX, -60) },
            DemoIds.VehicleLight,
            isVehicle: true,
            speed: 1.0));

        config.Objects.Add(Vehicle(
            "demo_truck",
            "灰色货车",
            new[] { new Point(LaneDownX, -60), new Point(LaneDownX, 700) },
            DemoIds.VehicleLight,
            isVehicle: true,
            speed: 0.8));

        config.Objects.Add(Person(
            "demo_ped_a",
            "行人 A",
            new[] { new Point(RoadLeft - 30, CrosswalkY + 16), new Point(RoadRight + 40, CrosswalkY + 16) }));

        config.Objects.Add(Person(
            "demo_ped_b",
            "行人 B",
            new[] { new Point(RoadRight + 40, CrosswalkY + 44), new Point(RoadLeft - 30, CrosswalkY + 44) }));

        return config;
    }

    private static PlacedObject Vehicle(string id, string name, Point[] points, string lightId, bool isVehicle, double speed) =>
        new()
        {
            Id = id,
            Name = name,
            Type = MapObjectType.Prop,
            LayerType = MapLayerKeys.Prop,
            IsVehicle = isVehicle,
            TrafficRole = TrafficRoles.Vehicle,
            Speed = speed,
            Loop = true,
            WalkMode = "loop",
            YellowTreatAsRed = true,
            PathPoints = points.Select(point => new MapPathPointData { X = point.X, Y = point.Y }).ToList(),
            BoundLightIds = new[] { lightId }.ToList(),
        };

    private static PlacedObject Person(string id, string name, Point[] points) =>
        new()
        {
            Id = id,
            Name = name,
            Type = MapObjectType.Character,
            LayerType = MapLayerKeys.Character,
            IsVehicle = false,
            TrafficRole = TrafficRoles.Pedestrian,
            Speed = 1.0,
            Loop = true,
            WalkMode = "loop",
            PathPoints = points.Select(point => new MapPathPointData { X = point.X, Y = point.Y }).ToList(),
            BoundLightIds = new[] { DemoIds.PedestrianLight }.ToList(),
        };
}

#endregion

#region 绘制

/// <summary>示例路口的自绘视图：只画示意图形，不加载任何贴图。</summary>
internal sealed class TrafficDemoView : Control
{
    #region 画刷与常量

    private static readonly IBrush AsphaltBrush = new SolidColorBrush(Color.Parse("#3A3F46"));
    private static readonly IBrush SidewalkBrush = new SolidColorBrush(Color.Parse("#77826F"));
    private static readonly IBrush FacadeBrush = new SolidColorBrush(Color.Parse("#2C322F"));
    private static readonly IBrush StripeBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xEE, 0xF1, 0xE9));
    private static readonly IBrush LaneBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xF2, 0xD9, 0x6B));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#F3F5F2"));
    private static readonly IBrush HintBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xD3, 0xD9, 0xD2));
    private static readonly IBrush TaxiBrush = new SolidColorBrush(Color.Parse("#F2C14E"));
    private static readonly IBrush TruckBrush = new SolidColorBrush(Color.Parse("#9AA6B2"));
    private static readonly IBrush PedestrianBrush = new SolidColorBrush(Color.Parse("#EDE3C8"));
    private static readonly IBrush WaitingBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xD5, 0x4F));
    private static readonly IBrush LampOffBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x10, 0x14, 0x18));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.Parse("#E53935"));
    private static readonly IBrush YellowBrush = new SolidColorBrush(Color.Parse("#FFB300"));
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#43A047"));

    private const double VehicleWidth = 34.0;
    private const double VehicleHeight = 58.0;
    private const double PersonWidth = 16.0;
    private const double PersonHeight = 30.0;

    #endregion

    /// <summary>要绘制的仿真；为空时只画静态街景。</summary>
    public TrafficSimulation? Sim { get; set; }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        context.FillRectangle(FacadeBrush, new Rect(0, 0, width, height));

        var scale = Math.Min(width / TrafficDemoScene.SceneWidth, height / TrafficDemoScene.SceneHeight);
        var offsetX = (width - TrafficDemoScene.SceneWidth * scale) * 0.5;
        var offsetY = (height - TrafficDemoScene.SceneHeight * scale) * 0.5;
        var matrix = Matrix.CreateTranslation(offsetX, offsetY) * Matrix.CreateScale(scale, scale);

        using var transform = context.PushTransform(matrix);
        DrawStreet(context);
        DrawHud(context);
        DrawLights(context);

        var sim = Sim;
        if (sim is null)
        {
            return;
        }

        DrawStopLines(context, sim);
        DrawPeople(context, sim);
        DrawVehicles(context, sim);
    }

    #region 街景

    private static void DrawStreet(DrawingContext context)
    {
        var scene = new Rect(0, 0, TrafficDemoScene.SceneWidth, TrafficDemoScene.SceneHeight);
        context.FillRectangle(SidewalkBrush, scene);

        // 竖向主路 + 横向副路，形成视频里的十字口
        context.FillRectangle(AsphaltBrush, new Rect(
            TrafficDemoScene.RoadLeft, 0,
            TrafficDemoScene.RoadRight - TrafficDemoScene.RoadLeft, TrafficDemoScene.SceneHeight));
        context.FillRectangle(AsphaltBrush, new Rect(
            0, TrafficDemoScene.CrossZoneTop,
            TrafficDemoScene.SceneWidth, TrafficDemoScene.CrossZoneBottom - TrafficDemoScene.CrossZoneTop));

        // 斑马线：横向条纹铺在竖向车道上
        var stripeWidth = 12.0;
        var stripeGap = 10.0;
        for (var x = TrafficDemoScene.RoadLeft + 6; x < TrafficDemoScene.RoadRight - stripeWidth; x += stripeWidth + stripeGap)
        {
            context.FillRectangle(
                StripeBrush,
                new Rect(x, TrafficDemoScene.CrosswalkY - 40, stripeWidth, 80));
        }

        // 车道分隔虚线（避开斑马线）
        for (var y = 8.0; y < TrafficDemoScene.SceneHeight; y += 34)
        {
            if (y > TrafficDemoScene.CrossZoneTop - 4 && y < TrafficDemoScene.CrossZoneBottom + 4)
            {
                continue;
            }

            context.FillRectangle(LaneBrush, new Rect(
                (TrafficDemoScene.RoadLeft + TrafficDemoScene.RoadRight) * 0.5 - 2, y, 4, 20));
        }
    }

    private static void DrawHud(DrawingContext context)
    {
        var title = CanvasText.Create("示例路口 · 交通规则仿真", 22.0, TextBrush);
        context.DrawText(title, new Point(24, 18));

        var legend = CanvasText.Create(
            "车灯红 6s / 绿 8s / 黄 2s；人灯绿 6s / 红 8s；车停停止线外，人站路缘等待",
            13.0,
            HintBrush);
        context.DrawText(legend, new Point(24, 48));

        var labels = CanvasText.Create("▲ 上行车道", 12.0, HintBrush);
        context.DrawText(labels, new Point(TrafficDemoScene.LaneUpX + 24, 560));

        var down = CanvasText.Create("▼ 下行车道", 12.0, HintBrush);
        context.DrawText(down, new Point(TrafficDemoScene.LaneDownX - 90, 560));
    }

    private void DrawLights(DrawingContext context)
    {
        var sim = Sim;
        if (sim is null)
        {
            return;
        }

        foreach (var light in sim.Lights)
        {
            var phase = sim.PhaseOf(light.Id);
            var center = new Point(light.PosX, light.PosY);

            // 灯杆
            context.FillRectangle(LampOffBrush, new Rect(center.X - 3, center.Y + 26, 6, 26));
            context.FillRectangle(LampOffBrush, new Rect(center.X - 16, center.Y - 30, 32, 64), 4);

            DrawLamp(context, new Point(center.X, center.Y - 18), phase == TrafficPhase.Red ? RedBrush : LampOffBrush);
            DrawLamp(context, new Point(center.X, center.Y), phase == TrafficPhase.Yellow ? YellowBrush : LampOffBrush);
            DrawLamp(context, new Point(center.X, center.Y + 18), phase == TrafficPhase.Green ? GreenBrush : LampOffBrush);

            var text = CanvasText.Create(
                $"{light.Name} {TrafficSimulation.PhaseText(phase)} {sim.PhaseRemaining(light.Id):0.0}s",
                12.0,
                TextBrush);
            context.DrawText(text, new Point(center.X - text.Width * 0.5, center.Y - 54));
        }
    }

    private static void DrawLamp(DrawingContext context, Point center, IBrush brush)
    {
        context.DrawEllipse(brush, new Pen(LampOffBrush, 1), center, 9, 9);
    }

    #endregion

    #region 角色与停线

    private void DrawStopLines(DrawingContext context, TrafficSimulation sim)
    {
        foreach (var agent in sim.Agents)
        {
            if (!agent.IsVehicle || agent.Stops.Count == 0)
            {
                continue;
            }

            var point = PathMath.PointAtDistance(agent.Points, agent.Stops[0].Distance);
            if (point is null)
            {
                continue;
            }

            var pen = new Pen(StripeBrush, 3);
            context.DrawLine(pen, new Point(point.Value.X - 46, point.Value.Y), new Point(point.Value.X + 46, point.Value.Y));
        }
    }

    private void DrawVehicles(DrawingContext context, TrafficSimulation sim)
    {
        foreach (var agent in sim.Agents)
        {
            if (!agent.IsVehicle)
            {
                continue;
            }

            var foot = agent.Position;
            var body = new Rect(foot.X - VehicleWidth * 0.5, foot.Y - VehicleHeight, VehicleWidth, VehicleHeight);
            var brush = agent.ObjectId == "demo_truck" ? TruckBrush : TaxiBrush;

            if (agent.Waiting)
            {
                context.DrawEllipse(WaitingBrush, null, foot, VehicleWidth, VehicleWidth * 0.6);
            }

            context.FillRectangle(brush, body, 6);
            context.DrawRectangle(null, new Pen(LampOffBrush, 1.5), body, 6, 6);

            // 车头一格深色挡风玻璃，方向按行进的正负 Y 判断
            var facingUp = agent.Speed >= 0 && agent.Points[^1].Y < agent.Points[0].Y;
            var glassHeight = 14.0;
            var glassY = facingUp ? body.Y + 8 : body.Bottom - glassHeight - 8;
            context.FillRectangle(LampOffBrush, new Rect(body.X + 5, glassY, VehicleWidth - 10, glassHeight), 3);

            if (agent.Waiting)
            {
                context.FillRectangle(RedBrush, new Rect(body.X + 3, body.Bottom - 4, 8, 3));
                context.FillRectangle(RedBrush, new Rect(body.Right - 11, body.Bottom - 4, 8, 3));
            }

            var label = CanvasText.Create(agent.Name, 10.0, LampOffBrush);
            context.DrawText(label, new Point(foot.X - label.Width * 0.5, body.Y - 14));
        }
    }

    private void DrawPeople(DrawingContext context, TrafficSimulation sim)
    {
        foreach (var agent in sim.Agents)
        {
            if (agent.IsVehicle)
            {
                continue;
            }

            var foot = agent.Position;
            if (agent.Waiting)
            {
                context.DrawEllipse(WaitingBrush, null, foot, 20, 10);
            }

            var body = new Rect(foot.X - PersonWidth * 0.5, foot.Y - PersonHeight, PersonWidth, PersonHeight);
            context.FillRectangle(PedestrianBrush, body, 6);
            context.DrawEllipse(PedestrianBrush, null, new Point(foot.X, body.Y - 6), 7, 7);

            var label = CanvasText.Create(agent.Name, 9.0, LampOffBrush);
            context.DrawText(label, new Point(foot.X - label.Width * 0.5, body.Y - 26));
        }
    }

    #endregion
}

#endregion




