/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectMoveCommand.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件拖动命令：记录每个实例移动前 / 后的左上角坐标，撤销即还原坐标
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 一次拖动的坐标快照。一个实例也走列表，便于以后多选拖动直接复用。
/// </summary>
/// <param name="Target">被移动的实例。</param>
/// <param name="From">拖动前的左上角世界坐标。</param>
/// <param name="To">拖动后的左上角世界坐标。</param>
public readonly record struct ObjectMoveEntry(PlacedObject Target, Point From, Point To);

/// <summary>
/// 「拖动改坐标」命令。
/// <para>
/// 只在松手时入栈一次：拖动过程中的 100+ 次 pointer move 不算 100 步，
/// 否则用户按一次 Ctrl+Z 只挪回一格，等于没有撤销。
/// </para>
/// </summary>
public sealed class ObjectMoveCommand : IMergeableUndoCommand
{
    private readonly List<ObjectMoveEntry> _entries;

    /// <summary>允许合并的时间窗：松手后短时间内再拖同一个物件（微调位置）算同一步。</summary>
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(1200);

    private readonly DateTime _createdAt = DateTime.UtcNow;

    /// <summary>用一组「移动前 / 移动后」快照构造命令。</summary>
    /// <param name="entries">本次拖动涉及的实例与坐标。</param>
    public ObjectMoveCommand(IReadOnlyList<ObjectMoveEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("移动命令至少要有一个实例", nameof(entries));
        }

        _entries = new List<ObjectMoveEntry>(entries);
        Description = BuildDescription(_entries);
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Undo() => Apply(useTo: false);

    /// <inheritdoc />
    public void Redo() => Apply(useTo: true);

    /// <inheritdoc />
    public bool TryMerge(IUndoableCommand next)
    {
        if (next is not ObjectMoveCommand other ||
            DateTime.UtcNow - _createdAt > MergeWindow ||
            other._entries.Count != _entries.Count)
        {
            return false;
        }

        // 只有「同批实例、连着再拖一次」才合并：移动前坐标保留最初的，移动后坐标取最新的
        for (var i = 0; i < _entries.Count; i++)
        {
            if (!ReferenceEquals(_entries[i].Target, other._entries[i].Target))
            {
                return false;
            }

            _entries[i] = _entries[i] with { To = other._entries[i].To };
        }

        return true;
    }

    private void Apply(bool useTo)
    {
        foreach (var entry in _entries)
        {
            var point = useTo ? entry.To : entry.From;
            entry.Target.X = point.X;
            entry.Target.Y = point.Y;
        }
    }

    private static string BuildDescription(IReadOnlyList<ObjectMoveEntry> entries)
    {
        var first = entries[0];
        var name = string.IsNullOrWhiteSpace(first.Target.Name) ? first.Target.Id : first.Target.Name;
        if (entries.Count == 1)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"移动 {name}（{first.From.X:0},{first.From.Y:0} → {first.To.X:0},{first.To.Y:0}）");
        }

        return $"移动 {entries.Count} 个物件（含 {name}）";
    }
}
