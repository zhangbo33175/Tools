/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TileAdjacency.cs
 * author:    云毅
 * created:   2026
 * descrip:   瓦片邻接口径：矩形九宫 8 邻 / 菱形错行 4 邻的映射表
 ***************************************************************/

using System.Collections.Generic;

namespace MapEditorTool.Models;

/// <summary>
/// 一条邻接槽位：九宫格里的位置 + 相对当前格的格偏移。
/// </summary>
/// <param name="Index">九宫格索引（0..8，行优先，4 是中心）。</param>
/// <param name="Dx">列偏移。</param>
/// <param name="Dy">行偏移。</param>
public readonly record struct TileAdjacencySlot(int Index, int Dx, int Dy);

/// <summary>
/// 瓦片邻接口径。
/// <para>
/// 矩形网格是标准的九宫 8 邻；菱形（2:1 等距）是「错行」网格，一个格只有 4 条共边邻居，
/// 屏幕上正好落在九宫格的四个<em>角</em>（左上 / 右上 / 右下 / 左下），且行奇偶会让列偏移翻一格。
/// 直接用矩形 8 邻去判菱形，边界会判错整圈——这正是 Unity Rule Tile 换 Cell Layout 后规则失效的原因。
/// </para>
/// </summary>
public static class TileAdjacency
{
    /// <summary>矩形网格的 8 邻：九宫除中心外的全部位置。</summary>
    private static readonly TileAdjacencySlot[] RectSlots =
    [
        new(0, -1, -1), new(1, 0, -1), new(2, 1, -1),
        new(3, -1, 0), new(5, 1, 0),
        new(6, -1, 1), new(7, 0, 1), new(8, 1, 1),
    ];

    /// <summary>菱形偶数行：共边邻居落在 (0,±1) / (-1,±1)。</summary>
    private static readonly TileAdjacencySlot[] DiamondEvenSlots =
    [
        new(0, -1, -1), new(2, 0, -1),
        new(8, 0, 1), new(6, -1, 1),
    ];

    /// <summary>菱形奇数行：共边邻居整体右移一格，落在 (0,±1) / (+1,±1)。</summary>
    private static readonly TileAdjacencySlot[] DiamondOddSlots =
    [
        new(0, 0, -1), new(2, 1, -1),
        new(8, 1, 1), new(6, 0, 1),
    ];

    /// <summary>矩形网格的邻接槽位（8 个）。</summary>
    public static IReadOnlyList<TileAdjacencySlot> Rect => RectSlots;

    /// <summary>
    /// 按当前绘制形状取邻接槽位：菱形只返回 4 条共边邻居，六边形沿用矩形 8 邻（蜂窝边缘由美术兜）。
    /// </summary>
    /// <param name="shape">绘制形状（<see cref="DrawShapes"/>）。</param>
    /// <param name="row">当前格行号（菱形需要奇偶）。</param>
    /// <returns>邻接槽位。</returns>
    public static IReadOnlyList<TileAdjacencySlot> ForShape(string? shape, int row) =>
        DrawShapes.IsDiamond(shape)
            ? (row % 2 == 0 ? DiamondEvenSlots : DiamondOddSlots)
            : RectSlots;
}
