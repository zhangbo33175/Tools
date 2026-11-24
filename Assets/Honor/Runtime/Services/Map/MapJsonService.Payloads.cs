/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapJsonService.Payloads.cs
 * author:    云毅
 * created:   2026
 * descrip:   运行时 JSON 载荷 DTO：配置、物件、角色、格子、交通与收集物
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public static partial class MapJsonService
{
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

    /// <summary>物件与角色载荷共享的通用字段。</summary>
    private class SharedObjectPayload
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

        /// <summary>Spine 骨架行走动画名；沿路径移动时播放。</summary>
        [JsonPropertyName("spineWalkAnimation")] public string SpineWalkAnimation { get; set; } = string.Empty;

        /// <summary>序列帧行走动画名；沿路径移动时播放。</summary>
        [JsonPropertyName("moveAnimName")] public string MoveAnimName { get; set; } = string.Empty;

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
    }

    /// <summary>普通物件载荷：共享字段 + 收集、复活、传送与交通专属字段。</summary>
    private sealed class ObjectPayload : SharedObjectPayload
    {
        [JsonPropertyName("isCollectible"), JsonPropertyOrder(100)] public bool IsCollectible { get; set; }

        /// <summary>收集物显示在普通物件上面（false = 在物件下面）。</summary>
        [JsonPropertyName("collectibleOverObjects"), JsonPropertyOrder(100)] public bool CollectibleOverObjects { get; set; } = true;

        [JsonPropertyName("respawnTime"), JsonPropertyOrder(100)] public double RespawnTime { get; set; }

        [JsonPropertyName("bubbleId"), JsonPropertyOrder(100)] public int BubbleId { get; set; }

        [JsonPropertyName("isMovable"), JsonPropertyOrder(100)] public bool IsMovable { get; set; }

        [JsonPropertyName("loopRestartDelay"), JsonPropertyOrder(100)] public double LoopRestartDelay { get; set; }

        [JsonPropertyName("hideWhenOffMap"), JsonPropertyOrder(100)] public bool HideWhenOffMap { get; set; }

        [JsonPropertyName("isTeleport"), JsonPropertyOrder(100)] public bool IsTeleport { get; set; }

        [JsonPropertyName("teleportTargetScene"), JsonPropertyOrder(100)] public string TeleportTargetScene { get; set; } = string.Empty;

        [JsonPropertyName("teleportTargetX"), JsonPropertyOrder(100)] public double TeleportTargetX { get; set; }

        [JsonPropertyName("teleportTargetY"), JsonPropertyOrder(100)] public double TeleportTargetY { get; set; }

        [JsonPropertyName("isVehicle"), JsonPropertyOrder(100)] public bool IsVehicle { get; set; }

        [JsonPropertyName("trafficRole"), JsonPropertyOrder(100)] public string TrafficRole { get; set; } = string.Empty;

        [JsonPropertyName("stopLineX"), JsonPropertyOrder(100)] public double StopLineX { get; set; }

        [JsonPropertyName("stopLineY"), JsonPropertyOrder(100)] public double StopLineY { get; set; }

        [JsonPropertyName("boundLightIds"), JsonPropertyOrder(100)] public List<string> BoundLightIds { get; set; } = new();

        [JsonPropertyName("yellowTreatAsRed"), JsonPropertyOrder(100)] public bool YellowTreatAsRed { get; set; }
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

    /// <summary>角色载荷：共享字段 + 行走模式、朝向与路径点。</summary>
    private sealed class CharacterPayload : SharedObjectPayload
    {
        /// <summary>行走模式，运行时枚举键。</summary>
        [JsonPropertyName("walkMode"), JsonPropertyOrder(100)] public string WalkMode { get; set; } = string.Empty;

        /// <summary>初始朝向，运行时枚举键。</summary>
        [JsonPropertyName("facingDirection"), JsonPropertyOrder(101)] public string FacingDirection { get; set; } = string.Empty;

        /// <summary>巡逻路径点，空列表表示原地角色。</summary>
        [JsonPropertyName("pathPoints"), JsonPropertyOrder(102)] public List<PathPointPayload> PathPoints { get; set; } = new();
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
