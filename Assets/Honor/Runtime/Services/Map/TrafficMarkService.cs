/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficMarkService.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通标记数据服务：人行道/道路区域、横道、红绿灯的增删、
 *            反向绑定与物件变化后的同步，全部只操作地图数据，不依赖窗口 UI。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>创建横道后的绑定结果，用于窗口层组织提示文案。</summary>
/// <param name="Crosswalk">新建的横道。</param>
/// <param name="NearestLight">附近的灯；没有则为 null。</param>
/// <param name="Bound">是否成功与灯建立双向绑定。</param>
/// <param name="ClearedOtherMarks">创建前是否清掉了该物件的其他交通标记。</param>
public readonly record struct CrosswalkMarkResult(
    MapCrosswalkData Crosswalk,
    MapTrafficLightData? NearestLight,
    bool Bound,
    bool ClearedOtherMarks);

/// <summary>交通标记种类，用于窗口层统一查询按钮状态，不直接读地图集合。</summary>
public enum TrafficMarkKind
{
    Sidewalk,
    Road,
    Crosswalk,
    Light,
}

/// <summary>创建红绿灯后的绑定结果，用于窗口层组织提示文案。</summary>
/// <param name="Light">新建的灯。</param>
/// <param name="NearestCrosswalk">附近的横道；没有则为 null。</param>
/// <param name="Bound">是否成功与横道建立双向绑定。</param>
/// <param name="ClearedOtherMarks">创建前是否清掉了该物件的其他交通标记。</param>
public readonly record struct LightMarkResult(
    MapTrafficLightData Light,
    MapCrosswalkData? NearestCrosswalk,
    bool Bound,
    bool ClearedOtherMarks);

/// <summary>
/// 交通标记的地图数据操作。所有方法只接收 <see cref="MapConfigData"/> 与物件，
/// 不读取窗口字段、不发消息，便于画布、导入和批处理复用。
/// </summary>
public static class TrafficMarkService
{
    /// <summary>自动绑定搜索半径：6 个标准格。</summary>
    private const double BindRadiusInGrids = 6.0;

    /// <summary>矩形同步比较容差。</summary>
    private const double NearTolerance = 0.001;

    /// <summary>横道轴与包围盒等价判断的半像素容差（缩放后包围盒可能带小数）。</summary>
    private const double GeometryTolerance = 0.5;

    #region 区域

    /// <summary>查询某个物件是否已带指定交通标记；空 Id 一律返回 false。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="objectId">物件 Id。</param>
    /// <param name="kind">标记种类。</param>
    /// <returns>true = 已标记。</returns>
    public static bool IsMarked(MapConfigData config, string? objectId, TrafficMarkKind kind)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        return kind switch
        {
            TrafficMarkKind.Sidewalk => config.Regions.Any(region =>
                region.Id == objectId && region.Type == RegionTypes.Sidewalk),
            TrafficMarkKind.Road => config.Regions.Any(region =>
                region.Id == objectId && region.Type == RegionTypes.Road),
            TrafficMarkKind.Crosswalk => config.Crosswalks.Any(crosswalk => crosswalk.Id == objectId),
            TrafficMarkKind.Light => config.TrafficLights.Any(light => light.Id == objectId),
            _ => false,
        };
    }

    /// <summary>把物件包围盒换算成格坐标矩形（左闭右开换算成闭区间格数）。</summary>
    /// <param name="config">地图配置，提供格宽。</param>
    /// <param name="item">地图物件。</param>
    /// <returns>格坐标矩形，单位为格。</returns>
    public static Rect BuildObjectCellRect(MapConfigData config, PlacedObject item)
    {
        var grid = Math.Max(1, config.GridSize);
        var bounds = item.Bounds;
        var left = (int)Math.Floor(bounds.Left / grid);
        var top = (int)Math.Floor(bounds.Top / grid);
        var right = (int)Math.Ceiling(bounds.Right / grid) - 1;
        var bottom = (int)Math.Ceiling(bounds.Bottom / grid) - 1;
        right = Math.Max(left, right);
        bottom = Math.Max(top, bottom);
        return new Rect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>写入一条物件区域标记，并把矩形覆盖的格子补进人行道 / 道路集合。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="item">物件事例。</param>
    /// <param name="type">区域类型。</param>
    /// <param name="cellRect">格坐标矩形。</param>
    /// <returns>本次新增的格子数量。</returns>
    public static int AddAreaMark(MapConfigData config, PlacedObject item, string type, Rect cellRect)
    {
        config.Regions.Add(new MapRegionData
        {
            Id = item.Id,
            Type = type,
            X = cellRect.X,
            Y = cellRect.Y,
            Width = cellRect.Width,
            Height = cellRect.Height,
        });

        return AddAreaCells(config, type, cellRect);
    }

    /// <summary>把矩形内的格子补进对应集合，已有格子不重复添加。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="type">区域类型。</param>
    /// <param name="cellRect">格坐标矩形。</param>
    /// <returns>新增格子数量。</returns>
    public static int AddAreaCells(MapConfigData config, string type, Rect cellRect)
    {
        var target = CellsFor(config, type);
        var added = 0;
        var right = (int)(cellRect.X + cellRect.Width);
        var bottom = (int)(cellRect.Y + cellRect.Height);

        for (var y = (int)cellRect.Y; y < bottom; y++)
        {
            for (var x = (int)cellRect.X; x < right; x++)
            {
                if (target.Any(cell => cell.X == x && cell.Y == y))
                {
                    continue;
                }

                target.Add(new MapGridCellData { X = x, Y = y });
                added++;
            }
        }

        return added;
    }

    /// <summary>按物件 Id 删除该物件的一条区域标记，并清掉不再被任何同类型区域覆盖的格子。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="objectId">物件 Id。</param>
    /// <param name="type">区域类型。</param>
    /// <returns>确实删掉了区域时返回 true。</returns>
    public static bool RemoveObjectArea(MapConfigData config, string objectId, string type)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        var removed = false;
        foreach (var region in config.Regions
                     .Where(region => region.Id == objectId && region.Type == type)
                     .ToList())
        {
            config.Regions.Remove(region);
            RemoveAreaCells(config, new Rect(region.X, region.Y, region.Width, region.Height), type);
            removed = true;
        }

        return removed;
    }

    /// <summary>删除矩形内、且不再被同类型区域覆盖的格子。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="cellRect">格坐标矩形。</param>
    /// <param name="type">区域类型。</param>
    public static void RemoveAreaCells(MapConfigData config, Rect cellRect, string type)
    {
        var target = CellsFor(config, type);
        var right = (int)(cellRect.X + cellRect.Width);
        var bottom = (int)(cellRect.Y + cellRect.Height);

        for (var y = (int)cellRect.Y; y < bottom; y++)
        {
            for (var x = (int)cellRect.X; x < right; x++)
            {
                if (config.Regions.Any(region =>
                        region.Type == type &&
                        x >= region.X && x < region.X + region.Width &&
                        y >= region.Y && y < region.Y + region.Height))
                {
                    continue;
                }

                target.RemoveAll(cell => cell.X == x && cell.Y == y);
            }
        }
    }

    /// <summary>
    /// 物件变化后的交通标记统一同步入口。
    /// <para>顺序固定：先同步人行道/道路区域，再同步横道几何，最后同步灯与横道的绑定关系，避免后者读到旧几何。</para>
    /// </summary>
    /// <param name="config">地图配置。</param>
    public static void SyncObjectMarks(MapConfigData config)
    {
        SyncObjectAreas(config);
        SyncObjectCrosswalks(config);
        SyncObjectLights(config);
    }

    /// <summary>物件被移动/缩放后，把以它为范围的人行道 / 道路区域同步到最新包围盒。</summary>
    /// <param name="config">地图配置。</param>
    public static void SyncObjectAreas(MapConfigData config)
    {
        if (config.Regions.Count == 0 || config.Objects.Count == 0)
        {
            return;
        }

        var objects = BuildObjectLookup(config);
        foreach (var region in config.Regions.ToList())
        {
            if (region.Type is not (RegionTypes.Sidewalk or RegionTypes.Road) ||
                !objects.TryGetValue(region.Id, out var instance))
            {
                continue;
            }

            var cellRect = BuildObjectCellRect(config, instance);
            if (Near(region.X, cellRect.X) &&
                Near(region.Y, cellRect.Y) &&
                Near(region.Width, cellRect.Width) &&
                Near(region.Height, cellRect.Height))
            {
                continue;
            }

            var oldRect = new Rect(region.X, region.Y, region.Width, region.Height);
            region.X = cellRect.X;
            region.Y = cellRect.Y;
            region.Width = cellRect.Width;
            region.Height = cellRect.Height;
            RemoveAreaCells(config, oldRect, region.Type);
            AddAreaCells(config, region.Type, cellRect);
        }
    }

    #endregion

    #region 横道与灯

    /// <summary>
    /// 把物件标记为横道。起终点取包围盒长边、宽度取短边；长边水平时方向为 horizontal。
    /// 创建前会清掉该物件身上的其他交通标记，并在附近有未绑定灯时自动双向绑定。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="item">被标记的物件。</param>
    /// <returns>横道与绑定结果。</returns>
    public static CrosswalkMarkResult CreateCrosswalk(MapConfigData config, PlacedObject item)
    {
        var clearedOthers = RemoveObjectTrafficMarks(config, item.Id);
        var crosswalk = new MapCrosswalkData { Id = item.Id };
        ApplyCrosswalkGeometry(crosswalk, item);
        config.Crosswalks.Add(crosswalk);

        var center = new Point(item.Bounds.Center.X, item.Bounds.Center.Y);
        var nearestLight = FindNearestLight(config, center);
        var bound = false;
        if (nearestLight is not null &&
            string.IsNullOrEmpty(nearestLight.BoundCrosswalkId) &&
            string.IsNullOrEmpty(crosswalk.BoundLightId))
        {
            crosswalk.BoundLightId = nearestLight.Id;
            nearestLight.BoundCrosswalkId = crosswalk.Id;
            nearestLight.Direction = crosswalk.Direction;
            bound = true;
        }

        return new CrosswalkMarkResult(crosswalk, nearestLight, bound, clearedOthers);
    }

    /// <summary>
    /// 把物件标记为红绿灯。灯 Uid 直接复用物件 Id，因此一个灯体物件对应一盏逻辑灯。
    /// 创建前会清掉该物件身上的其他交通标记，并在附近有未绑定横道时自动双向绑定。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="item">被标记的物件。</param>
    /// <returns>灯与绑定结果。</returns>
    public static LightMarkResult CreateLight(MapConfigData config, PlacedObject item)
    {
        var clearedOthers = RemoveObjectTrafficMarks(config, item.Id);
        var bounds = item.Bounds;
        var light = new MapTrafficLightData
        {
            Id = item.Id,
            TemplateId = TrafficTemplateIds.Light,
            Name = item.Name,
            PosX = bounds.Center.X,
            PosY = bounds.Bottom,
            Image = item.ImagePath,
            Anim = item.AnimName,
        };

        var nearest = FindNearestCrosswalk(config, new Point(light.PosX, light.PosY));
        var bound = false;
        if (nearest is not null && string.IsNullOrEmpty(nearest.BoundLightId))
        {
            light.BoundCrosswalkId = nearest.Id;
            light.Direction = nearest.Direction;
            nearest.BoundLightId = light.Id;
            bound = true;
        }

        config.TrafficLights.Add(light);
        return new LightMarkResult(light, nearest, bound, clearedOthers);
    }

    /// <summary>按物件 Id 删除由该物件标记出的红绿灯，并清理实例与横道上的反向引用。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="objectId">被删除/取消标记的地图物件 Id。</param>
    /// <returns>确实删掉了灯记录时返回 true。</returns>
    public static bool RemoveObjectLight(MapConfigData config, string objectId)
    {
        if (!RemoveById(config.TrafficLights, objectId, item => item.Id))
        {
            return false;
        }

        foreach (var instance in config.Objects)
        {
            instance.BoundLightIds.Remove(objectId);
        }

        foreach (var crosswalk in config.Crosswalks)
        {
            if (crosswalk.BoundLightId == objectId)
            {
                crosswalk.BoundLightId = string.Empty;
            }
        }

        return true;
    }

    /// <summary>按物件 Id 删除由该物件标记出的横道，并清掉红绿灯上的反向绑定。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="objectId">物件 Id。</param>
    /// <returns>确实删掉了横道时返回 true。</returns>
    public static bool RemoveObjectCrosswalk(MapConfigData config, string objectId)
    {
        if (!RemoveById(config.Crosswalks, objectId, item => item.Id))
        {
            return false;
        }

        foreach (var light in config.TrafficLights)
        {
            if (light.BoundCrosswalkId == objectId)
            {
                light.BoundCrosswalkId = string.Empty;
            }
        }

        return true;
    }

    /// <summary>清掉某个物件身上的全部交通标记（红绿灯 / 横道 / 人行道 / 道路）。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="objectId">物件 Id。</param>
    /// <returns>确实清掉任意一种标记时返回 true。</returns>
    public static bool RemoveObjectTrafficMarks(MapConfigData config, string objectId)
    {
        var removed = false;
        removed |= RemoveObjectLight(config, objectId);
        removed |= RemoveObjectCrosswalk(config, objectId);
        removed |= RemoveObjectArea(config, objectId, RegionTypes.Sidewalk);
        removed |= RemoveObjectArea(config, objectId, RegionTypes.Road);
        return removed;
    }

    /// <summary>物件移动/缩放/改名后，把横道同步到最新包围盒并更新绑定灯的朝向。</summary>
    /// <param name="config">地图配置。</param>
    public static void SyncObjectCrosswalks(MapConfigData config)
    {
        if (config.Crosswalks.Count == 0 || config.Objects.Count == 0)
        {
            return;
        }

        var objects = BuildObjectLookup(config);
        foreach (var crosswalk in config.Crosswalks)
        {
            if (!objects.TryGetValue(crosswalk.Id, out var instance))
            {
                continue;
            }

            // 斑马线贴图多是 356×299 的横幅块：物件长宽比例会让「南北穿行」被判成「东西横穿」。
            // 只要当前 A→B 轴 + 宽度表达的矩形已经和包围盒重合，就保留人工标好的穿行轴，
            // 只同步名称；否则（物件真的被移动/缩放过）再按包围盒重算。
            if (!CrosswalkMatchesBounds(crosswalk, instance))
            {
                ApplyCrosswalkGeometry(crosswalk, instance);

                foreach (var light in config.TrafficLights)
                {
                    if (light.BoundCrosswalkId == crosswalk.Id)
                    {
                        light.Direction = crosswalk.Direction;
                    }
                }
            }

            crosswalk.Name = instance.Name;
        }
    }

    /// <summary>
    /// 判断横道的 A→B 轴与宽度表达的矩形是否已与物件包围盒重合。
    /// 重合时说明规则和美术一致，不需要按包围盒重算（重算会把穿行轴转 90°）。
    /// </summary>
    /// <param name="crosswalk">横道数据。</param>
    /// <param name="instance">横道对应的地图物件。</param>
    /// <returns>矩形已重合时返回 true。</returns>
    private static bool CrosswalkMatchesBounds(MapCrosswalkData crosswalk, PlacedObject instance)
    {
        var bounds = instance.Bounds;
        var half = Math.Max(0, crosswalk.Width) / 2.0;
        double left;
        double right;
        double top;
        double bottom;
        if (crosswalk.Direction == TrafficDirections.Vertical)
        {
            var centerX = (crosswalk.PointA.X + crosswalk.PointB.X) / 2.0;
            left = centerX - half;
            right = centerX + half;
            top = Math.Min(crosswalk.PointA.Y, crosswalk.PointB.Y);
            bottom = Math.Max(crosswalk.PointA.Y, crosswalk.PointB.Y);
        }
        else
        {
            var centerY = (crosswalk.PointA.Y + crosswalk.PointB.Y) / 2.0;
            top = centerY - half;
            bottom = centerY + half;
            left = Math.Min(crosswalk.PointA.X, crosswalk.PointB.X);
            right = Math.Max(crosswalk.PointA.X, crosswalk.PointB.X);
        }

        return Math.Abs(left - bounds.Left) < GeometryTolerance
               && Math.Abs(top - bounds.Top) < GeometryTolerance
               && Math.Abs(right - bounds.Right) < GeometryTolerance
               && Math.Abs(bottom - bounds.Bottom) < GeometryTolerance;
    }

    /// <summary>物件移动/缩放/改名后，把以它为灯体的红绿灯同步到最新脚底锚点。</summary>
    /// <param name="config">地图配置。</param>
    public static void SyncObjectLights(MapConfigData config)
    {
        if (config.TrafficLights.Count == 0 || config.Objects.Count == 0)
        {
            return;
        }

        var objects = BuildObjectLookup(config);
        foreach (var light in config.TrafficLights)
        {
            if (!objects.TryGetValue(light.Id, out var instance))
            {
                continue;
            }

            var bounds = instance.Bounds;
            light.Name = instance.Name;
            light.PosX = bounds.Center.X;
            light.PosY = bounds.Bottom;
            light.Image = instance.ImagePath;
            light.Anim = instance.AnimName;
        }
    }

    /// <summary>找距灯脚最近、且在 6 格范围内的横道；没有时返回 null。</summary>
    /// <param name="config">当前地图。</param>
    /// <param name="anchor">灯脚世界坐标。</param>
    /// <returns>最近横道。</returns>
    public static MapCrosswalkData? FindNearestCrosswalk(MapConfigData config, Point anchor)
    {
        return FindNearest(
            config.Crosswalks,
            anchor,
            crosswalk => new Point(
                (crosswalk.PointA.X + crosswalk.PointB.X) * 0.5,
                (crosswalk.PointA.Y + crosswalk.PointB.Y) * 0.5));
    }

    /// <summary>找距横道中心最近、且尚未绑定横道的红绿灯；没有时返回 null。</summary>
    /// <param name="config">当前地图。</param>
    /// <param name="anchor">横道中心世界坐标。</param>
    /// <returns>最近且空闲的灯。</returns>
    public static MapTrafficLightData? FindNearestLight(MapConfigData config, Point anchor)
    {
        return FindNearest(
            config.TrafficLights,
            anchor,
            light => new Point(light.PosX, light.PosY),
            light => string.IsNullOrEmpty(light.BoundCrosswalkId));
    }

    #endregion

    #region 内部工具

    /// <summary>
    /// 按对象 Id 从集合中删除一条记录。空 Id 直接返回 false，
    /// 避免调用方各自重复空值判断与查找逻辑。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    /// <param name="items">待删除的集合（横道或红绿灯）。</param>
    /// <param name="objectId">目标记录 Id。</param>
    /// <param name="idOf">从元素提取 Id 的委托。</param>
    /// <returns>找到并删除时返回 true。</returns>
    private static bool RemoveById<T>(List<T> items, string? objectId, Func<T, string> idOf)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        var index = items.FindIndex(item => idOf(item) == objectId);
        if (index < 0)
        {
            return false;
        }

        items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// 在绑定半径内找离锚点最近的对象。距离相同保留先遍历到的对象，
    /// 与旧实现口径一致；可选过滤用于排除已绑定的灯。
    /// </summary>
    /// <typeparam name="T">候选项类型。</typeparam>
    /// <param name="candidates">候选集合。</param>
    /// <param name="anchor">世界坐标锚点。</param>
    /// <param name="pointOf">从候选项取比较坐标的委托。</param>
    /// <param name="accept">可选的候选过滤条件。</param>
    /// <returns>最近的候选；超出绑定半径或无候选时返回 null。</returns>
    private static T? FindNearest<T>(
        IEnumerable<T> candidates,
        Point anchor,
        Func<T, Point> pointOf,
        Func<T, bool>? accept = null) where T : class
    {
        var bestDistanceSquared = MaxBindDistanceSquared();
        T? best = null;

        foreach (var candidate in candidates)
        {
            if (accept is not null && !accept(candidate))
            {
                continue;
            }

            var point = pointOf(candidate);
            var dx = anchor.X - point.X;
            var dy = anchor.Y - point.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            best = candidate;
        }

        return best;
    }

    /// <summary>按区域类型取对应的格子集合。</summary>
    private static List<MapGridCellData> CellsFor(MapConfigData config, string type) =>
        type == RegionTypes.Sidewalk ? config.SidewalkCells : config.RoadCells;

    /// <summary>按非空 Id 建物件索引，供同步方法一次遍历复用。</summary>
    private static Dictionary<string, PlacedObject> BuildObjectLookup(MapConfigData config)
    {
        var objects = new Dictionary<string, PlacedObject>(StringComparer.Ordinal);
        foreach (var instance in config.Objects)
        {
            if (!string.IsNullOrEmpty(instance.Id))
            {
                objects[instance.Id] = instance;
            }
        }

        return objects;
    }

    /// <summary>按物件包围盒长边写入横道的名称、朝向、宽度与起终点。</summary>
    /// <param name="crosswalk">待更新的横道。</param>
    /// <param name="instance">横道对应的地图物件。</param>
    private static void ApplyCrosswalkGeometry(MapCrosswalkData crosswalk, PlacedObject instance)
    {
        var bounds = instance.Bounds;
        var horizontal = bounds.Width >= bounds.Height;
        crosswalk.Name = instance.Name;
        crosswalk.Direction = horizontal ? TrafficDirections.Horizontal : TrafficDirections.Vertical;
        crosswalk.Width = horizontal ? bounds.Height : bounds.Width;
        crosswalk.PointA = horizontal
            ? new MapPathPointData(bounds.Left, bounds.Center.Y)
            : new MapPathPointData(bounds.Center.X, bounds.Top);
        crosswalk.PointB = horizontal
            ? new MapPathPointData(bounds.Right, bounds.Center.Y)
            : new MapPathPointData(bounds.Center.X, bounds.Bottom);
    }

    /// <summary>绑定搜索半径：6 个标准格。</summary>
    private static double MaxBindDistanceSquared()
    {
        var maxDistance = MapToolDefaults.GridSize * BindRadiusInGrids;
        return maxDistance * maxDistance;
    }

    /// <summary>矩形同步比较用的小容差。</summary>
    private static bool Near(double left, double right) => Math.Abs(left - right) < NearTolerance;

    #endregion
}
