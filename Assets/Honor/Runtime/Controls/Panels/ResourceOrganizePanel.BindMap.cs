/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.BindMap.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源整理：把地图上用到的资源补齐成配置行并分配数字 ID
 ***************************************************************/
using System;
using System.Linq;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel
{
    #region 地图资源入表

    /// <summary>
    /// 「补齐地图资源 ID」：把当前地图里仍在用资源名当模板 ID 的实例，
    /// 对到已有配置行；配置表里没有的资源按分类新建行、分配该分类段的下一个数字 ID，
    /// 写回实例模板 ID 后立刻保存配置表（保存前先整目录备份）。
    /// </summary>
    private void BindMapResourcesToTable()
    {
        if (_itemTable is null)
        {
            _notify("配置表尚未加载，无法补齐地图资源 ID");
            return;
        }

        if (_config is null)
        {
            _notify("尚未载入地图，无法补齐地图资源 ID");
            return;
        }

        ItemTableBackup.TryCreate(_itemTable.Directory, out var backup);
        var reserved = _templates.Select(template => template.TemplateId).ToList();
        var result = MapTemplateBinder.Bind(_config, _itemTable, createMissing: true, reserved);

        if (result.Created > 0)
        {
            if (!_itemTable.TrySave(out var error))
            {
                _notify($"地图资源已对号，但配置表保存失败：{error}");
                return;
            }

            _tableDirty = false;
            AppendNewTableTemplates();
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
        RefreshAll();

        var backupText = backup.Length > 0 ? $"，备份在 {backup}" : "，备份未生成";
        _notify(
            $"地图资源已对齐配置表 ID：新建 {result.Created} 行、对号 {result.Bound} 个实例，已有 {result.AlreadyBound} 个实例在用配置表 ID" +
            (result.Created > 0 ? backupText : "，表无需改动"));
        if (result.Missing.Count > 0)
        {
            _notify($"仍有 {result.Missing.Count} 个资源没有对上配置表：{string.Join("、", result.Missing.Take(6))}");
        }
    }

    /// <summary>
    /// 新建行之后同步进模板集合，场景资源列表与物品元件编辑才能按 ID 摆放同一批资源。
    /// 只补「模板集合里还没有的配置行」，已有模板不覆盖。
    /// </summary>
    private void AppendNewTableTemplates()
    {
        if (_itemTable is null)
        {
            return;
        }

        var known = _templates
            .Select(template => template.TemplateId.Trim())
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in _itemTable.Files)
        {
            foreach (var row in file.Rows)
            {
                var id = row.Id.Trim();
                if (id.Length == 0 || !known.Add(id))
                {
                    continue;
                }

                var type = ResourceCategories.ObjectTypeOfName(row.Icon);
                _templates.Add(ItemConfigSet.CreateTemplate(row, type));
            }
        }
    }

    #endregion
}
