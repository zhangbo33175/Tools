/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件模板的本地持久化：JSON 工程配置 + Unity 口径 CSV 导入导出
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 元件模板仓库。负责读写独立版自己的 JSON 工程配置，以及导出/导入与 Unity 配置表
/// 列口径一致的 CSV（4 行表头：标题 / 列名 / 类型 / 备注，第 5 行起为数据）。
/// </summary>
public sealed class TemplateStore
{
    #region 常量

    /// <summary>CSV 表头行数（标题 / 列名 / 类型 / 备注）。</summary>
    private const int CsvHeaderRowCount = 4;

    /// <summary>列名行在 CSV 中的下标。</summary>
    private const int CsvColumnNameRowIndex = 1;

    /// <summary>类型提示行的类型关键字，用于导入时识别并跳过。</summary>
    private static readonly HashSet<string> TypeHintTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "bool", "int", "float", "double",
    };

    /// <summary>模板配置列定义：列名 + 类型提示 + 取值 + 赋值。</summary>
    private static readonly IReadOnlyList<TemplateColumn> Columns = BuildColumns();

    #endregion

    #region 构造

    /// <summary>初始化仓库。</summary>
    /// <param name="outputDirectory">输出目录，JSON 与 CSV 默认落在该目录下。</param>
    public TemplateStore(string outputDirectory)
    {
        OutputDirectory = outputDirectory;
        JsonPath = Path.Combine(outputDirectory, "ObjectTemplates.json");
        NineSlicePath = Path.Combine(outputDirectory, "NineSliceConfig.json");
    }

    #endregion

    #region 公开属性

    /// <summary>输出目录。</summary>
    public string OutputDirectory { get; }

    /// <summary>JSON 工程配置文件路径。</summary>
    public string JsonPath { get; }

    /// <summary>九宫格配置旁路文件路径：配置表没有对应列时，九宫格按素材名存在这里。</summary>
    public string NineSlicePath { get; }

    /// <summary>CSV 默认文件名。</summary>
    public string DefaultCsvFileName => "ItemElementConfig";

    #endregion

    #region 公开方法

    /// <summary>读取 JSON 工程配置；文件缺失或损坏时用工厂方法构建默认模板集合并立即落盘。</summary>
    /// <param name="factory">默认模板构建方法。</param>
    /// <returns>模板列表。</returns>
    public List<ObjectTemplate> LoadOrCreate(Func<List<ObjectTemplate>> factory)
    {
        var templates = TryLoadJson(JsonPath);
        if (templates.Count > 0)
        {
            return templates;
        }

        templates = factory();
        TrySaveJson(templates, out _);
        return templates;
    }

    /// <summary>把模板集合写入 JSON 工程配置。</summary>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否保存成功。</returns>
    public bool TrySaveJson(IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            var payload = templates.Select(TemplatePayload.FromModel).ToList();
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(JsonPath, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>读取九宫格旁路配置；文件缺失或损坏时返回空目录，不阻塞编辑器启动。</summary>
    /// <returns>九宫格目录。</returns>
    public NineSliceCatalog LoadNineSlices()
    {
        var catalog = new NineSliceCatalog();
        try
        {
            if (!File.Exists(NineSlicePath))
            {
                return catalog;
            }

            var payload = JsonSerializer.Deserialize<NineSliceFilePayload>(File.ReadAllText(NineSlicePath, Encoding.UTF8));
            if (payload?.Entries is null)
            {
                return catalog;
            }

            foreach (var entry in payload.Entries)
            {
                catalog.Register(
                    entry.TemplateId,
                    entry.ResName,
                    entry.ImagePath,
                    entry.TemplateName,
                    new NineSliceSettings
                    {
                        Enabled = entry.Enabled,
                        Left = Math.Max(0, entry.Left),
                        Right = Math.Max(0, entry.Right),
                        Top = Math.Max(0, entry.Top),
                        Bottom = Math.Max(0, entry.Bottom),
                        CenterMode = NineSliceModes.ToCode(entry.CenterMode),
                    });
            }
        }
        catch (Exception)
        {
            // 九宫格文件损坏时按未配置处理，画布退回普通拉伸
        }

        return catalog;
    }

    /// <summary>把模板集合中的九宫格配置写入旁路文件（仅写启用项；表行与 JSON 模板共用同一份）。</summary>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否保存成功。</returns>
    public bool TrySaveNineSlices(IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            var entries = new List<NineSliceEntryPayload>();
            foreach (var template in templates)
            {
                var nineSlice = template.NineSlice;
                if (nineSlice is null || !nineSlice.Enabled)
                {
                    continue;
                }

                entries.Add(new NineSliceEntryPayload
                {
                    TemplateId = template.TemplateId,
                    ResName = template.ResName,
                    ImagePath = template.ImagePath,
                    TemplateName = template.TemplateName,
                    Enabled = true,
                    Left = Math.Max(0, nineSlice.Left),
                    Right = Math.Max(0, nineSlice.Right),
                    Top = Math.Max(0, nineSlice.Top),
                    Bottom = Math.Max(0, nineSlice.Bottom),
                    CenterMode = NineSliceModes.ToCode(nineSlice.CenterMode),
                });
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(
                NineSlicePath,
                JsonSerializer.Serialize(new NineSliceFilePayload { Entries = entries }, options),
                new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>导出 CSV（4 行表头 + 数据行，列口径与 Unity 配置表一致）。</summary>
    /// <param name="path">目标路径。</param>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否导出成功。</returns>
    public bool TryExportCsv(string path, IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("物品元件配置表");
            builder.AppendLine(string.Join(",", Columns.Select(column => column.Name)));
            builder.AppendLine(string.Join(",", Columns.Select(column => column.TypeHint)));
            builder.AppendLine("自动生成：独立版地图编辑器导出");

            foreach (var template in templates)
            {
                builder.AppendLine(string.Join(",", Columns.Select(column => Escape(column.Read(template)))));
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>导入 CSV，兼容 4 行表头与单行表头两种布局。</summary>
    /// <param name="path">文件路径。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>解析出的模板列表；失败返回空列表。</returns>
    public List<ObjectTemplate> ImportCsv(string path, out string error)
    {
        var templates = new List<ObjectTemplate>();
        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            if (lines.Count == 0)
            {
                error = "文件为空。";
                return templates;
            }

            var headerIndex = FindHeaderRow(lines);
            if (headerIndex < 0)
            {
                error = "未找到 TemplateId 列，无法识别配置表。";
                return templates;
            }

            var header = SplitCells(lines[headerIndex]);
            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < header.Count; index++)
            {
                columnIndex[header[index].Trim()] = index;
            }

            for (var rowIndex = headerIndex + 1; rowIndex < lines.Count; rowIndex++)
            {
                var cells = SplitCells(lines[rowIndex]);

                // 备注行 / 分隔行：单元格数量明显少于列数，直接跳过
                if (cells.Count < header.Count)
                {
                    continue;
                }

                var rawId = Cell(cells, columnIndex, "TemplateId");
                if (string.IsNullOrWhiteSpace(rawId) || TypeHintTokens.Contains(rawId.Trim()))
                {
                    continue;
                }

                var template = new ObjectTemplate { IsBuiltIn = false };
                foreach (var column in Columns)
                {
                    column.Assign(template, Cell(cells, columnIndex, column.Name));
                }

                if (string.IsNullOrWhiteSpace(template.TemplateName))
                {
                    template.TemplateName = template.TemplateId;
                }

                templates.Add(template);
            }

            error = string.Empty;
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        return templates;
    }

    /// <summary>按类型分区生成下一个可用模板 ID（人物 1xxx / 建筑 2xxx / 物件 3xxx）。</summary>
    /// <param name="templates">现有模板集合。</param>
    /// <param name="type">目标类型。</param>
    /// <returns>新的模板 ID。</returns>
    public static string NextTemplateId(IReadOnlyList<ObjectTemplate> templates, MapObjectType type)
    {
        var start = type switch
        {
            MapObjectType.Character => 1000,
            MapObjectType.Building => 2000,
            _ => 3000,
        };

        var max = start;
        foreach (var template in templates.Where(template => template.Type == type))
        {
            if (int.TryParse(template.TemplateId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > max)
            {
                max = id;
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region JSON

    private static List<ObjectTemplate> TryLoadJson(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<ObjectTemplate>();
            }

            var payload = JsonSerializer.Deserialize<List<TemplatePayload>>(File.ReadAllText(path, Encoding.UTF8));
            if (payload is null)
            {
                return new List<ObjectTemplate>();
            }

            return payload
                .Where(item => !string.IsNullOrWhiteSpace(item.TemplateId))
                .Select(item => item.ToModel())
                .ToList();
        }
        catch (Exception)
        {
            // 配置损坏时回落到内置目录，不阻塞编辑器启动
            return new List<ObjectTemplate>();
        }
    }

    /// <summary>九宫格旁路文件：一个版本号 + 若干按素材别名登记的条目。</summary>
    private sealed class NineSliceFilePayload
    {
        [JsonPropertyName("Version")] public int Version { get; set; } = 1;

        [JsonPropertyName("Entries")] public List<NineSliceEntryPayload> Entries { get; set; } = new();
    }

    /// <summary>九宫格条目：键别名 + 四条边距 + 中心填充方式。</summary>
    private sealed class NineSliceEntryPayload
    {
        [JsonPropertyName("TemplateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("ResName")] public string ResName { get; set; } = string.Empty;

        [JsonPropertyName("ImagePath")] public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("TemplateName")] public string TemplateName { get; set; } = string.Empty;

        [JsonPropertyName("Enabled")] public bool Enabled { get; set; }

        [JsonPropertyName("Left")] public int Left { get; set; }

        [JsonPropertyName("Right")] public int Right { get; set; }

        [JsonPropertyName("Top")] public int Top { get; set; }

        [JsonPropertyName("Bottom")] public int Bottom { get; set; }

        [JsonPropertyName("CenterMode")] public string CenterMode { get; set; } = NineSliceModes.Stretch;
    }

    /// <summary>JSON 传输对象：属性名与 Unity 配置表列名保持一致，追加独立版扩展列。</summary>
    private sealed class TemplatePayload
    {
        [JsonPropertyName("TemplateId")] public string TemplateId { get; set; } = string.Empty;
        [JsonPropertyName("TemplateName")] public string TemplateName { get; set; } = string.Empty;
        [JsonPropertyName("ResName")] public string ResName { get; set; } = string.Empty;
        [JsonPropertyName("ImagePath")] public string ImagePath { get; set; } = string.Empty;
        [JsonPropertyName("AnimName")] public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;
        [JsonPropertyName("Loop")] public bool Loop { get; set; } = true;
        [JsonPropertyName("Speed")] public double Speed { get; set; } = 1.0;
        [JsonPropertyName("SortOrder")] public int SortOrder { get; set; }
        [JsonPropertyName("DisplayLayer")] public string DisplayLayer { get; set; } = string.Empty;
        [JsonPropertyName("DialogBubble")] public string DialogBubble { get; set; } = string.Empty;
        [JsonPropertyName("BubbleId")] public int BubbleId { get; set; }
        [JsonPropertyName("ShadowEnabled")] public bool ShadowEnabled { get; set; } = true;
        [JsonPropertyName("IsCollectible")] public bool IsCollectible { get; set; }
        [JsonPropertyName("RespawnTime")] public double RespawnTime { get; set; }
        [JsonPropertyName("TrafficCategory")] public string TrafficCategory { get; set; } = TemplateTrafficCategories.None;
        [JsonPropertyName("Interactable")] public bool Interactable { get; set; } = true;
        [JsonPropertyName("InteractType")] public string InteractType { get; set; } = TemplateInteractTypes.None;
        [JsonPropertyName("CollisionEnabled")] public bool CollisionEnabled { get; set; } = true;
        [JsonPropertyName("FacingDirection")] public string FacingDirection { get; set; } = Facings.Down;
        [JsonPropertyName("YSortEnabled")] public bool YSortEnabled { get; set; } = true;
        [JsonPropertyName("YSortOffset")] public double YSortOffset { get; set; }
        [JsonPropertyName("IsMovable")] public bool IsMovable { get; set; }
        [JsonPropertyName("MoveAnimName")] public string MoveAnimName { get; set; } = "walk";
        [JsonPropertyName("LoopRestartDelay")] public double LoopRestartDelay { get; set; } = 3.0;
        [JsonPropertyName("HideWhenOffMap")] public bool HideWhenOffMap { get; set; } = true;
        [JsonPropertyName("IsTeleport")] public bool IsTeleport { get; set; }
        [JsonPropertyName("TeleportTargetScene")] public string TeleportTargetScene { get; set; } = string.Empty;
        [JsonPropertyName("TeleportTargetX")] public double TeleportTargetX { get; set; }
        [JsonPropertyName("TeleportTargetY")] public double TeleportTargetY { get; set; }

        /// <summary>独立版扩展：默认占格宽。</summary>
        [JsonPropertyName("GridWidth")] public int GridWidth { get; set; } = 1;

        /// <summary>独立版扩展：默认占格高。</summary>
        [JsonPropertyName("GridHeight")] public int GridHeight { get; set; } = 1;

        /// <summary>独立版扩展：显式图层键。</summary>
        [JsonPropertyName("LayerTypeKey")] public string LayerTypeKey { get; set; } = string.Empty;

        /// <summary>独立版扩展：是否启用九宫格。</summary>
        [JsonPropertyName("NineSliceEnabled")] public bool NineSliceEnabled { get; set; }

        /// <summary>独立版扩展：九宫格左边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceLeft")] public int NineSliceLeft { get; set; }

        /// <summary>独立版扩展：九宫格右边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceRight")] public int NineSliceRight { get; set; }

        /// <summary>独立版扩展：九宫格上边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceTop")] public int NineSliceTop { get; set; }

        /// <summary>独立版扩展：九宫格下边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceBottom")] public int NineSliceBottom { get; set; }

        /// <summary>独立版扩展：九宫格中心填充方式。</summary>
        [JsonPropertyName("NineSliceCenterMode")] public string NineSliceCenterMode { get; set; } = NineSliceModes.Stretch;

        /// <summary>独立版扩展：类型（Unity 侧按 ResName 前缀推断）。</summary>
        [JsonPropertyName("ObjectType")] public string ObjectType { get; set; } = string.Empty;

        /// <summary>模型转传输对象。</summary>
        public static TemplatePayload FromModel(ObjectTemplate template) => new()
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            ResName = template.ResName,
            ImagePath = template.ImagePath,
            AnimName = template.AnimName,
            Loop = template.Loop,
            Speed = template.Speed,
            SortOrder = template.SortOrder,
            DisplayLayer = template.DisplayLayer,
            DialogBubble = template.DialogBubble,
            BubbleId = template.BubbleId,
            ShadowEnabled = template.ShadowEnabled,
            IsCollectible = template.IsCollectible,
            RespawnTime = template.RespawnTime,
            TrafficCategory = template.TrafficCategory,
            Interactable = template.Interactable,
            InteractType = template.InteractType,
            CollisionEnabled = template.CollisionEnabled,
            FacingDirection = template.FacingDirection,
            YSortEnabled = template.YSortEnabled,
            YSortOffset = template.YSortOffset,
            IsMovable = template.IsMovable,
            MoveAnimName = template.MoveAnimName,
            LoopRestartDelay = template.LoopRestartDelay,
            HideWhenOffMap = template.HideWhenOffMap,
            IsTeleport = template.IsTeleport,
            TeleportTargetScene = template.TeleportTargetScene,
            TeleportTargetX = template.TeleportTargetX,
            TeleportTargetY = template.TeleportTargetY,
            GridWidth = template.DefaultWidth,
            GridHeight = template.DefaultHeight,
            LayerTypeKey = template.LayerTypeKey,
            ObjectType = ObjectTypeNames.Of(template.Type),
            NineSliceEnabled = template.NineSlice.Enabled,
            NineSliceLeft = template.NineSlice.Left,
            NineSliceRight = template.NineSlice.Right,
            NineSliceTop = template.NineSlice.Top,
            NineSliceBottom = template.NineSlice.Bottom,
            NineSliceCenterMode = template.NineSlice.CenterMode,
        };

        /// <summary>传输对象转模型。</summary>
        public ObjectTemplate ToModel() => new()
        {
            TemplateId = TemplateId,
            TemplateName = string.IsNullOrWhiteSpace(TemplateName) ? TemplateId : TemplateName,
            ResName = ResName,
            ImagePath = ImagePath,
            AnimName = string.IsNullOrWhiteSpace(AnimName) ? MapToolDefaults.DefaultAnimName : AnimName,
            Loop = Loop,
            Speed = Speed,
            SortOrder = SortOrder,
            DisplayLayer = DisplayLayer ?? string.Empty,
            DialogBubble = DialogBubble ?? string.Empty,
            BubbleId = BubbleId,
            ShadowEnabled = ShadowEnabled,
            IsCollectible = IsCollectible,
            RespawnTime = RespawnTime,
            TrafficCategory = TemplateTrafficCategories.ToCode(TrafficCategory),
            Interactable = Interactable,
            InteractType = TemplateInteractTypes.ToCode(InteractType),
            CollisionEnabled = CollisionEnabled,
            FacingDirection = FacingNames.ToCode(FacingDirection),
            YSortEnabled = YSortEnabled,
            YSortOffset = YSortOffset,
            IsMovable = IsMovable,
            MoveAnimName = string.IsNullOrWhiteSpace(MoveAnimName) ? "walk" : MoveAnimName,
            LoopRestartDelay = LoopRestartDelay,
            HideWhenOffMap = HideWhenOffMap,
            IsTeleport = IsTeleport,
            TeleportTargetScene = TeleportTargetScene ?? string.Empty,
            TeleportTargetX = TeleportTargetX,
            TeleportTargetY = TeleportTargetY,
            LayerTypeKey = LayerTypeKey ?? string.Empty,
            DefaultWidth = Math.Max(1, GridWidth),
            DefaultHeight = Math.Max(1, GridHeight),
            NineSlice = new NineSliceSettings
            {
                Enabled = NineSliceEnabled,
                Left = Math.Max(0, NineSliceLeft),
                Right = Math.Max(0, NineSliceRight),
                Top = Math.Max(0, NineSliceTop),
                Bottom = Math.Max(0, NineSliceBottom),
                CenterMode = NineSliceModes.ToCode(NineSliceCenterMode),
            },
            Type = ObjectTypeNames.ToType(ObjectType),
            IsBuiltIn = true,
        };
    }

    #endregion

    #region CSV 列定义

    /// <summary>单个 CSV 列的定义与读写委托。</summary>
    private sealed class TemplateColumn
    {
        /// <summary>初始化列定义。</summary>
        /// <param name="name">列名。</param>
        /// <param name="typeHint">类型提示。</param>
        /// <param name="read">模型取值。</param>
        /// <param name="assign">文本赋值。</param>
        public TemplateColumn(string name, string typeHint, Func<ObjectTemplate, string> read, Action<ObjectTemplate, string> assign)
        {
            Name = name;
            TypeHint = typeHint;
            Read = read;
            Assign = assign;
        }

        /// <summary>列名。</summary>
        public string Name { get; }

        /// <summary>类型提示。</summary>
        public string TypeHint { get; }

        /// <summary>模型 → 单元格文本。</summary>
        public Func<ObjectTemplate, string> Read { get; }

        /// <summary>单元格文本 → 模型。</summary>
        public Action<ObjectTemplate, string> Assign { get; }
    }

    /// <summary>构建列定义表：前 29 列与 Unity 配置表列顺序逐一对应，末尾追加独立版扩展列。</summary>
    private static IReadOnlyList<TemplateColumn> BuildColumns() => new List<TemplateColumn>
    {
        new("TemplateId", "string", template => template.TemplateId, (template, raw) => template.TemplateId = raw),
        new("TemplateName", "string", template => template.TemplateName, (template, raw) => template.TemplateName = raw),
        new("ResName", "string", template => template.ResName, (template, raw) => template.ResName = raw),
        new("ImagePath", "string", template => template.ImagePath, (template, raw) => template.ImagePath = raw),
        new("DisplayLayer", "string", template => template.DisplayLayer, (template, raw) => template.DisplayLayer = raw),
        new("SortOrder", "int", template => template.SortOrder.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.SortOrder = ParseInt(raw, template.SortOrder)),
        new("AnimName", "string", template => template.AnimName, (template, raw) => template.AnimName = raw),
        new("Loop", "bool", template => Bool(template.Loop), (template, raw) => template.Loop = ParseBool(raw)),
        new("LoopRestartDelay", "float", template => Number(template.LoopRestartDelay), (template, raw) => template.LoopRestartDelay = ParseNumber(raw, template.LoopRestartDelay)),
        new("MoveAnimName", "string", template => template.MoveAnimName, (template, raw) => template.MoveAnimName = raw),
        new("Speed", "float", template => Number(template.Speed), (template, raw) => template.Speed = ParseNumber(raw, template.Speed)),
        new("Interactable", "bool", template => Bool(template.Interactable), (template, raw) => template.Interactable = ParseBool(raw)),
        new("InteractType", "string", template => TemplateInteractTypes.ToTemplateCode(template.InteractType), (template, raw) => template.InteractType = TemplateInteractTypes.ToCode(raw)),
        new("CollisionEnabled", "bool", template => Bool(template.CollisionEnabled), (template, raw) => template.CollisionEnabled = ParseBool(raw)),
        new("FacingDirection", "string", template => template.FacingDirection, (template, raw) => template.FacingDirection = FacingNames.ToCode(raw)),
        new("ShadowEnabled", "bool", template => Bool(template.ShadowEnabled), (template, raw) => template.ShadowEnabled = ParseBool(raw)),
        new("DialogBubble", "string", template => template.DialogBubble, (template, raw) => template.DialogBubble = raw),
        new("BubbleId", "int", template => template.BubbleId.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.BubbleId = ParseInt(raw, template.BubbleId)),
        new("IsCollectible", "bool", template => Bool(template.IsCollectible), (template, raw) => template.IsCollectible = ParseBool(raw)),
        new("RespawnTime", "float", template => Number(template.RespawnTime), (template, raw) => template.RespawnTime = ParseNumber(raw, template.RespawnTime)),
        new("TrafficCategory", "string", template => template.TrafficCategory, (template, raw) => template.TrafficCategory = TemplateTrafficCategories.ToCode(raw)),
        new("YSortEnabled", "bool", template => Bool(template.YSortEnabled), (template, raw) => template.YSortEnabled = ParseBool(raw)),
        new("YSortOffset", "float", template => Number(template.YSortOffset), (template, raw) => template.YSortOffset = ParseNumber(raw, template.YSortOffset)),
        new("IsMovable", "bool", template => Bool(template.IsMovable), (template, raw) => template.IsMovable = ParseBool(raw)),
        new("HideWhenOffMap", "bool", template => Bool(template.HideWhenOffMap), (template, raw) => template.HideWhenOffMap = ParseBool(raw)),
        new("IsTeleport", "bool", template => Bool(template.IsTeleport), (template, raw) => template.IsTeleport = ParseBool(raw)),
        new("TeleportTargetScene", "string", template => template.TeleportTargetScene, (template, raw) => template.TeleportTargetScene = raw),
        new("TeleportTargetX", "float", template => Number(template.TeleportTargetX), (template, raw) => template.TeleportTargetX = ParseNumber(raw, template.TeleportTargetX)),
        new("TeleportTargetY", "float", template => Number(template.TeleportTargetY), (template, raw) => template.TeleportTargetY = ParseNumber(raw, template.TeleportTargetY)),
        new("GridWidth", "int", template => template.DefaultWidth.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.DefaultWidth = Math.Max(1, ParseInt(raw, template.DefaultWidth))),
        new("GridHeight", "int", template => template.DefaultHeight.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.DefaultHeight = Math.Max(1, ParseInt(raw, template.DefaultHeight))),
        new("LayerTypeKey", "string", template => template.LayerTypeKey, (template, raw) => template.LayerTypeKey = raw),
        new("ObjectType", "string", template => ObjectTypeNames.Of(template.Type), (template, raw) => template.Type = ObjectTypeNames.ToType(raw)),
        new("NineSliceEnabled", "bool", template => Bool(template.NineSlice.Enabled),
            (template, raw) => template.NineSlice.Enabled = ParseBool(raw)),
        new("NineSliceLeft", "int", template => template.NineSlice.Left.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Left = Math.Max(0, ParseInt(raw, template.NineSlice.Left))),
        new("NineSliceRight", "int", template => template.NineSlice.Right.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Right = Math.Max(0, ParseInt(raw, template.NineSlice.Right))),
        new("NineSliceTop", "int", template => template.NineSlice.Top.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Top = Math.Max(0, ParseInt(raw, template.NineSlice.Top))),
        new("NineSliceBottom", "int", template => template.NineSlice.Bottom.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Bottom = Math.Max(0, ParseInt(raw, template.NineSlice.Bottom))),
        new("NineSliceCenterMode", "string", template => NineSliceModes.ToDisplay(template.NineSlice.CenterMode),
            (template, raw) => template.NineSlice.CenterMode = NineSliceModes.ToCode(raw)),
    };

    /// <summary>在文件前若干行里查找列名行。</summary>
    /// <param name="lines">文件行。</param>
    /// <returns>列名行下标，未找到返回 -1。</returns>
    private static int FindHeaderRow(IReadOnlyList<string> lines)
    {
        var limit = Math.Min(lines.Count, CsvHeaderRowCount + 2);
        for (var index = 0; index < limit; index++)
        {
            if (lines[index].Contains("TemplateId", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>按列名取值。</summary>
    /// <param name="cells">单元格。</param>
    /// <param name="columnIndex">列名 → 下标。</param>
    /// <param name="name">列名。</param>
    /// <returns>单元格文本，缺列返回空串。</returns>
    private static string Cell(IReadOnlyList<string> cells, Dictionary<string, int> columnIndex, string name)
    {
        if (!columnIndex.TryGetValue(name, out var index) || index < 0 || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index];
    }

    /// <summary>拆分 CSV 行，支持双引号包裹与转义。</summary>
    /// <param name="line">行文本。</param>
    /// <returns>单元格集合。</returns>
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

    /// <summary>转义单元格文本。</summary>
    /// <param name="value">文本。</param>
    /// <returns>可直接写入 CSV 的文本。</returns>
    private static string Escape(string value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return text;
        }

        return '"' + text.Replace("\"", "\"\"") + '"';
    }

    private static string Bool(bool value) => value ? "1" : "0";

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool ParseBool(string raw) => bool.TryParse(raw, out var parsed) ? parsed : raw switch
    {
        "1" => true,
        "0" => false,
        _ => false,
    };

    private static int ParseInt(string raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static double ParseNumber(string raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    #endregion
}
