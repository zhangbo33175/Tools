/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图编辑器数据模型（与 Unity 版 MapToolModels 字段口径对齐）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MapEditorTool.Models;

#region 常量与枚举

/// <summary>编辑器默认参数，数值与 Unity 版 MapToolConstants 保持一致。</summary>
public static class MapToolDefaults
{
    /// <summary>关卡像素边长，4096。</summary>
    public const int LevelPixelSize = 4096;

    /// <summary>格子像素边长，32。</summary>
    public const int GridSize = 32;

    /// <summary>最小缩放倍率。</summary>
    public const double MinZoom = 0.1;

    /// <summary>最大缩放倍率。</summary>
    public const double MaxZoom = 5.0;

    /// <summary>适配视图时的最大缩放，避免小地图被放大到格子级。</summary>
    public const double AutoFitMaxZoom = 4.0;

    /// <summary>相邻显示层基线步长。</summary>
    public const int LayerBaselineStep = 10;

    /// <summary>默认动画名。</summary>
    public const string DefaultAnimName = "Idle";
}

/// <summary>物体类型。</summary>
public enum MapObjectType
{
    /// <summary>建筑。</summary>
    Building,

    /// <summary>人物。</summary>
    Character,

    /// <summary>物件。</summary>
    Prop,
}

/// <summary>绘制形状与区域类型的常量键，对应 Unity 版 DrawShape / RegionType。</summary>
public static class DrawShapes
{
    /// <summary>正交矩形格子。</summary>
    public const string Rect = "rect";

    /// <summary>菱形格子，与本项目 45° 斜角地图对齐。</summary>
    public const string Diamond = "diamond";
}

/// <summary>刷选区域类型。</summary>
public static class RegionTypes
{
    /// <summary>可摆放区。</summary>
    public const string Place = "place";

    /// <summary>禁放区。</summary>
    public const string Forbid = "forbid";

    /// <summary>通用区。</summary>
    public const string Common = "common";

    /// <summary>人行道刷格产生的区域回显。</summary>
    public const string Sidewalk = "sidewalk";

    /// <summary>道路刷格产生的区域回显。</summary>
    public const string Road = "road";
}

/// <summary>交互类型。</summary>
public static class InteractTypes
{
    /// <summary>无交互。</summary>
    public const string None = "none";

    /// <summary>点击。</summary>
    public const string Click = "click";

    /// <summary>进入触发。</summary>
    public const string Enter = "enter";

    /// <summary>对话。</summary>
    public const string Dialog = "dialog";

    /// <summary>拾取。</summary>
    public const string Pickup = "pickup";

    /// <summary>查看。</summary>
    public const string View = "view";
}

/// <summary>初始朝向。</summary>
public static class Facings
{
    /// <summary>上。</summary>
    public const string Up = "up";

    /// <summary>下。</summary>
    public const string Down = "down";

    /// <summary>左。</summary>
    public const string Left = "left";

    /// <summary>右。</summary>
    public const string Right = "right";
}

/// <summary>八层功能分桶键，与 Unity 版 MapLayerCatalog 一致。</summary>
public static class MapLayerKeys
{
    /// <summary>背景。</summary>
    public const string Background = "background";

    /// <summary>绿化。</summary>
    public const string Greenery = "greenery";

    /// <summary>道路。</summary>
    public const string Road = "road";

    /// <summary>建筑。</summary>
    public const string Building = "building";

    /// <summary>道具。</summary>
    public const string Prop = "prop";

    /// <summary>角色。</summary>
    public const string Character = "character";

    /// <summary>前景。</summary>
    public const string Foreground = "foreground";

    /// <summary>气泡。</summary>
    public const string Bubble = "bubble";

    /// <summary>
    /// 收集物的编辑器独立显隐层。它不是第九个数据层，收集物仍然按道具层写 JSON；
    /// 只用于顶部图层开关与左侧收集物切页，避免和普通物件共用显隐。
    /// </summary>
    public const string Collectible = "collectible";

    /// <summary>八层键全集（自底向上，与 Unity MapLayerCatalog.AllLayerTypes 同序）。</summary>
    public static readonly string[] AllKeys =
    {
        Background, Greenery, Road, Building, Prop, Character, Foreground, Bubble,
    };

    /// <summary>
    /// 图层键 → 资源子文件夹名，与 Unity <c>MapLayerCatalog.GetLayerDir</c> 的目录名逐字一致：
    /// Background / Greenery / Road / Buildings / Props / Characters / Foreground / Bubbles。
    /// </summary>
    /// <param name="layerKey">图层键（大小写与旧键都会归一）。</param>
    /// <returns>目录名。</returns>
    public static string FolderName(string layerKey) => layerKey switch
    {
        Background => "Background",
        Greenery => "Greenery",
        Road => "Road",
        Building => "Buildings",
        Prop => "Props",
        Character => "Characters",
        Foreground => "Foreground",
        Bubble => "Bubbles",
        _ => "Props",
    };

    /// <summary>
    /// 目录名 → 图层键（八层目录反查），无法识别时回退道具层。
    /// </summary>
    /// <param name="folder">目录名，如 <c>Buildings</c>。</param>
    /// <returns>图层键。</returns>
    public static string FromFolderName(string folder) => folder switch
    {
        "Background" => Background,
        "Greenery" => Greenery,
        "Road" => Road,
        "Buildings" => Building,
        "Props" => Prop,
        "Characters" => Character,
        "Foreground" => Foreground,
        "Bubbles" => Bubble,
        _ => Prop,
    };

    /// <summary>取图层显示名，供图层面板与导出使用。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>中文显示名。</returns>
    public static string DisplayName(string layerKey) => layerKey switch
    {
        Background => "背景",
        Greenery => "绿化",
        Road => "道路",
        Building => "建筑",
        Prop => "道具",
        Character => "角色",
        Foreground => "前景",
        Bubble => "气泡",
        Collectible => "收集物",
        _ => "道具",
    };

    /// <summary>取图层代表色，用于画布占位填充。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>色值。</param>
    public static string Hex(string layerKey) => layerKey switch
    {
        Background => "#0A6E96",
        Greenery => "#43C08A",
        Road => "#9BB0C6",
        Building => "#E8A23F",
        Prop => "#9B8BE8",
        Character => "#FF7FA3",
        Foreground => "#C79A6B",
        Bubble => "#FFD24A",
        Collectible => "#2FC58B",
        _ => "#8A8F9C",
    };
}

/// <summary>
/// 收集物相对普通物件的显示层级。收集物单独成组（<see cref="MapLayerKeys.Collectible"/>），
/// 但数据上仍是道具层，只有绘制次序在立体带内做前后夹层：
/// 物件下 → 所有普通立体物之前；物件上 → 所有普通立体物之后、前景之前。
/// </summary>
public static class CollectibleLayers
{
    /// <summary>显示在普通物件下面。</summary>
    public const string UnderObjects = "under";

    /// <summary>显示在普通物件上面。</summary>
    public const string OverObjects = "over";

    /// <summary>默认显示在物件上面。</summary>
    public const string Default = OverObjects;

    /// <summary>代码值 → 中文显示名。</summary>
    /// <param name="mode">under / over 或空串。</param>
    /// <returns>显示名。</returns>
    public static string DisplayName(string? mode) =>
        string.Equals(mode, UnderObjects, StringComparison.OrdinalIgnoreCase) ? "物件下" : "物件上";

    /// <summary>中文显示名 → 代码值。</summary>
    /// <param name="display">显示名。</param>
    /// <returns>代码值。</returns>
    public static string ToCode(string? display) =>
        string.Equals(display, "物件下", StringComparison.Ordinal) ? UnderObjects : OverObjects;

    /// <summary>是否显示在物件上面。</summary>
    /// <param name="mode">层级代码。</param>
    /// <returns>true = 上，false = 下。</returns>
    public static bool IsOver(string? mode) =>
        !string.Equals(mode, UnderObjects, StringComparison.OrdinalIgnoreCase);

    /// <summary>把任意输入归一到两个合法值。</summary>
    /// <param name="mode">原始值。</param>
    /// <returns>under / over。</returns>
    public static string Normalize(string? mode) => IsOver(mode) ? OverObjects : UnderObjects;
}

/// <summary>收集物切页中的一种素材：来自物件表，保存足够重建模板的字段。</summary>
public sealed class CollectibleKindData
{
    /// <summary>模板 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>素材路径。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>资源名。</summary>
    public string ResName { get; set; } = string.Empty;

    /// <summary>默认占格宽。</summary>
    public int Width { get; set; } = 1;

    /// <summary>默认占格高。</summary>
    public int Height { get; set; } = 1;

    /// <summary>归属层键。</summary>
    public string LayerType { get; set; } = MapLayerKeys.Prop;

    /// <summary>默认动画名。</summary>
    public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;

    /// <summary>模板 → 切页数据。</summary>
    /// <param name="template">模板。</param>
    /// <returns>切页数据。</returns>
    public static CollectibleKindData FromTemplate(ObjectTemplate template) => new()
    {
        TemplateId = template.TemplateId,
        Name = template.TemplateName,
        ImagePath = template.ImagePath,
        ResName = template.ResName,
        Width = Math.Max(1, template.DefaultWidth),
        Height = Math.Max(1, template.DefaultHeight),
        LayerType = template.LayerKey,
        AnimName = template.AnimName,
    };

    /// <summary>切页数据 → 可摆放模板。</summary>
    /// <returns>模板。</returns>
    public ObjectTemplate ToTemplate() => new()
    {
        TemplateId = TemplateId,
        TemplateName = Name,
        Type = MapObjectType.Prop,
        ImagePath = ImagePath,
        ResName = ResName,
        DefaultWidth = Math.Max(1, Width),
        DefaultHeight = Math.Max(1, Height),
        LayerTypeKey = string.IsNullOrWhiteSpace(LayerType) ? MapLayerKeys.Prop : LayerType,
        AnimName = string.IsNullOrWhiteSpace(AnimName) ? MapToolDefaults.DefaultAnimName : AnimName,
        IsCollectible = true,
    };
}

/// <summary>收集物计划：目标数量、层级与已选素材；保存进地图 JSON，重新打开后可继续生成。</summary>
public sealed class CollectiblePlanData
{
    /// <summary>目标收集物数量。</summary>
    public int Total { get; set; }

    /// <summary>层级：under / over。</summary>
    public string LayerMode { get; set; } = CollectibleLayers.Default;

    /// <summary>已选素材。</summary>
    public List<CollectibleKindData> Kinds { get; set; } = new();
}

#endregion

#region 模板与实例

/// <summary>
/// 物体模板。同一类物体的公共属性只配置一次，放置时通过 <see cref="TemplateId"/> 引用。
/// </summary>
public sealed class ObjectTemplate
{
    /// <summary>模板唯一 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>模板显示名称。</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>物体类型。</summary>
    public MapObjectType Type { get; set; } = MapObjectType.Prop;

    /// <summary>素材资源名。</summary>
    public string ResName { get; set; } = string.Empty;

    /// <summary>预览图路径。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>是否内置模板（内置不可删除）。</summary>
    public bool IsBuiltIn { get; set; } = true;

    /// <summary>默认动画名。</summary>
    public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;

    /// <summary>默认循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>默认播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>默认可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>默认交互类型。</summary>
    public string InteractType { get; set; } = InteractTypes.None;

    /// <summary>默认气泡文本。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>默认气泡配置 ID。</summary>
    public int BubbleId { get; set; }

    /// <summary>渲染层级排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>显示层键，空串表示跟随类型归属层。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>启用 Y 轴深度排序。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>Y 轴深度排序偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>启用碰撞。</summary>
    public bool CollisionEnabled { get; set; } = true;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = Facings.Down;

    /// <summary>显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>是否可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>移动动画名。</summary>
    public string MoveAnimName { get; set; } = "walk";

    /// <summary>循环行驶离开地图后的重启延迟，秒。</summary>
    public double LoopRestartDelay { get; set; } = 3.0;

    /// <summary>离开地图时隐藏。</summary>
    public bool HideWhenOffMap { get; set; } = true;

    /// <summary>可收集。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>重生时间。</summary>
    public double RespawnTime { get; set; }

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>传送目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>传送目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>传送目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>交通类别键。</summary>
    public string TrafficCategory { get; set; } = "none";

    /// <summary>默认占格宽，单位格子。</summary>
    public int DefaultWidth { get; set; } = 1;

    /// <summary>默认占格高，单位格子。</summary>
    public int DefaultHeight { get; set; } = 1;

    /// <summary>
    /// 九宫格配置。仅在该素材被「是否缩放」拉伸时参与绘制：
    /// 四角保持原尺寸，四条边单轴填充，中心按 <see cref="NineSliceSettings.CenterMode"/> 填充。
    /// </summary>
    public NineSliceSettings NineSlice { get; set; } = new();

    /// <summary>显式指定的图层键，留空时按类型推导。</summary>
    public string LayerTypeKey { get; set; } = string.Empty;

    /// <summary>归属图层键，优先取显式配置，否则按类型推导。</summary>
    public string LayerKey => !string.IsNullOrEmpty(LayerTypeKey)
        ? LayerTypeKey
        : Type switch
        {
            MapObjectType.Building => MapLayerKeys.Building,
            MapObjectType.Character => MapLayerKeys.Character,
            _ => MapLayerKeys.Prop,
        };
}

/// <summary>
/// 九宫格（Nine-Slice）配置：按素材像素给出左 / 右 / 上 / 下四条边距，
/// 拉伸绘制时四角不参与缩放，边与中心分别填充，避免边框和圆角被拉变形。
/// </summary>
public sealed class NineSliceSettings
{
    /// <summary>是否启用九宫格。</summary>
    public bool Enabled { get; set; }

    /// <summary>左边距（素材原始像素）。</summary>
    public int Left { get; set; }

    /// <summary>右边距（素材原始像素）。</summary>
    public int Right { get; set; }

    /// <summary>上边距（素材原始像素）。</summary>
    public int Top { get; set; }

    /// <summary>下边距（素材原始像素）。</summary>
    public int Bottom { get; set; }

    /// <summary>中心区域填充方式，见 <see cref="NineSliceModes"/>。</summary>
    public string CenterMode { get; set; } = NineSliceModes.Stretch;

    /// <summary>四条边距是否都非负且左+右 / 上+下没有越过素材尺寸。</summary>
    /// <param name="pixelWidth">素材原始像素宽。</param>
    /// <param name="pixelHeight">素材原始像素高。</param>
    /// <returns>可以按九宫格绘制时为 true。</returns>
    public bool IsUsable(double pixelWidth, double pixelHeight)
    {
        return Enabled
            && Left >= 0
            && Right >= 0
            && Top >= 0
            && Bottom >= 0
            && Left + Right < pixelWidth - 1
            && Top + Bottom < pixelHeight - 1;
    }

    /// <summary>复制一份配置，避免模板之间共享同一个可变对象。</summary>
    /// <returns>配置副本。</returns>
    public NineSliceSettings Clone() => new()
    {
        Enabled = Enabled,
        Left = Left,
        Right = Right,
        Top = Top,
        Bottom = Bottom,
        CenterMode = CenterMode,
    };
}

/// <summary>九宫格中心区域的填充方式。</summary>
public static class NineSliceModes
{
    /// <summary>中心区域拉伸（默认，适合纯色 / 渐变）。</summary>
    public const string Stretch = "stretch";

    /// <summary>中心区域按素材原始尺寸平铺（适合纹理 / 网格）。</summary>
    public const string Tile = "tile";

    /// <summary>下拉框显示项。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[] { "拉伸", "平铺" };

    /// <summary>代码值转显示文案。</summary>
    /// <param name="code">代码值。</param>
    /// <returns>显示文案。</returns>
    public static string ToDisplay(string? code) =>
        string.Equals(code, Tile, StringComparison.OrdinalIgnoreCase) ? "平铺" : "拉伸";

    /// <summary>显示文案转代码值。</summary>
    /// <param name="display">显示文案。</param>
    /// <returns>代码值。</returns>
    public static string ToCode(string? display) =>
        string.Equals(display, "平铺", StringComparison.Ordinal) ? Tile : Stretch;
}

/// <summary>
/// 实例动画来源。静态贴图是缺省值；序列帧走 <see cref="SpriteAnimationCatalog"/>，
/// Spine 走导入的 <c>SpineAnimations.json</c> 目录并按骨架动画名播放。
/// </summary>
public static class AnimationKinds
{
    /// <summary>静态贴图 / 序列帧（缺省）。</summary>
    public const string Sprite = "sprite";

    /// <summary>Spine 骨骼动画。</summary>
    public const string Spine = "spine";

    /// <summary>下拉框显示项。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[] { "序列帧", "Spine" };

    /// <summary>代码值转显示文案。</summary>
    /// <param name="code">代码值。</param>
    /// <returns>显示文案；未知值按序列帧处理。</param>
    public static string ToDisplay(string? code) =>
        string.Equals(code, Spine, StringComparison.OrdinalIgnoreCase) ? "Spine" : "序列帧";

    /// <summary>显示文案转代码值。</summary>
    /// <param name="display">显示文案。</param>
    /// <returns>代码值。</returns>
    public static string ToCode(string? display) =>
        string.Equals(display, "Spine", StringComparison.OrdinalIgnoreCase) ? Spine : Sprite;
}

/// <summary>
/// 序列帧动画片段。名字与地图 JSON 的 <c>anim</c> 字段同口径，
/// <see cref="Frames"/> 按播放顺序保存 PNG/JPG 路径（绝对路径或 Unity 工程内相对路径均可）。
/// </summary>
public sealed class SpriteAnimationClip
{
    /// <summary>动画名，对应 <see cref="PlacedObject.AnimName"/> / JSON 的 <c>anim</c>。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>播放帧率。</summary>
    public double Fps { get; set; } = 10.0;

    /// <summary>是否循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>帧图片路径，按数组顺序播放。</summary>
    public List<string> Frames { get; set; } = new();

    /// <summary>帧数（不参与序列化，避免配置里出现冗余字段）。</summary>
    [JsonIgnore]
    public int FrameCount => Frames.Count;

    /// <summary>片段是否可用于画布播放。</summary>
    [JsonIgnore]
    public bool IsUsable => !string.IsNullOrWhiteSpace(Name) && Frames.Count > 0 && Fps > 0;

    /// <summary>复制一份片段，避免 UI 编辑时改到目录里的对象。</summary>
    /// <returns>片段副本。</returns>
    public SpriteAnimationClip Clone() => new()
    {
        Name = Name,
        Fps = Fps,
        Loop = Loop,
        Frames = new List<string>(Frames),
    };
}

/// <summary>
/// 已放置到地图上的物体实例。只保存实例自身的位姿与开关，模板属性通过 TemplateId 引用。
/// </summary>
public sealed class PlacedObject
{
    /// <summary>实例唯一 ID，8 位。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>引用模板 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>物体显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>物体类型。</summary>
    public MapObjectType Type { get; set; } = MapObjectType.Prop;

    /// <summary>左上角世界坐标 X。</summary>
    public double X { get; set; }

    /// <summary>左上角世界坐标 Y。</summary>
    public double Y { get; set; }

    /// <summary>宽度，单位格子。</summary>
    public int Width { get; set; } = 1;

    /// <summary>高度，单位格子。</summary>
    public int Height { get; set; } = 1;

    /// <summary>PSD 原始像素宽，大于 0 时按像素还原。</summary>
    public double PixelWidth { get; set; }

    /// <summary>PSD 原始像素高，大于 0 时按像素还原。</summary>
    public double PixelHeight { get; set; }

    /// <summary>是否拉伸到配置尺寸。</summary>
    public bool Stretch { get; set; }

    /// <summary>图层类型键。</summary>
    public string LayerType { get; set; } = MapLayerKeys.Prop;

    /// <summary>显示层键。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>
    /// 手工指定的遮挡带（编辑器侧）：-1 = 跟随图层类型 / 名字自动判定，0 地表，1 立体，2 叠加。
    /// <para>改这个值等于把这张贴图抬到另一层；导出时只在非 -1 时写 <c>band</c> 字段，
    /// 排序仍然由 <see cref="SortOrder"/>（带号 × 1000 + 带内序位）表达。</para>
    /// </summary>
    public int BandOverride { get; set; } = -1;

    /// <summary>
    /// 当前 <see cref="BandOverride"/> 是否由导入/刷新策略自动写入（仅本次会话有效，不落盘）。
    /// <para>属性卡用它区分「自动提层」与「用户手改遮挡带」；JSON 只保存 <c>band</c>，
    /// 重启后无法还原来源，按手改显示即可。用户手动改带或恢复自动判定时必须清掉。</para>
    /// </summary>
    public bool BandOverrideAuto { get; set; }

    /// <summary>素材路径。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>动画名。</summary>
    public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;

    /// <summary>动画来源，见 <see cref="AnimationKinds"/>；缺省为序列帧。</summary>
    public string AnimationKind { get; set; } = AnimationKinds.Sprite;

    /// <summary>Spine 资产名（对应 <c>SpineAnimations.json</c> 的 Name）。</summary>
    public string SpineAsset { get; set; } = string.Empty;

    /// <summary>Spine 骨架内的动画名；空串表示使用资产默认动画。</summary>
    public string SpineAnimation { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用动画。关闭时画布始终使用静态贴图；
    /// true 且 <see cref="AnimName"/> 能在动画目录里找到可用帧序列时，画布按帧播放。
    /// </summary>
    public bool AnimEnabled { get; set; } = true;

    /// <summary>循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>交互类型。</summary>
    public string InteractType { get; set; } = InteractTypes.None;

    /// <summary>气泡文本。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>气泡配置 ID。</summary>
    public int BubbleId { get; set; }

    /// <summary>渲染层级排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>启用 Y 轴排序。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>Y 轴排序偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>旋转角度，度。</summary>
    public double Rotation { get; set; }

    /// <summary>水平镜像。</summary>
    public bool FlipX { get; set; }

    /// <summary>缩放倍率。</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>启用碰撞。</summary>
    public bool CollisionEnabled { get; set; } = true;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = Facings.Down;

    /// <summary>显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>可收集。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>收集物显示在普通物件上面（false = 在物件下面）。仅 <see cref="IsCollectible"/> 为 true 时参与排序。</summary>
    public bool CollectibleOverObjects { get; set; } = true;

    /// <summary>碰撞区域左偏移（相对包围盒左上角，像素）。</summary>
    public double CollisionX { get; set; }

    /// <summary>碰撞区域上偏移（相对包围盒左上角，像素）。</summary>
    public double CollisionY { get; set; }

    /// <summary>碰撞区域宽（像素），0 表示铺满包围盒。</summary>
    public double CollisionWidth { get; set; }

    /// <summary>碰撞区域高（像素），0 表示铺满包围盒。</summary>
    public double CollisionHeight { get; set; }

    /// <summary>阴影区域左偏移（相对包围盒左上角，像素）。</summary>
    public double ShadowX { get; set; }

    /// <summary>阴影区域上偏移（相对包围盒左上角，像素）。</summary>
    public double ShadowY { get; set; }

    /// <summary>阴影区域宽（像素），0 表示铺满包围盒。</summary>
    public double ShadowWidth { get; set; }

    /// <summary>阴影区域高（像素），0 表示铺满包围盒。</summary>
    public double ShadowHeight { get; set; }

    /// <summary>重生时间。</summary>
    public double RespawnTime { get; set; }

    /// <summary>可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>移动动画名。</summary>
    public string MoveAnimName { get; set; } = "walk";

    /// <summary>行走模式。</summary>
    public string WalkMode { get; set; } = "loop";

    /// <summary>行走路径点，单位世界像素。</summary>
    public List<MapPathPointData> PathPoints { get; set; } = new();

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>传送目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>传送目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>传送目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>是否载具（勾选后交通角色同步为载具）。</summary>
    public bool IsVehicle { get; set; }

    /// <summary>交通角色：pedestrian / vehicle / none。</summary>
    public string TrafficRole { get; set; } = TrafficRoles.Pedestrian;

    /// <summary>停止线 X（世界像素）。</summary>
    public double StopLineX { get; set; }

    /// <summary>停止线 Y（世界像素）。</summary>
    public double StopLineY { get; set; }

    /// <summary>绑定的红绿灯 ID 集合。</summary>
    public List<string> BoundLightIds { get; set; } = new();

    /// <summary>黄灯是否按红灯处理（仅载具消费）。</summary>
    public bool YellowTreatAsRed { get; set; } = true;

    /// <summary>格坐标 X，由世界坐标换算。</summary>
    public int CellX => (int)Math.Floor(X / MapToolDefaults.GridSize);

    /// <summary>格坐标 Y，由世界坐标换算。</summary>
    public int CellY => (int)Math.Floor(Y / MapToolDefaults.GridSize);

    /// <summary>世界空间包围盒（格宽高按像素换算后再乘缩放倍率）。</summary>
    public Avalonia.Rect Bounds
    {
        get
        {
            var width = (PixelWidth > 0 ? PixelWidth : Width * MapToolDefaults.GridSize) * Scale;
            var height = (PixelHeight > 0 ? PixelHeight : Height * MapToolDefaults.GridSize) * Scale;
            return new Avalonia.Rect(X, Y, width, height);
        }
    }

    /// <summary>序列化时使用的格宽。</summary>
    public int ExportWidth => PixelWidth > 0 ? (int)Math.Max(1, Math.Ceiling(PixelWidth / MapToolDefaults.GridSize)) : Width;

    /// <summary>序列化时使用的格高。</summary>
    public int ExportHeight => PixelHeight > 0 ? (int)Math.Max(1, Math.Ceiling(PixelHeight / MapToolDefaults.GridSize)) : Height;

    /// <summary>碰撞区域（世界像素）；宽或高为 0 时按偏移铺满包围盒剩余部分。</summary>
    public Avalonia.Rect CollisionBounds => ResolveArea(CollisionX, CollisionY, CollisionWidth, CollisionHeight);

    /// <summary>阴影区域（世界像素）；宽或高为 0 时按偏移铺满包围盒剩余部分。</summary>
    public Avalonia.Rect ShadowBounds => ResolveArea(ShadowX, ShadowY, ShadowWidth, ShadowHeight);

    /// <summary>把「相对包围盒偏移 + 尺寸」换算成世界像素矩形，尺寸缺省时铺满包围盒。</summary>
    /// <param name="offsetX">相对包围盒左偏移。</param>
    /// <param name="offsetY">相对包围盒上偏移。</param>
    /// <param name="width">宽，0 表示铺满。</param>
    /// <param name="height">高，0 表示铺满。</param>
    /// <returns>世界像素矩形。</returns>
    private Avalonia.Rect ResolveArea(double offsetX, double offsetY, double width, double height)
    {
        var bounds = Bounds;
        var w = width > 0 ? width : Math.Max(1, bounds.Width - offsetX);
        var h = height > 0 ? height : Math.Max(1, bounds.Height - offsetY);
        return new Avalonia.Rect(bounds.X + offsetX, bounds.Y + offsetY, w, h);
    }

    /// <summary>
    /// 世界坐标是否落在物体内：先按 <see cref="Rotation"/> 绕包围盒中心逆向旋转回局部坐标，再判轴对齐包围盒。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <returns>命中返回 true。</returns>
    public bool ContainsPoint(Avalonia.Point world) => TryGetLocalPoint(world, out _);

    /// <summary>
    /// 把世界坐标逆旋转回包围盒局部坐标（左上角为原点）。几何换算只在这里维护一份，
    /// 命中测试与透明像素采样共享同一口径。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="local">局部坐标；未命中时也可能有值，调用方应以返回值判断。</param>
    /// <returns>落在包围盒内返回 true。</returns>
    public bool TryGetLocalPoint(Avalonia.Point world, out Avalonia.Point local)
    {
        var bounds = Bounds;
        local = default;

        if (Rotation == 0)
        {
            local = new Avalonia.Point(world.X - bounds.X, world.Y - bounds.Y);
            return bounds.Contains(world);
        }

        var center = bounds.Center;
        var rad = -Rotation * Math.PI / 180.0;
        var dx = world.X - center.X;
        var dy = world.Y - center.Y;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        // 逆旋转回局部坐标后仍按轴对齐包围盒判定，旋转不改包围盒口径
        var localWorld = new Avalonia.Point(
            center.X + (dx * cos - dy * sin),
            center.Y + (dx * sin + dy * cos));
        local = new Avalonia.Point(localWorld.X - bounds.X, localWorld.Y - bounds.Y);
        return bounds.Contains(localWorld);
    }

    /// <summary>
    /// 把世界坐标换算成包围盒内的归一化局部坐标，供贴图 alpha 采样使用。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="normalizedX">局部 X，0 为左边、1 为右边。</param>
    /// <param name="normalizedY">局部 Y，0 为上边、1 为下边。</param>
    /// <returns>落在包围盒内且尺寸有效时返回 true。</returns>
    public bool TryGetNormalizedPoint(Avalonia.Point world, out double normalizedX, out double normalizedY)
    {
        normalizedX = 0;
        normalizedY = 0;

        if (!TryGetLocalPoint(world, out var local))
        {
            return false;
        }

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        normalizedX = local.X / bounds.Width;
        normalizedY = local.Y / bounds.Height;
        return true;
    }
}

#endregion

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
