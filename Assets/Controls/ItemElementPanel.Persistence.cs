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
            row.Id = _itemTable.NextId(_tabType);
            row.Icon = _tabType switch
            {
                MapObjectType.Building => $"building_new_{row.Id}",
                MapObjectType.Character => $"char_new_{row.Id}",
                _ => $"prop_new_{row.Id}",
            };
            row.TemplateName = $"新建{ObjectTypeNames.Of(_tabType)}{row.Id}";
            row.InteractType = _tabType == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk;
            table.Rows.Add(row);
            table.Dirty = true;
            _itemTable.RefreshIndexes();

            var created = TemplateOf(row);
            _selected = created;
            RefreshList();
            LoadForm();
            CatalogChanged?.Invoke(this, EventArgs.Empty);
            _notify($"已在 {table.Name} 新建一行 {row.Icon}，编辑后点「保存」写回表");
            return;
        }

        var template = new ObjectTemplate
        {
            TemplateId = TemplateStore.NextTemplateId(_templates, _tabType),
            TemplateName = $"新建{ObjectTypeNames.Of(_tabType)}元件",
            Type = _tabType,
            ResName = string.Empty,
            IsBuiltIn = false,
            AnimName = MapToolDefaults.DefaultAnimName,
            LayerTypeKey = _tabType switch
            {
                MapObjectType.Building => MapLayerKeys.Building,
                MapObjectType.Character => MapLayerKeys.Character,
                _ => MapLayerKeys.Prop,
            },
            InteractType = _tabType == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk,
            DefaultWidth = _tabType == MapObjectType.Building ? 4 : 1,
            DefaultHeight = _tabType == MapObjectType.Building ? 4 : _tabType == MapObjectType.Character ? 2 : 1,
            SortOrder = 0,
        };

        _templates.Add(template);
        _selected = template;
        RefreshList();
        LoadForm();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已新建元件 {template.TemplateId}，编辑后请点「保存」写盘");
    }

    private void RemoveTemplate()
    {
        if (_selected is null)
        {
            _notify("请先在列表中选中一个元件。");
            return;
        }

        // 列表直接读表时，删除就是把这一行从对应表里删掉
        if (_itemTable is not null && _rowTemplates.FirstOrDefault(pair => ReferenceEquals(pair.Value, _selected)) is { Key: { } tableRow })
        {
            var file = _itemTable.FileOf(tableRow);
            file?.Rows.Remove(tableRow);
            if (file is not null)
            {
                file.Dirty = true;
            }

            _itemTable.RefreshIndexes();
            _rowTemplates.Remove(tableRow);
            _selected = null;
            RefreshList();
            LoadForm();
            CatalogChanged?.Invoke(this, EventArgs.Empty);
            _notify(file is null
                ? $"已删除表行 {tableRow.Icon}（尚未写盘）"
                : $"已从 {file.Name} 删除行 {tableRow.Icon}（尚未写盘）");
            return;
        }

        var reason = _canRemove(_selected);
        if (reason is not null)
        {
            _notify(reason);
            return;
        }

        var removed = _selected;
        _templates.Remove(removed);
        _selected = null;
        RefreshList();
        LoadForm();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已删除元件 {removed.TemplateId}（尚未写盘）");
    }

    private Task SaveAsync()
    {
        if (_templates.Count == 0)
        {
            _notify("当前没有可保存的元件模板。");
            return Task.CompletedTask;
        }

        // 保存前先把仍在编辑中的输入框提交一次，避免最后一处改动丢失
        foreach (var commit in _committers)
        {
            commit();
        }

        if (!_store.TrySaveJson(_templates, out var error))
        {
            _notify($"保存失败：{error}");
            return Task.CompletedTask;
        }

        // 列表来自配置表时，把表单里的当前值同步回各行，再按各文件自身格式写回
        var tableNote = string.Empty;
        if (_itemTable is not null && _rowTemplates.Count > 0)
        {            foreach (var pair in _rowTemplates)
            {
                ApplyTemplateToRow(pair.Key, pair.Value);
                _itemTable.FileOf(pair.Key)?.Dirty = true;
            }

            _itemTable.RefreshIndexes();
            if (!_itemTable.TrySave(out var tableError))
            {
                _notify($"元件模板已保存，但配置表写回失败：{tableError}");
                return Task.CompletedTask;
            }

            tableNote = $"，并写回配置表 {_itemTable.Files.Count} 张 / {_itemTable.RowCount} 行";
        }

        // 九宫格是独立版扩展：现有 xlsm 没有这些列，单独存一份按素材名索引的旁路配置，
        // 既不会把列强塞进策划表，也不会在下次读表时被表行覆盖丢失。
        var nineSliceTemplates = _templates.Concat(_rowTemplates.Values).ToList();
        if (!_store.TrySaveNineSlices(nineSliceTemplates, out var nineSliceError))
        {
            _notify($"元件模板已保存，但九宫格配置写入失败：{nineSliceError}");
        }
        else
        {
            _nineSlices = BuildNineSliceCatalog();
        }

        _notify($"已保存 {_templates.Count} 个元件模板到 {_store.JsonPath}{tableNote}");
        NineSliceChanged?.Invoke(this, EventArgs.Empty);
        TemplatesSaved?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private async Task ImportCsvAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入元件配置表",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV 配置表") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
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
        foreach (var template in imported)
        {
            _templates.Add(template);
        }

        _selected = null;
        _tabType = MapObjectType.Building;
        RefreshList();
        LoadForm();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已导入 {imported.Count} 个元件模板：{Path.GetFileName(path)}");
    }

    private async Task ExportCsvAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出元件配置表",
            SuggestedFileName = _store.DefaultCsvFileName,
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV 配置表") { Patterns = new[] { "*.csv" } },
            },
        });

        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        if (!_store.TryExportCsv(path, _templates, out var error))
        {
            _notify($"导出失败：{error}");
            return;
        }

        _notify($"已导出 {_templates.Count} 个元件模板到 {path}");
    }

    #endregion
}
