/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateIdCoordinator.cs
 * author:    云毅
 * created:   2026
 * descrip:   模板与配置表共用的 ID 生命周期协调：分配、占用、修复与顺延
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 模板 / 配置表 ID 的统一协调入口。
/// 页面只负责提供当前数据源并接收改号映射，不再自己判断分段、重复或顺延规则。
/// </summary>
internal sealed class TemplateIdCoordinator
{
    #region 字段

    private readonly Func<ItemConfigSet?> _tableProvider;
    private readonly Func<IEnumerable<ObjectTemplate>> _templateProvider;
    private readonly Action<IReadOnlyDictionary<string, string>> _applyReferences;

    #endregion

    #region 构造

    /// <summary>初始化 ID 协调器。</summary>
    /// <param name="tableProvider">当前配置表集合，可为空。</param>
    /// <param name="templateProvider">当前模板集合。</param>
    /// <param name="applyReferences">改号后同步模板 / 地图引用的回调。</param>
    public TemplateIdCoordinator(
        Func<ItemConfigSet?> tableProvider,
        Func<IEnumerable<ObjectTemplate>> templateProvider,
        Action<IReadOnlyDictionary<string, string>> applyReferences)
    {
        _tableProvider = tableProvider ?? throw new ArgumentNullException(nameof(tableProvider));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        _applyReferences = applyReferences ?? throw new ArgumentNullException(nameof(applyReferences));
    }

    #endregion

    #region 查询与分配

    /// <summary>生成模板与配置表共用的下一个 ID，两个来源都参与去重。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>未占用的 ID 文本。</returns>
    public string Allocate(MapObjectType type)
    {
        var templateIds = _templateProvider().Select(template => template.TemplateId);
        var tables = _tableProvider();
        return tables?.NextId(type, templateIds)
               ?? TemplateIdAllocator.Next(type, templateIds);
    }

    /// <summary>判断 ID 是否已被模板或配置表行占用。</summary>
    /// <param name="id">待检查 ID。</param>
    /// <returns>已占用返回 true。</returns>
    public bool IsTaken(string? id)
    {
        var key = id?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return false;
        }

        return _templateProvider().Any(template => string.Equals(template.TemplateId.Trim(), key, StringComparison.Ordinal))
               || _tableProvider()?.Rows.Any(row => string.Equals(row.Id.Trim(), key, StringComparison.Ordinal)) == true;
    }

    #endregion

    #region 模板匹配

    /// <summary>按资源名 / 路径查找模板；路径优先，避免表单改名后匹配到别的资源。</summary>
    /// <param name="name">资源名或模板名。</param>
    /// <param name="path">资源磁盘路径，可为空。</param>
    /// <returns>命中的模板；找不到为 null。</returns>
    public ObjectTemplate? FindTemplate(string? name, string? path = null)
    {
        var templates = _templateProvider().ToList();
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var template in templates)
            {
                if (MatchesResource(template, path: path))
                {
                    return template;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var template in templates)
        {
            if (MatchesResource(template, name))
            {
                return template;
            }
        }

        return null;
    }

    /// <summary>判断模板集合里是否已有同名资源。</summary>
    /// <param name="name">资源名。</param>
    /// <returns>已存在返回 true。</returns>
    public bool HasTemplateNamed(string? name) => FindTemplate(name) is not null;

    /// <summary>判断模板是否对应指定资源；路径优先，其次比较资源名。</summary>
    /// <param name="template">模板；null 直接返回 false。</param>
    /// <param name="name">资源名。</param>
    /// <param name="path">资源磁盘路径，可为空。</param>
    /// <returns>属于同一资源返回 true。</returns>
    public static bool MatchesResource(ObjectTemplate? template, string? name = null, string? path = null)
    {
        if (template is null)
        {
            return false;
        }

        var value = string.IsNullOrWhiteSpace(path) ? name : path;
        return ResourceNames.MatchesAny(
            value,
            template.ImagePath,
            template.ResName,
            template.TemplateName);
    }

    #endregion

    #region 表内修复与顺延

    /// <summary>修复所有表内的重复 ID；只处理确有重复的类型段，正常表保持原样。</summary>
    /// <returns>发生了改号返回 true。</returns>
    public bool RepairDuplicateTableIds()
    {
        var tables = _tableProvider();
        if (tables is null)
        {
            return false;
        }

        var changed = false;
        foreach (var file in tables.Files)
        {
            foreach (var type in TemplateIdAllocator.Types)
            {
                if (tables.HasDuplicateIds(file, type))
                {
                    changed |= ApplyChange(file, tables.NormalizeSequentialIds(file, type));
                }
            }
        }

        return changed;
    }

    /// <summary>目标表指定类型段确有重复时才修复，避免正常表被无谓改号。</summary>
    /// <param name="file">目标表；null 表示未指定。</param>
    /// <param name="type">物件类型。</param>
    /// <returns>发生了改号返回 true。</returns>
    public bool RepairTableIfNeeded(ItemTableFile? file, MapObjectType type)
    {
        var tables = _tableProvider();
        if (tables is null || file is null || !tables.HasDuplicateIds(file, type))
        {
            return false;
        }

        return ApplyChange(file, tables.NormalizeSequentialIds(file, type));
    }

    /// <summary>删除表行后，把同一类型段内后续 ID 顺延前移。</summary>
    /// <param name="file">目标表；null 表示未指定。</param>
    /// <param name="type">物件类型。</param>
    /// <param name="removedId">刚被删除的行 ID。</param>
    /// <returns>发生了改号返回 true。</returns>
    public bool ShiftAfterRemoval(ItemTableFile? file, MapObjectType type, string removedId)
    {
        var tables = _tableProvider();
        if (tables is null || file is null)
        {
            return false;
        }

        return ApplyChange(file, tables.ShiftSequentialIdsAfterRemoval(file, type, removedId));
    }

    #endregion

    #region 内部实现

    /// <summary>应用一次表内改号结果，并通知页面同步外部引用。</summary>
    /// <param name="file">发生改号的表。</param>
    /// <param name="change">改号结果。</param>
    /// <returns>是否发生了改号。</returns>
    private bool ApplyChange(ItemTableFile file, IdSequenceChange change)
    {
        if (!change.HasChanges)
        {
            return false;
        }

        file.Dirty = true;
        _applyReferences(change.Mapping);
        return true;
    }

    #endregion
}
