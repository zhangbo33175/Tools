/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorMapModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器地图数据模型：章节、关卡、区域、交通、迷雾与地图配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MapEditorTool.Models;

#region 地图数据

/// <summary>网格占用格子。</summary>
public sealed class MapGridCellData
{
    /// <summary>列。</summary>
    public int X { get; set; }

    /// <summary>行。</summary>
    public int Y { get; set; }
}

/// <summary>路径点。</summary>
public sealed class MapPathPointData
{
    /// <summary>世界 X。</summary>
    public double X { get; set; }

    /// <summary>世界 Y。</summary>
    public double Y { get; set; }

    /// <summary>格 X。</summary>
    public int GridX => (int)Math.Floor(X / MapToolDefaults.GridSize);

    /// <summary>格 Y。</summary>
    public int GridY => (int)Math.Floor(Y / MapToolDefaults.GridSize);

    /// <summary>默认构造。</summary>
    public MapPathPointData()
    {
    }

    /// <summary>带参构造。</summary>
    /// <param name="x">世界 X。</param>
    /// <param name="y">世界 Y。</param>
    public MapPathPointData(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>矩形区域回显。</summary>
public sealed class MapRegionData
{
    /// <summary>区域 ID。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>区域类型：sidewalk / road。</summary>
    public string Type { get; set; } = "sidewalk";

    /// <summary>左上 X。</summary>
    public double X { get; set; }

    /// <summary>左上 Y。</summary>
    public double Y { get; set; }

    /// <summary>宽。</summary>
    public double Width { get; set; }

    /// <summary>高。</summary>
    public double Height { get; set; }
}

/// <summary>
/// 地图配置数据，对应保存到 docs\MapJson 的 JSON 文件。
/// </summary>
public sealed class MapConfigData
{
    /// <summary>场景 ID。</summary>
    public string SceneId { get; set; } = "DefaultLevel";

    /// <summary>场景名称。</summary>
    public string SceneName { get; set; } = "未命名场景";

    /// <summary>地图宽，像素。</summary>
    public int Width { get; set; } = MapToolDefaults.LevelPixelSize;

    /// <summary>地图高，像素。</summary>
    public int Height { get; set; } = MapToolDefaults.LevelPixelSize;

    /// <summary>章节 ID。</summary>
    public string ChapterId { get; set; } = "DefaultChapter";

    /// <summary>章节名。</summary>
    public string ChapterName { get; set; } = "默认章节";

    /// <summary>背景图资源名。</summary>
    public string BgName { get; set; } = string.Empty;

    /// <summary>格子像素尺寸。</summary>
    public int GridSize { get; set; } = MapToolDefaults.GridSize;

    /// <summary>显示层基线步长。</summary>
    public int BaselineStep { get; set; } = MapToolDefaults.LayerBaselineStep;

    /// <summary>绘制形状。</summary>
    public string DrawShape { get; set; } = DrawShapes.Rect;

    /// <summary>区域类型。</summary>
    public string RegionType { get; set; } = RegionTypes.Place;

    /// <summary>
    /// 网格列数：正交模式为「地图宽 / 格子边长」向上取整；菱形模式按 TileMap 45° 格子口径
    /// （格子本身是菱形、行错半格），列数对应 <see cref="DiamondLattice.Cols"/>。
    /// </summary>
    public int GridCols => GridSize <= 0
        ? 0
        : DrawShape == DrawShapes.Diamond
            ? DiamondLattice.Cols(Width, GridSize)
            : (int)Math.Ceiling(Width / (double)GridSize);

    /// <summary>网格行数：菱形模式行距为半格，行数约为正交模式的两倍。</summary>
    public int GridRows => GridSize <= 0
        ? 0
        : DrawShape == DrawShapes.Diamond
            ? DiamondLattice.Rows(Height, GridSize)
            : (int)Math.Ceiling(Height / (double)GridSize);

    /// <summary>全部已放置物体，含建筑/物件/人物。</summary>
    public List<PlacedObject> Objects { get; set; } = new();

    /// <summary>人行道格集合。</summary>
    public List<MapGridCellData> SidewalkCells { get; set; } = new();

    /// <summary>道路格集合。</summary>
    public List<MapGridCellData> RoadCells { get; set; } = new();

    /// <summary>区域回显集合。</summary>
    public List<MapRegionData> Regions { get; set; } = new();

    /// <summary>红绿灯集合。</summary>
    public List<MapTrafficLightData> TrafficLights { get; set; } = new();

    /// <summary>斑马线集合。</summary>
    public List<MapCrosswalkData> Crosswalks { get; set; } = new();

    /// <summary>收集物计划：目标数量、层级模式与选中的素材。</summary>
    public CollectiblePlanData Collectibles { get; set; } = new();

    /// <summary>统计指定类型的实例数量。</summary>
    /// <param name="type">物体类型。</param>
    /// <returns>数量。</returns>
    public int CountOf(MapObjectType type)
    {
        int count = 0;
        foreach (var item in Objects)
        {
            if (item.Type == type)
            {
                count++;
            }
        }

        return count;
    }
}

#endregion

