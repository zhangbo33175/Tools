/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemRowMapper.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表行映射：列索引、单元格取值与 ItemElementRow 绑定
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MapEditorTool.Services;
#region 行数据映射

/// <summary>把「列名 → 单元格」的表行映射成 <see cref="ItemElementRow"/>。</summary>
internal static class ItemRowMapper
{
    /// <summary>按列名表建立字段 → 列下标映射。</summary>
    /// <param name="columnNames">列名（按列序）。</param>
    /// <param name="target">目标映射表。</summary>
    public static void BuildColumnIndex(IReadOnlyList<string> columnNames, Dictionary<string, int> target)
    {
        target.Clear();
        foreach (var pair in ItemFieldNames.Aliases)
        {
            for (var index = 0; index < columnNames.Count; index++)
            {
                var name = columnNames[index].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (pair.Value.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    target.TryAdd(pair.Key, index);
                    break;
                }
            }
        }
    }

    /// <summary>把一行单元格映射成行模型。</summary>
    /// <param name="cells">单元格（按列序）。</param>
    /// <param name="columns">字段 → 列下标。</param>
    /// <param name="rowNumber">行号。</param>
    /// <returns>行模型。</returns>
    public static ItemElementRow Bind(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, int rowNumber)
    {
        var row = new ItemElementRow { RowNumber = rowNumber };
        foreach (var cell in cells)
        {
            row.Cells.Add(cell);
        }

        row.Id = Get(cells, columns, ItemFieldNames.Id);
        row.Icon = Get(cells, columns, ItemFieldNames.Icon);
        row.ResName = Get(cells, columns, ItemFieldNames.ResName);
        row.TemplateName = Get(cells, columns, ItemFieldNames.TemplateName);
        row.AnimName = Get(cells, columns, ItemFieldNames.AnimName) is { Length: > 0 } anim ? anim : "Idle";
        row.Loop = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Loop), true);
        row.Speed = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.Speed), 1.0);
        row.Interactable = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Interactable), true);
        row.InteractType = Get(cells, columns, ItemFieldNames.InteractType);
        row.SortOrder = (int)TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.SortOrder), 0);
        row.CollisionEnabled = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Collision), false);
        row.DialogBubble = Get(cells, columns, ItemFieldNames.DialogBubble);
        row.FacingDirection = Get(cells, columns, ItemFieldNames.FacingDirection);
        row.DisplayLayer = Get(cells, columns, ItemFieldNames.DisplayLayer);
        row.ShadowEnabled = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Shadow), true);
        row.IsCollectible = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Collectible), false);
        row.RespawnTime = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.RespawnTime), 0);
        row.TrafficCategory = Get(cells, columns, ItemFieldNames.TrafficCategory) is { Length: > 0 } traffic ? traffic : "none";
        row.YSortEnabled = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.YSortEnabled), true);
        row.YSortOffset = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.YSortOffset), 0);
        row.IsMovable = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Movable), false);
        row.LoopRestartDelay = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.LoopRestartDelay), 0);
        row.HideWhenOffMap = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.HideWhenOffMap), true);
        row.IsTeleport = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.Teleport), false);
        row.TeleportTargetScene = Get(cells, columns, ItemFieldNames.TeleportScene);
        row.TeleportTargetX = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.TeleportX), 0);
        row.TeleportTargetY = TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.TeleportY), 0);
        row.NineSliceEnabled = TableTextFormat.ParseBool(Get(cells, columns, ItemFieldNames.NineSliceEnabled), false);
        row.NineSliceLeft = (int)TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceLeft), 0);
        row.NineSliceRight = (int)TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceRight), 0);
        row.NineSliceTop = (int)TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceTop), 0);
        row.NineSliceBottom = (int)TableTextFormat.ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceBottom), 0);
        row.NineSliceCenterMode = Get(cells, columns, ItemFieldNames.NineSliceCenterMode);
        return row;
    }

    /// <summary>该行看起来是不是「类型 / 备注」表头行。</summary>
    /// <param name="cells">单元格。</param>
    /// <returns>是表头行返回 true。</param>
    public static bool LooksLikeTypeRow(IReadOnlyList<string> cells)
    {
        var nonEmpty = cells.Where(cell => !string.IsNullOrWhiteSpace(cell)).ToList();
        if (nonEmpty.Count == 0)
        {
            return true;
        }

        return nonEmpty.Take(6).Any(cell => ItemFieldNames.TypeTokens.Contains(cell.Trim()));
    }

    private static string Get(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string field)
    {
        if (!columns.TryGetValue(field, out var index) || index < 0 || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index].Trim();
    }
}

#endregion
