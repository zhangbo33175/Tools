/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NamingAuditService.cs
 * author:    云毅
 * created:   2026
 * descrip:   命名体检：为配置表每一行生成中文名建议，并写回模板名称或显示名称
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace MapEditorTool.Services;

#region 写入目标

/// <summary>中文名写到哪一列。</summary>
public enum NamingTarget
{
    /// <summary>改写「模板名称」列（表格里的建筑 / 道具 / 角色名称，对外唯一可见名）。</summary>
    TemplateName,

    /// <summary>写到额外的「显示名称」列（不动原有模板名称，适合只想备注中文的场景）。</summary>
    DisplayName,
}

#endregion

#region 体检条目

/// <summary>一条命名体检结果（一行配置表）。</summary>
public sealed class NamingAuditEntry
{
    internal NamingAuditEntry(
        ItemTableFile file,
        ItemElementRow row,
        NamingSuggestion suggestion,
        int iconRepeat)
    {
        File = file;
        Row = row;
        Suggestion = suggestion.Text;
        Reason = suggestion.Reason;
        Confidence = suggestion.Confidence;
        IconRepeat = iconRepeat;
    }

    /// <summary>所在表。</summary>
    public ItemTableFile File { get; }

    /// <summary>数据行。</summary>
    public ItemElementRow Row { get; }

    /// <summary>表名（界面用）。</summary>
    public string TableName => File.Name;

    /// <summary>模板 ID。</summary>
    public string Id => Row.Id;

    /// <summary>图片名。</summary>
    public string Icon => Row.Icon;

    /// <summary>同一张图在整个表集合里的行数（含「汇总 CSV」与「分类 xlsm」各登记一份）。</summary>
    public int IconRepeat { get; }

    /// <summary>是否为重复登记（同一张图出现多行，通常是汇总 CSV 与分类表重复）。</summary>
    public bool IsRepeatedIcon => IconRepeat > 1;

    /// <summary>当前的模板名称（多数是英文图片名）。</summary>
    public string TemplateName => Row.TemplateName;

    /// <summary>当前「显示名称」列的值。</summary>
    public string DisplayName => Row.DisplayName;

    /// <summary>建议的中文名（已按同表重名顺延过序号，界面所见即写入所得）。</summary>
    public string Suggestion { get; internal set; }

    /// <summary>建议依据。</summary>
    public string Reason { get; }

    /// <summary>可信度。</summary>
    public NamingConfidence Confidence { get; }

    /// <summary>取目标列的当前值。</summary>
    /// <param name="target">写入目标。</param>
    /// <returns>当前值；空为无值。</returns>
    public string Current(NamingTarget target) =>
        (target == NamingTarget.TemplateName ? Row.TemplateName : Row.DisplayName).Trim();

    /// <summary>是否需要写回（高可信且有建议、且与目标列当前值不同）。</summary>
    /// <param name="target">写入目标。</param>
    /// <returns>需要写为 true。</returns>
    public bool NeedsWrite(NamingTarget target) =>
        Confidence == NamingConfidence.High
        && Suggestion.Length > 0
        && !string.Equals(Current(target), Suggestion.Trim(), StringComparison.Ordinal);

    /// <summary>界面上的动作标签。</summary>
    /// <param name="target">写入目标。</param>
    /// <returns>新增 / 覆盖 / 已一致 / 待补词表。</returns>
    public string Action(NamingTarget target)
    {
        if (Confidence != NamingConfidence.High)
        {
            return "待补词表";
        }

        if (!NeedsWrite(target))
        {
            return "已一致";
        }

        return Current(target).Length == 0 ? "新增" : "覆盖";
    }
}

#endregion

#region 体检服务

/// <summary>命名体检：批量给配置表生成中文名建议，并提供一次性写回。</summary>
public static class NamingAuditService
{
    /// <summary>「显示名称」列的列名（与 <see cref="ItemFieldNames"/> 的别名表一致）。</summary>
    public const string DisplayNameColumn = "显示名称";

    /// <summary>扫描整个表集合。</summary>
    /// <param name="set">配置表集合。</param>
    /// <param name="glossary">命名词表。</param>
    /// <returns>按表名、行号排序的体检结果。</returns>
    public static List<NamingAuditEntry> Audit(ItemConfigSet set, NamingGlossary glossary)
    {
        var entries = new List<NamingAuditEntry>();
        // 只统计去重后的行：同一张图在汇总 CSV 与分类 xlsm 各登记一份时，按一条算，避免把镜像当重复
        var iconCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in set.DistinctRows)
        {
            var key = row.Icon.Trim();
            if (key.Length == 0)
            {
                continue;
            }

            iconCount[key] = iconCount.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        foreach (var row in set.DistinctRows)
        {
            var file = set.OwnerOf(row);
            if (file is null)
            {
                continue;
            }

            var icon = row.Icon.Trim();
            var repeat = icon.Length > 0 && iconCount.TryGetValue(icon, out var count) ? count : 0;
            entries.Add(new NamingAuditEntry(file, row, glossary.Suggest(row.Icon), repeat));
        }

        // 同一张图会在「汇总 CSV」和「分类 xlsm」各出现一行，编号必须按图片名全局规划，
        // 否则两边各排一次会得到不同名字（csv 叫「路灯 1」、xlsm 叫「路灯」）
        ApplyIconNames(entries);

        return entries
            .OrderBy(entry => entry.TableName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Row.RowNumber)
            .ToList();
    }

    /// <summary>按图片名统一规划最终中文名：同族（基础名相同）出现混合序号或撞号时，按首次出现顺序重排。</summary>
    private static void ApplyIconNames(List<NamingAuditEntry> entries)
    {
        var firstSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var initial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var icon = entry.Icon.Trim();
            if (icon.Length == 0 || entry.Confidence != NamingConfidence.High || entry.Suggestion.Trim().Length == 0)
            {
                continue;
            }

            if (firstSeen.ContainsKey(icon))
            {
                continue;
            }

            firstSeen[icon] = firstSeen.Count;
            initial[icon] = entry.Suggestion.Trim();
        }

        var final = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in initial.GroupBy(pair => BaseName(pair.Value), StringComparer.Ordinal))
        {
            var items = family.OrderBy(pair => firstSeen[pair.Key]).ToList();
            var numbers = items.Select(pair => TrailingIndex(pair.Value)).ToList();
            var allNumbered = numbers.All(number => number > 0);
            var distinct = numbers.Distinct().Count() == numbers.Count;
            if (items.Count == 1 || (allNumbered && distinct))
            {
                foreach (var pair in items)
                {
                    final[pair.Key] = pair.Value;
                }

                continue;
            }

            var index = 1;
            foreach (var pair in items)
            {
                final[pair.Key] = $"{family.Key} {index++}";
            }
        }

        foreach (var entry in entries)
        {
            var icon = entry.Icon.Trim();
            if (final.TryGetValue(icon, out var name))
            {
                entry.Suggestion = name;
            }
        }
    }

    /// <summary>
    /// 把建议写入目标列（默认改写「模板名称」，也就是表格里的建筑 / 道具 / 角色名称）。
    /// 只写高可信条目；同一张表里撞名时自动顺延序号，保证写出去的名称表内唯一。
    /// </summary>
    /// <param name="set">配置表集合。</param>
    /// <param name="entries">体检结果；null 表示重新扫描。</param>
    /// <param name="glossary">命名词表（<paramref name="entries"/> 为 null 时使用）。</param>
    /// <param name="target">写入目标。</param>
    /// <returns>实际写入的行数。</returns>
    public static int Apply(
        ItemConfigSet set,
        IEnumerable<NamingAuditEntry>? entries,
        NamingGlossary glossary,
        NamingTarget target = NamingTarget.TemplateName)
    {
        var targets = entries is null ? Audit(set, glossary) : entries.ToList();
        var written = 0;
        foreach (var group in targets.GroupBy(entry => entry.File))
        {
            var file = group.Key;
            var rows = group.OrderBy(entry => entry.Row.RowNumber).ToList();

            // 再占用「表内所有行的现值」，把 A 改成 B 已有的名字这种撞车挡在写之前
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in file.Rows)
            {
                var current = (target == NamingTarget.TemplateName ? row.TemplateName : row.DisplayName).Trim();
                if (current.Length > 0)
                {
                    taken.Add(current);
                }
            }

            // 本批要改写的行，其旧值先整批释放：否则上一轮留下的旧中文名会把自己顶成「路灯 8」。
            // 已经一致的行不释放，免得它们的名字被别的行抢走。
            foreach (var entry in rows)
            {
                if (!entry.NeedsWrite(target))
                {
                    continue;
                }

                var current = entry.Current(target);
                if (current.Length > 0)
                {
                    taken.Remove(current);
                }
            }

            foreach (var entry in rows)
            {
                if (entry.Confidence != NamingConfidence.High)
                {
                    continue;
                }

                var suggestion = entry.Suggestion.Trim();
                if (suggestion.Length == 0)
                {
                    continue;
                }

                var current = entry.Current(target);
                if (string.Equals(current, suggestion, StringComparison.Ordinal))
                {
                    continue;
                }

                var name = MakeUnique(suggestion, taken);
                taken.Add(name);

                if (target == NamingTarget.TemplateName)
                {
                    entry.Row.TemplateName = name;
                }
                else
                {
                    file.EnsureColumn(ItemFieldNames.DisplayName, DisplayNameColumn);
                    entry.Row.DisplayName = name;
                }

                file.Dirty = true;
                written++;
            }
        }

        return written;
    }

    /// <summary>剥掉名称末尾的「 序号」，得到同族的基础名。</summary>
    private static string BaseName(string name)
    {
        var cut = name.LastIndexOf(' ');
        if (cut > 0 && int.TryParse(name[(cut + 1)..], out _))
        {
            return name[..cut];
        }

        return name;
    }

    /// <summary>名称末尾的序号；没有为 0。</summary>
    private static int TrailingIndex(string name)
    {
        var cut = name.LastIndexOf(' ');
        if (cut > 0 && int.TryParse(name[(cut + 1)..], out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    /// <summary>
    /// 生成表内唯一名：<c>箱子</c> 已存在时顺延成 <c>箱子 2</c>；已带序号的从该序号往后找空位。
    /// </summary>
    private static string MakeUnique(string name, HashSet<string> taken)
    {
        if (!taken.Contains(name))
        {
            return name;
        }

        var baseName = name;
        var start = 2;
        var cut = name.LastIndexOf(' ');
        if (cut > 0 && int.TryParse(name[(cut + 1)..], out var parsed))
        {
            baseName = name[..cut];
            start = Math.Max(parsed + 1, 2);
        }

        for (var index = start; index < start + 1000; index++)
        {
            var candidate = $"{baseName} {index}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        return name;
    }

    /// <summary>统计摘要（界面顶部一行）。</summary>
    /// <param name="entries">体检结果。</param>
    /// <returns>摘要文本。</returns>
    public static string Describe(IReadOnlyCollection<NamingAuditEntry> entries)
    {
        var total = entries.Count;
        var high = entries.Count(entry => entry.Confidence == NamingConfidence.High);
        var low = total - high;
        var repeated = entries.Count(entry => entry.IsRepeatedIcon);
        return $"共 {total} 行 · 可识别 {high} · 待补词表 {low} · 重复图片 {repeated}";
    }

    /// <summary>按目标统计待写回行数。</summary>
    /// <param name="entries">体检结果。</param>
    /// <param name="target">写入目标。</param>
    /// <returns>待写回行数。</returns>
    public static int PendingCount(IEnumerable<NamingAuditEntry> entries, NamingTarget target) =>
        entries.Count(entry => entry.NeedsWrite(target));
}

#endregion
