/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.Ids.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表 ID 领域规则：分配、重复检测、连续重排与删除顺延
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>一次表内 ID 变更的结果。</summary>
/// <param name="Mapping">可安全应用的「旧 ID → 新 ID」映射；重复旧 ID 因存在歧义不进入映射。</param>
/// <param name="ChangedRows">实际改号的行数。</param>
internal sealed record IdSequenceChange(
    IReadOnlyDictionary<string, string> Mapping,
    int ChangedRows)
{
    /// <summary>无变化的空结果。</summary>
    public static IdSequenceChange Empty { get; } = new(new Dictionary<string, string>(), 0);

    /// <summary>是否发生了改号。</summary>
    public bool HasChanges => ChangedRows > 0;
}

public sealed partial class ItemConfigSet
{
    #region ID 分配

    /// <summary>按分类生成下一个 ID：建筑 2xxx / 人物 1xxx / 物件 3xxx。</summary>
    /// <param name="type">物件类型。</param>
    /// <param name="extraIds">额外参与去重的 ID（例如模板集合，表与模板必须共用同一分配源）。</param>
    /// <returns>新的、未被占用的 ID 文本。</returns>
    public string NextId(MapObjectType type, IEnumerable<string>? extraIds = null) =>
        TemplateIdAllocator.Next(type, Rows.Select(row => row.Id), extraIds);

    #endregion

    #region 重复检测

    /// <summary>找出表内存在重复 ID 的类型段。</summary>
    /// <param name="file">目标表。</param>
    /// <returns>需要重排的类型。</returns>
    internal IReadOnlyList<MapObjectType> DuplicateTypes(ItemTableFile file)
    {
        var types = new List<MapObjectType>();
        foreach (var type in TemplateIdAllocator.Types)
        {
            if (HasDuplicateIds(file, type))
            {
                types.Add(type);
            }
        }

        return types;
    }

    /// <summary>判断目标表指定类型段是否有重复 ID。</summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">物件类型。</param>
    /// <returns>有重复返回 true。</returns>
    internal bool HasDuplicateIds(ItemTableFile file, MapObjectType type)
    {
        var ids = SegmentRows(file, type).Select(row => row.Id.Trim()).ToList();
        return ids.Count != ids.Distinct(StringComparer.Ordinal).Count();
    }

    #endregion

    #region 连续重排与删除顺延

    /// <summary>
    /// 按表内行序把同一类型段的 ID 重排为连续递增：首行沿用当前最小 ID，后续逐行 +1。
    /// </summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">要重排的类型段。</param>
    /// <returns>可用于同步模板 / 地图引用的唯一 ID 映射。</returns>
    internal IdSequenceChange NormalizeSequentialIds(ItemTableFile file, MapObjectType type)
    {
        var rows = SegmentRows(file, type);
        if (rows.Count == 0)
        {
            return IdSequenceChange.Empty;
        }

        var oldIds = rows.Select(row => row.Id.Trim()).ToList();
        var counts = oldIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var values = oldIds.Select(id => ParseRequired(id)).ToList();
        var start = values.Min();
        var limit = TemplateIdAllocator.EndOf(type);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var changedRows = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var next = start + index;
            if (next > limit)
            {
                break;
            }

            var row = rows[index];
            var oldId = oldIds[index];
            var newId = TableTextFormat.Integer(next);
            if (string.Equals(oldId, newId, StringComparison.Ordinal))
            {
                continue;
            }

            row.Id = newId;
            changedRows++;
            if (counts[oldId] == 1)
            {
                mapping[oldId] = newId;
            }
        }

        return CommitIdSequence(file, mapping, changedRows);
    }

    /// <summary>
    /// 删除一行后，把同一类型段内大于被删 ID 的行各前移一位。
    /// 只影响被删行之后的序号，不重排前面的历史行。
    /// </summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">物件类型。</param>
    /// <param name="removedId">刚被删除的行 ID。</param>
    /// <returns>可用于同步模板 / 地图引用的唯一 ID 映射。</returns>
    internal IdSequenceChange ShiftSequentialIdsAfterRemoval(
        ItemTableFile file,
        MapObjectType type,
        string removedId)
    {
        if (HasDuplicateIds(file, type))
        {
            // 删除前若仍有重复，行身份已不唯一，退回完整重排。
            return NormalizeSequentialIds(file, type);
        }

        if (!TemplateIdAllocator.TryParse(removedId, out var removed))
        {
            return IdSequenceChange.Empty;
        }

        var rows = SegmentRows(file, type);
        if (rows.Count == 0)
        {
            return IdSequenceChange.Empty;
        }

        var start = TemplateIdAllocator.StartOf(type);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var changedRows = 0;

        foreach (var row in rows)
        {
            if (!TemplateIdAllocator.TryParse(row.Id, out var id) || id <= removed)
            {
                continue;
            }

            var next = id - 1;
            if (next < start)
            {
                continue;
            }

            var oldId = row.Id.Trim();
            var newId = TableTextFormat.Integer(next);
            row.Id = newId;
            changedRows++;
            mapping[oldId] = newId;
        }

        return CommitIdSequence(file, mapping, changedRows);
    }

    #endregion

    #region 内部实现

    /// <summary>
    /// 提交一次表内 ID 变更：无改动时返回空结果，有改动时标脏并重建索引。
    /// 完整重排与删除顺延共用同一收尾逻辑。
    /// </summary>
    /// <param name="file">被修改的表。</param>
    /// <param name="mapping">旧 ID → 新 ID 映射。</param>
    /// <param name="changedRows">实际改号行数。</param>
    /// <returns>本次 ID 变更结果。</returns>
    // ---TODO 重构：完整重排与删除顺延共用同一提交入口，避免两处各自标脏 / 重建索引。
    private IdSequenceChange CommitIdSequence(
        ItemTableFile file,
        Dictionary<string, string> mapping,
        int changedRows)
    {
        if (changedRows == 0)
        {
            return IdSequenceChange.Empty;
        }

        file.Dirty = true;
        RebuildIndex();
        return new IdSequenceChange(mapping, changedRows);
    }

    /// <summary>取目标表指定类型段的数据行（保持表内顺序）。</summary>
    /// <param name="file">目标表。</param>
    /// <param name="type">物件类型。</param>
    /// <returns>数据行列表。</returns>
    private static List<ItemElementRow> SegmentRows(ItemTableFile file, MapObjectType type) =>
        file.Rows
            .Where(row => TemplateIdAllocator.IsInRange(row.Id, type))
            .ToList();

    /// <summary>把已确认在段内的 ID 文本解析为整数。</summary>
    /// <param name="id">ID 文本。</param>
    /// <returns>整数 ID。</returns>
    private static int ParseRequired(string id)
    {
        if (!TemplateIdAllocator.TryParse(id, out var value))
        {
            throw new InvalidOperationException($"ID 不在有效分段内：{id}");
        }

        return value;
    }

    #endregion
}
