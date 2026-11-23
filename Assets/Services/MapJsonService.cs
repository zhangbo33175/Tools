/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapJsonService.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图配置导出为运行时 JSON（字段名与 Unity 版 MapConfigData 一致）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 地图配置序列化服务。输出结构与 Unity 版 MapToolModels 中的 MapConfigData 完全对齐，
/// 运行时地图加载端可直接消费。
/// </summary>
public static class MapJsonService
{
    #region 序列化选项

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #endregion

    #region 公开方法

    /// <summary>
    /// 将地图配置序列化为 JSON 文本。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <returns>格式化 JSON。</returns>
    public static string Serialize(MapConfigData config)
    {
        return JsonSerializer.Serialize(BuildPayload(config), Options);
    }

    /// <summary>
    /// 将地图配置写入指定路径，目录不存在时自动创建。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="path">目标 JSON 绝对路径。</param>
    /// <returns>实际写入路径。</returns>
    public static string Save(MapConfigData config, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serialize(config));
        return path;
    }

    /// <summary>
    /// 由场景 ID 推导默认导出文件名。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <returns>形如 Level_001.json 的文件名。</returns>
    public static string BuildFileName(MapConfigData config)
    {
        var sceneId = string.IsNullOrWhiteSpace(config.SceneId) ? "DefaultLevel" : config.SceneId.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sceneId = sceneId.Replace(invalid, '_');
        }

        return sceneId + ".json";
    }

    #endregion

    #region 组装

    private static ConfigPayload BuildPayload(MapConfigData config)
    {
        var buildings = new List<ObjectPayload>();
        var objects = new List<ObjectPayload>();
        var characters = new List<CharacterPayload>();

        foreach (var item in config.Objects)
        {
            switch (item.Type)
            {
                case MapObjectType.Building:
                    buildings.Add(BuildObject(item));
                    break;

                case MapObjectType.Character:
                    characters.Add(BuildCharacter(item));
                    break;

                default:
                    objects.Add(BuildObject(item));
                    break;
            }
        }

        return new ConfigPayload
        {
            SceneId = config.SceneId,
            SceneName = config.SceneName,
            Width = config.Width,
            Height = config.Height,
            ChapterId = config.ChapterId,
            ChapterName = config.ChapterName,
            BgName = config.BgName,
            GridSize = config.GridSize,
            BaselineStep = config.BaselineStep,
            DrawShape = config.DrawShape,
            RegionType = config.RegionType,
            GridCols = config.GridCols,
            GridRows = config.GridRows,
            OccupiedCells = BuildOccupiedCells(config),
            Buildings = buildings,
            Objects = objects,
            Characters = characters,
            TrafficLights = BuildTrafficLights(config),
            Crosswalks = BuildCrosswalks(config),
            Regions = BuildRegions(config),
            SidewalkCells = BuildCells(config.SidewalkCells),
            RoadCells = BuildCells(config.RoadCells),
            CollectibleTotal = config.Collectibles.Total,
            CollectibleLayer = CollectibleLayers.Normalize(config.Collectibles.LayerMode),
            CollectibleKinds = BuildCollectibleKinds(config),
        };
    }

    /// <summary>收集物切页数据 → JSON DTO；没有计划时输出空数组。</summary>
    /// <param name="config">地图配置。</param>
    /// <returns>DTO 列表。</returns>
    private static List<CollectibleKindPayload> BuildCollectibleKinds(MapConfigData config)
    {
        var result = new List<CollectibleKindPayload>();
        foreach (var kind in config.Collectibles.Kinds)
        {
            result.Add(new CollectibleKindPayload
            {
                TemplateId = kind.TemplateId,
                Name = kind.Name,
                ImagePath = kind.ImagePath,
                ResName = kind.ResName,
                Width = kind.Width,
                Height = kind.Height,
                LayerType = kind.LayerType,
                AnimName = kind.AnimName,
            });
        }

        return result;
    }

    private static ObjectPayload BuildObject(PlacedObject item)
    {
        return new ObjectPayload
        {
            Id = item.Id,
            TemplateId = item.TemplateId,
            Name = item.Name,
            PosX = item.X,
            PosY = item.Y,
            GridX = item.CellX,
            GridY = item.CellY,
            Width = item.ExportWidth,
            Height = item.ExportHeight,
            PixelWidth = item.PixelWidth,
            PixelHeight = item.PixelHeight,
            Stretch = item.Stretch,
            LayerType = item.LayerType,
            DisplayLayer = item.DisplayLayer,
            Band = item.BandOverride >= 0 ? item.BandOverride : null,
            Image = item.ImagePath,
            Anim = item.AnimName,
            AnimEnabled = item.AnimEnabled,
            AnimationKind = item.AnimationKind,
            SpineAsset = item.SpineAsset,
            SpineAnimation = item.SpineAnimation,
            Loop = item.Loop,
            Speed = item.Speed,
            Interactable = item.Interactable,
            InteractType = item.InteractType,
            SortOrder = item.SortOrder,
            YSortEnabled = item.YSortEnabled,
            YSortOffset = item.YSortOffset,
            Rotation = item.Rotation,
            FlipX = item.FlipX,
            Scale = item.Scale,
            ScaleX = item.Scale,
            ScaleY = item.Scale,
            CollisionEnabled = item.CollisionEnabled,
            CollisionX = item.CollisionX,
            CollisionY = item.CollisionY,
            CollisionWidth = item.CollisionWidth,
            CollisionHeight = item.CollisionHeight,
            DialogBubble = item.DialogBubble,
            ShadowEnabled = item.ShadowEnabled,
            ShadowX = item.ShadowX,
            ShadowY = item.ShadowY,
            ShadowWidth = item.ShadowWidth,
            ShadowHeight = item.ShadowHeight,
            IsCollectible = item.IsCollectible,
            CollectibleOverObjects = item.CollectibleOverObjects,
            RespawnTime = item.RespawnTime,
            BubbleId = item.BubbleId,
            IsMovable = item.IsMovable,
            LoopRestartDelay = 3.0,
            HideWhenOffMap = true,
            IsTeleport = item.IsTeleport,
            TeleportTargetScene = item.TeleportTargetScene,
            TeleportTargetX = item.TeleportTargetX,
            TeleportTargetY = item.TeleportTargetY,
            IsVehicle = item.IsVehicle,
            TrafficRole = item.TrafficRole,
            StopLineX = item.StopLineX,
            StopLineY = item.StopLineY,
            BoundLightIds = new List<string>(item.BoundLightIds),
            YellowTreatAsRed = item.YellowTreatAsRed,
        };
    }

    private static CharacterPayload BuildCharacter(PlacedObject item)
    {
        var basePayload = BuildObject(item);

        var points = new List<PathPointPayload>();
        foreach (var point in item.PathPoints)
        {
            points.Add(new PathPointPayload
            {
                X = point.X,
                Y = point.Y,
                GridX = point.GridX,
                GridY = point.GridY,
            });
        }

        return new CharacterPayload
        {
            Id = basePayload.Id,
            TemplateId = basePayload.TemplateId,
            Name = basePayload.Name,
            PosX = basePayload.PosX,
            PosY = basePayload.PosY,
            GridX = basePayload.GridX,
            GridY = basePayload.GridY,
            Width = basePayload.Width,
            Height = basePayload.Height,
            PixelWidth = basePayload.PixelWidth,
            PixelHeight = basePayload.PixelHeight,
            Stretch = basePayload.Stretch,
            LayerType = basePayload.LayerType,
            DisplayLayer = basePayload.DisplayLayer,
            Band = basePayload.Band,
            Image = basePayload.Image,
            Anim = basePayload.Anim,
            AnimEnabled = basePayload.AnimEnabled,
            AnimationKind = basePayload.AnimationKind,
            SpineAsset = basePayload.SpineAsset,
            SpineAnimation = basePayload.SpineAnimation,
            Loop = basePayload.Loop,
            Speed = basePayload.Speed,
            Interactable = basePayload.Interactable,
            InteractType = basePayload.InteractType,
            WalkMode = item.WalkMode,
            FacingDirection = item.FacingDirection,
            DialogBubble = basePayload.DialogBubble,
            SortOrder = basePayload.SortOrder,
            YSortEnabled = basePayload.YSortEnabled,
            YSortOffset = basePayload.YSortOffset,
            Rotation = basePayload.Rotation,
            FlipX = basePayload.FlipX,
            Scale = basePayload.Scale,
            ScaleX = basePayload.ScaleX,
            ScaleY = basePayload.ScaleY,
            ShadowEnabled = basePayload.ShadowEnabled,
            CollisionEnabled = basePayload.CollisionEnabled,
            CollisionX = basePayload.CollisionX,
            CollisionY = basePayload.CollisionY,
            CollisionWidth = basePayload.CollisionWidth,
            CollisionHeight = basePayload.CollisionHeight,
            ShadowX = basePayload.ShadowX,
            ShadowY = basePayload.ShadowY,
            ShadowWidth = basePayload.ShadowWidth,
            ShadowHeight = basePayload.ShadowHeight,
            PathPoints = points,
        };
    }

    /// <summary>按每个物体占格范围铺开占用格集合；菱形模式下按 45° 菱形格中心判定。</summary>
    private static List<CellPayload> BuildOccupiedCells(MapConfigData config)
    {
        var cells = new List<CellPayload>();
        var seen = new HashSet<(int, int)>();

        if (config.DrawShape == DrawShapes.Diamond)
        {
            var cell = config.GridSize;
            foreach (var item in config.Objects)
            {
                var bounds = item.Bounds;
                var topLeft = DiamondLattice.IndexAt(bounds.Left, bounds.Top, cell);
                var bottomRight = DiamondLattice.IndexAt(bounds.Right, bounds.Bottom, cell);
                var minCol = (int)Math.Min(topLeft.X, bottomRight.X) - 1;
                var maxCol = (int)Math.Max(topLeft.X, bottomRight.X) + 1;
                var minRow = (int)Math.Min(topLeft.Y, bottomRight.Y) - 1;
                var maxRow = (int)Math.Max(topLeft.Y, bottomRight.Y) + 1;

                for (var col = minCol; col <= maxCol; col++)
                {
                    for (var row = minRow; row <= maxRow; row++)
                    {
                        var center = DiamondLattice.Center(col, row, cell);
                        if (!bounds.Contains(center) || !seen.Add((col, row)))
                        {
                            continue;
                        }

                        cells.Add(new CellPayload { X = col, Y = row });
                    }
                }
            }

            return cells;
        }

        foreach (var item in config.Objects)
        {
            int startX = item.CellX;
            int startY = item.CellY;
            int endX = startX + Math.Max(1, item.ExportWidth);
            int endY = startY + Math.Max(1, item.ExportHeight);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (seen.Add((x, y)))
                    {
                        cells.Add(new CellPayload { X = x, Y = y });
                    }
                }
            }
        }

        return cells;
    }

    private static List<RegionPayload> BuildRegions(MapConfigData config)
    {
        var regions = new List<RegionPayload>();
        foreach (var region in config.Regions)
        {
            regions.Add(new RegionPayload
            {
                Id = region.Id,
                Type = region.Type,
                X = region.X,
                Y = region.Y,
                Width = region.Width,
                Height = region.Height,
            });
        }

        return regions;
    }

    private static List<CellPayload> BuildCells(IEnumerable<MapGridCellData> cells)
    {
        var result = new List<CellPayload>();
        foreach (var cell in cells)
        {
            result.Add(new CellPayload { X = cell.X, Y = cell.Y });
        }

        return result;
    }

    /// <summary>红绿灯序列化。</summary>
    private static List<TrafficLightPayload> BuildTrafficLights(MapConfigData config)
    {
        var result = new List<TrafficLightPayload>();
        foreach (var light in config.TrafficLights)
        {
            result.Add(new TrafficLightPayload
            {
                Id = light.Id,
                TemplateId = light.TemplateId,
                Name = light.Name,
                PosX = light.PosX,
                PosY = light.PosY,
                GridX = light.GridX,
                GridY = light.GridY,
                Direction = light.Direction,
                Side = light.Side,
                IntersectionId = light.IntersectionId,
                RedDuration = light.RedDuration,
                GreenDuration = light.GreenDuration,
                YellowDuration = light.YellowDuration,
                OffsetPhase = light.OffsetPhase,
                BoundCrosswalkId = light.BoundCrosswalkId,
                InitialPhase = light.InitialPhase,
                Image = light.Image,
                Anim = light.Anim,
            });
        }

        return result;
    }

    /// <summary>斑马线序列化。</summary>
    private static List<CrosswalkPayload> BuildCrosswalks(MapConfigData config)
    {
        var result = new List<CrosswalkPayload>();
        foreach (var crosswalk in config.Crosswalks)
        {
            result.Add(new CrosswalkPayload
            {
                Id = crosswalk.Id,
                Name = crosswalk.Name,
                PointA = new PathPointPayload
                {
                    X = crosswalk.PointA.X,
                    Y = crosswalk.PointA.Y,
                    GridX = crosswalk.PointA.GridX,
                    GridY = crosswalk.PointA.GridY,
                },
                PointB = new PathPointPayload
                {
                    X = crosswalk.PointB.X,
                    Y = crosswalk.PointB.Y,
                    GridX = crosswalk.PointB.GridX,
                    GridY = crosswalk.PointB.GridY,
                },
                Width = crosswalk.Width,
                Direction = crosswalk.Direction,
                BoundLightId = crosswalk.BoundLightId,
            });
        }

        return result;
    }

    #endregion

    #region DTO

    private sealed class ConfigPayload
    {
        [JsonPropertyName("sceneId")] public string SceneId { get; set; } = string.Empty;

        [JsonPropertyName("sceneName")] public string SceneName { get; set; } = string.Empty;

        [JsonPropertyName("width")] public int Width { get; set; }

        [JsonPropertyName("height")] public int Height { get; set; }

        [JsonPropertyName("chapterId")] public string ChapterId { get; set; } = string.Empty;

        [JsonPropertyName("chapterName")] public string ChapterName { get; set; } = string.Empty;

        [JsonPropertyName("bgName")] public string BgName { get; set; } = string.Empty;

        [JsonPropertyName("gridSize")] public int GridSize { get; set; }

        [JsonPropertyName("baselineStep")] public int BaselineStep { get; set; }

        [JsonPropertyName("drawShape")] public string DrawShape { get; set; } = string.Empty;

        [JsonPropertyName("regionType")] public string RegionType { get; set; } = string.Empty;

        [JsonPropertyName("gridCols")] public int GridCols { get; set; }

        [JsonPropertyName("gridRows")] public int GridRows { get; set; }

        [JsonPropertyName("occupiedCells")] public List<CellPayload> OccupiedCells { get; set; } = new();

        [JsonPropertyName("buildings")] public List<ObjectPayload> Buildings { get; set; } = new();

        [JsonPropertyName("objects")] public List<ObjectPayload> Objects { get; set; } = new();

        [JsonPropertyName("characters")] public List<CharacterPayload> Characters { get; set; } = new();

        [JsonPropertyName("trafficLights")] public List<TrafficLightPayload> TrafficLights { get; set; } = new();

        [JsonPropertyName("crosswalks")] public List<CrosswalkPayload> Crosswalks { get; set; } = new();

        [JsonPropertyName("regions")] public List<RegionPayload> Regions { get; set; } = new();

        [JsonPropertyName("sidewalkCells")] public List<CellPayload> SidewalkCells { get; set; } = new();

        [JsonPropertyName("roadCells")] public List<CellPayload> RoadCells { get; set; } = new();

        /// <summary>收集物目标总数（编辑器生成计划，运行时可用于关卡目标）。</summary>
        [JsonPropertyName("collectibleTotal")] public int CollectibleTotal { get; set; }

        /// <summary>收集物层级：under / over。</summary>
        [JsonPropertyName("collectibleLayer")] public string CollectibleLayer { get; set; } = CollectibleLayers.Default;

        /// <summary>收集物切页中选中的素材。</summary>
        [JsonPropertyName("collectibleKinds")] public List<CollectibleKindPayload> CollectibleKinds { get; set; } = new();
    }

    /// <summary>收集物素材的 JSON 形状。</summary>
    private sealed class CollectibleKindPayload
    {
        [JsonPropertyName("templateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")] public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("resName")] public string ResName { get; set; } = string.Empty;

        [JsonPropertyName("width")] public int Width { get; set; } = 1;

        [JsonPropertyName("height")] public int Height { get; set; } = 1;

        [JsonPropertyName("layerType")] public string LayerType { get; set; } = MapLayerKeys.Prop;

        [JsonPropertyName("animName")] public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;
    }

    private sealed class ObjectPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("templateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("posX")] public double PosX { get; set; }

        [JsonPropertyName("posY")] public double PosY { get; set; }

        [JsonPropertyName("gridX")] public int GridX { get; set; }

        [JsonPropertyName("gridY")] public int GridY { get; set; }

        [JsonPropertyName("width")] public int Width { get; set; }

        [JsonPropertyName("height")] public int Height { get; set; }

        [JsonPropertyName("pixelWidth")] public double PixelWidth { get; set; }

        [JsonPropertyName("pixelHeight")] public double PixelHeight { get; set; }

        [JsonPropertyName("stretch")] public bool Stretch { get; set; }

        [JsonPropertyName("layerType")] public string LayerType { get; set; } = string.Empty;

        [JsonPropertyName("displayLayer")] public string DisplayLayer { get; set; } = string.Empty;

        /// <summary>手工指定的遮挡带（0 地表 / 1 立体 / 2 叠加），null = 自动判定。</summary>
        [JsonPropertyName("band")] public int? Band { get; set; }

        [JsonPropertyName("image")] public string Image { get; set; } = string.Empty;

        [JsonPropertyName("anim")] public string Anim { get; set; } = string.Empty;

        [JsonPropertyName("animEnabled")] public bool AnimEnabled { get; set; }

        /// <summary>动画来源：sprite = 序列帧 / 静态贴图，spine = Spine 骨骼动画。</summary>
        [JsonPropertyName("animationKind")] public string AnimationKind { get; set; } = AnimationKinds.Sprite;

        /// <summary>Spine 资产名，对应 output\Config\SpineAnimations.json 的 Name。</summary>
        [JsonPropertyName("spineAsset")] public string SpineAsset { get; set; } = string.Empty;

        /// <summary>Spine 骨架动画名；空串表示资产默认动画。</summary>
        [JsonPropertyName("spineAnimation")] public string SpineAnimation { get; set; } = string.Empty;

        [JsonPropertyName("loop")] public bool Loop { get; set; }

        [JsonPropertyName("speed")] public double Speed { get; set; }

        [JsonPropertyName("interactable")] public bool Interactable { get; set; }

        [JsonPropertyName("interactType")] public string InteractType { get; set; } = string.Empty;

        [JsonPropertyName("sortOrder")] public int SortOrder { get; set; }

        [JsonPropertyName("ySortEnabled")] public bool YSortEnabled { get; set; }

        [JsonPropertyName("ySortOffset")] public double YSortOffset { get; set; }

        [JsonPropertyName("rotation")] public double Rotation { get; set; }

        [JsonPropertyName("flipX")] public bool FlipX { get; set; }

        [JsonPropertyName("scale")] public double Scale { get; set; }

        [JsonPropertyName("scaleX")] public double ScaleX { get; set; }

        [JsonPropertyName("scaleY")] public double ScaleY { get; set; }

        [JsonPropertyName("collisionEnabled")] public bool CollisionEnabled { get; set; }

        [JsonPropertyName("collisionX")] public double CollisionX { get; set; }

        [JsonPropertyName("collisionY")] public double CollisionY { get; set; }

        [JsonPropertyName("collisionWidth")] public double CollisionWidth { get; set; }

        [JsonPropertyName("collisionHeight")] public double CollisionHeight { get; set; }

        [JsonPropertyName("dialogBubble")] public string DialogBubble { get; set; } = string.Empty;

        [JsonPropertyName("shadowEnabled")] public bool ShadowEnabled { get; set; }

        [JsonPropertyName("shadowX")] public double ShadowX { get; set; }

        [JsonPropertyName("shadowY")] public double ShadowY { get; set; }

        [JsonPropertyName("shadowWidth")] public double ShadowWidth { get; set; }

        [JsonPropertyName("shadowHeight")] public double ShadowHeight { get; set; }

        [JsonPropertyName("isCollectible")] public bool IsCollectible { get; set; }

        /// <summary>收集物显示在普通物件上面（false = 在物件下面）。</summary>
        [JsonPropertyName("collectibleOverObjects")] public bool CollectibleOverObjects { get; set; } = true;

        [JsonPropertyName("respawnTime")] public double RespawnTime { get; set; }

        [JsonPropertyName("bubbleId")] public int BubbleId { get; set; }

        [JsonPropertyName("isMovable")] public bool IsMovable { get; set; }

        [JsonPropertyName("loopRestartDelay")] public double LoopRestartDelay { get; set; }

        [JsonPropertyName("hideWhenOffMap")] public bool HideWhenOffMap { get; set; }

        [JsonPropertyName("isTeleport")] public bool IsTeleport { get; set; }

        [JsonPropertyName("teleportTargetScene")] public string TeleportTargetScene { get; set; } = string.Empty;

        [JsonPropertyName("teleportTargetX")] public double TeleportTargetX { get; set; }

        [JsonPropertyName("teleportTargetY")] public double TeleportTargetY { get; set; }

        [JsonPropertyName("isVehicle")] public bool IsVehicle { get; set; }

        [JsonPropertyName("trafficRole")] public string TrafficRole { get; set; } = string.Empty;

        [JsonPropertyName("stopLineX")] public double StopLineX { get; set; }

        [JsonPropertyName("stopLineY")] public double StopLineY { get; set; }

        [JsonPropertyName("boundLightIds")] public List<string> BoundLightIds { get; set; } = new();

        [JsonPropertyName("yellowTreatAsRed")] public bool YellowTreatAsRed { get; set; }
    }

    /// <summary>红绿灯载荷。</summary>
    private sealed class TrafficLightPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("templateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("posX")] public double PosX { get; set; }

        [JsonPropertyName("posY")] public double PosY { get; set; }

        [JsonPropertyName("gridX")] public int GridX { get; set; }

        [JsonPropertyName("gridY")] public int GridY { get; set; }

        [JsonPropertyName("direction")] public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("side")] public string Side { get; set; } = string.Empty;

        [JsonPropertyName("intersectionId")] public string IntersectionId { get; set; } = string.Empty;

        [JsonPropertyName("redDuration")] public double RedDuration { get; set; }

        [JsonPropertyName("greenDuration")] public double GreenDuration { get; set; }

        [JsonPropertyName("yellowDuration")] public double YellowDuration { get; set; }

        [JsonPropertyName("offsetPhase")] public double OffsetPhase { get; set; }

        [JsonPropertyName("boundCrosswalkId")] public string BoundCrosswalkId { get; set; } = string.Empty;

        [JsonPropertyName("initialPhase")] public string InitialPhase { get; set; } = string.Empty;

        [JsonPropertyName("image")] public string Image { get; set; } = string.Empty;

        [JsonPropertyName("anim")] public string Anim { get; set; } = string.Empty;
    }

    /// <summary>斑马线载荷。</summary>
    private sealed class CrosswalkPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("pointA")] public PathPointPayload PointA { get; set; } = new();

        [JsonPropertyName("pointB")] public PathPointPayload PointB { get; set; } = new();

        [JsonPropertyName("width")] public double Width { get; set; }

        [JsonPropertyName("direction")] public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("boundLightId")] public string BoundLightId { get; set; } = string.Empty;
    }

    private sealed class CharacterPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("templateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("posX")] public double PosX { get; set; }

        [JsonPropertyName("posY")] public double PosY { get; set; }

        [JsonPropertyName("gridX")] public int GridX { get; set; }

        [JsonPropertyName("gridY")] public int GridY { get; set; }

        [JsonPropertyName("width")] public int Width { get; set; }

        [JsonPropertyName("height")] public int Height { get; set; }

        [JsonPropertyName("pixelWidth")] public double PixelWidth { get; set; }

        [JsonPropertyName("pixelHeight")] public double PixelHeight { get; set; }

        [JsonPropertyName("stretch")] public bool Stretch { get; set; }

        [JsonPropertyName("layerType")] public string LayerType { get; set; } = string.Empty;

        [JsonPropertyName("displayLayer")] public string DisplayLayer { get; set; } = string.Empty;

        /// <summary>手工指定的遮挡带（0 地表 / 1 立体 / 2 叠加），null = 自动判定。</summary>
        [JsonPropertyName("band")] public int? Band { get; set; }

        [JsonPropertyName("image")] public string Image { get; set; } = string.Empty;

        [JsonPropertyName("anim")] public string Anim { get; set; } = string.Empty;

        [JsonPropertyName("animEnabled")] public bool AnimEnabled { get; set; }

        /// <summary>动画来源：sprite = 序列帧 / 静态贴图，spine = Spine 骨骼动画。</summary>
        [JsonPropertyName("animationKind")] public string AnimationKind { get; set; } = AnimationKinds.Sprite;

        /// <summary>Spine 资产名，对应 output\Config\SpineAnimations.json 的 Name。</summary>
        [JsonPropertyName("spineAsset")] public string SpineAsset { get; set; } = string.Empty;

        /// <summary>Spine 骨架动画名；空串表示资产默认动画。</summary>
        [JsonPropertyName("spineAnimation")] public string SpineAnimation { get; set; } = string.Empty;

        [JsonPropertyName("loop")] public bool Loop { get; set; }

        [JsonPropertyName("speed")] public double Speed { get; set; }

        [JsonPropertyName("interactable")] public bool Interactable { get; set; }

        [JsonPropertyName("interactType")] public string InteractType { get; set; } = string.Empty;

        [JsonPropertyName("walkMode")] public string WalkMode { get; set; } = string.Empty;

        [JsonPropertyName("facingDirection")] public string FacingDirection { get; set; } = string.Empty;

        [JsonPropertyName("dialogBubble")] public string DialogBubble { get; set; } = string.Empty;

        [JsonPropertyName("sortOrder")] public int SortOrder { get; set; }

        [JsonPropertyName("ySortEnabled")] public bool YSortEnabled { get; set; }

        [JsonPropertyName("ySortOffset")] public double YSortOffset { get; set; }

        [JsonPropertyName("rotation")] public double Rotation { get; set; }

        [JsonPropertyName("flipX")] public bool FlipX { get; set; }

        [JsonPropertyName("scale")] public double Scale { get; set; }

        [JsonPropertyName("scaleX")] public double ScaleX { get; set; }

        [JsonPropertyName("scaleY")] public double ScaleY { get; set; }

        [JsonPropertyName("shadowEnabled")] public bool ShadowEnabled { get; set; }

        [JsonPropertyName("collisionEnabled")] public bool CollisionEnabled { get; set; }

        [JsonPropertyName("collisionX")] public double CollisionX { get; set; }

        [JsonPropertyName("collisionY")] public double CollisionY { get; set; }

        [JsonPropertyName("collisionWidth")] public double CollisionWidth { get; set; }

        [JsonPropertyName("collisionHeight")] public double CollisionHeight { get; set; }

        [JsonPropertyName("shadowX")] public double ShadowX { get; set; }

        [JsonPropertyName("shadowY")] public double ShadowY { get; set; }

        [JsonPropertyName("shadowWidth")] public double ShadowWidth { get; set; }

        [JsonPropertyName("shadowHeight")] public double ShadowHeight { get; set; }

        [JsonPropertyName("pathPoints")] public List<PathPointPayload> PathPoints { get; set; } = new();
    }

    private sealed class PathPointPayload
    {
        [JsonPropertyName("x")] public double X { get; set; }

        [JsonPropertyName("y")] public double Y { get; set; }

        [JsonPropertyName("gridX")] public int GridX { get; set; }

        [JsonPropertyName("gridY")] public int GridY { get; set; }
    }

    private sealed class CellPayload
    {
        [JsonPropertyName("x")] public int X { get; set; }

        [JsonPropertyName("y")] public int Y { get; set; }
    }

    private sealed class RegionPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;

        [JsonPropertyName("x")] public double X { get; set; }

        [JsonPropertyName("y")] public double Y { get; set; }

        [JsonPropertyName("width")] public double Width { get; set; }

        [JsonPropertyName("height")] public double Height { get; set; }
    }

    #endregion
}
