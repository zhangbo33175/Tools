/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateStore.Csv.cs
 * author:    云毅
 * created:   2026
 * descrip:   模板仓库 CSV 通道：列定义、表头识别、单元格切分与类型转换
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>模板仓库的 Unity 口径 CSV 列定义与解析辅助。</summary>
public sealed partial class TemplateStore
{
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
        new("LoopRestartDelay", "float", template => TableTextFormat.Number(template.LoopRestartDelay), (template, raw) => template.LoopRestartDelay = ParseNumber(raw, template.LoopRestartDelay)),
        new("MoveAnimName", "string", template => template.MoveAnimName, (template, raw) => template.MoveAnimName = raw),
        new("Speed", "float", template => TableTextFormat.Number(template.Speed), (template, raw) => template.Speed = ParseNumber(raw, template.Speed)),
        new("Interactable", "bool", template => Bool(template.Interactable), (template, raw) => template.Interactable = ParseBool(raw)),
        new("InteractType", "string", template => TemplateInteractTypes.ToTemplateCode(template.InteractType), (template, raw) => template.InteractType = TemplateInteractTypes.ToCode(raw)),
        new("CollisionEnabled", "bool", template => Bool(template.CollisionEnabled), (template, raw) => template.CollisionEnabled = ParseBool(raw)),
        new("FacingDirection", "string", template => template.FacingDirection, (template, raw) => template.FacingDirection = FacingNames.ToCode(raw)),
        new("ShadowEnabled", "bool", template => Bool(template.ShadowEnabled), (template, raw) => template.ShadowEnabled = ParseBool(raw)),
        new("DialogBubble", "string", template => template.DialogBubble, (template, raw) => template.DialogBubble = raw),
        new("BubbleId", "int", template => template.BubbleId.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.BubbleId = ParseInt(raw, template.BubbleId)),
        new("IsCollectible", "bool", template => Bool(template.IsCollectible), (template, raw) => template.IsCollectible = ParseBool(raw)),
        new("RespawnTime", "float", template => TableTextFormat.Number(template.RespawnTime), (template, raw) => template.RespawnTime = ParseNumber(raw, template.RespawnTime)),
        new("TrafficCategory", "string", template => template.TrafficCategory, (template, raw) => template.TrafficCategory = TemplateTrafficCategories.ToCode(raw)),
        new("YSortEnabled", "bool", template => Bool(template.YSortEnabled), (template, raw) => template.YSortEnabled = ParseBool(raw)),
        new("YSortOffset", "float", template => TableTextFormat.Number(template.YSortOffset), (template, raw) => template.YSortOffset = ParseNumber(raw, template.YSortOffset)),
        new("IsMovable", "bool", template => Bool(template.IsMovable), (template, raw) => template.IsMovable = ParseBool(raw)),
        new("HideWhenOffMap", "bool", template => Bool(template.HideWhenOffMap), (template, raw) => template.HideWhenOffMap = ParseBool(raw)),
        new("IsTeleport", "bool", template => Bool(template.IsTeleport), (template, raw) => template.IsTeleport = ParseBool(raw)),
        new("TeleportTargetScene", "string", template => template.TeleportTargetScene, (template, raw) => template.TeleportTargetScene = raw),
        new("TeleportTargetX", "float", template => TableTextFormat.Number(template.TeleportTargetX), (template, raw) => template.TeleportTargetX = ParseNumber(raw, template.TeleportTargetX)),
        new("TeleportTargetY", "float", template => TableTextFormat.Number(template.TeleportTargetY), (template, raw) => template.TeleportTargetY = ParseNumber(raw, template.TeleportTargetY)),
        new("GridWidth", "int", template => template.DefaultWidth.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.DefaultWidth = Math.Max(MinGridExtent, ParseInt(raw, template.DefaultWidth))),
        new("GridHeight", "int", template => template.DefaultHeight.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.DefaultHeight = Math.Max(MinGridExtent, ParseInt(raw, template.DefaultHeight))),
        new("LayerTypeKey", "string", template => template.LayerTypeKey, (template, raw) => template.LayerTypeKey = raw),
        new("ObjectType", "string", template => ObjectTypeNames.Of(template.Type), (template, raw) => template.Type = ObjectTypeNames.ToType(raw)),
        new("NineSliceEnabled", "bool", template => Bool(template.NineSlice.Enabled),
            (template, raw) => template.NineSlice.Enabled = ParseBool(raw)),
        new("NineSliceLeft", "int", template => template.NineSlice.Left.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Left = Math.Max(MinNineSliceInset, ParseInt(raw, template.NineSlice.Left))),
        new("NineSliceRight", "int", template => template.NineSlice.Right.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Right = Math.Max(MinNineSliceInset, ParseInt(raw, template.NineSlice.Right))),
        new("NineSliceTop", "int", template => template.NineSlice.Top.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Top = Math.Max(MinNineSliceInset, ParseInt(raw, template.NineSlice.Top))),
        new("NineSliceBottom", "int", template => template.NineSlice.Bottom.ToString(CultureInfo.InvariantCulture),
            (template, raw) => template.NineSlice.Bottom = Math.Max(MinNineSliceInset, ParseInt(raw, template.NineSlice.Bottom))),
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

    private static string Bool(bool value) => value ? "1" : "0";


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
