/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficRuleService.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则编辑服务：灯与横道的双向绑定、时长归一到合法区间、
 *            规则自检与角色候选筛选；不依赖任何窗口控件。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 交通规则的编辑期数据操作。与 <see cref="TrafficMarkService"/> 的分工：
/// 标记服务负责「从地图物件生成 / 取消标记」，本服务负责「已存在的规则怎么改、怎么绑定、是否可预览」。
/// </summary>
public static class TrafficRuleService
{
    /// <summary>最短相位时长（秒），避免 0 时长导致相位瞬间跳过。</summary>
    public const double MinDuration = 0.5;

    /// <summary>最长相位时长（秒），避免误输入把等待拉成几分钟。</summary>
    public const double MaxDuration = 120.0;

    /// <summary>相位偏移允许的最大绝对值（秒）。</summary>
    public const double MaxOffset = 120.0;

    #region 灯与横道绑定

    /// <summary>
    /// 把灯绑定到横道（<paramref name="crosswalkId"/> 传空串表示解绑）。
    /// <para>一个横道只挂一盏灯、一盏灯只挂一条横道，所以换绑时会把旧关系的另一端一起清掉，
    /// 避免出现「A 横道指向灯 L、灯 L 却指向 B 横道」的半绑定状态。</para>
    /// </summary>
    /// <param name="config">地图数据。</param>
    /// <param name="lightId">灯 ID。</param>
    /// <param name="crosswalkId">横道 ID；空串解绑。</param>
    /// <returns>灯或横道不存在时返回 false。</returns>
    public static bool BindLightToCrosswalk(MapConfigData config, string? lightId, string? crosswalkId)
    {
        var light = config.TrafficLights.FirstOrDefault(item => item.Id == lightId);
        if (light is null)
        {
            return false;
        }

        var target = string.IsNullOrWhiteSpace(crosswalkId)
            ? null
            : config.Crosswalks.FirstOrDefault(item => item.Id == crosswalkId);

        if (target is null)
        {
            ClearCrosswalkBackReference(config, light.BoundCrosswalkId, light.Id);
            light.BoundCrosswalkId = string.Empty;
            return true;
        }

        // 旧横道反向引用先清掉
        if (!string.Equals(light.BoundCrosswalkId, target.Id, StringComparison.Ordinal))
        {
            ClearCrosswalkBackReference(config, light.BoundCrosswalkId, light.Id);
        }

        // 目标横道原先挂的灯也要解除，保证一对一
        if (!string.IsNullOrEmpty(target.BoundLightId) &&
            !string.Equals(target.BoundLightId, light.Id, StringComparison.Ordinal) &&
            config.TrafficLights.FirstOrDefault(item => item.Id == target.BoundLightId) is { } previous)
        {
            previous.BoundCrosswalkId = string.Empty;
        }

        light.BoundCrosswalkId = target.Id;
        light.Direction = target.Direction;
        target.BoundLightId = light.Id;
        return true;
    }

    /// <summary>把横道绑定到灯，等价于 <see cref="BindLightToCrosswalk"/> 的反向写法。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="crosswalkId">横道 ID。</param>
    /// <param name="lightId">灯 ID；空串解绑。</param>
    /// <returns>横道或灯不存在时返回 false。</returns>
    public static bool BindCrosswalkToLight(MapConfigData config, string? crosswalkId, string? lightId)
    {
        var crosswalk = config.Crosswalks.FirstOrDefault(item => item.Id == crosswalkId);
        if (crosswalk is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(lightId))
        {
            if (config.TrafficLights.FirstOrDefault(item => item.Id == crosswalk.BoundLightId) is { } old)
            {
                old.BoundCrosswalkId = string.Empty;
            }

            crosswalk.BoundLightId = string.Empty;
            return true;
        }

        return BindLightToCrosswalk(config, lightId, crosswalk.Id);
    }

    /// <summary>删除一盏灯，并清掉横道与角色实例上的反向引用。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="lightId">灯 ID。</param>
    /// <returns>确实删掉时返回 true。</returns>
    public static bool RemoveLight(MapConfigData config, string? lightId)
    {
        if (string.IsNullOrWhiteSpace(lightId))
        {
            return false;
        }

        var light = config.TrafficLights.FirstOrDefault(item => item.Id == lightId);
        if (light is null)
        {
            return false;
        }

        ClearCrosswalkBackReference(config, light.BoundCrosswalkId, light.Id);
        config.TrafficLights.Remove(light);

        foreach (var instance in config.Objects)
        {
            instance.BoundLightIds.RemoveAll(id => id == lightId);
        }

        // ---TODO 一致性：灯体物件身上的红绿灯标记由 TrafficMarkService 维护，
        // 这里只删规则记录，保留物件与贴图；需要连标记一起清时走「取消红绿灯」按钮。
        return true;
    }

    /// <summary>删除一条横道，并清掉灯与角色实例上的反向引用。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="crosswalkId">横道 ID。</param>
    /// <returns>确实删掉时返回 true。</returns>
    public static bool RemoveCrosswalk(MapConfigData config, string? crosswalkId)
    {
        if (string.IsNullOrWhiteSpace(crosswalkId))
        {
            return false;
        }

        var crosswalk = config.Crosswalks.FirstOrDefault(item => item.Id == crosswalkId);
        if (crosswalk is null)
        {
            return false;
        }

        foreach (var light in config.TrafficLights)
        {
            if (string.Equals(light.BoundCrosswalkId, crosswalk.Id, StringComparison.Ordinal))
            {
                light.BoundCrosswalkId = string.Empty;
            }
        }

        config.Crosswalks.Remove(crosswalk);
        return true;
    }

    #endregion

    #region 数值归一

    /// <summary>把灯的相位时长与偏移夹到合法区间，并在路口聚合键为空时补上灯自身的分组。</summary>
    /// <param name="light">待归一的灯。</param>
    public static void NormalizeLight(MapTrafficLightData light)
    {
        light.RedDuration = ClampDuration(light.RedDuration);
        light.GreenDuration = ClampDuration(light.GreenDuration);
        light.YellowDuration = ClampDuration(light.YellowDuration);
        light.OffsetPhase = Math.Clamp(light.OffsetPhase, -MaxOffset, MaxOffset);

        if (string.IsNullOrWhiteSpace(light.InitialPhase))
        {
            light.InitialPhase = TrafficLightDefaults.InitialPhase;
        }
    }

    /// <summary>把秒数夹到 [<see cref="MinDuration"/>, <see cref="MaxDuration"/>]；NaN 退回默认红灯时长。</summary>
    /// <param name="value">原始秒数。</param>
    /// <returns>合法秒数。</returns>
    public static double ClampDuration(double value) =>
        double.IsNaN(value) ? TrafficLightDefaults.RedDuration : Math.Clamp(value, MinDuration, MaxDuration);

    #endregion

    #region 角色与自检

    /// <summary>
    /// 交通角色候选：带有效路径的物件（≥2 个路径点）。没有路径的物件即使标了载具也不会动，
    /// 放进列表只会误导，所以这里直接筛掉。
    /// </summary>
    /// <param name="config">地图数据。</param>
    /// <returns>可参与仿真的物件集合。</returns>
    public static List<PlacedObject> BuildActors(MapConfigData config) =>
        config.Objects.Where(item => item.PathPoints.Count >= 2).ToList();

    /// <summary>统计角色的停点覆盖情况，用于面板状态行与预览前的自检提示。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="actors">参与仿真的角色数量。</param>
    /// <param name="armed">确实拿到停点的角色数量。</param>
    /// <param name="waitingLights">有角色绑定的灯数量。</param>
    public static void DescribeReadiness(
        MapConfigData config,
        out int actors,
        out int armed,
        out int waitingLights)
    {
        var simulation = new TrafficSimulation(config);
        actors = simulation.Agents.Count;
        armed = simulation.Agents.Count(agent => agent.HasStops);
        waitingLights = simulation.Agents
            .SelectMany(agent => agent.Stops)
            .SelectMany(stop => stop.LightIds)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    /// <summary>规则自检：返回「还不能在预览里看到等待」的缺口描述；齐全时返回空串。</summary>
    /// <param name="config">地图数据。</param>
    /// <returns>可直接展示的提示文案。</returns>
    public static string DescribeGaps(MapConfigData config)
    {
        if (config.TrafficLights.Count == 0 && config.Crosswalks.Count == 0)
        {
            return "还没有灯和横道：先在场景编辑里把红绿灯 / 斑马线物件标记出来";
        }

        var unboundLights = config.TrafficLights.Count(light => string.IsNullOrEmpty(light.BoundCrosswalkId));
        var unboundCrosswalks = config.Crosswalks.Count(crosswalk => string.IsNullOrEmpty(crosswalk.BoundLightId));
        DescribeReadiness(config, out var actors, out var armed, out _);

        var parts = new List<string>();
        if (unboundLights > 0)
        {
            parts.Add($"{unboundLights} 盏灯未绑定横道");
        }

        if (unboundCrosswalks > 0)
        {
            parts.Add($"{unboundCrosswalks} 条横道未绑定灯");
        }

        if (actors == 0)
        {
            parts.Add("没有带路径的角色，预览里不会有人/车移动");
        }
        else if (armed < actors)
        {
            parts.Add($"{actors - armed} 个角色没有停点（未绑灯或路径未穿过横道）");
        }

        return parts.Count == 0 ? string.Empty : string.Join("；", parts);
    }

    #endregion

    #region 内部工具

    /// <summary>清掉某条横道上指向指定灯的反向引用。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="crosswalkId">横道 ID。</param>
    /// <param name="lightId">灯 ID。</param>
    private static void ClearCrosswalkBackReference(MapConfigData config, string? crosswalkId, string lightId)
    {
        if (string.IsNullOrEmpty(crosswalkId))
        {
            return;
        }

        var crosswalk = config.Crosswalks.FirstOrDefault(item => item.Id == crosswalkId);
        if (crosswalk is not null && string.Equals(crosswalk.BoundLightId, lightId, StringComparison.Ordinal))
        {
            crosswalk.BoundLightId = string.Empty;
        }
    }

    #endregion
}
