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
        new("SortOrder", "int", template => TableTextFormat.Integer(template.SortOrder),
            (template, raw) => template.SortOrder = TableTextFormat.ParseInt(raw, template.SortOrder)),
        new("AnimName", "string", template => template.AnimName, (template, raw) => template.AnimName = raw),
        new("Loop", "bool", template => TableTextFormat.Flag(template.Loop), (template, raw) => template.Loop = TableTextFormat.ParseBool(raw, false)),
        new("LoopRestartDelay", "float", template => TableTextFormat.Number(template.LoopRestartDelay), (template, raw) => template.LoopRestartDelay = TableTextFormat.ParseNumber(raw, template.LoopRestartDelay)),
        new("MoveAnimName", "string", template => template.MoveAnimName, (template, raw) => template.MoveAnimName = raw),
        new("Speed", "float", template => TableTextFormat.Number(template.Speed), (template, raw) => template.Speed = TableTextFormat.ParseNumber(raw, template.Speed)),
        new("Interactable", "bool", template => TableTextFormat.Flag(template.Interactable), (template, raw) => template.Interactable = TableTextFormat.ParseBool(raw, false)),
        new("InteractType", "string", template => TemplateInteractTypes.ToTemplateCode(template.InteractType), (template, raw) => template.InteractType = TemplateInteractTypes.ToCode(raw)),
        new("CollisionEnabled", "bool", template => TableTextFormat.Flag(template.CollisionEnabled), (template, raw) => template.CollisionEnabled = TableTextFormat.ParseBool(raw, false)),
        new("FacingDirection", "string", template => template.FacingDirection, (template, raw) => template.FacingDirection = FacingNames.ToCode(raw)),
        new("ShadowEnabled", "bool", template => TableTextFormat.Flag(template.ShadowEnabled), (template, raw) => template.ShadowEnabled = TableTextFormat.ParseBool(raw, false)),
        new("DialogBubble", "string", template => template.DialogBubble, (template, raw) => template.DialogBubble = raw),
        new("BubbleId", "int", template => TableTextFormat.Integer(template.BubbleId),
            (template, raw) => template.BubbleId = TableTextFormat.ParseInt(raw, template.BubbleId)),
        new("IsCollectible", "bool", template => TableTextFormat.Flag(template.IsCollectible), (template, raw) => template.IsCollectible = TableTextFormat.ParseBool(raw, false)),
        new("RespawnTime", "float", template => TableTextFormat.Number(template.RespawnTime), (template, raw) => template.RespawnTime = TableTextFormat.ParseNumber(raw, template.RespawnTime)),
        new("TrafficCategory", "string", template => template.TrafficCategory, (template, raw) => template.TrafficCategory = TemplateTrafficCategories.ToCode(raw)),
        new("YSortEnabled", "bool", template => TableTextFormat.Flag(template.YSortEnabled), (template, raw) => template.YSortEnabled = TableTextFormat.ParseBool(raw, false)),
        new("YSortOffset", "float", template => TableTextFormat.Number(template.YSortOffset), (template, raw) => template.YSortOffset = TableTextFormat.ParseNumber(raw, template.YSortOffset)),
        new("IsMovable", "bool", template => TableTextFormat.Flag(template.IsMovable), (template, raw) => template.IsMovable = TableTextFormat.ParseBool(raw, false)),
        new("HideWhenOffMap", "bool", template => TableTextFormat.Flag(template.HideWhenOffMap), (template, raw) => template.HideWhenOffMap = TableTextFormat.ParseBool(raw, false)),
        new("IsTeleport", "bool", template => TableTextFormat.Flag(template.IsTeleport), (template, raw) => template.IsTeleport = TableTextFormat.ParseBool(raw, false)),
        new("TeleportTargetScene", "string", template => template.TeleportTargetScene, (template, raw) => template.TeleportTargetScene = raw),
        new("TeleportTargetX", "float", template => TableTextFormat.Number(template.TeleportTargetX), (template, raw) => template.TeleportTargetX = TableTextFormat.ParseNumber(raw, template.TeleportTargetX)),
        new("TeleportTargetY", "float", template => TableTextFormat.Number(template.TeleportTargetY), (template, raw) => template.TeleportTargetY = TableTextFormat.ParseNumber(raw, template.TeleportTargetY)),
        new("GridWidth", "int", template => TableTextFormat.Integer(template.DefaultWidth),
            (template, raw) => template.DefaultWidth = Math.Max(MinGridExtent, TableTextFormat.ParseInt(raw, template.DefaultWidth))),
        new("GridHeight", "int", template => TableTextFormat.Integer(template.DefaultHeight),
            (template, raw) => template.DefaultHeight = Math.Max(MinGridExtent, TableTextFormat.ParseInt(raw, template.DefaultHeight))),
        new("LayerTypeKey", "string", template => template.LayerTypeKey, (template, raw) => template.LayerTypeKey = raw),
        new("ObjectType", "string", template => ObjectTypeNames.Of(template.Type), (template, raw) => template.Type = ObjectTypeNames.ToType(raw)),
        new("NineSliceEnabled", "bool", template => TableTextFormat.Flag(template.NineSlice.Enabled),
            (template, raw) => template.NineSlice.Enabled = TableTextFormat.ParseBool(raw, false)),
        new("NineSliceLeft", "int", template => TableTextFormat.Integer(template.NineSlice.Left),
            (template, raw) => template.NineSlice.Left = NineSliceSettings.ClampInset(TableTextFormat.ParseInt(raw, template.NineSlice.Left))),
        new("NineSliceRight", "int", template => TableTextFormat.Integer(template.NineSlice.Right),
            (template, raw) => template.NineSlice.Right = NineSliceSettings.ClampInset(TableTextFormat.ParseInt(raw, template.NineSlice.Right))),
        new("NineSliceTop", "int", template => TableTextFormat.Integer(template.NineSlice.Top),
            (template, raw) => template.NineSlice.Top = NineSliceSettings.ClampInset(TableTextFormat.ParseInt(raw, template.NineSlice.Top))),
        new("NineSliceBottom", "int", template => TableTextFormat.Integer(template.NineSlice.Bottom),
            (template, raw) => template.NineSlice.Bottom = NineSliceSettings.ClampInset(TableTextFormat.ParseInt(raw, template.NineSlice.Bottom))),
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

    #endregion
}
