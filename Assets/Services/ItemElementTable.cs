/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementTable.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件元件配置表（ItemElementConfig.csv）读写与按名字绑定
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 行模型

/// <summary>
/// 物件元件配置表的一行（<c>ItemElementConfig.csv</c>）。
/// 列口径与 Unity 侧策划表一致（标题行 + 列名行 + 类型行，之后为数据）。
/// </summary>
public sealed class ItemElementRow
{
    /// <summary>配置表行号（1 起）。</summary>
    public int RowNumber { get; set; }

    /// <summary>ID（建筑 2xxx / 人物 1xxx / 物件 3xxx）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>图片名 / 资源名（与 Map 目录下的 PNG 同名）。</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>预制体名（Unity 侧 <c>GameObject_Building</c> 等）。</summary>
    public string ResName { get; set; } = string.Empty;

    /// <summary>模板名称。</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>默认动画。</summary>
    public string AnimName { get; set; } = "Idle";

    /// <summary>是否循环。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>是否可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>交互类型。</summary>
    public string InteractType { get; set; } = string.Empty;

    /// <summary>渲染层级（SortOrder）。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否有碰撞。</summary>
    public bool CollisionEnabled { get; set; }

    /// <summary>对话气泡。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = string.Empty;

    /// <summary>显示层 / 层级档（空 = 跟随归属层）。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>是否显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>是否收集物。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>重生时间（秒）。</summary>
    public double RespawnTime { get; set; }

    /// <summary>交通类别。</summary>
    public string TrafficCategory { get; set; } = string.Empty;

    /// <summary>启用 YSort。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>YSort 偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>循环重启延迟。</summary>
    public double LoopRestartDelay { get; set; }

    /// <summary>离开地图隐藏。</summary>
    public bool HideWhenOffMap { get; set; } = true;

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>是否启用九宫格（独立版扩展；表里没有该列时由 NineSliceConfig.json 兜底）。</summary>
    public bool NineSliceEnabled { get; set; }

    /// <summary>九宫格左边距（素材原始像素）。</summary>
    public int NineSliceLeft { get; set; }

    /// <summary>九宫格右边距（素材原始像素）。</summary>
    public int NineSliceRight { get; set; }

    /// <summary>九宫格上边距（素材原始像素）。</summary>
    public int NineSliceTop { get; set; }

    /// <summary>九宫格下边距（素材原始像素）。</summary>
    public int NineSliceBottom { get; set; }

    /// <summary>九宫格中心填充方式：stretch / tile。</summary>
    public string NineSliceCenterMode { get; set; } = NineSliceModes.Stretch;

    /// <summary>原始单元格（按列下标），保存时按列回写，未识别列原样保留。</summary>
    public List<string> Cells { get; } = new();

    /// <summary>把行数据套到模板上（未配置资源选中时的建议值）。</summary>
    /// <param name="template">目标模板（通常是新建的空模板）。</param>
    public void ApplyToTemplate(ObjectTemplate template)
    {
        if (!string.IsNullOrWhiteSpace(Id) && int.TryParse(Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            template.TemplateId = Id;
        }

        if (!string.IsNullOrWhiteSpace(TemplateName))
        {
            template.TemplateName = TemplateName;
        }

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            template.ResName = Icon;
        }

        template.AnimName = string.IsNullOrWhiteSpace(AnimName) ? MapToolDefaults.DefaultAnimName : AnimName;
        template.Loop = Loop;
        template.Speed = Speed;
        template.SortOrder = SortOrder;
        template.ShadowEnabled = ShadowEnabled;
        template.IsCollectible = IsCollectible;
        template.RespawnTime = RespawnTime;
        template.Interactable = Interactable;
        template.InteractType = InteractType;
        template.CollisionEnabled = CollisionEnabled;
        template.DialogBubble = DialogBubble;
        template.FacingDirection = FacingDirection;
        template.DisplayLayer = DisplayLayer;
        template.TrafficCategory = TrafficCategory;
        template.YSortEnabled = YSortEnabled;
        template.YSortOffset = YSortOffset;
        template.IsMovable = IsMovable;
        template.LoopRestartDelay = LoopRestartDelay;
        template.HideWhenOffMap = HideWhenOffMap;
        template.IsTeleport = IsTeleport;
        template.TeleportTargetScene = TeleportTargetScene;
        template.TeleportTargetX = TeleportTargetX;
        template.TeleportTargetY = TeleportTargetY;
        template.NineSlice = new NineSliceSettings
        {
            Enabled = NineSliceEnabled,
            Left = NineSliceLeft,
            Right = NineSliceRight,
            Top = NineSliceTop,
            Bottom = NineSliceBottom,
            CenterMode = NineSliceModes.ToCode(NineSliceCenterMode),
        };
    }

    /// <summary>用模板的当前值回写这一行（编辑保存时）。</summary>
    /// <param name="template">来源模板。</param>
    public void UpdateFromTemplate(ObjectTemplate template)
    {
        Id = template.TemplateId;
        Icon = string.IsNullOrWhiteSpace(template.ResName) ? Icon : template.ResName;
        TemplateName = template.TemplateName;
        AnimName = template.AnimName;
        Loop = template.Loop;
        Speed = template.Speed;
        SortOrder = template.SortOrder;
        ShadowEnabled = template.ShadowEnabled;
        IsCollectible = template.IsCollectible;
        RespawnTime = template.RespawnTime;
        Interactable = template.Interactable;
        InteractType = template.InteractType;
        CollisionEnabled = template.CollisionEnabled;
        DialogBubble = template.DialogBubble;
        FacingDirection = template.FacingDirection;
        DisplayLayer = template.DisplayLayer;
        TrafficCategory = template.TrafficCategory;
        YSortEnabled = template.YSortEnabled;
        YSortOffset = template.YSortOffset;
        IsMovable = template.IsMovable;
        LoopRestartDelay = template.LoopRestartDelay;
        HideWhenOffMap = template.HideWhenOffMap;
        IsTeleport = template.IsTeleport;
        TeleportTargetScene = template.TeleportTargetScene;
        TeleportTargetX = template.TeleportTargetX;
        TeleportTargetY = template.TeleportTargetY;

        var nineSlice = template.NineSlice ?? new NineSliceSettings();
        NineSliceEnabled = nineSlice.Enabled;
        NineSliceLeft = Math.Max(0, nineSlice.Left);
        NineSliceRight = Math.Max(0, nineSlice.Right);
        NineSliceTop = Math.Max(0, nineSlice.Top);
        NineSliceBottom = Math.Max(0, nineSlice.Bottom);
        NineSliceCenterMode = NineSliceModes.ToCode(nineSlice.CenterMode);
    }

    /// <summary>一行摘要（供详情卡展示绑定的配置表信息）。</summary>
    /// <returns>紧凑文本。</returns>
    public string Describe()
    {
        var parts = new List<string> { $"#{Id}" };
        if (!string.IsNullOrWhiteSpace(ResName))
        {
            parts.Add(ResName);
        }

        parts.Add($"层级 {SortOrder}");
        parts.Add($"碰撞 {(CollisionEnabled ? "是" : "否")}");
        parts.Add($"交通 {TrafficCategory}");
        if (!string.IsNullOrWhiteSpace(DialogBubble))
        {
            parts.Add($"气泡 {DialogBubble}");
        }

        if (IsCollectible)
        {
            parts.Add($"收集 {RespawnTime:0.##}s 再生");
        }

        return string.Join(" · ", parts);
    }
}

#endregion

#region 表

/// <summary>
/// <c>ItemElementConfig.csv</c> 的内存视图：按图片名（Icon / 模板名称）建索引，
/// 支持新增 / 更新 / 删除行并按原表头写回。
/// </summary>
public sealed class ItemElementTable
{
    #region 常量

    /// <summary>表头行数（列名 / 类型）。</summary>
    private const int HeaderRowCount = 2;


    #endregion

    #region 字段

    private readonly Dictionary<string, ItemElementRow> _index = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 构造

    private ItemElementTable(string path, IReadOnlyList<string> headerLines, IReadOnlyList<string> columnNames)
    {
        Path = path;
        HeaderLines = headerLines;
        ColumnNames = columnNames;
        for (var index = 0; index < columnNames.Count; index++)
        {
            ColumnIndex[columnNames[index].Trim()] = index;
        }
    }

    #endregion

    #region 公开属性

    /// <summary>CSV 绝对路径。</summary>
    public string Path { get; }

    /// <summary>前两行原始表头（列名 / 类型），保存时原样写回。</summary>
    public IReadOnlyList<string> HeaderLines { get; }

    /// <summary>列名集合（按原表顺序）。</summary>
    public IReadOnlyList<string> ColumnNames { get; }

    /// <summary>列名 → 下标。</summary>
    public Dictionary<string, int> ColumnIndex { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>全部数据行。</summary>
    public List<ItemElementRow> Rows { get; } = new();

    #endregion

    #region 加载

    /// <summary>读取 CSV；文件不存在或格式不识别时返回 null。</summary>
    /// <param name="path">CSV 绝对路径。</param>
    /// <returns>表实例，失败为 null。</returns>
    public static ItemElementTable? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(path!, Encoding.UTF8)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            if (lines.Count <= HeaderRowCount)
            {
                return null;
            }

            var names = SplitCells(lines[0]);
            if (names.Count == 0 || !names.Any(name => name.Equals("Icon", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var table = new ItemElementTable(path!, lines.Take(HeaderRowCount).ToList(), names);
            for (var index = HeaderRowCount; index < lines.Count; index++)
            {
                table.Rows.Add(table.ParseRow(lines[index], index + 1));
            }

            table.RebuildIndex();
            return table;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>按图片名取行；支持大小写与末尾下划线（<c>prop_box_1_</c>）的宽松匹配。</summary>
    /// <param name="imageName">图片名（可含扩展名）。</param>
    /// <returns>命中的行，未命中为 null。</returns>
    public ItemElementRow? Find(string? imageName)
    {
        var key = NormalizeKey(imageName);
        if (key.Length == 0)
        {
            return null;
        }

        if (_index.TryGetValue(key, out var row))
        {
            return row;
        }

        // 宽松匹配：去掉末尾下划线再比一次（表里有 prop_box_1_ / prop_grass_1_ 这类）
        var trimmed = key.TrimEnd('_');
        return _index.TryGetValue(trimmed, out row) ? row : null;
    }

    /// <summary>新增或更新一行（按 Icon 匹配）。</summary>
    /// <param name="template">模板数据。</param>
    /// <returns>写入的行。</returns>
    public ItemElementRow Upsert(ObjectTemplate template)
    {
        var key = NormalizeKey(string.IsNullOrWhiteSpace(template.ResName) ? template.TemplateName : template.ResName);
        var row = key.Length == 0 ? null : Find(key);
        if (row is null)
        {
            row = new ItemElementRow
            {
                Id = template.TemplateId,
                Icon = string.IsNullOrWhiteSpace(template.ResName) ? template.TemplateName : template.ResName,
                TemplateName = template.TemplateName,
                SortOrder = template.SortOrder,
            };
            foreach (var _ in ColumnNames)
            {
                row.Cells.Add(string.Empty);
            }

            Rows.Add(row);
        }

        row.UpdateFromTemplate(template);
        if (string.IsNullOrWhiteSpace(row.Icon))
        {
            row.Icon = template.TemplateName;
        }

        SyncCells(row);
        RebuildIndex();
        return row;
    }

    /// <summary>按图片名删除一行。</summary>
    /// <param name="imageName">图片名。</param>
    /// <returns>是否删掉了。</returns>
    public bool Remove(string? imageName)
    {
        var row = Find(imageName);
        if (row is null)
        {
            return false;
        }

        Rows.Remove(row);
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
            if (int.TryParse(row.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > max && id / 1000 == start / 1000)
            {
                max = id;
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region 保存

    /// <summary>按原表头写回 CSV（前两行原样，数据行按列下标回写）。</summary>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否写成功。</returns>
    public bool TrySave(out string error)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var header in HeaderLines)
            {
                builder.Append(header).Append("\r\n");
            }

            foreach (var row in Rows)
            {
                SyncCells(row);
                builder.Append(string.Join(",", row.Cells.Select(Escape))).Append("\r\n");
            }

            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(Path, builder.ToString(), new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    #endregion

    #region 内部实现

    private ItemElementRow ParseRow(string line, int rowNumber)
    {
        var cells = SplitCells(line);
        while (cells.Count < ColumnNames.Count)
        {
            cells.Add(string.Empty);
        }

        var row = new ItemElementRow { RowNumber = rowNumber };
        row.Cells.AddRange(cells);
        row.Id = Cell(cells, "ID") is { Length: > 0 } id ? id : Cell(cells, "TemplateId");
        row.Icon = Cell(cells, "Icon") is { Length: > 0 } icon ? icon : Cell(cells, "ImagePath");
        row.ResName = Cell(cells, "ResName");
        row.TemplateName = Cell(cells, "模板名称") is { Length: > 0 } cn ? cn : Cell(cells, "TemplateName");
        row.AnimName = Cell(cells, "默认动画") is { Length: > 0 } anim ? anim : Cell(cells, "AnimName");
        row.Loop = ParseBool(Cell(cells, "是否循环"), true);
        row.Speed = ParseNumber(Cell(cells, "播放速度"), 1.0);
        row.Interactable = ParseBool(Cell(cells, "是否可交互"), true);
        row.InteractType = Cell(cells, "交互类型");
        row.SortOrder = (int)ParseNumber(Cell(cells, "渲染层级"), 0);
        row.CollisionEnabled = ParseBool(Cell(cells, "是否有碰撞"), false);
        row.DialogBubble = Cell(cells, "对话气泡");
        row.FacingDirection = Cell(cells, "初始朝向");
        row.ShadowEnabled = ParseBool(Cell(cells, "是否显示阴影"), true);
        row.IsCollectible = ParseBool(Cell(cells, "是否收集物"), false);
        row.RespawnTime = ParseNumber(Cell(cells, "重生时间"), 0);
        row.TrafficCategory = Cell(cells, "交通类别");
        row.YSortEnabled = ParseBool(Cell(cells, "启用YSort"), true);
        row.YSortOffset = ParseNumber(Cell(cells, "YSort偏移"), 0);
        row.IsMovable = ParseBool(Cell(cells, "可移动"), false);
        row.LoopRestartDelay = ParseNumber(Cell(cells, "循环重启延迟"), 0);
        row.HideWhenOffMap = ParseBool(Cell(cells, "离开地图隐藏"), true);
        row.IsTeleport = ParseBool(Cell(cells, "是否传送门"), false);
        row.TeleportTargetScene = Cell(cells, "目标场景");
        row.TeleportTargetX = ParseNumber(Cell(cells, "目标X"), 0);
        row.TeleportTargetY = ParseNumber(Cell(cells, "目标Y"), 0);
        row.NineSliceEnabled = ParseBool(Cell(cells, "九宫格"), false);
        row.NineSliceLeft = (int)ParseNumber(Cell(cells, "九宫格左边距"), 0);
        row.NineSliceRight = (int)ParseNumber(Cell(cells, "九宫格右边距"), 0);
        row.NineSliceTop = (int)ParseNumber(Cell(cells, "九宫格上边距"), 0);
        row.NineSliceBottom = (int)ParseNumber(Cell(cells, "九宫格下边距"), 0);
        row.NineSliceCenterMode = Cell(cells, "九宫格中心");
        return row;
    }

    /// <summary>把行模型写回 Cells 列表（未识别列保持原值）。</summary>
    private void SyncCells(ItemElementRow row)
    {
        while (row.Cells.Count < ColumnNames.Count)
        {
            row.Cells.Add(string.Empty);
        }

        Set("ID", row.Id);
        Set("Icon", row.Icon);
        Set("ResName", row.ResName);
        Set("模板名称", row.TemplateName);
        Set("TemplateName", row.TemplateName);
        Set("默认动画", row.AnimName);
        Set("AnimName", row.AnimName);
        Set("是否循环", row.Loop ? "True" : "False");
        Set("Loop", row.Loop ? "True" : "False");
        Set("播放速度", Number(row.Speed));
        Set("Speed", Number(row.Speed));
        Set("是否可交互", row.Interactable ? "True" : "False");
        Set("Interactable", row.Interactable ? "True" : "False");
        Set("交互类型", row.InteractType);
        Set("InteractType", row.InteractType);
        Set("渲染层级", row.SortOrder.ToString(CultureInfo.InvariantCulture));
        Set("SortOrder", row.SortOrder.ToString(CultureInfo.InvariantCulture));
        Set("是否有碰撞", row.CollisionEnabled ? "True" : "False");
        Set("CollisionEnabled", row.CollisionEnabled ? "True" : "False");
        Set("对话气泡", row.DialogBubble);
        Set("DialogBubble", row.DialogBubble);
        Set("初始朝向", row.FacingDirection);
        Set("FacingDirection", row.FacingDirection);
        Set("是否显示阴影", row.ShadowEnabled ? "True" : "False");
        Set("ShadowEnabled", row.ShadowEnabled ? "True" : "False");
        Set("是否收集物", row.IsCollectible ? "True" : "False");
        Set("IsCollectible", row.IsCollectible ? "True" : "False");
        Set("重生时间", Number(row.RespawnTime));
        Set("RespawnTime", Number(row.RespawnTime));
        Set("交通类别", row.TrafficCategory);
        Set("TrafficCategory", row.TrafficCategory);
        Set("启用YSort", row.YSortEnabled ? "True" : "False");
        Set("YSortEnabled", row.YSortEnabled ? "True" : "False");
        Set("YSort偏移", Number(row.YSortOffset));
        Set("YSortOffset", Number(row.YSortOffset));
        Set("可移动", row.IsMovable ? "True" : "False");
        Set("IsMovable", row.IsMovable ? "True" : "False");
        Set("循环重启延迟", Number(row.LoopRestartDelay));
        Set("LoopRestartDelay", Number(row.LoopRestartDelay));
        Set("离开地图隐藏", row.HideWhenOffMap ? "True" : "False");
        Set("HideWhenOffMap", row.HideWhenOffMap ? "True" : "False");
        Set("是否传送门", row.IsTeleport ? "True" : "False");
        Set("IsTeleport", row.IsTeleport ? "True" : "False");
        Set("目标场景", row.TeleportTargetScene);
        Set("TeleportTargetScene", row.TeleportTargetScene);
        Set("目标X", Number(row.TeleportTargetX));
        Set("TeleportTargetX", Number(row.TeleportTargetX));
        Set("目标Y", Number(row.TeleportTargetY));
        Set("TeleportTargetY", Number(row.TeleportTargetY));
        Set("九宫格", row.NineSliceEnabled ? "True" : "False");
        Set("NineSliceEnabled", row.NineSliceEnabled ? "True" : "False");
        Set("九宫格左边距", row.NineSliceLeft.ToString(CultureInfo.InvariantCulture));
        Set("NineSliceLeft", row.NineSliceLeft.ToString(CultureInfo.InvariantCulture));
        Set("九宫格右边距", row.NineSliceRight.ToString(CultureInfo.InvariantCulture));
        Set("NineSliceRight", row.NineSliceRight.ToString(CultureInfo.InvariantCulture));
        Set("九宫格上边距", row.NineSliceTop.ToString(CultureInfo.InvariantCulture));
        Set("NineSliceTop", row.NineSliceTop.ToString(CultureInfo.InvariantCulture));
        Set("九宫格下边距", row.NineSliceBottom.ToString(CultureInfo.InvariantCulture));
        Set("NineSliceBottom", row.NineSliceBottom.ToString(CultureInfo.InvariantCulture));
        Set("九宫格中心", NineSliceModes.ToDisplay(row.NineSliceCenterMode));
        Set("NineSliceCenterMode", row.NineSliceCenterMode);

        void Set(string column, string value)
        {
            if (ColumnIndex.TryGetValue(column, out var index) && index >= 0 && index < row.Cells.Count)
            {
                row.Cells[index] = value ?? string.Empty;
            }
        }
    }

    private void RebuildIndex()
    {
        _index.Clear();
        foreach (var row in Rows)
        {
            var icon = NormalizeKey(row.Icon);
            if (icon.Length > 0)
            {
                _index.TryAdd(icon, row);
            }

            var name = NormalizeKey(row.TemplateName);
            if (name.Length > 0)
            {
                _index.TryAdd(name, row);
            }
        }
    }

    private string Cell(IReadOnlyList<string> cells, string column)
    {
        if (!ColumnIndex.TryGetValue(column, out var index) || index < 0 || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index];
    }

    private static string NormalizeKey(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(name!.Trim()).ToLowerInvariant();

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool ParseBool(string raw, bool fallback) => bool.TryParse(raw, out var parsed) ? parsed : raw.Trim() switch
    {
        "1" => true,
        "0" => false,
        _ => fallback,
    };

    private static double ParseNumber(string raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static List<string> SplitCells(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                cells.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        cells.Add(builder.ToString());
        return cells;
    }

    private static string Escape(string value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
            ? text
            : '"' + text.Replace("\"", "\"\"") + '"';
    }

    #endregion
}

#endregion
