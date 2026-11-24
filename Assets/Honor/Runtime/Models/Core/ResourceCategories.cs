/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceCategories.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源分类元数据：显示名、扫描键、资源前缀、配置表名与物件类型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapEditorTool.Models;

/// <summary>
/// 一个资源分类的完整定义。
/// UI 用它生成筛选按钮与显示名，持久化层用它定位配置表、按资源前缀回退。
/// </summary>
/// <param name="DisplayName">左侧筛选与提示中使用的中文名。</param>
/// <param name="Key">文件名推断出的内部键，如 <c>bg</c>、<c>building</c>。</param>
/// <param name="AssetPrefix">资源名回退前缀，如 <c>bg_</c>。</param>
/// <param name="TableHints">配置表文件名候选（不含扩展名）。</param>
/// <param name="ObjectType">该分类对应的场景物件类型。</param>
/// <param name="RequiresOwnTable">
/// 是否必须使用独立分类表：true 时不会退化为「按行前缀混在其他表里」，
/// 收集物必须落在 <c>TableCollectibles</c>，避免与物件表混用。
/// </param>
public sealed record ResourceCategoryDefinition(
    string DisplayName,
    string Key,
    string AssetPrefix,
    string[] TableHints,
    MapObjectType ObjectType,
    bool RequiresOwnTable = false);

/// <summary>
/// 资源分类的唯一目录。分类显示名、扫描键、资源前缀与配置表名只在这里定义，
/// 避免面板筛选、层级显示与配置表定位各维护一份中文常量。
/// </summary>
public static class ResourceCategories
{
    /// <summary>「全部」筛选显示名；它不对应具体配置表。</summary>
    public const string AllDisplayName = "全部";

    /// <summary>背景分类的扫描键。</summary>
    public const string BackgroundKey = "bg";

    /// <summary>建筑分类的扫描键。</summary>
    public const string BuildingKey = "building";

    /// <summary>人物分类的扫描键。</summary>
    public const string CharacterKey = "character";

    /// <summary>物件分类的扫描键，也是无法识别时的默认分类。</summary>
    public const string PropKey = "prop";

    /// <summary>收集物分类的扫描键。</summary>
    public const string CollectibleKey = "collectible";

    /// <summary>地图目录下收集物图片的专用文件夹名。</summary>
    public const string CollectibleFolder = "Collectibles";

    /// <summary>猫猫分类的扫描键。</summary>
    public const string CatKey = "cat";

    /// <summary>背景分类定义。</summary>
    public static readonly ResourceCategoryDefinition BackgroundCategory = new(
        "背景",
        BackgroundKey,
        "bg_",
        new[] { "TableBackgrounds", "TableBackground", "Backgrounds" },
        MapObjectType.Prop);

    /// <summary>建筑分类定义。</summary>
    public static readonly ResourceCategoryDefinition BuildingCategory = new(
        "建筑",
        BuildingKey,
        "building_",
        new[] { "TableBuildings", "TableBuilding", "Buildings" },
        MapObjectType.Building);

    /// <summary>人物分类定义。</summary>
    public static readonly ResourceCategoryDefinition CharacterCategory = new(
        "人物",
        CharacterKey,
        "char_",
        new[] { "TableCharacters", "TableCharacter", "Characters" },
        MapObjectType.Character);

    /// <summary>物件分类定义。</summary>
    public static readonly ResourceCategoryDefinition PropCategory = new(
        "物件",
        PropKey,
        "prop_",
        new[] { "TableProps", "TableProp", "Props" },
        MapObjectType.Prop);

    /// <summary>
    /// 收集物分类定义：素材固定放在 <c>MapData\Map\Collectibles</c>，
    /// 表固定为 <c>TableCollectibles</c>；场景层仍按道具层落位，只是实例带收集物标记。
    /// </summary>
    public static readonly ResourceCategoryDefinition CollectibleCategory = new(
        "收集物",
        CollectibleKey,
        "collect_",
        new[] { "TableCollectibles", "TableCollectible", "Collectibles" },
        MapObjectType.Prop,
        RequiresOwnTable: true);

    /// <summary>猫猫分类定义；猫猫在场景中按人物类型摆放。</summary>
    public static readonly ResourceCategoryDefinition CatCategory = new(
        "猫猫",
        CatKey,
        "cat_",
        new[] { "TableCats", "TableCat", "Cats" },
        MapObjectType.Character);

    /// <summary>参与筛选的具体分类，顺序即左侧按钮顺序。</summary>
    public static readonly IReadOnlyList<ResourceCategoryDefinition> ConfiguredCategories =
        new[]
        {
            BackgroundCategory,
            BuildingCategory,
            CharacterCategory,
            PropCategory,
            CollectibleCategory,
            CatCategory,
        };

    /// <summary>筛选按钮文案：首项为「全部」，其后为具体分类。</summary>
    public static readonly string[] FilterNames =
        new[] { AllDisplayName }.Concat(ConfiguredCategories.Select(item => item.DisplayName)).ToArray();

    /// <summary>按中文显示名查找分类定义。</summary>
    /// <param name="displayName">中文显示名。</param>
    /// <returns>分类定义；「全部」或未知名称返回 null。</returns>
    public static ResourceCategoryDefinition? FindByDisplay(string? displayName) =>
        ConfiguredCategories.FirstOrDefault(item => item.DisplayName == displayName);

    /// <summary>按内部扫描键查找分类定义。</summary>
    /// <param name="key">扫描键。</param>
    /// <returns>分类定义；未知键返回 null。</returns>
    public static ResourceCategoryDefinition? FindByKey(string? key) =>
        ConfiguredCategories.FirstOrDefault(item => item.Key == key);

    /// <summary>扫描键对应的中文显示名，未知键回退到「物件」。</summary>
    /// <param name="key">扫描键。</param>
    public static string DisplayNameOfKey(string? key) =>
        FindByKey(key)?.DisplayName ?? PropCategory.DisplayName;

    /// <summary>扫描键对应的场景物件类型，未知键回退到道具。</summary>
    /// <param name="key">扫描键。</summary>
    public static MapObjectType ObjectTypeOfKey(string? key) =>
        FindByKey(key)?.ObjectType ?? MapObjectType.Prop;

    /// <summary>
    /// 资源文件名或路径直接对应的场景物件类型。
    /// 面板、表行与未配置资源都走这一入口，避免各处重复拼 <c>Classify → ObjectTypeOfKey</c>。
    /// </summary>
    /// <param name="name">文件名或含目录的路径。</param>
    /// <returns>分类对应的物件类型；无法识别时回退道具。</returns>
    // ---TODO 重构：资源名到物件类型的统一入口，调用方不再自行拼 Classify + ObjectTypeOfKey。
    public static MapObjectType ObjectTypeOfName(string? name) => ObjectTypeOfKey(Classify(name));

    /// <summary>当前筛选是否为「全部」。</summary>
    /// <param name="displayName">筛选显示名。</param>
    public static bool IsAll(string? displayName) => displayName == AllDisplayName;

    /// <summary>背景分类的文件名关键词（除 <c>bg_</c> 前缀外）。</summary>
    private static readonly string[] BackgroundTokens = { "background", "grass", "ground" };

    /// <summary>建筑分类的文件名关键词。</summary>
    private static readonly string[] BuildingTokens = { "building", "house", "tower", "wall" };

    /// <summary>人物分类的文件名关键词。</summary>
    private static readonly string[] CharacterTokens = { "char", "player", "npc", "person" };

    /// <summary>
    /// 按资源文件名推断分类键。面板筛选、层级回退与 CSV 导出共用这一份关键词表，
    /// 避免分类规则分散在 UI 各处。
    /// </summary>
    /// <param name="name">文件名或含目录的路径。</param>
    /// <returns>分类扫描键；无法识别时回退 <see cref="PropKey"/>。</returns>
    public static string Classify(string? name)
    {
        // 目录优先：MapData\Map\Collectibles 下的图片无论叫什么名字都算收集物
        var folder = Path.GetFileName(Path.GetDirectoryName(name ?? string.Empty));
        if (string.Equals(folder, CollectibleFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(folder, CollectibleKey, StringComparison.OrdinalIgnoreCase))
        {
            return CollectibleKey;
        }

        var lower = ResourceNames.Stem(name);

        if (lower.StartsWith(CollectibleCategory.AssetPrefix, StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("collectible_", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("collectible", StringComparison.Ordinal))
        {
            return CollectibleKey;
        }

        if (lower.StartsWith(BackgroundCategory.AssetPrefix, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(lower, BackgroundTokens))
        {
            return BackgroundKey;
        }

        if (ContainsAny(lower, BuildingTokens))
        {
            return BuildingKey;
        }

        if (lower.Contains(CatKey, StringComparison.Ordinal))
        {
            return CatKey;
        }

        if (ContainsAny(lower, CharacterTokens))
        {
            return CharacterKey;
        }

        return PropKey;
    }

    /// <summary>按场景物件类型取默认资源前缀，供新建资源命名使用。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>带下划线的资源前缀。</returns>
    public static string AssetPrefixOfObjectType(MapObjectType type) => type switch
    {
        MapObjectType.Building => BuildingCategory.AssetPrefix,
        MapObjectType.Character => CharacterCategory.AssetPrefix,
        _ => PropCategory.AssetPrefix,
    };

    /// <summary>
    /// 资源目录名或图层键转中文显示名。目录名（<c>Buildings</c>）与图层键（<c>building</c>）
    /// 都接受，大小写不敏感；人物与物件沿用资源页文案「人物 / 物件」。
    /// </summary>
    /// <param name="layerOrFolder">目录名或图层键。</param>
    /// <returns>中文显示名；无法识别返回 null。</returns>
    public static string? LayerDisplayName(string? layerOrFolder)
    {
        var text = layerOrFolder?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        if (string.Equals(text, CollectibleFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, CollectibleKey, StringComparison.OrdinalIgnoreCase))
        {
            return CollectibleCategory.DisplayName;
        }

        foreach (var key in MapLayerKeys.AllKeys)
        {
            if (!string.Equals(key, text, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(MapLayerKeys.FolderName(key), text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return key switch
            {
                MapLayerKeys.Character => CharacterCategory.DisplayName,
                MapLayerKeys.Prop => PropCategory.DisplayName,
                _ => MapLayerKeys.DisplayName(key),
            };
        }

        return null;
    }

    /// <summary>文本是否命中任一关键词（入参已转小写）。</summary>
    /// <param name="text">小写文本。</param>
    /// <param name="tokens">小写关键词。</param>
    /// <returns>命中任一关键词返回 true。</returns>
    private static bool ContainsAny(string text, IEnumerable<string> tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
}
