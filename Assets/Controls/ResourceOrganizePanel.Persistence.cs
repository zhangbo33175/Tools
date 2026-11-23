/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Persistence.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源面板持久化：配置表保存、打开与路径选择。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    #region 保存与打开

    private void SaveChanges()
    {
        if (!_store.TrySaveJson(_templates, out var error))
        {
            _notify($"保存失败：{error}");
            return;
        }

        // 地图物件配置表：只回写「本来就在表里 / 本次显式加进去」的行，
        // 不能把已配置资源列表里的老模板（B_/C_/P_ 这类来自 xlsm 的）全塞进 CSV。
        var tableNote = string.Empty;
        if (_itemTable is not null && _tableDirty)
        {
            foreach (var template in _templates)
            {
                var key = string.IsNullOrWhiteSpace(template.ResName) ? template.TemplateName : template.ResName;
                if (_itemTable.Find(key) is not null)
                {
                    _itemTable.Upsert(template);
                }
            }

            if (!_itemTable.TrySave(out var tableError))
            {
                _notify($"配置表写入失败：{tableError}");
                return;
            }

            _tableDirty = false;
            tableNote = $"；已写回 {_itemTable.Describe()}（共 {_itemTable.RowCount} 行）";
        }

        var tables = ExportCategoryTables();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已保存 {_templates.Count} 条配置到 {_store.JsonPath}"
                + (tables.Length == 0 ? string.Empty : $"；同时导出 {tables}")
                + tableNote);
    }

    /// <summary>按分类把模板导出为 CSV 配置表（与 Unity 侧每类一张表的口径一致）。</summary>
    private string ExportCategoryTables()
    {
        var tableDirectory = Path.Combine(_store.OutputDirectory, "Tables");
        Directory.CreateDirectory(tableDirectory);

        var files = new List<string>();
        foreach (var category in new[] { "bg", "building", "character", "prop", "cat" })
        {
            var subset = _templates
                .Where(template => CategoryOf(template.ResName.Length > 0 ? template.ResName : template.TemplateName) == category)
                .ToList();

            // 空分类也写一张只有表头的表，保持五张表齐全（Unity 侧按表存在与否决定是否加载）
            var path = Path.Combine(tableDirectory, category + "Config.csv");
            if (_store.TryExportCsv(path, subset, out _))
            {
                files.Add(Path.GetFileName(path));
            }
        }

        return string.Join("、", files);
    }

    private void OpenConfigTable()
    {
        var path = _itemTable is not null && File.Exists(_itemTable.PrimaryPath)
            ? _itemTable.PrimaryPath
            : File.Exists(_store.JsonPath) ? _store.JsonPath : _store.OutputDirectory;
        OpenWithShell(path);
    }

    private void OpenFolder()
    {
        var directory = _itemTable is not null && Directory.Exists(_itemTable.Directory)
            ? _itemTable.Directory
            : Path.Combine(_store.OutputDirectory, "Tables");
        if (!Directory.Exists(directory))
        {
            directory = _store.OutputDirectory;
        }

        OpenWithShell(directory);
    }

    private void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            _notify($"已打开：{path}");
        }
        catch (Exception error)
        {
            _notify($"打开失败：{error.Message}");
        }
    }

    #endregion
}
