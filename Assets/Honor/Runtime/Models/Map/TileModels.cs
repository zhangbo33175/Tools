/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TileModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   瓦片地图数据模型：格瓦片、九宫邻接规则与规则匹配
 ***************************************************************/
using System;
using System.Collections.Generic;

namespace MapEditorTool.Models;

/// <summary>
/// 九宫邻接判定：规则里每个邻居位可以是「必须有 / 必须没有 / 无所谓」。
/// 与 Unity Rule Tile 的绿箭头 / 红叉 / 空白三态一致。
/// </summary>
public static class TileNeighborStates
{
    /// <summary>无所谓：邻居有没有都不影响匹配。</summary>
    public const int Any = 0;

    /// <summary>必须有：邻居必须存在同类瓦片（Unity 的绿箭头）。</summary>
    public const int Required = 1;

    /// <summary>必须没有：邻居必须为空（Unity 的红叉）。</summary>
    public const int Forbidden = 2;

    /// <summary>把任意整数钳到三态里，脏存档不会把编辑器带崩。</summary>
    public static int Normalize(int value) => value is Required or Forbidden ? value : Any;

    /// <summary>点击循环：无所谓 → 必须有 → 必须没有 → 无所谓。</summary>
    public static int Cycle(int value) => Normalize(value) switch
    {
        Any => Required,
        Required => Forbidden,
        _ => Any,
    };
}

/// <summary>
/// 九宫格内的一个位置：按行优先存放，索引 = (dy + 1) * 3 + (dx + 1)。
/// </summary>
public static class TilePatternCells
{
    /// <summary>九宫格边长。</summary>
    public const int Size = 3;

    /// <summary>格数。</summary>
    public const int Count = Size * Size;

    /// <summary>中心位（即瓦片自己脚下），规则里不参与判定。</summary>
    public const int Center = 4;

    /// <summary>偏移量换算成九宫格索引，保证结果落在 0..8。</summary>
    public static int Index(int dx, int dy) =>
        Math.Clamp((dy + 1) * Size + (dx + 1), 0, Count - 1);

    /// <summary>索引换算成偏移量。</summary>
    public static (int Dx, int Dy) Offset(int index) =>
        (index % Size - 1, index / Size - 1);
}

/// <summary>
/// 一张地图上的一个瓦片格：格坐标 + 贴图路径。
/// 和道路 / 人行道格子一样按整数格存盘，不跟随物体包围盒。
/// </summary>
public sealed class TileCellData
{
    /// <summary>格列号。</summary>
    public int X { get; set; }

    /// <summary>格行号。</summary>
    public int Y { get; set; }

    /// <summary>
    /// 所属楼层（0 = 地面层，1 = 二楼地板 …）。
    /// 剖面式等距场景里每层地板是独立平面，同一格号可以在不同楼层各有一块瓦片。
    /// </summary>
    public int Level { get; set; }

    /// <summary>贴图路径或资源名，交给资源库解析。</summary>
    public string Image { get; set; } = string.Empty;
}

/// <summary>
/// 一条邻接规则：九宫格图案 + 命中后使用的贴图。
/// 图案全「无所谓」时等价于兜底规则。
/// </summary>
public sealed class TileRuleData
{
    /// <summary>规则名，只用于界面展示。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>命中后替换成的贴图。</summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>九宫格三态，长度固定 9，按行优先。</summary>
    public int[] Pattern { get; set; } = new int[TilePatternCells.Count];

    /// <summary>兜底规则：除中心外全部「无所谓」。</summary>
    public bool IsFallback
    {
        get
        {
            for (var i = 0; i < TilePatternCells.Count; i++)
            {
                if (i != TilePatternCells.Center && TileNeighborStates.Normalize(Pattern[i]) != TileNeighborStates.Any)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>限定条件数量：命中后用来挑「最具体」的那条规则。</summary>
    public int Specificity
    {
        get
        {
            var count = 0;
            for (var i = 0; i < TilePatternCells.Count; i++)
            {
                if (i != TilePatternCells.Center && TileNeighborStates.Normalize(Pattern[i]) != TileNeighborStates.Any)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>把数组补足到 9 位并清洗非法值，读档与新建都走这里。</summary>
    public void Normalize()
    {
        var normalized = new int[TilePatternCells.Count];
        for (var i = 0; i < normalized.Length; i++)
        {
            normalized[i] = Pattern is not null && i < Pattern.Length
                ? TileNeighborStates.Normalize(Pattern[i])
                : TileNeighborStates.Any;
        }

        Pattern = normalized;
        Image ??= string.Empty;
        Name ??= string.Empty;
    }

    /// <summary>复制一份图案，避免列表里多条规则共享同一个数组。</summary>
    /// <returns>新数组。</returns>
    public int[] ClonePattern() => (int[])Pattern.Clone();
}

/// <summary>规则匹配：把九宫格图案与「邻居是否存在」的实时判定对齐。</summary>
public static class TileRuleMatcher
{
    /// <summary>判断一条规则是否命中当前格。</summary>
    /// <param name="rule">待判定规则。</param>
    /// <param name="occupied">邻居判定：给出偏移量，返回该位置是否有同类瓦片。</param>
    /// <returns>命中为 true。</returns>
    public static bool Matches(TileRuleData rule, Func<int, int, bool> occupied) =>
        Matches(rule, occupied, TileAdjacency.Rect);

    /// <summary>按给定邻接槽位判断规则是否命中（菱形只判 4 条共边，矩形判 8 邻）。</summary>
    /// <param name="rule">待判定规则。</param>
    /// <param name="occupied">邻居判定：给出偏移量，返回该位置是否有同类瓦片。</param>
    /// <param name="slots">邻接槽位表，见 <see cref="TileAdjacency"/>。</param>
    /// <returns>命中为 true。</returns>
    public static bool Matches(TileRuleData rule, Func<int, int, bool> occupied, IReadOnlyList<TileAdjacencySlot> slots)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var state = TileNeighborStates.Normalize(rule.Pattern[slot.Index]);
            if (state == TileNeighborStates.Any)
            {
                continue;
            }

            var has = occupied(slot.Dx, slot.Dy);
            if (state == TileNeighborStates.Required && !has)
            {
                return false;
            }

            if (state == TileNeighborStates.Forbidden && has)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 在一组规则里挑最合适的那条：先看命中，再比限定条件数量，
    /// 同等具体度取靠前的（列表顺序即优先级）。
    /// </summary>
    /// <param name="rules">规则集合。</param>
    /// <param name="occupied">邻居判定。</param>
    /// <returns>命中的规则；全不命中返回 null。</returns>
    public static TileRuleData? Pick(IReadOnlyList<TileRuleData> rules, Func<int, int, bool> occupied) =>
        Pick(rules, occupied, TileAdjacency.Rect);

    /// <summary>按给定邻接槽位挑规则（菱形走 4 邻，矩形走 8 邻）。</summary>
    /// <param name="rules">规则集合。</param>
    /// <param name="occupied">邻居判定。</param>
    /// <param name="slots">邻接槽位表。</param>
    /// <returns>命中的规则；全不命中返回 null。</returns>
    public static TileRuleData? Pick(
        IReadOnlyList<TileRuleData> rules,
        Func<int, int, bool> occupied,
        IReadOnlyList<TileAdjacencySlot> slots)
    {
        TileRuleData? best = null;
        var bestScore = -1;
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (string.IsNullOrWhiteSpace(rule.Image) || !Matches(rule, occupied, slots))
            {
                continue;
            }

            // 兜底规则永远排最后：它谁都能匹配，但不能压掉更具体的规则
            var score = rule.IsFallback ? 0 : rule.Specificity + 1;
            if (score > bestScore)
            {
                best = rule;
                bestScore = score;
            }
        }

        return best;
    }
}

/// <summary>
/// 瓦片贴图路径比较：地图 JSON 里可能写正斜杠、调色板里是 Windows 反斜杠，
/// 两侧必须按同一口径判重，否则同一张图会在调色板里出现两份。
/// </summary>
public static class TilePaths
{
    /// <summary>统一成比较用的形式（正斜杠、去空白）。</summary>
    /// <param name="path">原始路径。</param>
    /// <returns>规范化路径；空值返回空串。</returns>
    public static string Normalize(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();

    /// <summary>判断两个路径是否指向同一张贴图（忽略分隔符差异与大小写）。</summary>
    /// <param name="left">左值。</param>
    /// <param name="right">右值。</param>
    /// <returns>相同为 true。</returns>
    public static bool Same(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 瓦片图层读写辅助：
/// 复制修改放在模型侧，画布与面板只负责调用，避免两边各写一套遍历。
/// </summary>
public static class TileLayerOps
{
    /// <summary>按行列找一条瓦片格数据（第 0 层，兼容旧调用口径）。</summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <returns>命中项；不存在返回 null。</returns>
    public static TileCellData? Find(IReadOnlyList<TileCellData> cells, int x, int y) => Find(cells, x, y, 0);

    /// <summary>按行列 + 楼层找一条瓦片格数据。</summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="level">楼层号。</param>
    /// <returns>命中项；不存在返回 null。</returns>
    public static TileCellData? Find(IReadOnlyList<TileCellData> cells, int x, int y, int level)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].X == x && cells[i].Y == y && cells[i].Level == level)
            {
                return cells[i];
            }
        }

        return null;
    }

    /// <summary>写入一格瓦片（第 0 层，兼容旧调用口径）。</summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="image">贴图路径。</param>
    /// <returns>是否发生了变化。</returns>
    public static bool Set(IList<TileCellData> cells, int x, int y, string image) => Set(cells, x, y, 0, image);

    /// <summary>
    /// 写入一格瓦片：同层同格已存在则替换贴图，不同贴图视为一次变更。
    /// </summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="level">楼层号。</param>
    /// <param name="image">贴图路径。</param>
    /// <returns>是否发生了变化。</returns>
    public static bool Set(IList<TileCellData> cells, int x, int y, int level, string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return false;
        }

        var existing = Find((IReadOnlyList<TileCellData>)cells, x, y, level);
        if (existing is not null)
        {
            if (TilePaths.Same(existing.Image, image))
            {
                return false;
            }

            existing.Image = image;
            return true;
        }

        cells.Add(new TileCellData { X = x, Y = y, Level = level, Image = image });
        return true;
    }

    /// <summary>擦掉一格瓦片（第 0 层，兼容旧调用口径）。</summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <returns>是否发生了删除。</returns>
    public static bool Remove(IList<TileCellData> cells, int x, int y) => Remove(cells, x, y, 0);

    /// <summary>擦掉某层的某一格瓦片。</summary>
    /// <param name="cells">瓦片格集合。</param>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="level">楼层号。</param>
    /// <returns>是否发生了删除。</returns>
    public static bool Remove(IList<TileCellData> cells, int x, int y, int level)
    {
        for (var i = cells.Count - 1; i >= 0; i--)
        {
            if (cells[i].X == x && cells[i].Y == y && cells[i].Level == level)
            {
                cells.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
}

