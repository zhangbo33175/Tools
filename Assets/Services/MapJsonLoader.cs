/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapJsonLoader.cs
 * author:    云毅
 * created:   2026
 * descrip:   把导出的地图 JSON 读回内存模型（供 --open-map 打开存档）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 地图 JSON 读取服务。与 <see cref="MapJsonService"/> 的导出字段一一对应，
/// 保证「导出 → 重新打开 → 再导出」结果一致。
/// </summary>
public static class MapJsonLoader
{
    #region 公开方法

    /// <summary>读取地图 JSON。</summary>
    /// <param name="path">地图 JSON 绝对路径。</param>
    /// <returns>地图配置。</returns>
    public static MapConfigData Load(string path)
    {
        var text = File.ReadAllText(path);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        var config = new MapConfigData
        {
            SceneId = ReadString(root, "sceneId"),
            SceneName = ReadString(root, "sceneName"),
            ChapterId = ReadString(root, "chapterId") is { Length: > 0 } chapterId ? chapterId : "DefaultChapter",
            ChapterName = ReadString(root, "chapterName"),
            BgName = ReadString(root, "bgName"),
            Width = ReadInt(root, "width", MapToolDefaults.LevelPixelSize),
            Height = ReadInt(root, "height", MapToolDefaults.LevelPixelSize),
            GridSize = ReadInt(root, "gridSize", MapToolDefaults.GridSize),
            // 老存档没有 drawShape 字段：默认按矩形口径读（新工程默认矩形），显式写 diamond 的仍按菱形
            DrawShape = ReadString(root, "drawShape") == DrawShapes.Diamond ? DrawShapes.Diamond : DrawShapes.Rect,
            RegionType = ReadString(root, "regionType") is { Length: > 0 } region ? region : RegionTypes.Place,
        };

        LoadObjects(root, "buildings", MapObjectType.Building, config.Objects);
        LoadObjects(root, "objects", MapObjectType.Prop, config.Objects);
        LoadObjects(root, "characters", MapObjectType.Character, config.Objects);

        foreach (var element in ReadArray(root, "sidewalkCells"))
        {
            config.SidewalkCells.Add(new MapGridCellData
            {
                X = ReadInt(element, "x", 0),
                Y = ReadInt(element, "y", 0),
            });
        }

        foreach (var element in ReadArray(root, "roadCells"))
        {
            config.RoadCells.Add(new MapGridCellData
            {
                X = ReadInt(element, "x", 0),
                Y = ReadInt(element, "y", 0),
            });
        }

        foreach (var element in ReadArray(root, "regions"))
        {
            config.Regions.Add(new MapRegionData
            {
                Id = ReadString(element, "id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..8],
                Type = ReadString(element, "type"),
                X = ReadDouble(element, "x", 0),
                Y = ReadDouble(element, "y", 0),
                Width = ReadDouble(element, "width", 0),
                Height = ReadDouble(element, "height", 0),
            });
        }

        foreach (var element in ReadArray(root, "trafficLights"))
        {
            config.TrafficLights.Add(new MapTrafficLightData
            {
                Id = ReadString(element, "id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..8],
                TemplateId = ReadString(element, "templateId"),
                Name = ReadString(element, "name"),
                PosX = ReadDouble(element, "posX", 0),
                PosY = ReadDouble(element, "posY", 0),
                Direction = ReadString(element, "direction") is { Length: > 0 } dir ? dir : TrafficDirections.Horizontal,
                Side = ReadString(element, "side") is { Length: > 0 } side ? side : "north",
                IntersectionId = ReadString(element, "intersectionId"),
                RedDuration = ReadDouble(element, "redDuration", 5),
                GreenDuration = ReadDouble(element, "greenDuration", 5),
                YellowDuration = ReadDouble(element, "yellowDuration", 1),
                OffsetPhase = ReadDouble(element, "offsetPhase", 0),
                BoundCrosswalkId = ReadString(element, "boundCrosswalkId"),
                InitialPhase = ReadString(element, "initialPhase") is { Length: > 0 } phase ? phase : "red",
                Image = ReadString(element, "image"),
                Anim = ReadString(element, "anim") is { Length: > 0 } anim ? anim : MapToolDefaults.DefaultAnimName,
            });
        }

        foreach (var element in ReadArray(root, "crosswalks"))
        {
            config.Crosswalks.Add(new MapCrosswalkData
            {
                Id = ReadString(element, "id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..8],
                Name = ReadString(element, "name"),
                PointA = ReadPoint(element, "pointA"),
                PointB = ReadPoint(element, "pointB"),
                Width = ReadDouble(element, "width", MapToolDefaults.GridSize),
                Direction = ReadString(element, "direction") is { Length: > 0 } dir ? dir : TrafficDirections.Horizontal,
                BoundLightId = ReadString(element, "boundLightId"),
            });
        }

        config.Collectibles.Total = ReadInt(root, "collectibleTotal", 0);
        config.Collectibles.LayerMode = CollectibleLayers.Normalize(ReadString(root, "collectibleLayer"));
        foreach (var element in ReadArray(root, "collectibleKinds"))
        {
            config.Collectibles.Kinds.Add(new CollectibleKindData
            {
                TemplateId = ReadString(element, "templateId"),
                Name = ReadString(element, "name"),
                ImagePath = ReadString(element, "imagePath"),
                ResName = ReadString(element, "resName"),
                Width = ReadInt(element, "width", 1),
                Height = ReadInt(element, "height", 1),
                LayerType = ReadString(element, "layerType") is { Length: > 0 } layer ? layer : MapLayerKeys.Prop,
                AnimName = ReadString(element, "animName") is { Length: > 0 } anim ? anim : MapToolDefaults.DefaultAnimName,
            });
        }

        return config;
    }

    #endregion

    #region 组装

    private static void LoadObjects(JsonElement root, string name, MapObjectType type, List<PlacedObject> target)
    {
        foreach (var element in ReadArray(root, name))
        {
            var item = new PlacedObject
            {
                Id = ReadString(element, "id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..8],
                TemplateId = ReadString(element, "templateId"),
                Name = ReadString(element, "name"),
                Type = type,
                X = ReadDouble(element, "posX", 0),
                Y = ReadDouble(element, "posY", 0),
                Width = ReadInt(element, "width", 1),
                Height = ReadInt(element, "height", 1),
                PixelWidth = ReadDouble(element, "pixelWidth", 0),
                PixelHeight = ReadDouble(element, "pixelHeight", 0),
                Stretch = ReadBool(element, "stretch", false),
                LayerType = ReadString(element, "layerType") is { Length: > 0 } layer ? layer : MapLayerKeys.Prop,
                DisplayLayer = ReadString(element, "displayLayer"),
                BandOverride = ReadInt(element, "band", -1),
                ImagePath = ReadString(element, "image"),
                AnimName = ReadString(element, "anim") is { Length: > 0 } anim ? anim : MapToolDefaults.DefaultAnimName,
                AnimEnabled = ReadBool(element, "animEnabled", true),
                AnimationKind = NormalizeAnimationKind(ReadString(element, "animationKind")),
                SpineAsset = ReadString(element, "spineAsset"),
                SpineAnimation = ReadString(element, "spineAnimation"),
                Loop = ReadBool(element, "loop", true),
                Speed = ReadDouble(element, "speed", 1),
                Interactable = ReadBool(element, "interactable", true),
                InteractType = ReadString(element, "interactType"),
                SortOrder = ReadInt(element, "sortOrder", 0),
                YSortEnabled = ReadBool(element, "ySortEnabled", true),
                YSortOffset = ReadDouble(element, "ySortOffset", 0),
                Rotation = ReadDouble(element, "rotation", 0),
                FlipX = ReadBool(element, "flipX", false),
                Scale = ReadDouble(element, "scale", 1),
                CollisionEnabled = ReadBool(element, "collisionEnabled", true),
                CollisionX = ReadDouble(element, "collisionX", 0),
                CollisionY = ReadDouble(element, "collisionY", 0),
                CollisionWidth = ReadDouble(element, "collisionWidth", 0),
                CollisionHeight = ReadDouble(element, "collisionHeight", 0),
                DialogBubble = ReadString(element, "dialogBubble"),
                ShadowEnabled = ReadBool(element, "shadowEnabled", true),
                ShadowX = ReadDouble(element, "shadowX", 0),
                ShadowY = ReadDouble(element, "shadowY", 0),
                ShadowWidth = ReadDouble(element, "shadowWidth", 0),
                ShadowHeight = ReadDouble(element, "shadowHeight", 0),
                IsCollectible = ReadBool(element, "isCollectible", false),
                CollectibleOverObjects = ReadBool(element, "collectibleOverObjects", true),
                RespawnTime = ReadDouble(element, "respawnTime", 0),
                BubbleId = ReadInt(element, "bubbleId", 0),
                IsMovable = ReadBool(element, "isMovable", false),
                WalkMode = ReadString(element, "walkMode") is { Length: > 0 } walk ? walk : "loop",
                FacingDirection = ReadString(element, "facingDirection") is { Length: > 0 } facing ? facing : Facings.Down,
                IsTeleport = ReadBool(element, "isTeleport", false),
                TeleportTargetScene = ReadString(element, "teleportTargetScene"),
                TeleportTargetX = ReadDouble(element, "teleportTargetX", 0),
                TeleportTargetY = ReadDouble(element, "teleportTargetY", 0),
                IsVehicle = ReadBool(element, "isVehicle", false),
                TrafficRole = ReadString(element, "trafficRole") is { Length: > 0 } role ? role : TrafficRoles.Pedestrian,
                StopLineX = ReadDouble(element, "stopLineX", 0),
                StopLineY = ReadDouble(element, "stopLineY", 0),
                YellowTreatAsRed = ReadBool(element, "yellowTreatAsRed", true),
            };

            foreach (var point in ReadArray(element, "pathPoints"))
            {
                item.PathPoints.Add(new MapPathPointData(
                    ReadDouble(point, "x", 0),
                    ReadDouble(point, "y", 0)));
            }

            foreach (var lightRef in ReadArray(element, "boundLightIds"))
            {
                if (lightRef.ValueKind == JsonValueKind.String)
                {
                    item.BoundLightIds.Add(lightRef.GetString() ?? string.Empty);
                }
            }

            // SortOrder 换算到 Unity 八层坐标（0–79）：旧档里的「带 × 1000 / 100 + 序位」在这里一次性归位
            item.SortOrder = LayerOrder.Normalize(item, item.SortOrder);
            target.Add(item);
        }
    }

    /// <summary>把旧档缺失 / 非法动画来源归一到 sprite，避免未知字符串让画布静默变成空白。</summary>
    /// <param name="value">JSON 里的 animationKind。</param>
    /// <returns>sprite 或 spine。</returns>
    private static string NormalizeAnimationKind(string value) =>
        string.Equals(value, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            ? AnimationKinds.Spine
            : AnimationKinds.Sprite;

    private static IEnumerable<JsonElement> ReadArray(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().ToList()
            : Enumerable.Empty<JsonElement>();
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement root, string name, int fallback)
    {
        return root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    }

    private static double ReadDouble(JsonElement root, string name, double fallback)
    {
        return root.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : fallback;
    }

    private static bool ReadBool(JsonElement root, string name, bool fallback)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    private static MapPathPointData ReadPoint(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return new MapPathPointData();
        }

        return new MapPathPointData(
            ReadDouble(value, "x", 0),
            ReadDouble(value, "y", 0));
    }

    #endregion
}
