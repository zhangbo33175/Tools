/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Persistence.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件面板增删与持久化：模板新建/删除、JSON/CSV 导入导出、保存。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;
using MapEditorTool.Controls;
namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 增删与持久化

    private void AddTemplate()
    {
        // 有对应配置表时，新增直接落表（否则列表读表，看不到 JSON 里新建的模板）
        var table = ActiveTableFile();
        if (table is not null && _itemTable is not null)
        {
            var row = table.CreateRow();
            row.Id = _itemTable.NextId(_tabType, _templates.Select(template => template.TemplateId));
            row.Icon = BuildNewRowIcon(row.Id);
            row.TemplateName = $"新建{ObjectTypeNames.Of(_tabType)}{row.Id}";
            row.InteractType = TemplateDefaults.InteractType(_tabType);
            table.Rows.Add(row);
            table.Dirty = true;
            _itemTable.RefreshIndexes();

            var created = TemplateOf(row);
            RefreshAfterCatalogChange(created);
            _notify($"已在 {table.Name} 新建一行 {row.Icon}，编辑后点「保存」写回表");
            return;
        }

        var template = new ObjectTemplate
        {
            TemplateId = TemplateIdAllocator.Next(_tabType, _templates.Select(template => template.TemplateId)),
            TemplateName = $"新建{ObjectTypeNames.Of(_tabType)}元件",
            Type = _tabType,
            IsBuiltIn = false,
            LayerTypeKey = TemplateDefaults.LayerKey(_tabType),
            InteractType = TemplateDefaults.InteractType(_tabType),
            DefaultWidth = TemplateDefaults.PlacementWidth(_tabType),
            DefaultHeight = TemplateDefaults.PlacementHeight(_tabType),
        };

        _templates.Add(template);
        RefreshAfterCatalogChange(template);
        _notify($"已新建元件 {template.TemplateId}，编辑后请点「保存」写盘");
    }

    /// <summary>按当前分类生成新建表行的建议图片名前缀。</summary>
    /// <param name="id">表行 Id。</param>
    /// <returns>图片名，如 <c>building_new_3000</c>。</returns>
    private string BuildNewRowIcon(string id) =>
        $"{ResourceCategories.AssetPrefixOfObjectType(_tabType)}new_{id}";

    /// <summary>
    /// 新增、删除、导入后的统一收尾：写入新的选中对象、重建列表、加载表单并广播目录变更。
    /// </summary>
    /// <param name="selected">新的选中模板，null 表示清空选中。</param>
    private void RefreshAfterCatalogChange(ObjectTemplate? selected)
    {
        _selected = selected;
        RefreshList();
        LoadForm();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task SaveAsync()
    {
        if (_templates.Count == 0)
        {
            _notify("当前没有可保存的元件模板。");
            return Task.CompletedTask;
        }

        CommitPendingEdits();

        var duplicates = TemplateSyncService.FindDuplicateIds(_templates);
        if (duplicates.Count > 0)
        {
            _notify($"模板 ID 重复：{string.Join("、", duplicates)}。请先删除重复行再保存，否则实例同步只会取靠后那一行。");
            return Task.CompletedTask;
        }

        if (!_store.TrySaveJson(_templates, out var error))
        {
            _notify(UiFeedback.Failed("保存", error));
            return Task.CompletedTask;
        }

        var tableNote = SyncConfigTables();
        if (tableNote is null)
        {
            return Task.CompletedTask;
        }

        SaveNineSliceCatalog();
        _notify($"已保存 {_templates.Count} 个元件模板到 {_store.JsonPath}{tableNote}");
        NineSliceChanged?.Invoke(this, EventArgs.Empty);
        TemplatesSaved?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>保存前提交所有仍在编辑中的输入框，避免最后一处改动丢失。</summary>
    private void CommitPendingEdits()
    {
        foreach (var commit in _committers)
        {
            commit();
        }
    }

    /// <summary>
    /// 把表单当前值同步回各配置表行并按文件格式写回。
    /// </summary>
    /// <returns>成功时返回用于保存提示的表数量说明；写回失败时返回 null 并已提示用户。</returns>
    private string? SyncConfigTables()
    {
        if (_itemTable is null || _rowTemplates.Count == 0)
        {
            return string.Empty;
        }

        // 列表来自配置表时，把表单里的当前值同步回各行，再按各文件自身格式写回
        foreach (var pair in _rowTemplates)
        {
            ApplyTemplateToRow(pair.Key, pair.Value);
            _itemTable.FileOf(pair.Key)?.Dirty = true;
        }

        _itemTable.RefreshIndexes();
        if (!_itemTable.TrySave(out var tableError))
        {
            _notify(UiFeedback.Failed("元件模板已保存，但配置表写回", tableError));
            return null;
        }

        return $"，并写回配置表 {_itemTable.Files.Count} 张 / {_itemTable.RowCount} 行";
    }

    /// <summary>
    /// 九宫格是独立版扩展：现有 xlsm 没有这些列，单独存一份按素材名索引的旁路配置，
    /// 既不会把列强塞进策划表，也不会在下次读表时被表行覆盖丢失。
    /// </summary>
    private void SaveNineSliceCatalog()
    {
        var nineSliceTemplates = _templates.Concat(_rowTemplates.Values).ToList();
        if (!_store.TrySaveNineSlices(nineSliceTemplates, out var nineSliceError))
        {
            _notify(UiFeedback.Failed("元件模板已保存，但九宫格配置写入", nineSliceError));
            return;
        }

        _nineSlices = BuildNineSliceCatalog();
    }

    private async Task ImportCsvAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var path = await FileDialogService.PickFileAsync(
            top,
            ImportDialogTitle,
            FilePickerCatalog.CsvTable,
            FilePickerFileTypes.All);

        if (path is null)
        {
            return;
        }

        var imported = _store.ImportCsv(path, out var error);
        if (imported.Count == 0)
        {
            _notify($"导入失败：{(string.IsNullOrEmpty(error) ? "文件中没有数据行。" : error)}");
            return;
        }

        _templates.Clear();
        _templates.AddRange(imported);

        _tabType = MapObjectType.Building;
        RefreshAfterCatalogChange(null);
        _notify($"已导入 {imported.Count} 个元件模板：{Path.GetFileName(path)}");
    }

    private async Task ExportCsvAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var path = await FileDialogService.SaveFileAsync(
            top,
            ExportDialogTitle,
            _store.DefaultCsvFileName,
            "csv",
            FilePickerCatalog.CsvTable);

        if (path is null)
        {
            return;
        }

        if (!_store.TryExportCsv(path, _templates, out var error))
        {
            _notify(UiFeedback.Failed("导出", error));
            return;
        }

        _notify($"已导出 {_templates.Count} 个元件模板到 {path}");
    }

    #endregion
}
