/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DiamondLattice.cs
 * author:    云毅
 * created:   2026
 * descrip:   菱形格（2:1 等距 TileMap）几何换算
 ***************************************************************/

using System;
using Avalonia;

namespace MapEditorTool.Models;

/// <summary>
/// 菱形格（TileMap 的 2:1 等距网格）几何换算。
/// <para>
/// 一个格子是一块「宽 2×cell、高 cell」的扁菱形，和经典等距地砖（64×32）同比例：
/// 面积正好等于一个 cell×cell 的正方格，所以菱形模式与矩形模式的「格子数」口径一致。
/// 相邻行在垂直方向错开半个格高，奇数行在水平方向错开半格宽，格子共边铺满整张地图。
/// </para>
/// <para>
/// 行 <c>row</c> 的格子中心：<c>x = col * 2 * cell + (row % 2) * cell</c>，<c>y = row * cell / 2</c>。
/// </para>
/// </summary>
public static class DiamondLattice
{
    /// <summary>菱形半宽（= cell）：中心到左右顶点的水平距离。</summary>
    /// <param name="cell">格子边长。</param>
    /// <returns>半宽。</returns>
    private static double HalfWidth(int cell) => cell;

    /// <summary>菱形半高（= cell / 2）：中心到上下顶点的垂直距离。</summary>
    /// <param name="cell">格子边长。</param>
    /// <returns>半高。</returns>
    private static double HalfHeight(int cell) => cell / 2.0;

    /// <summary>格子中心的世界坐标。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长（正方格的边长，菱形宽为其两倍）。</param>
    /// <returns>格子中心。</returns>
    public static Point Center(int col, int row, int cell) =>
        new(col * (cell * 2.0) + (row % 2) * cell, row * HalfHeight(cell));

    /// <summary>世界坐标落在哪个菱形格里（取最近的格子中心）。</summary>
    /// <param name="x">世界 X。</param>
    /// <param name="y">世界 Y。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>格子行列（X = 列，Y = 行）。</returns>
    public static Point IndexAt(double x, double y, int cell)
    {
        if (cell <= 0)
        {
            return new Point(0, 0);
        }

        var half = HalfHeight(cell);
        var row = (int)Math.Round(y / half, MidpointRounding.AwayFromZero);
        var col = (int)Math.Round((x - (row % 2) * cell) / (cell * 2.0), MidpointRounding.AwayFromZero);
        return new Point(col, row);
    }

    /// <summary>菱形格的外接矩形（菱形的四个顶点落在矩形四边中点）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>外接矩形。</returns>
    public static Rect Bounds(int col, int row, int cell)
    {
        var center = Center(col, row, cell);
        return new Rect(
            center.X - HalfWidth(cell),
            center.Y - HalfHeight(cell),
            cell * 2.0,
            cell);
    }

    /// <summary>菱形格的四个顶点。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>上、右、下、左四个顶点。</returns>
    public static Point[] Vertices(int col, int row, int cell)
    {
        var center = Center(col, row, cell);
        var halfWidth = HalfWidth(cell);
        var halfHeight = HalfHeight(cell);
        return
        [
            new Point(center.X, center.Y - halfHeight),
            new Point(center.X + halfWidth, center.Y),
            new Point(center.X, center.Y + halfHeight),
            new Point(center.X - halfWidth, center.Y),
        ];
    }

    /// <summary>铺满地图需要的列数：半格错行时最右一列会探出半格，故多算一格。</summary>
    /// <param name="width">地图宽（像素）。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>列数。</returns>
    public static int Cols(double width, int cell) =>
        cell <= 0 || width <= 0 ? 0 : (int)Math.Ceiling(width / (cell * 2.0)) + 1;

    /// <summary>铺满地图需要的行数：行距为半个格高，行数约为正方格行数的两倍。</summary>
    /// <param name="height">地图高（像素）。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>行数。</returns>
    public static int Rows(double height, int cell) =>
        cell <= 0 || height <= 0 ? 0 : (int)Math.Ceiling(height / HalfHeight(cell)) + 1;

    /// <summary>判断格子中心是否落在地图矩形内（铺满时边缘格子会被裁掉半边）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <param name="width">地图宽。</param>
    /// <param name="height">地图高。</param>
    /// <returns>中心在地图内返回 true。</returns>
    public static bool InsideMap(int col, int row, int cell, double width, double height)
    {
        var center = Center(col, row, cell);
        return center.X >= 0 && center.X <= width && center.Y >= 0 && center.Y <= height;
    }

    #region 斜边（共边中点）

    /// <summary>
    /// 一条斜边的编号：0 = 左上边、1 = 右上边、2 = 右下边、3 = 左下边（屏幕顺时针）。
    /// <para>墙体要贴在菱形格的边上而不是格心，这个编号同时决定墙的朝向档位（×90°）。</para>
    /// </summary>
    public const int EdgeCount = 4;

    /// <summary>取一条斜边的中点（即「墙脚落点」）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <param name="edge">边号：0 左上 / 1 右上 / 2 右下 / 3 左下。</param>
    /// <returns>边中点世界坐标。</returns>
    public static Point EdgeMidpoint(int col, int row, int cell, int edge)
    {
        var center = Center(col, row, cell);
        var halfWidth = HalfWidth(cell) / 2.0;
        var halfHeight = HalfHeight(cell) / 2.0;
        return NormalizeEdge(edge) switch
        {
            1 => new Point(center.X + halfWidth, center.Y - halfHeight),
            2 => new Point(center.X + halfWidth, center.Y + halfHeight),
            3 => new Point(center.X - halfWidth, center.Y + halfHeight),
            _ => new Point(center.X - halfWidth, center.Y - halfHeight),
        };
    }

    /// <summary>格内四个边中点（按边号顺序）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>四条边的中点。</returns>
    public static Point[] EdgeMidpoints(int col, int row, int cell)
    {
        var result = new Point[EdgeCount];
        for (var i = 0; i < EdgeCount; i++)
        {
            result[i] = EdgeMidpoint(col, row, cell, i);
        }

        return result;
    }

    /// <summary>离给定世界坐标最近的那条斜边。</summary>
    /// <param name="world">鼠标世界坐标。</param>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>边号 0..3。</returns>
    public static int NearestEdge(Point world, int col, int row, int cell)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < EdgeCount; i++)
        {
            var mid = EdgeMidpoint(col, row, cell, i);
            var distance = SquaredDistance(mid, world);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>边号 × 90°，作为物件旋转角度落盘（0 / 90 / 180 / 270）。</summary>
    /// <param name="edge">边号。</param>
    /// <returns>旋转角度（度）。</returns>
    public static double EdgeRotation(int edge) => NormalizeEdge(edge) * 90.0;

    /// <summary>把任意整数钳到 0..3。</summary>
    /// <param name="edge">边号。</param>
    /// <returns>合法边号。</returns>
    private static int NormalizeEdge(int edge) => ((edge % EdgeCount) + EdgeCount) % EdgeCount;

    /// <summary>两点距离平方（比较用，省掉开方）。</summary>
    private static double SquaredDistance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    #endregion
}
