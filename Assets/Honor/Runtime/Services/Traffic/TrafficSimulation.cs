/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficSimulation.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器交通仿真模型：按弧长推进 + 刹车曲线 + 信号相位等待与续走
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 仿真默认参数

/// <summary>
/// 仿真默认参数（世界像素 / 秒）。与 Unity 侧 <c>BASE_MOVE_PX_PER_SEC=80</c> 同口径放大，
/// 只服务于编辑器预览，不写回任何地图数据。
/// </summary>
public static class TrafficSimDefaults
{
    /// <summary>载具基准速度（像素/秒）。出租车 200、公交 150、货车 160 的近似中值。</summary>
    public const double VehicleSpeed = 200.0;

    /// <summary>行人基准速度（像素/秒）。</summary>
    public const double PedestrianSpeed = 80.0;

    /// <summary>载具加速度。</summary>
    public const double VehicleAccel = 400.0;

    /// <summary>载具减速度（刹车曲线 v=√(2aΔs) 用同一值）。</summary>
    public const double VehicleDecel = 500.0;

    /// <summary>行人加速度。</summary>
    public const double PedestrianAccel = 300.0;

    /// <summary>行人减速度。</summary>
    public const double PedestrianDecel = 600.0;

    /// <summary>载具停止间隙：车头停在停止线外 6 像素。</summary>
    public const double VehicleStopGap = 6.0;

    /// <summary>行人停止间隙：站在路缘外 4 像素。</summary>
    public const double PedestrianStopGap = 4.0;

    /// <summary>载具最小跟车间距：同路径同向排队时，后车与前车至少留 34 像素。</summary>
    public const double VehicleFollowGap = 34.0;

    /// <summary>单帧最大步长，切窗口回来时避免瞬移。</summary>
    public const double MaxStep = 0.1;
}

#endregion

#region 相位与停点

/// <summary>红绿灯相位。</summary>
public enum TrafficPhase
{
    /// <summary>红灯。</summary>
    Red,

    /// <summary>黄灯。</summary>
    Yellow,

    /// <summary>绿灯。</summary>
    Green,
}

/// <summary>一个停点：路径里程 + 约束它的灯。</summary>
public sealed class TrafficStopMark
{
    /// <summary>停点在路径上的累计里程（世界像素）。</summary>
    public double Distance { get; init; }

    /// <summary>约束该停点的红绿灯 ID。</summary>
    public IReadOnlyList<string> LightIds { get; init; } = Array.Empty<string>();

    /// <summary>来源说明（「停止线」「横道」「灯位」），供状态栏排查用。</summary>
    public string Source { get; init; } = string.Empty;
}

#endregion

#region 参与者

/// <summary>仿真中的一台车 / 一个行人。</summary>
public sealed class TrafficAgent
{
    /// <summary>来源实例；演示数据里可为 null。</summary>
    public PlacedObject? Source { get; init; }

    /// <summary>实例 ID，用于按物件查位置。</summary>
    public string ObjectId { get; init; } = string.Empty;

    /// <summary>显示名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>路径点（脚底锚点）。</summary>
    public IReadOnlyList<MapPathPointData> Points { get; init; } = Array.Empty<MapPathPointData>();

    /// <summary>路径总长。</summary>
    public double Total { get; init; }

    /// <summary>
    /// 路径指纹：点序列完全一致的实例视为同一车道；只用于载具排队，
    /// 不参与规则判定。
    /// </summary>
    public string PathKey { get; init; } = string.Empty;

    /// <summary>是否载具；否则按行人处理。</summary>
    public bool IsVehicle { get; init; }

    /// <summary>
    /// 载具沿路径方向的半身长：停点按车头对齐停止线，避免车身穿进斑马线。
    /// 行人恒为 0。
    /// </summary>
    public double BodyHalfLength { get; init; }

    /// <summary>行走模式。</summary>
    public PathPlaybackMode Mode { get; init; } = PathPlaybackMode.Loop;

    public List<TrafficStopMark> Stops { get; } = new();

    /// <summary>最高速度。</summary>
    public double MaxSpeed { get; init; } = TrafficSimDefaults.PedestrianSpeed;

    /// <summary>加速度。</summary>
    public double Accel { get; init; } = TrafficSimDefaults.PedestrianAccel;

    /// <summary>减速度。</summary>
    public double Decel { get; init; } = TrafficSimDefaults.PedestrianDecel;

    public double Distance { get; set; }

    public double Speed { get; set; }

    public bool Waiting { get; private set; }

    public string WaitingLightId { get; private set; } = string.Empty;

    public bool Arrived { get; private set; }

    /// <summary>当前脚底世界坐标。</summary>
    public Point Position => PathMath.PointAtDistance(
        Points, PathMath.NormalizeDistance(Distance, Total, Mode)) ?? default;

    /// <summary>是否有停点。</summary>
    public bool HasStops => Stops.Count > 0;

    /// <summary>重置到起点。</summary>
    public void Reset()
    {
        Distance = 0;
        Speed = 0;
        Waiting = false;
        WaitingLightId = string.Empty;
        Arrived = false;
    }

    /// <summary>推进一帧。</summary>
    /// <param name="dt">步长（秒）。</param>
    /// <param name="isBlocked">查询某个停点当前是否被信号拦住。</param>
    internal void Advance(double dt, Func<TrafficStopMark, TrafficAgent, bool> isBlocked)
    {
        if (Total <= 0 || dt <= 0)
        {
            return;
        }

        Waiting = false;
        WaitingLightId = string.Empty;

        if (Arrived)
        {
            Speed = 0;
            return;
        }

        // 先找最近一个「当前被拦住」的停点；只有进入刹车距离内才参与限速，
        // 否则绿灯期间车辆会被远处的红灯提前拖慢。
        var brakingReach = Speed * Speed / (2 * Decel) + StopGapOf() + 4;
        TrafficStopMark? blocker = null;
        double blockerAhead = double.PositiveInfinity;
        foreach (var stop in Stops)
        {
            var ahead = AheadDistance(stop.Distance);
            if (ahead < 0 || ahead > brakingReach)
            {
                continue;
            }

            if (!isBlocked(stop, this))
            {
                continue;
            }

            if (ahead < blockerAhead)
            {
                blockerAhead = ahead;
                blocker = stop;
            }
        }

        var target = MaxSpeed;
        if (blocker is not null)
        {
            // 刹车曲线：v = √(2aΔs)，离停点越近上限越低；零距离直接停住
            target = Math.Min(target, Math.Sqrt(2 * Decel * Math.Max(0, blockerAhead)));
        }

        if (Speed < target)
        {
            Speed = Math.Min(target, Speed + Accel * dt);
        }
        else
        {
            Speed = Math.Max(target, Speed - Decel * dt);
        }

        if (blocker is not null && blockerAhead <= 0.5)
        {
            Distance = Wrap(blocker.Distance);
            Speed = 0;
            Waiting = true;
            WaitingLightId = blocker.LightIds.FirstOrDefault() ?? string.Empty;
            return;
        }

        var step = Speed * dt;
        if (blocker is not null && step >= blockerAhead)
        {
            Distance = Wrap(blocker.Distance);
            Speed = 0;
            Waiting = true;
            WaitingLightId = blocker.LightIds.FirstOrDefault() ?? string.Empty;
            return;
        }

        Distance = Wrap(Distance + step);
        if (Mode == PathPlaybackMode.Once && Distance >= Total)
        {
            Distance = Total;
            Speed = 0;
            Arrived = true;
        }
    }

    /// <summary>停点相对当前里程的剩余距离；已越过返回 -1。</summary>
    internal double AheadDistance(double stopDistance)
    {
        var delta = stopDistance - Distance;
        if (Mode == PathPlaybackMode.Loop)
        {
            if (delta < 0)
            {
                delta += Total;
            }

            return delta;
        }

        // 单程 / 往返只认前方：越过就不再回头等灯
        return delta < 0 ? -1 : delta;
    }

    private double StopGapOf() => IsVehicle ? TrafficSimDefaults.VehicleStopGap : TrafficSimDefaults.PedestrianStopGap;

    private double Wrap(double value)
    {
        if (Total <= 0)
        {
            return 0;
        }

        if (Mode == PathPlaybackMode.Loop)
        {
            var v = value % Total;
            return v < 0 ? v + Total : v;
        }

        return Math.Clamp(value, 0, Total);
    }
}

#endregion

#region 仿真器

/// <summary>
/// 交通仿真：只读消费 <see cref="MapConfigData"/> 里的灯 / 横道 / 路径物件，
/// 推进每个角色的里程并按信号相位决定停走，不修改任何地图数据。
/// <para>规则口径与 Unity 侧 <c>TrafficManager</c> 对齐：载具红灯、黄灯（按配置）停；行人要求绑定灯全绿。</para>
/// </summary>
public sealed class TrafficSimulation
{
    #region 字段

    private readonly MapConfigData _config;
    private readonly Dictionary<string, MapTrafficLightData> _lights =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MapCrosswalkData> _crosswalks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TrafficAgent> _agents = new();
    private readonly Dictionary<string, TrafficAgent> _agentsById = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 构造

    /// <summary>构造仿真器并冻结当前地图的灯、横道与角色。</summary>
    /// <param name="config">地图数据（只读）。</param>
    public TrafficSimulation(MapConfigData config)
    {
        _config = config;
        foreach (var light in config.TrafficLights)
        {
            if (!string.IsNullOrEmpty(light.Id))
            {
                _lights[light.Id] = light;
            }
        }

        foreach (var crosswalk in config.Crosswalks)
        {
            if (!string.IsNullOrEmpty(crosswalk.Id))
            {
                _crosswalks[crosswalk.Id] = crosswalk;
            }
        }

        foreach (var item in config.Objects)
        {
            if (!PathMath.HasPath(item.PathPoints))
            {
                continue;
            }

            _agents.Add(BuildAgent(item));
        }

        foreach (var agent in _agents)
        {
            if (!string.IsNullOrEmpty(agent.ObjectId))
            {
                _agentsById.TryAdd(agent.ObjectId, agent);
            }
        }
    }

    #endregion

    #region 只读视图

    /// <summary>参与仿真的角色。</summary>
    public IReadOnlyList<TrafficAgent> Agents => _agents;

    /// <summary>地图上的红绿灯。</summary>
    public IReadOnlyList<MapTrafficLightData> Lights => _config.TrafficLights;

    /// <summary>是否具备可演示的交通数据（灯或横道任意一项）。</summary>
    public bool HasTrafficData => _lights.Count > 0 || _crosswalks.Count > 0;

    /// <summary>已经推进的秒数。</summary>
    public double Elapsed { get; private set; }

    /// <summary>正在等灯的载具数量。</summary>
    public int WaitingVehicleCount => _agents.Count(agent => agent.Waiting && agent.IsVehicle);

    /// <summary>正在等灯的行人数量。</summary>
    public int WaitingPedestrianCount => _agents.Count(agent => agent.Waiting && !agent.IsVehicle);

    #endregion

    #region 构建角色

    private TrafficAgent BuildAgent(PlacedObject item)
    {
        var total = PathMath.Length(item.PathPoints);
        var isVehicle = item.IsVehicle ||
                        string.Equals(item.TrafficRole, TrafficRoles.Vehicle, StringComparison.OrdinalIgnoreCase);
        var freeMove = string.Equals(item.TrafficRole, TrafficRoles.Inactive, StringComparison.OrdinalIgnoreCase);
        var speedFactor = AnimationSpeeds.Clamp(item.Speed);
        // 车身半长按贴图算；上限放到 180，避免 300px 宽的公交/货车被当成小车，
        // 停车点压进斑马线、跟车时两车贴图互相穿透
        var bodyHalf = isVehicle
            ? Math.Min(180.0, Math.Max(0, Math.Max(item.PixelWidth, item.PixelHeight)) * 0.5)
            : 0;

        var agent = new TrafficAgent
        {
            Source = item,
            ObjectId = item.Id,
            Name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
            Points = item.PathPoints,
            Total = total,
            PathKey = BuildPathKey(item.PathPoints),
            IsVehicle = isVehicle,
            BodyHalfLength = bodyHalf,
            Mode = PathMath.ParseWalkMode(item.WalkMode),
            MaxSpeed = (isVehicle ? TrafficSimDefaults.VehicleSpeed : TrafficSimDefaults.PedestrianSpeed) * speedFactor,
            Accel = isVehicle ? TrafficSimDefaults.VehicleAccel : TrafficSimDefaults.PedestrianAccel,
            Decel = isVehicle ? TrafficSimDefaults.VehicleDecel : TrafficSimDefaults.PedestrianDecel,
        };

        if (freeMove || total <= 0)
        {
            return agent;
        }

        var lightIds = ResolveLightIds(item);
        if (lightIds.Count == 0)
        {
            return agent;
        }

        // 1) 显式停止线：物件的 StopLineX/Y 投影到路径
        if (item.StopLineX > 0 || item.StopLineY > 0)
        {
            var projection = ProjectOntoPath(item.PathPoints, new Point(item.StopLineX, item.StopLineY));
            if (projection is not null)
            {
                agent.Stops.Add(new TrafficStopMark
                {
                    Distance = projection.Value,
                    LightIds = lightIds,
                    Source = "停止线",
                });
                return agent;
            }
        }

        // 2) 绑定灯的横道中心投影，再回退到横道外沿
        var gap = isVehicle ? TrafficSimDefaults.VehicleStopGap : TrafficSimDefaults.PedestrianStopGap;
        var pushed = new List<double>();
        foreach (var lightId in lightIds)
        {
            var light = _lights.GetValueOrDefault(lightId);
            var stop = ResolveCrosswalkStop(light, item.PathPoints, gap, isVehicle, bodyHalf);
            if (stop is not null && pushed.All(d => Math.Abs(d - stop.Value) > 2))
            {
                pushed.Add(stop.Value);
                agent.Stops.Add(new TrafficStopMark
                {
                    Distance = stop.Value,
                    LightIds = new[] { lightId },
                    Source = "横道",
                });
            }
        }

        if (agent.Stops.Count > 0)
        {
            return agent;
        }

        // 3) 没有横道时退回灯位投影
        foreach (var lightId in lightIds)
        {
            var light = _lights.GetValueOrDefault(lightId);
            if (light is null)
            {
                continue;
            }

            var projection = ProjectOntoPath(item.PathPoints, new Point(light.PosX, light.PosY));
            if (projection is null)
            {
                continue;
            }

            var stop = projection.Value - gap - bodyHalf;
            if (stop <= 1)
            {
                // 灯位在路径起点之前：不生成停止点，避免夹到 0 把角色钉在原地
                continue;
            }

            if (pushed.All(d => Math.Abs(d - stop) > 2))
            {
                pushed.Add(stop);
                agent.Stops.Add(new TrafficStopMark
                {
                    Distance = stop,
                    LightIds = new[] { lightId },
                    Source = "灯位",
                });
            }
        }

        return agent;
    }

    /// <summary>取物件绑定的灯；没有显式绑定但路径穿过某盏灯的横道时自动补绑，避免「有灯有横道却没人停」。</summary>
    private List<string> ResolveLightIds(PlacedObject item)
    {
        var explicitIds = item.BoundLightIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && _lights.ContainsKey(id))
            .ToList();
        if (explicitIds.Count > 0)
        {
            return explicitIds;
        }

        var result = new List<string>();
        foreach (var light in _config.TrafficLights)
        {
            var crosswalk = _crosswalks.GetValueOrDefault(light.BoundCrosswalkId);
            if (crosswalk is null)
            {
                continue;
            }

            var center = CrosswalkCenter(crosswalk);
            var near = MinDistanceToPath(item.PathPoints, center);
            if (near <= Math.Max(crosswalk.Width, 32) * 2)
            {
                result.Add(light.Id);
            }
        }

        // ---TODO 绑定口径：自动补绑只是兜底，正式数据仍应在物件「绑定红绿灯」里显式填灯 ID。
        return result;
    }

    private static Point CrosswalkCenter(MapCrosswalkData crosswalk) => new(
        (crosswalk.PointA.X + crosswalk.PointB.X) * 0.5,
        (crosswalk.PointA.Y + crosswalk.PointB.Y) * 0.5);

    /// <summary>生成路径指纹：点坐标保留一位小数，避免同一车道因浮点尾差被判成不同路径。</summary>
    private static string BuildPathKey(IReadOnlyList<MapPathPointData> points) =>
        string.Join(";", points.Select(point =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#}", point.X, point.Y)));

    private double? ResolveCrosswalkStop(
        MapTrafficLightData? light,
        IReadOnlyList<MapPathPointData> points,
        double gap,
        bool isVehicle,
        double bodyHalf)
    {
        if (light is null)
        {
            return null;
        }

        var crosswalk = _crosswalks.GetValueOrDefault(light.BoundCrosswalkId);
        if (crosswalk is null)
        {
            return null;
        }

        if (!isVehicle)
        {
            // 行人停在路缘：取横道两个端点里里程较小的那个（沿路径进入横道的位置）
            var startA = ProjectOntoPath(points, new Point(crosswalk.PointA.X, crosswalk.PointA.Y));
            var startB = ProjectOntoPath(points, new Point(crosswalk.PointB.X, crosswalk.PointB.Y));
            if (startA is null || startB is null)
            {
                return null;
            }

            var pedStop = Math.Min(startA.Value, startB.Value) - gap;
            // 横道落在路径起点之前（角色出生在横道另一侧）时不产生停止点，
            // 否则会被夹到 0，角色原地“等灯”永远不出发
            return pedStop > 1 ? pedStop : null;
        }

        var projection = ProjectOntoPath(points, CrosswalkCenter(crosswalk));
        if (projection is null)
        {
            return null;
        }

        // 载具停在条纹外沿：中心投影按横道厚度的一半回退，再把车身长度算进去，
        // 让车头而不是车中心对齐停止线。
        var halfThickness = crosswalk.Width * 0.5;
        var stop = projection.Value - halfThickness - gap - bodyHalf;
        return stop > 1 ? stop : null;
    }

    private static double? ProjectOntoPath(IReadOnlyList<MapPathPointData> points, Point target)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var best = double.MaxValue;
        double bestDistance = 0;
        var walked = 0.0;
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = new Point(points[index].X, points[index].Y);
            var to = new Point(points[index + 1].X, points[index + 1].Y);
            var segment = PathMath.Distance(from, to);
            if (segment <= 0)
            {
                continue;
            }

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var t = Math.Clamp(((target.X - from.X) * dx + (target.Y - from.Y) * dy) / (segment * segment), 0, 1);
            var foot = new Point(from.X + dx * t, from.Y + dy * t);
            var distance = PathMath.Distance(foot, target);
            if (distance < best)
            {
                best = distance;
                bestDistance = walked + segment * t;
            }

            walked += segment;
        }

        return best == double.MaxValue ? null : bestDistance;
    }

    private static double MinDistanceToPath(IReadOnlyList<MapPathPointData> points, Point target)
    {
        if (points.Count == 0)
        {
            return double.MaxValue;
        }

        var best = double.MaxValue;
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = new Point(points[index].X, points[index].Y);
            var to = new Point(points[index + 1].X, points[index + 1].Y);
            var segment = PathMath.Distance(from, to);
            if (segment <= 0)
            {
                continue;
            }

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var t = Math.Clamp(((target.X - from.X) * dx + (target.Y - from.Y) * dy) / (segment * segment), 0, 1);
            var foot = new Point(from.X + dx * t, from.Y + dy * t);
            best = Math.Min(best, PathMath.Distance(foot, target));
        }

        return best;
    }

    #endregion

    #region 相位

    /// <summary>查询某盏灯在 <see cref="Elapsed"/> 时刻的相位；未登记的灯按绿灯处理。</summary>
    /// <param name="lightId">灯 ID。</param>
    /// <returns>相位。</returns>
    public TrafficPhase PhaseOf(string lightId)
    {
        if (!_lights.TryGetValue(lightId, out var light))
        {
            return TrafficPhase.Green;
        }

        var (phase, _) = Evaluate(light, Elapsed);
        return phase;
    }

    /// <summary>当前相位还剩多少秒。</summary>
    /// <param name="lightId">灯 ID。</param>
    /// <returns>剩余秒数；灯不存在返回 0。</returns>
    public double PhaseRemaining(string lightId)
    {
        if (!_lights.TryGetValue(lightId, out var light))
        {
            return 0;
        }

        var (_, remaining) = Evaluate(light, Elapsed);
        return remaining;
    }

    private static (TrafficPhase Phase, double Remaining) Evaluate(MapTrafficLightData light, double elapsed)
    {
        var red = Math.Max(0.05, light.RedDuration);
        var green = Math.Max(0.05, light.GreenDuration);
        var yellow = Math.Max(0, light.YellowDuration);
        var cycle = red + green + yellow;
        if (cycle <= 0)
        {
            return (PhaseFromName(light.InitialPhase), 0);
        }

        // 与 Unity 侧 TrafficManager:GetLightPhase 完全同口径：
        // t = (elapsed + OffsetPhase) % cycle，顺序红 → 绿 → 黄。
        // InitialPhase 只在周期为 0 时兜底，不参与正常相位推进。
        var t = (elapsed + light.OffsetPhase) % cycle;
        if (t < 0)
        {
            t += cycle;
        }

        if (t < red)
        {
            return (TrafficPhase.Red, red - t);
        }

        if (t < red + green)
        {
            return (TrafficPhase.Green, red + green - t);
        }

        return (TrafficPhase.Yellow, cycle - t);
    }

    /// <summary>把数据里的相位名转成枚举；无法识别时按红灯处理。</summary>
    /// <param name="name">red / green / yellow。</param>
    /// <returns>相位。</returns>
    public static TrafficPhase PhaseFromName(string? name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "green" => TrafficPhase.Green,
            "yellow" => TrafficPhase.Yellow,
            _ => TrafficPhase.Red,
        };

    private bool IsBlocking(TrafficStopMark stop, TrafficAgent agent)
    {
        if (stop.LightIds.Count == 0)
        {
            return false;
        }

        if (agent.IsVehicle)
        {
            foreach (var lightId in stop.LightIds)
            {
                var phase = PhaseOf(lightId);
                if (phase == TrafficPhase.Red)
                {
                    return true;
                }

                if (phase == TrafficPhase.Yellow && (agent.Source?.YellowTreatAsRed ?? true))
                {
                    return true;
                }
            }

            return false;
        }

        // 行人：绑定灯必须全绿才走（与 CharacterBase.StartPathMove 的 blockPredicate 同口径）
        foreach (var lightId in stop.LightIds)
        {
            if (PhaseOf(lightId) != TrafficPhase.Green)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 推进与查询

    /// <summary>推进仿真。</summary>
    /// <param name="deltaSeconds">步长（秒），内部按 <see cref="TrafficSimDefaults.MaxStep"/> 截断。</param>
    public void Advance(double deltaSeconds)
    {
        var dt = Math.Min(Math.Max(0, deltaSeconds), TrafficSimDefaults.MaxStep);
        if (dt <= 0)
        {
            return;
        }

        Elapsed += dt;
        foreach (var agent in _agents)
        {
            agent.Advance(dt, IsBlocking);
        }

        ApplyVehicleFollowing();
    }

    /// <summary>
    /// 同车道载具排队：路径指纹一致、同向循环的载具按里程排序，
    /// 后车与前车不足 <see cref="TrafficSimDefaults.VehicleFollowGap"/> 时回退到跟车间距。
    /// 只作用于载具，行人允许并肩；单程 / 往返不做回退，避免把人从终点拽回来。
    /// </summary>
    private void ApplyVehicleFollowing()
    {
        foreach (var lane in _agents
                     .Where(agent => agent.IsVehicle
                                     && agent.Mode == PathPlaybackMode.Loop
                                     && agent.Total > 1
                                     && agent.PathKey.Length > 0)
                     .GroupBy(agent => agent.PathKey, StringComparer.Ordinal))
        {
            var cars = lane.ToList();
            if (cars.Count < 2)
            {
                continue;
            }

            cars.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            var maxHalf = cars.Max(car => car.BodyHalfLength);
            var gap = Math.Min(
                Math.Max(TrafficSimDefaults.VehicleFollowGap, maxHalf * 2 + 6),
                cars[0].Total * 0.25);

            // 从队首往队尾压：后车不足间距就退到「前车 − gap」，
            // 三辆以上叠在一起时也能依次拉开，而不是两两互相推。
            for (var i = cars.Count - 2; i >= 0; i--)
            {
                var follower = cars[i];
                var leader = cars[i + 1];
                if (leader.Distance - follower.Distance >= gap)
                {
                    continue;
                }

                follower.Distance = PathMath.NormalizeDistance(
                    leader.Distance - gap, follower.Total, follower.Mode);
                follower.Speed = 0;
            }

            // 循环路首尾相接处：队尾（里程最大）与队首（里程最小）只差一圈，同样要留间距
            var seamGap = cars[0].Distance - cars[^1].Distance + cars[0].Total;
            if (seamGap > 0 && seamGap < gap)
            {
                cars[^1].Distance = PathMath.NormalizeDistance(
                    cars[0].Distance - gap + cars[0].Total, cars[^1].Total, cars[^1].Mode);
                cars[^1].Speed = 0;
            }
        }
    }

    /// <summary>按物件 ID 取仿真角色；不在仿真里返回 null。</summary>
    /// <param name="objectId">物件 ID。</param>
    /// <returns>角色。</returns>
    public TrafficAgent? AgentOf(string objectId) =>
        string.IsNullOrEmpty(objectId) ? null : _agentsById.GetValueOrDefault(objectId);

    /// <summary>按物件 ID 取当前脚底位置；不在仿真里返回 null（调用方应回退到匀速预览）。</summary>
    /// <param name="objectId">物件 ID。</param>
    /// <returns>脚底世界坐标。</returns>
    public Point? PositionOf(string objectId)
    {
        var agent = AgentOf(objectId);
        return agent is null ? null : agent.Position;
    }

    /// <summary>重置时间与所有角色。</summary>
    public void Reset()
    {
        Elapsed = 0;
        foreach (var agent in _agents)
        {
            agent.Reset();
        }
    }

    /// <summary>状态摘要：各灯相位 + 等待角色数。</summary>
    /// <returns>供 HUD 显示的一行文本。</returns>
    public string StatusSummary()
    {
        if (!HasTrafficData)
        {
            return "本关没有红绿灯 / 横道数据，角色按匀速移动";
        }

        var lights = _config.TrafficLights;
        var lightText = lights.Count == 0
            ? "无灯"
            : string.Join("｜", lights.Select(light =>
                $"{ShortName(light.Name, light.Id)} {PhaseText(PhaseOf(light.Id))} {PhaseRemaining(light.Id):0.0}s"));

        var waitingVehicles = _agents.Count(agent => agent.Waiting && agent.IsVehicle);
        var waitingPeople = _agents.Count(agent => agent.Waiting && !agent.IsVehicle);
        var moving = _agents.Count(agent => !agent.Waiting && agent.Speed > 0.01);
        return $"{lightText}｜等待 车{waitingVehicles}/人{waitingPeople}｜移动 {moving}";
    }

    /// <summary>相位中文名。</summary>
    /// <param name="phase">相位。</param>
    /// <returns>红 / 黄 / 绿。</returns>
    public static string PhaseText(TrafficPhase phase) => phase switch
    {
        TrafficPhase.Red => "红",
        TrafficPhase.Yellow => "黄",
        _ => "绿",
    };

    private static string ShortName(string? name, string fallbackId) =>
        string.IsNullOrWhiteSpace(name) ? fallbackId : name;

    #endregion
}

#endregion

