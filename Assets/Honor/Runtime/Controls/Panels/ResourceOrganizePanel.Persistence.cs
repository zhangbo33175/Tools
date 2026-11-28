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
using MapEditorTool.Controls;
namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel : UserControl
{
    #region 保存与打开

    /// <summary>为当前「必须独立成表」的分类创建配置表并立即落盘。</summary>
    private void CreateCurrentCategoryTable()
    {
        if (_itemTable is null)
        {
            _notify("配置表文件夹尚未加载，无法创建分类表");
            return;
        }

        var file = _itemTable.CreateCategoryTable(_filter, out var error);
        if (file is null)
        {
            _notify(error);
            return;
        }

        RefreshAfterTableChange();
        _notify($"已创建 {file.Name}：{file.Path}。现在可在左侧选择收集物图片并添加到这张表。");
    }

    /// <summary>打开命名体检窗：按图片名生成中文显示名建议，确认后写进各表的「显示名称」列。</summary>
    private void OpenNamingAudit()
    {
        if (_itemTable is null)
        {
            _notify("配置表文件夹尚未加载，无法做命名体检");
            return;
        }

        // 词表与 ObjectTemplates.json 同放在配置目录（output/Config）下
        var glossary = NamingGlossary.Load(_store.OutputDirectory);
        var window = new NamingAuditWindow(_itemTable, glossary, () =>
        {
            RefreshAfterTableChange();
            _notify($"命名体检已写回：{_itemTable.Describe()}（原表已备份）");
        });

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _ = window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }

    private void SaveChanges()
    {
        if (!_store.TrySaveJson(_templates, out var error))
        {
            _notify(UiFeedback.Failed("保存", error));
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
                _notify(UiFeedback.Failed("配置表写入", tableError));
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
        var tableDirectory = Path.Combine(_store.OutputDirectory, TablesDirectoryName);
        FileSystemIo.EnsureDirectory(tableDirectory);

        var files = new List<string>();
        foreach (var category in ResourceCategories.ConfiguredCategories)
        {
            var subset = _templates
                .Where(template => ResourceCategories.Classify(template.ResName.Length > 0 ? template.ResName : template.TemplateName) == category.Key)
                .ToList();

            // 空分类也写一张只有表头的表，保持五张表齐全（Unity 侧按表存在与否决定是否加载）
            var path = Path.Combine(tableDirectory, category.Key + "Config.csv");
            if (_store.TryExportCsv(path, subset, out _))
            {
                files.Add(Path.GetFileName(path));
            }
        }

        return string.Join("、", files);
    }

    private void OpenConfigTable()
    {
        // 分类页优先打开当前分类那张表：收集物 → TableCollectibles，而不是永远开主表
        var active = ActiveTableFile();
        if (active is not null)
        {
            if (File.Exists(active.Path))
            {
                OpenWithShell(active.Path);
            }
            else
            {
                _notify($"「{active.Name}」尚未落盘，请先点「保存修改」。");
            }

            return;
        }

        var path = _itemTable is not null && File.Exists(_itemTable.PrimaryPath)
            ? _itemTable.PrimaryPath
            : File.Exists(_store.JsonPath) ? _store.JsonPath : _store.OutputDirectory;
        OpenWithShell(path);
    }

    private void OpenFolder()
    {
        var directory = _itemTable is not null && Directory.Exists(_itemTable.Directory)
            ? _itemTable.Directory
            : Path.Combine(_store.OutputDirectory, TablesDirectoryName);
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
            _notify(UiFeedback.Failed("打开", error));
        }
    }

    #endregion
}
