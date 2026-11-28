/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器基础常量、枚举、图层键与默认值
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MapEditorTool.Models;
#region 常量与枚举

/// <summary>编辑器默认参数，数值与 Unity 版 MapToolConstants 保持一致。</summary>
public static class MapToolDefaults
{
    /// <summary>关卡像素边长，4096。</summary>
    public const int LevelPixelSize = 4096;

    /// <summary>地图宽高下限：小于 256px 只剩十来个格子，没有编辑意义。</summary>
    public const int MapPixelMin = 256;

    /// <summary>地图宽高上限：场景属性、网格设置窗、PSD 导入统一按这一档约束。</summary>
    public const int MapPixelMax = 8192;

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

    /// <summary>人物模板缺省移动动画名。</summary>
    public const string DefaultMoveAnimName = "walk";

    /// <summary>循环动画重启前的默认停留秒数。</summary>
    public const double DefaultLoopRestartDelay = 3.0;

    /// <summary>离开可视地图范围时是否默认隐藏。</summary>
    public const bool DefaultHideWhenOffMap = true;
}

/// <summary>
/// 新建模板时的类型默认值：图层键、交互类型、默认占格。
/// 面板新建与配置表行还原共用同一口径，避免两处各写一套三目表达式。
/// </summary>
public static class TemplateDefaults
{
    /// <summary>按类型取默认图层键。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>图层键。</returns>
    public static string LayerKey(MapObjectType type) => type switch
    {
        MapObjectType.Building => MapLayerKeys.Building,
        MapObjectType.Character => MapLayerKeys.Character,
        _ => MapLayerKeys.Prop,
    };

    /// <summary>按类型取默认交互类型：物件点击，其余对话。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>交互类型编码。</returns>
    public static string InteractType(MapObjectType type) =>
        type == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk;

    /// <summary>
    /// 动画名空值回退：空串或全空白统一取 <see cref="MapToolDefaults.DefaultAnimName"/>，
    /// 非空值原样保留，避免各读写路径各写一份三目判断。
    /// </summary>
    /// <param name="name">原始动画名。</param>
    /// <returns>可直接使用的动画名。</returns>
    public static string AnimNameOrDefault(string? name) =>
        string.IsNullOrWhiteSpace(name) ? MapToolDefaults.DefaultAnimName : name;

    /// <summary>按类型取默认碰撞：物件不参与碰撞，建筑与人物参与碰撞。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>默认是否启用碰撞。</returns>
    public static bool CollisionEnabled(MapObjectType type) => type != MapObjectType.Prop;

    /// <summary>按类型取默认占格宽：建筑 4 格，其余 1 格。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>格子数。</returns>
    public static int PlacementWidth(MapObjectType type) =>
        type == MapObjectType.Building ? 4 : 1;

    /// <summary>按类型取默认占格高：建筑 4 格、人物 2 格、其余 1 格。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>格子数。</returns>
    public static int PlacementHeight(MapObjectType type) => type switch
    {
        MapObjectType.Building => 4,
        MapObjectType.Character => 2,
        _ => 1,
    };
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

    /// <summary>菱形格子，导出到 Unity 时按 Isometric Z as Y（多层高度）认格。</summary>
    public const string DiamondZ = "diamondz";

    /// <summary>六边形格子，对应 Unity 的 Hexagonal Cell Layout。</summary>
    public const string Hex = "hex";

    /// <summary>是否菱形几何：Isometric Z as Y 的画布几何与等距菱形完全一致，只差导出布局。</summary>
    /// <param name="shape">绘制形状值。</param>
    /// <returns>菱形几何返回 true。</returns>
    public static bool IsDiamond(string? shape) =>
        string.Equals(shape, Diamond, StringComparison.OrdinalIgnoreCase)
        || string.Equals(shape, DiamondZ, StringComparison.OrdinalIgnoreCase);

    /// <summary>是否六边形几何。</summary>
    /// <param name="shape">绘制形状值。</param>
    /// <returns>六边形返回 true。</returns>
    public static bool IsHex(string? shape) =>
        string.Equals(shape, Hex, StringComparison.OrdinalIgnoreCase);
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

/// <summary>一个数据层的完整定义：层键、资源子目录名与中文显示名。</summary>
/// <param name="Key">Unity / JSON 使用的层键。</param>
/// <param name="Folder">资源目录名（大小写与 Unity 逐字一致）。</param>
/// <param name="DisplayName">图层筛选与状态栏使用的中文名。</param>
public sealed record MapLayerDefinition(string Key, string Folder, string DisplayName);

/// <summary>
/// 八层功能分桶键，与 Unity 版 MapLayerCatalog 一致。
/// 所有“层键 ↔ 目录”“层键 ↔ 中文名”的转换都从 <see cref="Definitions"/> 派生，新增层只改一张表。
/// </summary>
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

    /// <summary>
    /// 八层定义（自底向上，与 Unity <c>MapLayerCatalog.AllLayerTypes</c> 同序）。
    /// 目录名与显示名都只在这里维护，避免三份 switch 与 UI 下拉列表各自漂移。
    /// </summary>
    public static readonly IReadOnlyList<MapLayerDefinition> Definitions = new[]
    {
        new MapLayerDefinition(Background, "Background", "背景"),
        new MapLayerDefinition(Greenery, "Greenery", "绿化"),
        new MapLayerDefinition(Road, "Road", "道路"),
        new MapLayerDefinition(Building, "Buildings", "建筑"),
        new MapLayerDefinition(Prop, "Props", "道具"),
        new MapLayerDefinition(Character, "Characters", "角色"),
        new MapLayerDefinition(Foreground, "Foreground", "前景"),
        new MapLayerDefinition(Bubble, "Bubbles", "气泡"),
    };

    /// <summary>八层键全集（自底向上，与 <see cref="Definitions"/> 同序）。</summary>
    public static readonly string[] AllKeys = Definitions.Select(item => item.Key).ToArray();

    /// <summary>八层资源目录全集（自底向上，与 <see cref="Definitions"/> 同序）。</summary>
    // ---TODO 重构：PSD 烘焙与导出统一读取此目录清单，不再各自手写字符串数组。
    public static readonly string[] AllFolders = Definitions.Select(item => item.Folder).ToArray();

    /// <summary>
    /// 图层键 → 资源子文件夹名，与 Unity <c>MapLayerCatalog.GetLayerDir</c> 的目录名逐字一致：
    /// Background / Greenery / Road / Buildings / Props / Characters / Foreground / Bubbles。
    /// </summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>目录名；未知键回退 <c>Props</c>。</returns>
    public static string FolderName(string layerKey) =>
        Find(layerKey)?.Folder ?? "Props";

    /// <summary>
    /// 目录名 → 图层键（八层目录反查），无法识别时回退道具层。
    /// </summary>
    /// <param name="folder">目录名，如 <c>Buildings</c>。</param>
    /// <returns>图层键；未知目录回退道具层。</returns>
    public static string FromFolderName(string folder) =>
        Definitions.FirstOrDefault(item => item.Folder == folder)?.Key ?? Prop;

    /// <summary>取图层显示名，供图层面板与导出使用。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>中文显示名；未知键回退「道具」。</returns>
    public static string DisplayName(string layerKey) =>
        layerKey == Collectible ? "收集物" : Find(layerKey)?.DisplayName ?? "道具";

    /// <summary>按层键精确查找定义；不做大小写或旧键归一，保持原有 switch 口径。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>命中定义；未知键返回 null。</returns>
    private static MapLayerDefinition? Find(string? layerKey) =>
        Definitions.FirstOrDefault(item => item.Key == layerKey);
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
        AnimName = TemplateDefaults.AnimNameOrDefault(AnimName),
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

