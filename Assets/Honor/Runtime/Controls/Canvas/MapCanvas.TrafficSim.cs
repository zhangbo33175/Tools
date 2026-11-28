/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.TrafficSim.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布交通仿真接入：预览位置改写、相位灯头着色与等待提示
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>画布交通仿真接入：预览位置改写、相位灯头着色与等待提示。</summary>
public sealed partial class MapCanvas : Control
{
    #region 常量与画刷

    /// <summary>信号机屏幕尺寸（像素）：缩放后保持恒定，远看也能分辨红绿。</summary>
    private const double SimHeadWidth = 24.0;
    private const double SimHeadHeight = 54.0;
    private const double SimLampRadius = 6.2;

    /// <summary>
    /// 缩到全图时灯头的最小尺寸下限。恒定 24×54 在 16% 缩放的地图上相当于 4% 屏宽，
    /// 同一路口的四盏灯会糊成一团，看起来"只有一个红绿灯"，所以按缩放分档缩小；
    /// 0.55 是"还能认出三灯位"的下限，再小灯位之间就只剩几个像素。
    /// </summary>
    private const double SimHeadMinScale = 0.55;

    /// <summary>灯头缩到这个比例以下就不再画右侧相位文字：文字比灯箱还宽，是重叠的主因。</summary>
    private const double SimHeadLabelScale = 0.9;

    /// <summary>
    /// 行人灯头：只有红 / 绿两个灯位，比车行灯矮一截。
    /// 与车行灯同排在路缘时，高度差是唯一的"一眼区分"，否则四盏灯看着一模一样。
    /// </summary>
    private const double SimPedHeadWidth = 20.0;
    private const double SimPedHeadHeight = 40.0;


    /// <summary>
    /// 灭灯不能画成纯黑：深色灯箱上的纯黑圆点等于看不见，缩小后整盏灯只剩一个亮点，
    /// 远看就像「只有一个红绿灯」。灭灯保留同色暗档，三个灯位始终可辨。
    /// </summary>

    #endregion

    #region 字段

    private TrafficSimulation? _trafficSim;
    private bool _trafficPreviewEnabled;
    private bool _previewShowTrafficPlan = true;

    /// <summary>诊断用：上一次打印覆盖层参数的时间戳（毫秒），避免逐帧刷日志。</summary>
    private long _trafficOverlayTraceTick;

    #endregion

    #region 公开属性与方法

    /// <summary>
    /// 预览中是否按交通规则推进角色（停止线停车、相位等待、转绿续走）。
    /// 关闭时退回原有的匀速插值，行为与旧版预览完全一致。
    /// </summary>
    public bool TrafficPreviewEnabled
    {
        get => _trafficPreviewEnabled;
        set
        {
            if (_trafficPreviewEnabled == value)
            {
                return;
            }

            _trafficPreviewEnabled = value;
            if (value)
            {
                EnsureTrafficSimulation();
            }

            InvalidateVisual();
        }
    }

    /// <summary>当前仿真器；未开启或没建过时为 null。</summary>
    public TrafficSimulation? TrafficSim => _trafficSim;

    /// <summary>
    /// 预览里是否把「交通规划」本身画出来：每个仿真角色的停点（车实线、人虚线）与停点来源。
    /// 默认开；关掉后只保留灯头与等待标签，方便对照纯画面。
    /// </summary>
    public bool PreviewShowTrafficPlan
    {
        get => _previewShowTrafficPlan;
        set
        {
            if (_previewShowTrafficPlan == value)
            {
                return;
            }

            _previewShowTrafficPlan = value;
            InvalidateVisual();
        }
    }

    /// <summary>仿真状态摘要，供预览窗 HUD 显示。</summary>
    public string TrafficSimulationStatus =>
        _trafficSim is null ? "交通仿真未启动" : _trafficSim.StatusSummary();

    /// <summary>重建仿真器（地图灯 / 横道 / 路径数据变化后调用）。</summary>
    public void RebuildTrafficSimulation()
    {
        _trafficSim = _config is null ? null : new TrafficSimulation(_config);
        if (_trafficPreviewEnabled)
        {
            _trafficSim?.Reset();
        }

        InvalidateVisual();
    }

    /// <summary>丢弃sim，下一次开启或换图时再按新数据重建。</summary>
    // ---TODO 一致性：换图 / 改灯后统一从这里失效，避免旧仿真继续读上一份地图。
    private void InvalidateTrafficSimulation()
    {
        _trafficSim = null;
        if (_trafficPreviewEnabled)
        {
            EnsureTrafficSimulation();
        }
    }

    /// <summary>确保仿真器与当前地图一致。</summary>
    private void EnsureTrafficSimulation()
    {
        if (_config is null)
        {
            _trafficSim = null;
            return;
        }

        _trafficSim ??= new TrafficSimulation(_config);
    }

    #endregion

    #region 时钟推进与复位

    /// <summary>按预览步长推进仿真；未开启时什么也不做。</summary>
    /// <param name="deltaSeconds">预览时钟步长（秒）。</param>
    private void AdvanceTrafficSimulation(double deltaSeconds)
    {
        if (!_trafficPreviewEnabled)
        {
            return;
        }

        EnsureTrafficSimulation();
        _trafficSim?.Advance(deltaSeconds);
    }

    /// <summary>预览重播时把仿真归零。</summary>
    private void ResetTrafficSimulation() => _trafficSim?.Reset();

    #endregion

    #region 相位覆盖层

    /// <summary>
    /// 预览模式下的交通覆盖层：灯位按当前相位着色，正在等灯的角色头顶标「等灯」。
    /// 只在预览 + 交通仿真开启时绘制，编辑态不受影响。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图数据。</param>
    private void DrawTrafficSimOverlay(DrawingContext context, MapConfigData config)
    {
        if (!_trafficPreviewEnabled || !PreviewMode || _trafficSim is null)
        {
            return;
        }

        var h = Hairline;
        DrawStopMarks(context, h);

        // 灯头按缩放分档：缩放到 55% 及以上用满尺寸，干脆缩到全图后一路缩到 55%，四盏灯才能各自分开。
        var headScale = Math.Clamp(_zoom / SimHeadMinScale, SimHeadMinScale, 1.0);
        var showPhaseLabel = headScale >= SimHeadLabelScale;
        var now = Environment.TickCount64;
        if (MainWindow.TraceStartupEnabled && now - _trafficOverlayTraceTick > 2000)
        {
            _trafficOverlayTraceTick = now;
            var firstPos = config.TrafficLights.Count > 0
                ? $"{config.TrafficLights[0].PosX:0},{config.TrafficLights[0].PosY:0}"
                : "-";
            MainWindow.TraceStartup(
                $"overlay zoom={_zoom:0.000} h={h:0.00} head={headScale:0.00} lights={config.TrafficLights.Count} " +
                $"bounds={Bounds.Width:0}x{Bounds.Height:0} offset={_offset.X:0},{_offset.Y:0} l0={firstPos}");
        }
        // 路面纵向范围取自横道几何：灯头一律往「路面外侧」挂，
        // 南侧路缘的灯挂在锚点下方、北侧挂在锚点上方，灯箱就不会压在车道和车上。
        var roadBand = RoadBand(config);
        var roadMid = roadBand is null ? double.NaN : (roadBand.Value.Top + roadBand.Value.Bottom) * 0.5;
        var pedestrianLightIds = PedestrianLightIds(config);
        foreach (var light in config.TrafficLights)
        {
            var phase = _trafficSim.PhaseOf(light.Id);
            var hangBelow = !double.IsNaN(roadMid) && light.PosY > roadMid;
            DrawSignalHead(
                context,
                new Point(light.PosX, light.PosY),
                phase,
                _trafficSim.PhaseRemaining(light.Id),
                h,
                headScale,
                showPhaseLabel,
                hangBelow,
                pedestrianLightIds.Contains(light.Id));
        }

        if (_zoom < 0.2)
        {
            return;
        }

        foreach (var agent in _trafficSim.Agents)
        {
            if (!agent.Waiting)
            {
                continue;
            }

            var foot = agent.Position;
            var lightId = agent.WaitingLightId;
            var remain = string.IsNullOrEmpty(lightId) ? 0 : _trafficSim.PhaseRemaining(lightId);
            // 标签带上停点来源（停止线 / 横道 / 灯位），预览里能直接对上是哪条规划在拦这个角色
            var source = agent.Stops.Count > 0 ? agent.Stops[0].Source : string.Empty;
            var text = string.IsNullOrEmpty(source)
                ? $"等灯 {remain:0.0}s"
                : $"等灯 {remain:0.0}s · {source}";
            var tag = CanvasText.Create(text, 9.0 * h, UiTheme.Sim.Waiting);
            var width = tag.Width * h;
            // 行人标签抬到头顶上方，否则站在路缘时会盖在前面的车上；
            // 载具标签留在车顶前，贴近停止线更好认
            var lift = agent.IsVehicle
                ? 28 * h
                : Math.Max(90.0, (agent.Source?.PixelHeight ?? 0) + 14);
            var pill = new Rect(foot.X - width * 0.5 - 6 * h, foot.Y - lift, width + 12 * h, 15 * h);
            context.DrawRectangle(UiTheme.Sim.WaitingBack, null, pill, 7.5 * h, 7.5 * h);
            context.DrawText(tag, new Point(pill.X + 6 * h, pill.Y + 2 * h));
        }
    }

    /// <summary>
    /// 行人灯 ID 集合：横道绑定的那盏灯就是行人信号。
    /// 用数据判定而不是靠 id 命名去猜，换图 / 改名都不会把车行灯误判成行人灯。
    /// </summary>
    /// <param name="config">地图数据。</param>
    /// <returns>行人灯 ID 集合（可能为空）。</returns>
    private static HashSet<string> PedestrianLightIds(MapConfigData config)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var crosswalk in config.Crosswalks)
        {
            if (!string.IsNullOrEmpty(crosswalk.BoundLightId))
            {
                ids.Add(crosswalk.BoundLightId);
            }
        }

        return ids;
    }

    /// <summary>
    /// 路面纵向范围（世界 Y）。单行道地图里所有横道都跨同一条路，取所有横道端点的最小 / 最大值即可；
    /// 没有横道时返回 null，灯头退回"统一挂在锚点上方"的旧表现。
    /// </summary>
    /// <param name="config">地图数据。</param>
    private static (double Top, double Bottom)? RoadBand(MapConfigData config)
    {
        if (config.Crosswalks.Count == 0)
        {
            return null;
        }

        var top = double.MaxValue;
        var bottom = double.MinValue;
        foreach (var crosswalk in config.Crosswalks)
        {
            foreach (var y in new[] { crosswalk.PointA.Y, crosswalk.PointB.Y })
            {
                if (y < top)
                {
                    top = y;
                }

                if (y > bottom)
                {
                    bottom = y;
                }
            }
        }

        return bottom > top ? (top, bottom) : null;
    }

    /// <summary>
    /// 画一盏信号机：深色灯箱 + 灯位（当前相位高亮，其余压暗），右侧标相位与倒计时。
    /// 车行灯三灯位（红黄绿），行人灯两灯位（红绿）且更矮，一排灯里一眼能分出谁是行人信号。
    /// </summary>
    /// <param name="headScale">灯头缩放档（1 = 满尺寸，全图视图下缩到 0.55）。</param>
    /// <param name="showPhaseLabel">是否画右侧相位文字；缩得太小时由底部相位胶囊接手。</param>
    /// <param name="hangBelow">true = 灯头挂在锚点下方（南侧路缘灯），false = 挂在上方（北侧路缘灯）。</param>
    /// <param name="pedestrian">true = 行人灯（两灯位、矮灯箱）。</param>
    private static void DrawSignalHead(
        DrawingContext context,
        Point center,
        TrafficPhase phase,
        double remaining,
        double h,
        double headScale,
        bool showPhaseLabel,
        bool hangBelow,
        bool pedestrian)
    {
        var headWidth = (pedestrian ? SimPedHeadWidth : SimHeadWidth) * headScale * h;
        var headHeight = (pedestrian ? SimPedHeadHeight : SimHeadHeight) * headScale * h;
        var gap = 6 * headScale * h;
        // 灯箱挂在锚点外侧：headHeight / gap 已经是世界单位（内部乘过 h），
        // 不能再对 headHeight 二次乘 h，否则整个灯箱会被推到地图顶边外（缩到全图时表现为"看不见/只剩一个灯"）。
        var head = new Rect(
            center.X - headWidth * 0.5,
            hangBelow ? center.Y + gap : center.Y - headHeight - gap,
            headWidth,
            headHeight);

        // 灯杆：锚点与灯箱之间那一段，南北两侧灯都是"杆从锚点伸向灯箱"
        context.FillRectangle(
            UiTheme.Sim.PhasePole,
            new Rect(
                center.X - 1.8 * headScale * h,
                hangBelow ? center.Y : head.Bottom,
                3.6 * headScale * h,
                gap + 1 * headScale * h));
        // 杆脚底座：让灯"立在路缘上"，而不是从地面凭空长出一根灯箱
        context.FillRectangle(
            UiTheme.Sim.PhasePole,
            new Rect(
                center.X - 5 * headScale * h,
                center.Y - 2.5 * headScale * h,
                10 * headScale * h,
                5 * headScale * h));
        context.DrawRectangle(UiTheme.Sim.PhaseHousing, null, head, 4 * headScale * h, 4 * headScale * h);
        context.DrawRectangle(
            null,
            new Pen(UiTheme.Sim.PhaseHousingEdge, Math.Max(0.8, 1.0 * headScale * h)),
            head,
            4 * headScale * h,
            4 * headScale * h);

        if (pedestrian)
        {
            // 行人灯只有红 / 绿：黄灯对行人没有意义，多一个灯位只会让灯箱更像车行灯
            DrawLamp(context, new Point(center.X, head.Y + head.Height * 0.30), phase == TrafficPhase.Red, UiTheme.Sim.PhaseRed, UiTheme.Sim.PhaseRedDim, h, headScale);
            DrawLamp(context, new Point(center.X, head.Y + head.Height * 0.72), phase == TrafficPhase.Green, UiTheme.Sim.PhaseGreen, UiTheme.Sim.PhaseGreenDim, h, headScale);
        }
        else
        {
            DrawLamp(context, new Point(center.X, head.Y + head.Height * 0.20), phase == TrafficPhase.Red, UiTheme.Sim.PhaseRed, UiTheme.Sim.PhaseRedDim, h, headScale);
            DrawLamp(context, new Point(center.X, head.Y + head.Height * 0.50), phase == TrafficPhase.Yellow, UiTheme.Sim.PhaseYellow, UiTheme.Sim.PhaseYellowDim, h, headScale);
            DrawLamp(context, new Point(center.X, head.Y + head.Height * 0.80), phase == TrafficPhase.Green, UiTheme.Sim.PhaseGreen, UiTheme.Sim.PhaseGreenDim, h, headScale);
        }

        if (!showPhaseLabel)
        {
            return;
        }

        var label = CanvasText.Create(
            $"{TrafficSimulation.PhaseText(phase)} {remaining:0.0}s",
            11.5 * h,
            Brushes.White);
        var labelWidth = label.Width * h;
        var labelBox = new Rect(
            head.Right + 5 * h,
            head.Y + head.Height * 0.30,
            labelWidth + 9 * h,
            17 * h);
        context.DrawRectangle(UiTheme.Sim.PhaseHousing, null, labelBox, 4 * h, 4 * h);
        context.DrawText(label, new Point(labelBox.X + 4.5 * h, labelBox.Y + 2.5 * h));
    }

    /// <summary>单个灯位：亮灯实心 + 柔光；灭灯用同色暗档，保留三灯位轮廓。</summary>
    /// <param name="brush">亮灯颜色。</param>
    /// <param name="dimBrush">灭灯颜色（同色暗档，深色灯箱上仍能看出灯位）。</param>
    private static void DrawLamp(
        DrawingContext context,
        Point center,
        bool active,
        IBrush brush,
        IBrush dimBrush,
        double h,
        double lampScale)
    {
        var radius = SimLampRadius * lampScale * h;
        var ring = new Pen(UiTheme.Sim.PhaseRing, Math.Max(0.6, 0.8 * lampScale * h));
        if (active)
        {
            context.DrawEllipse(
                UiTheme.Sim.LampGlow,
                null,
                center,
                radius * 1.7,
                radius * 1.7);
            context.DrawEllipse(brush, ring, center, radius, radius);
            return;
        }

        context.DrawEllipse(dimBrush, ring, center, radius, radius);
    }

    /// <summary>
    /// 交通规划里的停点：以停点为中心画一条垂直于路径的短线。
    /// 载具用实线（白），行人用虚线（黄），来源（停止线 / 横道 / 灯位）在等待标签上标出，
    /// 这样路径点一改，就能在预览里直接看出停点被交通规划挪到了哪里。
    /// </summary>
    private void DrawStopMarks(DrawingContext context, double h)
    {
        if (!_previewShowTrafficPlan || _trafficSim is null || _zoom < 0.12)
        {
            return;
        }

        var vehiclePen = new Pen(UiTheme.Sim.StopLine, Math.Max(0.7, 1.4 * h));
        var pedestrianPen = new Pen(
            UiTheme.Sim.Waiting,
            Math.Max(0.6, 1.1 * h),
            new DashStyle(new[] { 4.0, 3.0 }, 0));

        foreach (var agent in _trafficSim.Agents)
        {
            if (agent.Stops.Count == 0)
            {
                continue;
            }

            var stop = agent.Stops[0].Distance;
            var before = PathMath.PointAtDistance(agent.Points, Math.Max(0, stop - 3));
            var after = PathMath.PointAtDistance(agent.Points, Math.Min(agent.Total, stop + 3));
            if (before is null || after is null)
            {
                continue;
            }

            var dx = after.Value.X - before.Value.X;
            var dy = after.Value.Y - before.Value.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01)
            {
                continue;
            }

            // 载具的停点记的是车身中心，线要画在车头外侧，否则会从车身穿过去；行人按脚底算。
            var half = agent.IsVehicle ? 16.0 : 9.0;
            var foot = PathMath.PointAtDistance(
                agent.Points,
                stop + (agent.IsVehicle ? agent.BodyHalfLength : 0));
            if (foot is null)
            {
                continue;
            }

            var nx = -dy / length * half * h;
            var ny = dx / length * half * h;
            context.DrawLine(
                agent.IsVehicle ? vehiclePen : pedestrianPen,
                new Point(foot.Value.X - nx, foot.Value.Y - ny),
                new Point(foot.Value.X + nx, foot.Value.Y + ny));
        }
    }

    #endregion
}
