/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Table.Rows.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表：行渲染、行内编辑、删除与撤销
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel : UserControl
{

    /// <summary>把配置表的行画成「已配置资源列表」（单元格直接改行模型并标记该表待保存）。</summary>
    /// <param name="rows">要显示的行（当前分类那张表的全部行）。</param>
    private void BuildTableRows(IEnumerable<ItemElementRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            var tableName = ActiveTableFile()?.Name ?? "配置表";
            AddEmptyMessage($"「{_filter}」在 {tableName} 里还没有行 —— 左侧选中图片后点「{AddToTableButtonText}」。");
            return;
        }

        BuildResourceBodyGrid(list, BuildTableRowActions, MarkTableRowDirty);
    }

    /// <summary>
    /// 已配置资源表的统一行体：按共享行视图一次性映射 12 个字段，
    /// 配置表行与模板库只提供行尾操作与写回后的附加动作。
    /// </summary>
    /// <typeparam name="TRow">实现共享行视图的行类型。</typeparam>
    /// <param name="rows">要渲染的行。</param>
    /// <param name="buildActions">构建行尾「编辑 / 删除」按钮。</param>
    /// <param name="afterWrite">任一字段写回后的附加动作，可为 null。</param>
    private void BuildResourceBodyGrid<TRow>(
        IReadOnlyList<TRow> rows,
        Func<TRow, Control> buildActions,
        Action<TRow>? afterWrite)
        where TRow : IResourceRowData
    {
        var bodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(TableColumns),
            RowDefinitions = BuildBodyRowDefinitions(rows.Count),
        };

        // 重建前清空上一张表的行→单元格索引，避免悬停高亮打到已移除的旧单元格
        _rowHoverCells.Clear();
        _hoveredRow = -1;

        for (var row = 0; row < rows.Count; row++)
        {
            var item = rows[row];

            AddCell(bodyGrid, ReadOnly(item.TemplateId), 0, row);
            AddCell(bodyGrid, BindResourceCell(item, item.ImagePath, (value, text) => value.ImagePath = text, afterWrite), 1, row);
            AddCell(bodyGrid, BindResourceCell(item, item.ResName, (value, text) => value.ResName = text, afterWrite), 2, row);
            AddCell(bodyGrid, BindResourceCell(item, item.TemplateName, (value, text) => value.TemplateName = text, afterWrite), 3, row);
            AddCell(bodyGrid, BindResourceCell(item, item.AnimName, (value, text) => value.AnimName = text, afterWrite), 4, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Flag(item.Loop),
                (value, text) => value.Loop = TableTextFormat.ParseBool(text, value.Loop),
                afterWrite), 5, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Number(item.Speed),
                (value, text) => value.Speed = TableTextFormat.ParseNumber(text, value.Speed),
                afterWrite), 6, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Integer(item.SortOrder),
                (value, text) => value.SortOrder = (int)TableTextFormat.ParseNumber(text, value.SortOrder),
                afterWrite), 7, row);

            AddCell(bodyGrid, BindResourceCell(item, item.DialogBubble, (value, text) => value.DialogBubble = text, afterWrite), 8, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Flag(item.ShadowEnabled),
                (value, text) => value.ShadowEnabled = TableTextFormat.ParseBool(text, value.ShadowEnabled),
                afterWrite), 9, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Flag(item.IsCollectible),
                (value, text) => value.IsCollectible = TableTextFormat.ParseBool(text, value.IsCollectible),
                afterWrite), 10, row);

            AddCell(bodyGrid, BindResourceCell(
                item,
                TableTextFormat.Number(item.RespawnTime),
                (value, text) => value.RespawnTime = TableTextFormat.ParseNumber(text, value.RespawnTime),
                afterWrite), 11, row);

            AddActionCell(bodyGrid, buildActions(item), row);
        }

        _configuredHost.Children.Add(bodyGrid);
        AttachRowHover(bodyGrid, rows.Count);
    }

    /// <summary>绑定一个共享行字段：写回后统一触发附加动作（如标脏、刷新索引）。</summary>
    /// <typeparam name="TRow">行类型。</typeparam>
    /// <param name="row">目标行。</param>
    /// <param name="text">当前显示文本。</param>
    /// <param name="write">把文本解析结果写回行。</param>
    /// <param name="afterWrite">写回后的附加动作。</param>
    private Control BindResourceCell<TRow>(
        TRow row,
        string text,
        Action<TRow, string> write,
        Action<TRow>? afterWrite)
        where TRow : IResourceRowData =>
        Bind(text, value =>
        {
            write(row, value);
            afterWrite?.Invoke(row);
        });

    /// <summary>配置表行任一字段改动后：标记所在表待保存，并刷新表行索引。</summary>
    /// <param name="item">被修改的表行。</param>
    private void MarkTableRowDirty(ItemElementRow item)
    {
        if (_itemTable?.FileOf(item) is { } file)
        {
            file.Dirty = true;
            _tableDirty = true;
        }

        _itemTable?.RefreshIndexes();
    }

    /// <summary>行内「编辑 / 删除」按钮（针对配置表行，不是模板库）。</summary>
    /// <param name="item">表行。</param>
    /// <returns>按钮组。</returns>
    private Control BuildTableRowActions(ItemElementRow item) =>
        BuildRowActions(() => EditTableRow(item), () => RemoveTableRow(item));

    /// <summary>点表行的「编辑」：把该行套进表单进入修改模式（有同名模板就复用，没有就造一个临时模板）。</summary>
    private void EditTableRow(ItemElementRow item)
    {
        var template = _idCoordinator.FindTemplate(item.Icon)
                       ?? _idCoordinator.FindTemplate(item.TemplateName);
        if (template is null)
        {
            template = new ObjectTemplate { Type = ResourceCategories.ObjectTypeOfName(item.Icon), IsBuiltIn = false };
            item.ApplyToTemplate(template);
            template.ImagePath = item.Icon;
            template.ResName = string.IsNullOrWhiteSpace(item.Icon) ? item.TemplateName : item.Icon;
        }

        SelectTemplateForEdit(template);
    }

    /// <summary>
    /// 删除配置表行的公共流程：修复重复 ID、校验实例引用、删行并顺延同段 ID。
    /// </summary>
    /// <param name="item">要删除的行。</param>
    /// <returns>删除成功返回 true；失败时已给出提示。</returns>
    // ---TODO 重构：模板删除与表格行删除共用同一套行删除事务，避免两套流程状态不一致。
    private bool RemoveTableRowCore(ItemElementRow item)
    {
        var file = _itemTable?.FileOf(item);
        if (file is null)
        {
            _notify($"没能删除 {item.Icon}：配置表里找不到该行");
            return false;
        }

        var type = TemplateIdAllocator.TypeOf(item.Id)
                   ?? ResourceCategories.ObjectTypeOfName(item.Icon);
        // ---TODO ID 管理：删除前只在确有重复时修复，避免无谓改号；删除后再把后续行顺延。
        RepairTableIdsIfNeeded(file, type);

        var displayId = item.Id.Trim();
        if (!EnsureNotReferenced(displayId))
        {
            return false;
        }

        if (_itemTable?.Remove(item) != true)
        {
            _notify($"没能删除 {item.Icon}：配置表里找不到该行");
            return false;
        }

        ShiftTableIdsAfterRemoval(file, type, displayId);
        return true;
    }

    /// <summary>表行「删除」：删掉该表里的这一行（拦被场景实例引用的 ID），并让后续 ID 顺延。</summary>
    private void RemoveTableRow(ItemElementRow item)
    {
        if (!RemoveTableRowCore(item))
        {
            return;
        }

        var displayId = item.Id.Trim();
        var template = _idCoordinator.FindTemplate(item.Icon);
        if (template is not null)
        {
            _templates.Remove(template);
        }

        RefreshAfterTableChange();
        _notify($"已删除 {displayId}（{item.Icon}），后续 ID 已顺延，记得点「保存修改」落盘");
    }
}
