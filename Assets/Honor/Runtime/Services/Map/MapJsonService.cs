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
public static partial class MapJsonService
{
    #region 公开方法

    /// <summary>
    /// 将地图配置序列化为 JSON 文本。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <returns>格式化 JSON。</returns>
    public static string Serialize(MapConfigData config)
    {
        return JsonSerializer.Serialize(BuildPayload(config), JsonFileOptions.RelaxedIgnoreNull);
    }

    /// <summary>
    /// 将地图配置写入指定路径，目录不存在时自动创建。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="path">目标 JSON 绝对路径。</param>
    /// <returns>实际写入路径。</returns>
    public static string Save(MapConfigData config, string path)
    {
        FileSystemIo.EnsureParentDirectory(path);

        FileSystemIo.WriteText(path, Serialize(config));
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

        return MapFileNames.Scene(sceneId);
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

    /// <summary>构造普通物件载荷：通用字段 + 物件专属字段。</summary>
    private static ObjectPayload BuildObject(PlacedObject item)
    {
        var payload = new ObjectPayload();
        FillSharedObjectPayload(payload, item);
        payload.IsCollectible = item.IsCollectible;
        payload.CollectibleOverObjects = item.CollectibleOverObjects;
        payload.RespawnTime = item.RespawnTime;
        payload.BubbleId = item.BubbleId;
        payload.IsMovable = item.IsMovable;
        payload.LoopRestartDelay = MapToolDefaults.DefaultLoopRestartDelay;
        payload.HideWhenOffMap = MapToolDefaults.DefaultHideWhenOffMap;
        payload.IsTeleport = item.IsTeleport;
        payload.TeleportTargetScene = item.TeleportTargetScene;
        payload.TeleportTargetX = item.TeleportTargetX;
        payload.TeleportTargetY = item.TeleportTargetY;
        payload.IsVehicle = item.IsVehicle;
        payload.TrafficRole = item.TrafficRole;
        payload.StopLineX = item.StopLineX;
        payload.StopLineY = item.StopLineY;
        payload.BoundLightIds = new List<string>(item.BoundLightIds);
        payload.YellowTreatAsRed = item.YellowTreatAsRed;
        return payload;
    }

    /// <summary>构造角色载荷：通用字段 + 行走模式、朝向与路径点。</summary>
    private static CharacterPayload BuildCharacter(PlacedObject item)
    {
        var payload = new CharacterPayload();
        FillSharedObjectPayload(payload, item);
        payload.WalkMode = item.WalkMode;
        payload.FacingDirection = item.FacingDirection;
        payload.PathPoints = BuildPathPoints(item.PathPoints);
        return payload;
    }

    /// <summary>把编辑器物件的通用字段写入载荷基类，避免物件与角色载荷重复手抄。</summary>
    private static void FillSharedObjectPayload(SharedObjectPayload payload, PlacedObject item)
    {
        payload.Id = item.Id;
        payload.TemplateId = item.TemplateId;
        payload.Name = item.Name;
        payload.PosX = item.X;
        payload.PosY = item.Y;
        payload.GridX = item.CellX;
        payload.GridY = item.CellY;
        payload.Width = item.ExportWidth;
        payload.Height = item.ExportHeight;
        payload.PixelWidth = item.PixelWidth;
        payload.PixelHeight = item.PixelHeight;
        payload.Stretch = item.Stretch;
        payload.LayerType = item.LayerType;
        payload.DisplayLayer = item.DisplayLayer;
        payload.Band = item.BandOverride >= 0 ? item.BandOverride : null;
        payload.Image = item.ImagePath;
        payload.Anim = item.AnimName;
        payload.AnimEnabled = item.AnimEnabled;
        payload.AnimationKind = item.AnimationKind;
        payload.SpineAsset = item.SpineAsset;
        payload.SpineAnimation = item.SpineAnimation;
        payload.Loop = item.Loop;
        payload.Speed = item.Speed;
        payload.Interactable = item.Interactable;
        payload.InteractType = item.InteractType;
        payload.SortOrder = item.SortOrder;
        payload.YSortEnabled = item.YSortEnabled;
        payload.YSortOffset = item.YSortOffset;
        payload.Rotation = item.Rotation;
        payload.FlipX = item.FlipX;
        payload.Scale = item.Scale;
        payload.ScaleX = item.Scale;
        payload.ScaleY = item.Scale;
        payload.CollisionEnabled = item.CollisionEnabled;
        payload.CollisionX = item.CollisionX;
        payload.CollisionY = item.CollisionY;
        payload.CollisionWidth = item.CollisionWidth;
        payload.CollisionHeight = item.CollisionHeight;
        payload.DialogBubble = item.DialogBubble;
        payload.ShadowEnabled = item.ShadowEnabled;
        payload.ShadowX = item.ShadowX;
        payload.ShadowY = item.ShadowY;
        payload.ShadowWidth = item.ShadowWidth;
        payload.ShadowHeight = item.ShadowHeight;
    }

    /// <summary>把编辑器路径点转换为运行时路径点载荷。</summary>
    private static List<PathPointPayload> BuildPathPoints(IEnumerable<MapPathPointData> points)
    {
        var result = new List<PathPointPayload>();
        foreach (var point in points)
        {
            result.Add(new PathPointPayload
            {
                X = point.X,
                Y = point.Y,
                GridX = point.GridX,
                GridY = point.GridY,
            });
        }

        return result;
    }
    /// <summary>按每个物体占格范围铺开占用格集合；菱形模式下按 45° 菱形格中心判定。</summary>

    #endregion
}
