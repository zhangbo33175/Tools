/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则与迷雾编辑的数据模型
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Models;

#region 交通规则

/// <summary>
/// 交通类别的键值口径，对应 Unity 版 TemplateTrafficCategory。
/// </summary>
public static class TrafficCategories
{
    public const string None = "none";
    public const string StreetLight = "streetlight";
    public const string Vehicle = "vehicle";
    public const string Pedestrian = "pedestrian";
    public const string Road = "road";
    public const string Crosswalk = "crosswalk";
}

/// <summary>
/// 横道 / 灯朝向的序列化口径，避免在模型、导入器和标记服务里各写一份字符串。
/// </summary>
public static class TrafficDirections
{
    public const string Horizontal = "horizontal";
    public const string Vertical = "vertical";
}

/// <summary>
/// 红绿灯默认时序与初始状态。模型构造与 JSON 读取共用同一组值，
/// 避免缺字段导入时与新建对象出现两套默认口径。
/// </summary>
public static class TrafficLightDefaults
{
    /// <summary>默认红灯时长（秒）。</summary>
    public const double RedDuration = 5.0;

    /// <summary>默认绿灯时长（秒）。</summary>
    public const double GreenDuration = 5.0;

    /// <summary>默认黄灯时长（秒）。</summary>
    public const double YellowDuration = 1.0;

    /// <summary>默认相位偏移（秒）。</summary>
    public const double OffsetPhase = 0.0;

    /// <summary>默认对向端位。</summary>
    public const string Side = "north";

    /// <summary>默认初始相位。</summary>
    public const string InitialPhase = "red";
}

/// <summary>
/// 交通标记默认模板 ID。
/// </summary>
public static class TrafficTemplateIds
{
    public const string Light = "traffic_light";
}

/// <summary>
/// 交通角色（实例侧）：行人 / 载具 / 不参与。对应 Unity 版 TrafficRole。
/// </summary>
public static class TrafficRoles
{
    public const string Pedestrian = "pedestrian";
    public const string Vehicle = "vehicle";
    public const string Inactive = "none";

    /// <summary>交通角色编码 → 下拉显示名。</summary>
    /// <param name="code">角色编码。</param>
    /// <returns>显示名。</returns>
    public static string ToDisplay(string code) => code switch
    {
        Vehicle => "载具",
        Inactive => "不参与",
        _ => "行人",
    };

    /// <summary>下拉显示名 → 交通角色编码。</summary>
    /// <param name="display">显示名。</param>
    /// <returns>角色编码。</returns>
    public static string ToCode(string display) => display switch
    {
        "载具" => Vehicle,
        "不参与" => Inactive,
        _ => Pedestrian,
    };

    /// <summary>交通角色下拉的全部显示名。</summary>
    public static IReadOnlyList<string> DisplayChoices { get; } = new[] { "行人", "载具", "不参与" };
}

/// <summary>
/// 红绿灯数据，对应 Unity 版 MapTrafficLightData。
/// </summary>
public sealed class MapTrafficLightData
{
    /// <summary>实例 ID，8 位。</summary>
    public string Id { get; set; } = IdGenerator.NewShortId();

    /// <summary>模板 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>灯体位置 X（世界像素）。</summary>
    public double PosX { get; set; }

    /// <summary>灯体位置 Y（世界像素）。</summary>
    public double PosY { get; set; }

    /// <summary>格子 X。</summary>
    public int GridX => (int)Math.Floor(PosX / MapToolDefaults.GridSize);

    /// <summary>格子 Y。</summary>
    public int GridY => (int)Math.Floor(PosY / MapToolDefaults.GridSize);

    /// <summary>管控横道朝向：horizontal / vertical。</summary>
    public string Direction { get; set; } = TrafficDirections.Horizontal;

    /// <summary>对向端位：north / south / east / west。</summary>
    public string Side { get; set; } = TrafficLightDefaults.Side;

    /// <summary>路口聚合键。</summary>
    public string IntersectionId { get; set; } = string.Empty;

    /// <summary>红灯时长（秒）。</summary>
    public double RedDuration { get; set; } = TrafficLightDefaults.RedDuration;

    /// <summary>绿灯时长（秒）。</summary>
    public double GreenDuration { get; set; } = TrafficLightDefaults.GreenDuration;

    /// <summary>黄灯时长（秒）。</summary>
    public double YellowDuration { get; set; } = TrafficLightDefaults.YellowDuration;

    /// <summary>相位偏移（秒）。</summary>
    public double OffsetPhase { get; set; } = TrafficLightDefaults.OffsetPhase;

    /// <summary>绑定斑马线 ID。</summary>
    public string BoundCrosswalkId { get; set; } = string.Empty;

    /// <summary>初始相位：red / green / yellow。</summary>
    public string InitialPhase { get; set; } = TrafficLightDefaults.InitialPhase;

    /// <summary>贴图资源路径。</summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>动画名。</summary>
    public string Anim { get; set; } = "idle";
}

/// <summary>
/// 斑马线数据，对应 Unity 版 MapCrosswalkData。
/// </summary>
public sealed class MapCrosswalkData
{
    /// <summary>实例 ID，8 位。</summary>
    public string Id { get; set; } = IdGenerator.NewShortId();

    /// <summary>显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>横道起点。</summary>
    public MapPathPointData PointA { get; set; } = new();

    /// <summary>横道终点。</summary>
    public MapPathPointData PointB { get; set; } = new();

    /// <summary>横道宽度（像素）。</summary>
    public double Width { get; set; } = MapToolDefaults.GridSize;

    /// <summary>横穿方向：horizontal / vertical。</summary>
    public string Direction { get; set; } = TrafficDirections.Horizontal;

    /// <summary>绑定红绿灯 ID。</summary>
    public string BoundLightId { get; set; } = string.Empty;
}

#endregion

#region 迷雾

/// <summary>
/// 迷雾格子：按格记录浓度，便于笔刷反复叠加与擦除。
/// </summary>
public sealed class FogCellData
{
    /// <summary>格 X。</summary>
    public int X { get; set; }

    /// <summary>格 Y。</summary>
    public int Y { get; set; }

    /// <summary>浓度 0～1。</summary>
    public double Density { get; set; } = 1.0;
}

/// <summary>
/// 迷雾区域：一次笔刷绘制产生的一组格子，便于按区域整体删除。
/// </summary>
public sealed class FogRegionData
{
    /// <summary>区域 ID，8 位。</summary>
    public string Id { get; set; } = IdGenerator.NewShortId();

    /// <summary>区域名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>不透明度 0～1。</summary>
    public double Opacity { get; set; } = 0.8;

    /// <summary>笔刷大小（像素）。</summary>
    public double BrushSize { get; set; } = 100.0;

    /// <summary>该区域覆盖的格子。</summary>
    public List<FogCellData> Cells { get; set; } = new();

    /// <summary>格子数量。</summary>
    public int CellCount => Cells.Count;
}

/// <summary>
/// 迷雾文档：独立于地图 JSON 存放，避免污染 Unity 侧 MapConfigData 结构。
/// </summary>
public sealed class MapFogDocument
{
    /// <summary>关联场景 ID。</summary>
    public string SceneId { get; set; } = string.Empty;

    /// <summary>迷雾区域集合。</summary>
    public List<FogRegionData> Regions { get; set; } = new();
}

#endregion
