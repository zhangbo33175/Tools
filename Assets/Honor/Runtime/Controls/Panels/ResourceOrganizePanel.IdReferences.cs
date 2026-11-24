/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.IdReferences.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源整理页 ID 引用同步：模板、编辑对象与当前地图实例
 ***************************************************************/

using System.Collections.Generic;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel
{
    #region 字段

    /// <summary>模板与配置表共用的 ID 生命周期入口。</summary>
    private readonly TemplateIdCoordinator _idCoordinator;

    #endregion

    #region 引用同步

    /// <summary>修复所有已加载表的重复 ID；发生改号时标记表待保存。</summary>
    // ---TODO 重构：全表修复统一在此附带标脏，接入表与初始化不再各写一套。
    private void RepairAllTableIdsIfNeeded()
    {
        if (_idCoordinator.RepairDuplicateTableIds())
        {
            _tableDirty = true;
        }
    }

    /// <summary>按需修复目标表的重复 ID；发生改号时同步标记表待保存。</summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">物件类型。</param>
    /// <returns>是否发生改号。</returns>
    // ---TODO 重构：ID 修复调用统一附带标脏，调用方不再重复写 _tableDirty = true。
    private bool RepairTableIdsIfNeeded(ItemTableFile? file, MapObjectType type)
    {
        if (!_idCoordinator.RepairTableIfNeeded(file, type))
        {
            return false;
        }

        _tableDirty = true;
        return true;
    }

    /// <summary>删除行后顺延同段 ID，并标记表待保存。</summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">物件类型。</param>
    /// <param name="removedId">被删除行 ID。</param>
    private void ShiftTableIdsAfterRemoval(ItemTableFile? file, MapObjectType type, string removedId)
    {
        _idCoordinator.ShiftAfterRemoval(file, type, removedId);
        _tableDirty = true;
    }

    /// <summary>
    /// 把旧 ID 映射同步到模板集合、当前编辑对象、当前地图实例、红绿灯与收集物计划。
    /// 表内改号由 <see cref="TemplateIdCoordinator"/> 负责，这里只处理页面持有的引用。
    /// </summary>
    /// <param name="mapping">旧 ID → 新 ID；可为空。</param>
    private void ApplyIdMapping(IReadOnlyDictionary<string, string> mapping)
    {
        if (mapping.Count == 0)
        {
            return;
        }

        // ---TODO ID 管理：页面只同步引用，分段与顺延规则统一由 TemplateIdCoordinator 负责。
        foreach (var template in _templates)
        {
            if (mapping.TryGetValue(template.TemplateId, out var next))
            {
                template.TemplateId = next;
            }
        }

        if (_editing is not null && mapping.TryGetValue(_editing.TemplateId, out var editingId))
        {
            _editing.TemplateId = editingId;
        }

        if (_config is null)
        {
            return;
        }

        foreach (var item in _config.Objects)
        {
            if (mapping.TryGetValue(item.TemplateId, out var next))
            {
                item.TemplateId = next;
            }
        }

        foreach (var light in _config.TrafficLights)
        {
            if (mapping.TryGetValue(light.TemplateId, out var next))
            {
                light.TemplateId = next;
            }
        }

        foreach (var kind in _config.Collectibles.Kinds)
        {
            if (mapping.TryGetValue(kind.TemplateId, out var next))
            {
                kind.TemplateId = next;
            }
        }
    }

    #endregion
}
