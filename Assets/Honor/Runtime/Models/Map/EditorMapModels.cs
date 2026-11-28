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
    public string Id { get; set; } = IdGenerator.NewShortId();

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
    /// （格子本身是菱形、行错半格），列数对应 <see cref="DiamondLattice.Cols"/>；
    /// 六边形模式按蜂窝口径，对应 <see cref="HexLattice.Cols"/>。
    /// </summary>
    public int GridCols => GridSize <= 0
        ? 0
        : DrawShapes.IsHex(DrawShape)
            ? HexLattice.Cols(Width, GridSize)
            : DrawShapes.IsDiamond(DrawShape)
                ? DiamondLattice.Cols(Width, GridSize)
                : (int)Math.Ceiling(Width / (double)GridSize);

    /// <summary>网格行数：菱形模式行距为半格、六边形模式行距为 1.5 倍外接半径。</summary>
    public int GridRows => GridSize <= 0
        ? 0
        : DrawShapes.IsHex(DrawShape)
            ? HexLattice.Rows(Height, GridSize)
            : DrawShapes.IsDiamond(DrawShape)
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

    /// <summary>
    /// 楼层表：剖面式等距场景（小屋 / 洋馆切面）用楼层把「脚底 Y 排序」切成互不干扰的平面。
    /// <para>排序键 = 遮挡带 → 楼层 → 脚底 Y：同一楼层内仍按画家算法压叠，跨楼层由楼层号保证
    /// 上层永远画在下层之后（2F 楼板压住 1F 房间的上半部分），不会和高层家具抢前后。</para>
    /// <para>旧存档没有该字段时按 <see cref="EnsureLevels"/> 补出默认三层，物件与瓦片默认落在第 0 层。</para>
    /// </summary>
    public List<MapLevelData> Levels { get; set; } = MapLevelData.CreateDefaults();

    /// <summary>
    /// 房间表：剖面场景里「进屋前墙 / 屋顶 / 上层楼板要消失」的那一份触发范围与策略。
    /// <para>物件通过 <see cref="PlacedObject.RoomId"/> 挂到房间上，房间自己只管范围与开关；
    /// 旧存档没有该字段时为空表，所有物件都按「独立件」绘制，行为与之前一致。</para>
    /// </summary>
    public List<MapRoomData> Rooms { get; set; } = new();

    /// <summary>当前需要的楼层数（楼层表长度与所有内容的最大楼层号取大者）。</summary>
    public int LevelCount
    {
        get
        {
            var max = Levels.Count;
            foreach (var item in Objects)
            {
                max = Math.Max(max, item.Level + 1);
            }

            foreach (var cell in TileCells)
            {
                max = Math.Max(max, cell.Level + 1);
            }

            return Math.Max(1, max);
        }
    }

    /// <summary>补齐楼层表：内容楼层号超过表长时按「2F / 3F …」追加，保证 UI 与渲染取得到名字。</summary>
    public void EnsureLevels()
    {
        var count = LevelCount;
        while (Levels.Count < count)
        {
            var index = Levels.Count;
            Levels.Add(new MapLevelData
            {
                Index = index,
                Name = MapLevelData.DefaultName(index),
                Visible = true,
                Elevation = MapLevelData.DefaultElevationOf(index),
            });
        }

        foreach (var level in Levels)
        {
            if (string.IsNullOrWhiteSpace(level.Name))
            {
                level.Name = MapLevelData.DefaultName(level.Index);
            }

            // 旧图没有层高字段（读成 -1）：按楼层号补默认值，否则 2F 会和 1F 完全叠在一起
            if (level.Elevation < 0)
            {
                level.Elevation = MapLevelData.DefaultElevationOf(level.Index);
            }
        }
    }

    /// <summary>取某一层的像素抬升；表里缺这条时按楼层号推默认值。</summary>
    /// <param name="index">楼层号（0 起）。</param>
    /// <returns>抬升像素。</returns>
    public int LevelElevation(int index)
    {
        foreach (var level in Levels)
        {
            if (level.Index == index)
            {
                return MapLevelData.ClampElevation(level.Elevation);
            }
        }

        return MapLevelData.ClampElevation(MapLevelData.DefaultElevationOf(index));
    }

    /// <summary>改某一层的像素抬升（0 层恒为 0，剖面关系以地面为基准）。</summary>
    /// <param name="index">楼层号（0 起）。</param>
    /// <param name="elevation">抬升像素，内部夹到合法区间。</param>
    /// <returns>实际生效的抬升像素；楼层不存在时返回 -1。</returns>
    public int SetLevelElevation(int index, int elevation)
    {
        foreach (var level in Levels)
        {
            if (level.Index != index)
            {
                continue;
            }

            level.Elevation = index == 0 ? 0 : MapLevelData.ClampElevation(elevation);
            return level.Elevation;
        }

        return -1;
    }

    /// <summary>
    /// 在最高处追加一层：楼层号取当前表长，名字按「N F」自动生成。
    /// <para>只增不减地往高处加，是因为楼层号同时是物件 / 瓦片的归属键：
    /// 中间插层要让上面所有内容整体挪号，编辑到一半时风险远大于收益。</para>
    /// </summary>
    /// <returns>新楼层的楼层号；已达 <see cref="MapLevelData.MaxCount"/> 上限时返回 -1。</returns>
    public int AddTopLevel()
    {
        if (Levels.Count >= MapLevelData.MaxCount)
        {
            return -1;
        }

        var index = Levels.Count;
        Levels.Add(new MapLevelData
        {
            Index = index,
            Name = MapLevelData.DefaultName(index),
            Visible = true,
            Elevation = MapLevelData.DefaultElevationOf(index),
        });

        return index;
    }

    /// <summary>
    /// 删掉最高层（调用方先确认该层没有内容）。
    /// <para>只允许删顶层：删中间层会让上层内容全部错位一层，等于偷偷改了用户的数据。</para>
    /// </summary>
    /// <returns>被删掉的楼层号；只剩一层或楼层号不是顶层时返回 -1。</returns>
    public int RemoveTopLevel()
    {
        if (Levels.Count <= 1)
        {
            return -1;
        }

        var top = Levels.Count - 1;
        Levels.RemoveAt(top);
        return top;
    }

    /// <summary>某一层上的物件数（删除前用来判断能不能直接删）。</summary>
    /// <param name="level">楼层号。</param>
    /// <returns>物件数量。</returns>
    public int CountObjectsAt(int level)
    {
        var count = 0;
        foreach (var item in Objects)
        {
            if (item.Level == level)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>某一层上的瓦片格数（删除前用来判断能不能直接删）。</summary>
    /// <param name="level">楼层号。</param>
    /// <returns>瓦片格数量。</returns>
    public int CountTilesAt(int level)
    {
        var count = 0;
        foreach (var cell in TileCells)
        {
            if (cell.Level == level)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>取楼层名；越界时回落到「N F」。
    /// <para>名字同时承担「图层别名」的职责：Unity 侧按名字找楼层根节点。</para></summary>
    /// <param name="index">楼层号（0 起）。</param>
    /// <returns>楼层显示名。</returns>
    public string LevelName(int index)
    {
        foreach (var level in Levels)
        {
            if (level.Index == index && !string.IsNullOrWhiteSpace(level.Name))
            {
                return level.Name;
            }
        }

        return MapLevelData.DefaultName(index);
    }

    #region 房间

    /// <summary>按 ID 找房间；空 ID 或找不到时返回 null。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>房间数据。</returns>
    public MapRoomData? FindRoom(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        foreach (var room in Rooms)
        {
            if (string.Equals(room.Id, roomId, StringComparison.OrdinalIgnoreCase))
            {
                return room;
            }
        }

        return null;
    }

    /// <summary>房间显示名：找不到时返回「（未归房）」。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>显示名。</returns>
    public string RoomName(string? roomId) => FindRoom(roomId)?.DisplayName ?? string.Empty;

    /// <summary>新房间 ID：<c>r_</c> + 6 位数字，全图不撞号。</summary>
    /// <returns>可用 ID。</returns>
    public string NextRoomId()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in Rooms)
        {
            if (!string.IsNullOrWhiteSpace(room.Id))
            {
                used.Add(room.Id);
            }
        }

        string id;
        do
        {
            id = "r_" + IdGenerator.NewNumericId()[..6];
        }
        while (!used.Add(id));

        return id;
    }

    /// <summary>新建一个房间并加入房间表（范围由调用方给，内部夹成合法值）。</summary>
    /// <param name="name">房间名。</param>
    /// <param name="level">所属楼层。</param>
    /// <param name="cellX">左上角格 X。</param>
    /// <param name="cellY">左上角格 Y。</param>
    /// <param name="cellWidth">占格宽。</param>
    /// <param name="cellHeight">占格高。</param>
    /// <returns>新建的房间。</returns>
    public MapRoomData AddRoom(string name, int level, int cellX, int cellY, int cellWidth, int cellHeight)
    {
        var room = new MapRoomData
        {
            Id = NextRoomId(),
            Name = string.IsNullOrWhiteSpace(name) ? "新房间" : name.Trim(),
            Level = level,
            CellX = cellX,
            CellY = cellY,
            CellWidth = cellWidth,
            CellHeight = cellHeight,
        };
        room.Normalize();
        Rooms.Add(room);
        return room;
    }

    /// <summary>删掉一个房间，并把挂靠它的物件退回「独立件」。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>被删掉的房间；不存在时返回 null。</returns>
    public MapRoomData? RemoveRoom(string roomId)
    {
        var room = FindRoom(roomId);
        if (room is null)
        {
            return null;
        }

        Rooms.Remove(room);
        foreach (var item in Objects)
        {
            if (string.Equals(item.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                item.RoomId = string.Empty;
            }
        }

        return room;
    }

    /// <summary>该房间挂了多少个物件（删除确认与状态提示用）。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>物件数量。</returns>
    public int CountObjectsInRoom(string roomId)
    {
        var count = 0;
        foreach (var item in Objects)
        {
            if (string.Equals(item.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    #endregion

    /// <summary>取某一层的瓦片贴图（带楼层过滤）。</summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="level">楼层号。</param>
    /// <returns>贴图路径；该格没有瓦片返回 null。</returns>
    public string? TileAt(int x, int y, int level) => TileLayerOps.Find(TileCells, x, y, level)?.Image;

    /// <summary>地表瓦片格集合：一格一张图，绘制在所属楼层的所有物体之下。</summary>
    public List<TileCellData> TileCells { get; set; } = new();

    /// <summary>瓦片邻接规则集合（Unity Rule Tile 口径），用于一键按邻居重选贴图。</summary>
    public List<TileRuleData> TileRules { get; set; } = new();

    /// <summary>取第 0 层某一格的瓦片贴图（兼容旧调用口径）。</summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <returns>贴图路径；该格没有瓦片返回 null。</returns>
    public string? TileAt(int x, int y) => TileAt(x, y, 0);

    /// <summary>该格（第 0 层）是否铺了瓦片。</summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <returns>有瓦片为 true。</returns>
    public bool HasTile(int x, int y) => HasTile(x, y, 0);

    /// <summary>该格在指定楼层是否铺了瓦片。</summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="level">楼层号。</param>
    /// <returns>有瓦片为 true。</returns>
    public bool HasTile(int x, int y, int level) => TileLayerOps.Find(TileCells, x, y, level) is not null;

    /// <summary>加入一个实例，并保证它的 8 位数字 ID 不与现有物件撞号。</summary>
    /// <param name="instance">待加入的实例；ID 为空或撞号时会被重新生成。</param>
    /// <returns>已加入集合的同一个实例。</returns>
    public PlacedObject AddObject(PlacedObject instance)
    {
        var used = new HashSet<string>();
        foreach (var item in Objects)
        {
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                used.Add(item.Id);
            }
        }

        while (string.IsNullOrWhiteSpace(instance.Id) || !used.Add(instance.Id))
        {
            instance.Id = IdGenerator.NewNumericId();
        }

        Objects.Add(instance);
        return instance;
    }

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

