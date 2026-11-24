/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemTableFile.cs
 * author:    云毅
 * created:   2026
 * descrip:   单张物件配置表：列名映射、行号分配与字段写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

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
        SetExcelBool(row, ItemFieldNames.Loop, row.Loop);
        SetField(row, ItemFieldNames.Speed, TableTextFormat.Number(row.Speed));
        SetExcelBool(row, ItemFieldNames.Interactable, row.Interactable);
        SetField(row, ItemFieldNames.InteractType, row.InteractType);
        SetField(row, ItemFieldNames.SortOrder, TableTextFormat.Integer(row.SortOrder));
        SetExcelBool(row, ItemFieldNames.Collision, row.CollisionEnabled);
        SetField(row, ItemFieldNames.DialogBubble, row.DialogBubble);
        SetField(row, ItemFieldNames.FacingDirection, row.FacingDirection);
        SetField(row, ItemFieldNames.DisplayLayer, row.DisplayLayer);
        SetExcelBool(row, ItemFieldNames.Shadow, row.ShadowEnabled);
        SetExcelBool(row, ItemFieldNames.Collectible, row.IsCollectible);
        SetField(row, ItemFieldNames.RespawnTime, TableTextFormat.Number(row.RespawnTime));
        SetField(row, ItemFieldNames.TrafficCategory, row.TrafficCategory);
        SetExcelBool(row, ItemFieldNames.YSortEnabled, row.YSortEnabled);
        SetField(row, ItemFieldNames.YSortOffset, TableTextFormat.Number(row.YSortOffset));
        SetExcelBool(row, ItemFieldNames.Movable, row.IsMovable);
        SetField(row, ItemFieldNames.LoopRestartDelay, TableTextFormat.Number(row.LoopRestartDelay));
        SetExcelBool(row, ItemFieldNames.HideWhenOffMap, row.HideWhenOffMap);
        SetExcelBool(row, ItemFieldNames.Teleport, row.IsTeleport);
        SetField(row, ItemFieldNames.TeleportScene, row.TeleportTargetScene);
        SetField(row, ItemFieldNames.TeleportX, TableTextFormat.Number(row.TeleportTargetX));
        SetField(row, ItemFieldNames.TeleportY, TableTextFormat.Number(row.TeleportTargetY));
        SetExcelBool(row, ItemFieldNames.NineSliceEnabled, row.NineSliceEnabled);
        SetField(row, ItemFieldNames.NineSliceLeft, TableTextFormat.Integer(row.NineSliceLeft));
        SetField(row, ItemFieldNames.NineSliceRight, TableTextFormat.Integer(row.NineSliceRight));
        SetField(row, ItemFieldNames.NineSliceTop, TableTextFormat.Integer(row.NineSliceTop));
        SetField(row, ItemFieldNames.NineSliceBottom, TableTextFormat.Integer(row.NineSliceBottom));
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

    /// <summary>按 Excel 配置表口径写布尔单元格：<c>True</c> / <c>False</c>。</summary>
    /// <param name="row">数据行。</param>
    /// <param name="field">字段键。</param>
    /// <param name="value">布尔值。</param>
    private void SetExcelBool(ItemElementRow row, string field, bool value) =>
        SetField(row, field, TableTextFormat.ExcelBool(value));

}

#endregion

