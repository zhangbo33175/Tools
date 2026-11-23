/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图物件配置表「文件夹」：夹内多张 CSV / xlsm 一起读、按图片名绑定、可编辑写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 字段定义

/// <summary>表格式。</summary>
public enum ItemTableKind
{
    /// <summary>逗号分隔文本（两行表头：列名 / 类型）。</summary>
    Csv,

    /// <summary>Excel 宏工作簿（四行表头：标题 / 列名 / 类型 / 备注）。</summary>
    Xlsm,
}

/// <summary>配置表字段键与列名别名（CSV 中文列名 / Unity xlsm 英文列名共用一套读写逻辑）。</summary>
internal static class ItemFieldNames
{
    /// <summary>ID。</summary>
    public const string Id = "Id";

    /// <summary>图片名。</summary>
    public const string Icon = "Icon";

    /// <summary>预制体名。</summary>
    public const string ResName = "ResName";

    /// <summary>模板名称。</summary>
    public const string TemplateName = "TemplateName";

    /// <summary>默认动画。</summary>
    public const string AnimName = "AnimName";

    /// <summary>是否循环。</summary>
    public const string Loop = "Loop";

    /// <summary>播放速度。</summary>
    public const string Speed = "Speed";

    /// <summary>是否可交互。</summary>
    public const string Interactable = "Interactable";

    /// <summary>交互类型。</summary>
    public const string InteractType = "InteractType";

    /// <summary>渲染层级。</summary>
    public const string SortOrder = "SortOrder";

    /// <summary>是否有碰撞。</summary>
    public const string Collision = "CollisionEnabled";

    /// <summary>对话气泡。</summary>
    public const string DialogBubble = "DialogBubble";

    /// <summary>初始朝向。</summary>
    public const string FacingDirection = "FacingDirection";

    /// <summary>显示层 / 层级档（空 = 跟随归属层）。</summary>
    public const string DisplayLayer = "DisplayLayer";

    /// <summary>是否显示阴影。</summary>
    public const string Shadow = "ShadowEnabled";

    /// <summary>是否收集物。</summary>
    public const string Collectible = "IsCollectible";

    /// <summary>重生时间。</summary>
    public const string RespawnTime = "RespawnTime";

    /// <summary>交通类别。</summary>
    public const string TrafficCategory = "TrafficCategory";

    /// <summary>启用 YSort。</summary>
    public const string YSortEnabled = "YSortEnabled";

    /// <summary>YSort 偏移。</summary>
    public const string YSortOffset = "YSortOffset";

    /// <summary>可移动。</summary>
    public const string Movable = "IsMovable";

    /// <summary>循环重启延迟。</summary>
    public const string LoopRestartDelay = "LoopRestartDelay";

    /// <summary>离开地图隐藏。</summary>
    public const string HideWhenOffMap = "HideWhenOffMap";

    /// <summary>是否传送门。</summary>
    public const string Teleport = "IsTeleport";

    /// <summary>目标场景。</summary>
    public const string TeleportScene = "TeleportTargetScene";

    /// <summary>目标 X。</summary>
    public const string TeleportX = "TeleportTargetX";

    /// <summary>目标 Y。</summary>
    public const string TeleportY = "TeleportTargetY";

    /// <summary>是否启用九宫格。</summary>
    public const string NineSliceEnabled = "NineSliceEnabled";

    /// <summary>九宫格左边距。</summary>
    public const string NineSliceLeft = "NineSliceLeft";

    /// <summary>九宫格右边距。</summary>
    public const string NineSliceRight = "NineSliceRight";

    /// <summary>九宫格上边距。</summary>
    public const string NineSliceTop = "NineSliceTop";

    /// <summary>九宫格下边距。</summary>
    public const string NineSliceBottom = "NineSliceBottom";

    /// <summary>九宫格中心填充方式。</summary>
    public const string NineSliceCenterMode = "NineSliceCenterMode";

    /// <summary>字段 ↔ 列名别名表。</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        [Id] = new[] { "ID", "TemplateId" },
        [Icon] = new[] { "Icon", "ImagePath", "图片名称" },
        [ResName] = new[] { "ResName", "资源ResDef的名字" },
        [TemplateName] = new[] { "模板名称", "TemplateName", "物件名称" },
        [AnimName] = new[] { "默认动画", "AnimName" },
        [Loop] = new[] { "是否循环", "Loop", "是否循环播放" },
        [Speed] = new[] { "播放速度", "Speed", "动画播放速度" },
        [Interactable] = new[] { "是否可交互", "Interactable" },
        [InteractType] = new[] { "交互类型", "InteractType" },
        [SortOrder] = new[] { "渲染层级", "SortOrder" },
        [Collision] = new[] { "是否有碰撞", "CollisionEnabled" },
        [DialogBubble] = new[] { "对话气泡", "DialogBubble" },
        [FacingDirection] = new[] { "初始朝向", "FacingDirection" },
        [DisplayLayer] = new[] { "显示层", "层级档", "显示层级", "DisplayLayer" },
        [Shadow] = new[] { "是否显示阴影", "ShadowEnabled" },
        [Collectible] = new[] { "是否收集物", "IsCollectible" },
        [RespawnTime] = new[] { "重生时间", "RespawnTime" },
        [TrafficCategory] = new[] { "交通类别", "TrafficCategory" },
        [YSortEnabled] = new[] { "启用YSort", "YSortEnabled" },
        [YSortOffset] = new[] { "YSort偏移", "YSortOffset" },
        [Movable] = new[] { "可移动", "IsMovable" },
        [LoopRestartDelay] = new[] { "循环重启延迟", "LoopRestartDelay" },
        [HideWhenOffMap] = new[] { "离开地图隐藏", "HideWhenOffMap" },
        [Teleport] = new[] { "是否传送门", "IsTeleport" },
        [TeleportScene] = new[] { "目标场景", "TeleportTargetScene" },
        [TeleportX] = new[] { "目标X", "TeleportTargetX" },
        [TeleportY] = new[] { "目标Y", "TeleportTargetY" },
        [NineSliceEnabled] = new[] { "九宫格", "是否九宫格", "NineSliceEnabled", "NeedNineSlice" },
        [NineSliceLeft] = new[] { "九宫格左边距", "左边距", "NineSliceLeft" },
        [NineSliceRight] = new[] { "九宫格右边距", "右边距", "NineSliceRight" },
        [NineSliceTop] = new[] { "九宫格上边距", "上边距", "NineSliceTop" },
        [NineSliceBottom] = new[] { "九宫格下边距", "下边距", "NineSliceBottom" },
        [NineSliceCenterMode] = new[] { "九宫格中心", "中心填充", "NineSliceCenterMode" },
    };

    /// <summary>按数字列写回的字段（xlsm 里写成数值而不是文本）。</summary>
    public static readonly IReadOnlySet<string> NumericFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Speed, SortOrder, RespawnTime, YSortOffset, LoopRestartDelay, TeleportX, TeleportY,
        NineSliceLeft, NineSliceRight, NineSliceTop, NineSliceBottom,
    };

    /// <summary>类型行 / 备注行里出现的词，用来跳过表头。</summary>
    public static readonly IReadOnlySet<string> TypeTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "int", "string", "bool", "float", "double", "long", "备注", "序号", "类型", "说明",
    };
}

#endregion

#region 单张表

/// <summary>夹内的一张配置表（CSV 或 xlsm）：列名映射 + 数据行。</summary>
public sealed class ItemTableFile
{
    internal ItemTableFile(string path, ItemTableKind kind)
    {
        Path = path;
        Kind = kind;
        Name = System.IO.Path.GetFileName(path);
    }

    /// <summary>绝对路径。</summary>
    public string Path { get; }

    /// <summary>文件名（界面提示用）。</summary>
    public string Name { get; }

    /// <summary>格式。</summary>
    public ItemTableKind Kind { get; }

    /// <summary>本次会话中是否被改过。</summary>
    public bool Dirty { get; set; }

    /// <summary>数据行。</summary>
    public List<ItemElementRow> Rows { get; } = new();

    /// <summary>列名（按列序，空串表示该列无名字）。</summary>
    public List<string> ColumnNames { get; } = new();

    /// <summary>字段键 → 列下标（0 起）。</summary>
    internal Dictionary<string, int> ColumnIndex { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>CSV 的两行原始表头。</summary>
    internal List<string> HeaderLines { get; } = new();

    /// <summary>xlsm 的工作表 XML（保存时就地改单元格）。</summary>
    internal XDocument? SheetDocument { get; set; }

    /// <summary>xlsm 的表头行号。</summary>
    internal int HeaderRowNumber { get; set; }

    /// <summary>新建一行（列数对齐本表，行号接在最后一行之后）。</summary>
    /// <returns>新行。</returns>
    internal ItemElementRow CreateRow()
    {
        var row = new ItemElementRow { RowNumber = NextRowNumber() };
        for (var index = 0; index < ColumnNames.Count; index++)
        {
            row.Cells.Add(string.Empty);
        }

        return row;
    }

    /// <summary>本表下一个可用行号。</summary>
    internal int NextRowNumber()
    {
        if (Rows.Count == 0)
        {
            return Kind == ItemTableKind.Csv ? 3 : HeaderRowNumber + 1;
        }

        return Rows.Max(item => item.RowNumber) + 1;
    }

    /// <summary>按字段键把行值写回 <see cref="ItemElementRow.Cells"/>（CSV 用）。</summary>
    /// <param name="row">数据行。</param>
    internal void SyncCells(ItemElementRow row)
    {
        while (row.Cells.Count < ColumnNames.Count)
        {
            row.Cells.Add(string.Empty);
        }

        SetField(row, ItemFieldNames.Id, row.Id);
        SetField(row, ItemFieldNames.Icon, row.Icon);
        SetField(row, ItemFieldNames.ResName, row.ResName);
        SetField(row, ItemFieldNames.TemplateName, row.TemplateName);
        SetField(row, ItemFieldNames.AnimName, row.AnimName);
        SetField(row, ItemFieldNames.Loop, row.Loop ? "True" : "False");
        SetField(row, ItemFieldNames.Speed, Number(row.Speed));
        SetField(row, ItemFieldNames.Interactable, row.Interactable ? "True" : "False");
        SetField(row, ItemFieldNames.InteractType, row.InteractType);
        SetField(row, ItemFieldNames.SortOrder, row.SortOrder.ToString(CultureInfo.InvariantCulture));
        SetField(row, ItemFieldNames.Collision, row.CollisionEnabled ? "True" : "False");
        SetField(row, ItemFieldNames.DialogBubble, row.DialogBubble);
        SetField(row, ItemFieldNames.FacingDirection, row.FacingDirection);
        SetField(row, ItemFieldNames.DisplayLayer, row.DisplayLayer);
        SetField(row, ItemFieldNames.Shadow, row.ShadowEnabled ? "True" : "False");
        SetField(row, ItemFieldNames.Collectible, row.IsCollectible ? "True" : "False");
        SetField(row, ItemFieldNames.RespawnTime, Number(row.RespawnTime));
        SetField(row, ItemFieldNames.TrafficCategory, row.TrafficCategory);
        SetField(row, ItemFieldNames.YSortEnabled, row.YSortEnabled ? "True" : "False");
        SetField(row, ItemFieldNames.YSortOffset, Number(row.YSortOffset));
        SetField(row, ItemFieldNames.Movable, row.IsMovable ? "True" : "False");
        SetField(row, ItemFieldNames.LoopRestartDelay, Number(row.LoopRestartDelay));
        SetField(row, ItemFieldNames.HideWhenOffMap, row.HideWhenOffMap ? "True" : "False");
        SetField(row, ItemFieldNames.Teleport, row.IsTeleport ? "True" : "False");
        SetField(row, ItemFieldNames.TeleportScene, row.TeleportTargetScene);
        SetField(row, ItemFieldNames.TeleportX, Number(row.TeleportTargetX));
        SetField(row, ItemFieldNames.TeleportY, Number(row.TeleportTargetY));
        SetField(row, ItemFieldNames.NineSliceEnabled, row.NineSliceEnabled ? "True" : "False");
        SetField(row, ItemFieldNames.NineSliceLeft, row.NineSliceLeft.ToString(CultureInfo.InvariantCulture));
        SetField(row, ItemFieldNames.NineSliceRight, row.NineSliceRight.ToString(CultureInfo.InvariantCulture));
        SetField(row, ItemFieldNames.NineSliceTop, row.NineSliceTop.ToString(CultureInfo.InvariantCulture));
        SetField(row, ItemFieldNames.NineSliceBottom, row.NineSliceBottom.ToString(CultureInfo.InvariantCulture));
        SetField(row, ItemFieldNames.NineSliceCenterMode, NineSliceModes.ToDisplay(row.NineSliceCenterMode));
    }

    /// <summary>按字段键读/写单元格文本。</summary>
    /// <param name="row">数据行。</param>
    /// <param name="field">字段键。</param>
    /// <param name="value">新值；null 表示只读。</param>
    /// <returns>当前值。</returns>
    internal string? FieldValue(ItemElementRow row, string field, string? value = null)
    {
        var index = ColumnIndex.TryGetValue(field, out var column) ? column : -1;
        if (index < 0 || index >= row.Cells.Count)
        {
            return null;
        }

        if (value is not null)
        {
            row.Cells[index] = value;
        }

        return row.Cells[index];
    }

    private void SetField(ItemElementRow row, string field, string value) =>
        FieldValue(row, field, value ?? string.Empty);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

#endregion

#region 表集合

/// <summary>
/// 「地图物件配置表文件夹」：读取夹内所有 CSV / xlsm（顶层，不进备份子目录），
/// 合并成一套按图片名查找的行集合；保存时按各自文件格式写回。
/// </summary>
public sealed class ItemConfigSet
{
    #region 常量

    /// <summary>新建行时优先写的汇总 CSV 文件名。</summary>
    private const string PreferredCsvName = "ItemElementConfig.csv";

    /// <summary>新建 CSV 时的列口径（与 ItemElementConfig.csv 一致）。</summary>
    private static readonly string[] CanonicalColumns =
    {
        "#", "ID", "Icon", "ResName", "模板名称", "默认动画", "是否循环", "播放速度", "是否可交互", "交互类型",
        "渲染层级", "是否有碰撞", "对话气泡", "初始朝向", "是否显示阴影", "是否收集物", "重生时间", "交通类别",
        "启用YSort", "YSort偏移", "可移动", "循环重启延迟", "离开地图隐藏", "是否传送门", "目标场景", "目标X", "目标Y",
        "九宫格", "九宫格左边距", "九宫格右边距", "九宫格上边距", "九宫格下边距", "九宫格中心",
    };

    /// <summary>新建 CSV 时的类型行。</summary>
    private static readonly string[] CanonicalTypes =
    {
        "string", "string", "string", "string", "string", "string", "bool", "float", "bool", "string", "int", "bool",
        "string", "string", "bool", "bool", "float", "string", "bool", "float", "bool", "float", "bool", "bool",
        "string", "float", "float", "bool", "int", "int", "int", "int", "string",
    };

    #endregion

    #region 字段

    private readonly Dictionary<string, ItemElementRow> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ItemElementRow, ItemTableFile> _owner = new();

    #endregion

    #region 构造

    private ItemConfigSet(string directory)
    {
        Directory = directory;
    }

    #endregion

    #region 属性

    /// <summary>文件夹绝对路径。</summary>
    public string Directory { get; }

    /// <summary>夹内的表（按文件名排序，汇总 CSV 靠前）。</summary>
    public List<ItemTableFile> Files { get; } = new();

    /// <summary>主表（打开配置表 / 新建行落到这里）。</summary>
    public ItemTableFile? Primary => Files.FirstOrDefault();

    /// <summary>主表路径（可能为空）。</summary>
    public string PrimaryPath => Primary?.Path ?? string.Empty;

    /// <summary>所有数据行。</summary>
    public IEnumerable<ItemElementRow> Rows => Files.SelectMany(file => file.Rows);

    /// <summary>总行数。</summary>
    public int RowCount => Files.Sum(file => file.Rows.Count);

    /// <summary>夹内表名摘要。</summary>
    public string Describe() => string.Join("、", Files.Select(file => $"{file.Name}({file.Rows.Count})"));

    #endregion

    #region 读取

    /// <summary>读取文件夹里的全部配置表；不存在 / 没有可用表时返回 null。</summary>
    /// <param name="directory">文件夹绝对路径。</param>
    /// <returns>表集合。</returns>
    public static ItemConfigSet? LoadDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
        {
            return null;
        }

        var set = new ItemConfigSet(System.IO.Path.GetFullPath(directory));
        var paths = System.IO.Directory
            .EnumerateFiles(set.Directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => System.IO.Path.GetFileName(path).Equals(PreferredCsvName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => System.IO.Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in paths)
        {
            if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("TableTemplate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var file = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? CsvTableReader.Read(path)
                : ItemXlsmReader.Read(path);

            // 只收「物件配置表」：列里得能找到图片名 / 模板名 / ID 之一
            if (file is not null && file.ColumnIndex.ContainsKey(ItemFieldNames.Icon))
            {
                set.Files.Add(file);
            }
        }

        if (set.Files.Count == 0)
        {
            return null;
        }

        set.RebuildIndex();
        return set;
    }

    /// <summary>按图片名取行（先查主表，再查其余表）。</summary>
    /// <param name="imageName">图片名（可含扩展名）。</param>
    /// <returns>命中的行，未命中为 null。</returns>
    public ItemElementRow? Find(string? imageName)
    {
        var key = Normalize(imageName);
        if (key.Length == 0)
        {
            return null;
        }

        if (_index.TryGetValue(key, out var row))
        {
            return row;
        }

        var trimmed = key.TrimEnd('_');
        return _index.TryGetValue(trimmed, out row) ? row : null;
    }

    /// <summary>行来自哪张表。</summary>
    /// <param name="row">数据行。</param>
    /// <returns>所在表；未知为 null。</returns>
    public ItemTableFile? FileOf(ItemElementRow row) => _owner.TryGetValue(row, out var file) ? file : null;

    /// <summary>
    /// 按左侧分类（全部 / 背景 / 建筑 / 人物 / 物件 / 猫猫）取它对应的那张表 ——
    /// Unity 侧就是「分类按钮 ↔ 配置表」：建筑 → TableBuildings、人物 → TableCharacters、
    /// 物件 → TableProps、猫猫 → TableCats（没有就按 <c>cat_</c> 行回退）。
    /// </summary>
    /// <param name="category">分类显示名。</param>
    /// <returns>分类对应的表；「全部」或找不到为 null（null 表示看全部表）。</returns>
    public ItemTableFile? FileForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || category == "全部")
        {
            return null;
        }

        var (names, prefix) = category switch
        {
            "建筑" => (new[] { "TableBuildings", "TableBuilding", "Buildings" }, "building_"),
            "人物" => (new[] { "TableCharacters", "TableCharacter", "Characters" }, "char_"),
            "物件" => (new[] { "TableProps", "TableProp", "Props" }, "prop_"),
            "猫猫" => (new[] { "TableCats", "TableCat", "Cats" }, "cat_"),
            "背景" => (new[] { "TableBackgrounds", "TableBackground", "Backgrounds" }, "bg_"),
            _ => (Array.Empty<string>(), string.Empty),
        };

        foreach (var hint in names)
        {
            var byName = Files.FirstOrDefault(file =>
                System.IO.Path.GetFileNameWithoutExtension(file.Name)
                    .Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        // 没有同名表：退化为「表里有没有这类行」（如猫猫行可能混在别的表里）
        return prefix.Length == 0
            ? null
            : Files.FirstOrDefault(file => file.Rows.Any(row => row.Icon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>行内容改过之后重建查找索引（改了图片名要重新挂到新名字下）。</summary>
    public void RefreshIndexes() => RebuildIndex();

    /// <summary>
    /// 把配置表行铺成可直接摆放 / 编辑的模板（物品元件编辑与场景资源列表共用同一套映射）。
    /// </summary>
    /// <param name="row">表行。</param>
    /// <param name="type">该行所属分类。</param>
    /// <returns>由表行转换出的模板。</returns>
    public static ObjectTemplate CreateTemplate(ItemElementRow row, MapObjectType type)
    {
        return new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            TemplateId = row.Id,
            TemplateName = row.TemplateName,
            ResName = row.ResName,
            ImagePath = row.Icon,
            AnimName = string.IsNullOrWhiteSpace(row.AnimName) ? MapToolDefaults.DefaultAnimName : row.AnimName,
            Loop = row.Loop,
            Speed = row.Speed,
            SortOrder = row.SortOrder,
            ShadowEnabled = row.ShadowEnabled,
            IsCollectible = row.IsCollectible,
            RespawnTime = row.RespawnTime,
            Interactable = row.Interactable,
            InteractType = string.IsNullOrWhiteSpace(row.InteractType)
                ? (type == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk)
                : row.InteractType,
            CollisionEnabled = row.CollisionEnabled,
            DialogBubble = row.DialogBubble,
            FacingDirection = row.FacingDirection,
            DisplayLayer = row.DisplayLayer,
            TrafficCategory = row.TrafficCategory,
            YSortEnabled = row.YSortEnabled,
            YSortOffset = row.YSortOffset,
            IsMovable = row.IsMovable,
            LoopRestartDelay = row.LoopRestartDelay,
            HideWhenOffMap = row.HideWhenOffMap,
            IsTeleport = row.IsTeleport,
            TeleportTargetScene = row.TeleportTargetScene,
            TeleportTargetX = row.TeleportTargetX,
            TeleportTargetY = row.TeleportTargetY,
            DefaultWidth = type == MapObjectType.Building ? 4 : 1,
            DefaultHeight = type == MapObjectType.Building ? 4 : type == MapObjectType.Character ? 2 : 1,
            NineSlice = new NineSliceSettings
            {
                Enabled = row.NineSliceEnabled,
                Left = Math.Max(0, row.NineSliceLeft),
                Right = Math.Max(0, row.NineSliceRight),
                Top = Math.Max(0, row.NineSliceTop),
                Bottom = Math.Max(0, row.NineSliceBottom),
                CenterMode = NineSliceModes.ToCode(row.NineSliceCenterMode),
            },
            LayerTypeKey = type switch
            {
                MapObjectType.Building => MapLayerKeys.Building,
                MapObjectType.Character => MapLayerKeys.Character,
                _ => MapLayerKeys.Prop,
            },
        };
    }

    #endregion

    #region 增删改

    /// <summary>新增或更新一行（按图片名匹配，跨表）；新行优先落到 <paramref name="preferredFile"/>（当前分类那张表），没有就落主表。</summary>
    /// <param name="template">模板数据。</param>
    /// <param name="preferredFile">新行要落到的表（分类表）；null 表示主表。</param>
    /// <returns>写入的行。</returns>
    public ItemElementRow Upsert(ObjectTemplate template, ItemTableFile? preferredFile = null)
    {
        var name = string.IsNullOrWhiteSpace(template.ResName) ? template.TemplateName : template.ResName;
        var row = Find(name);
        ItemTableFile file;
        if (row is null)
        {
            file = preferredFile ?? Primary ?? CreatePrimary();
            row = file.CreateRow();
            file.Rows.Add(row);
            file.Dirty = true;
            _owner[row] = file;
        }
        else
        {
            file = _owner.TryGetValue(row, out var owner) ? owner : (Primary ?? Files[0]);
        }

        ApplyTemplate(row, template, name);
        file.Dirty = true;
        RebuildIndex();
        return row;
    }

    /// <summary>按图片名删行（只删第一张命中的表）。</summary>
    /// <param name="imageName">图片名。</param>
    /// <returns>是否删掉了。</param>
    public bool Remove(string? imageName)
    {
        var row = Find(imageName);
        if (row is null)
        {
            return false;
        }

        var file = _owner.TryGetValue(row, out var owner) ? owner : null;
        if (file is null)
        {
            return false;
        }

        file.Rows.Remove(row);
        file.Dirty = true;
        _owner.Remove(row);
        RebuildIndex();
        return true;
    }

    /// <summary>按分类生成下一个 ID：建筑 2xxx / 人物 1xxx / 物件 3xxx。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>新的 ID 文本。</returns>
    public string NextId(MapObjectType type)
    {
        var start = type switch
        {
            MapObjectType.Character => 1000,
            MapObjectType.Building => 2000,
            _ => 3000,
        };

        var max = start;
        foreach (var row in Rows)
        {
            if (int.TryParse(row.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && id > max && id / 1000 == start / 1000)
            {
                max = id;
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region 保存

    /// <summary>把所有改过的表写回磁盘（CSV 重写文件，xlsm 就地改单元格）。</summary>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否全部写成功。</returns>
    public bool TrySave(out string error)
    {
        error = string.Empty;
        foreach (var file in Files.Where(file => file.Dirty))
        {
            var ok = file.Kind == ItemTableKind.Csv
                ? CsvTableWriter.Write(file, out var fileError)
                : ItemXlsmWriter.Write(file, out fileError);
            if (!ok)
            {
                error = $"{file.Name}：{fileError}";
                return false;
            }

            file.Dirty = false;
        }

        return true;
    }

    #endregion

    #region 内部实现

    private void RebuildIndex()
    {
        _index.Clear();
        _owner.Clear();
        // Unity 侧真正吃的是分类 xlsm（TableProps / TableBuildings / TableCharacters），
        // 汇总 CSV 只是导出副本：同名的先认 xlsm，CSV 兜底。
        foreach (var file in Files.Where(file => file.Kind == ItemTableKind.Xlsm)
                     .Concat(Files.Where(file => file.Kind == ItemTableKind.Csv)))
        {
            foreach (var row in file.Rows)
            {
                _owner[row] = file;
                var icon = Normalize(row.Icon);
                if (icon.Length > 0)
                {
                    _index.TryAdd(icon, row);
                }

                var name = Normalize(row.TemplateName);
                if (name.Length > 0)
                {
                    _index.TryAdd(name, row);
                }
            }
        }
    }

    private ItemTableFile CreatePrimary()
    {
        var path = System.IO.Path.Combine(Directory, PreferredCsvName);
        var file = CsvTableReader.CreateEmpty(path, CanonicalColumns, CanonicalTypes);
        Files.Insert(0, file);
        return file;
    }

    private static void ApplyTemplate(ItemElementRow row, ObjectTemplate template, string name)
    {
        row.Id = string.IsNullOrWhiteSpace(template.TemplateId) ? row.Id : template.TemplateId;
        row.Icon = string.IsNullOrWhiteSpace(template.ResName) ? (string.IsNullOrWhiteSpace(row.Icon) ? name : row.Icon) : template.ResName;
        row.TemplateName = string.IsNullOrWhiteSpace(template.TemplateName) ? name : template.TemplateName;
        row.AnimName = string.IsNullOrWhiteSpace(template.AnimName) ? MapToolDefaults.DefaultAnimName : template.AnimName;
        row.Loop = template.Loop;
        row.Speed = template.Speed;
        row.SortOrder = template.SortOrder;
        row.ShadowEnabled = template.ShadowEnabled;
        row.IsCollectible = template.IsCollectible;
        row.RespawnTime = template.RespawnTime;
        row.Interactable = template.Interactable;
        row.InteractType = template.InteractType;
        row.CollisionEnabled = template.CollisionEnabled;
        row.DialogBubble = template.DialogBubble;
        row.FacingDirection = template.FacingDirection;
        row.TrafficCategory = template.TrafficCategory;
        row.YSortEnabled = template.YSortEnabled;
        row.YSortOffset = template.YSortOffset;
        row.IsMovable = template.IsMovable;
        row.LoopRestartDelay = template.LoopRestartDelay;
        row.HideWhenOffMap = template.HideWhenOffMap;
        row.IsTeleport = template.IsTeleport;
        row.TeleportTargetScene = template.TeleportTargetScene;
        row.TeleportTargetX = template.TeleportTargetX;
        row.TeleportTargetY = template.TeleportTargetY;
        var nineSlice = template.NineSlice ?? new NineSliceSettings();
        row.NineSliceEnabled = nineSlice.Enabled;
        row.NineSliceLeft = Math.Max(0, nineSlice.Left);
        row.NineSliceRight = Math.Max(0, nineSlice.Right);
        row.NineSliceTop = Math.Max(0, nineSlice.Top);
        row.NineSliceBottom = Math.Max(0, nineSlice.Bottom);
        row.NineSliceCenterMode = NineSliceModes.ToCode(nineSlice.CenterMode);
        if (string.IsNullOrWhiteSpace(row.ResName))
        {
            row.ResName = template.ResName;
        }
    }

    private static string Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(name!.Trim()).ToLowerInvariant();

    #endregion
}

#endregion
