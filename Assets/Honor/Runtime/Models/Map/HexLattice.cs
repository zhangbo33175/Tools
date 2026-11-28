/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HexLattice.cs
 * author:    云毅
 * created:   2026
 * descrip:   六边形格（Unity Hexagonal Cell Layout）几何换算
 ***************************************************************/

using System;
using Avalonia;

namespace MapEditorTool.Models;

/// <summary>
/// 六边形格（尖顶 pointy-top、odd-r 行偏移）几何换算。
/// <para>
/// 一个格子的横向宽度等于 <c>cell</c>，所以列数与正方格一致；外接半径为 <c>cell / √3</c>，
/// 六边形高为 <c>2 × 半径</c>，行距为 <c>1.5 × 半径</c>，奇数行整体右移半个格宽。
/// 这样相邻六边形共边咬合，和 Unity Hexagonal 的 <c>cellSize</c> 口径对得上。
/// </para>
/// <para>
/// 行 <c>row</c> 的格子中心：<c>x = col * cell + (row % 2) * cell / 2</c>，<c>y = row * 1.5 * radius</c>。
/// </para>
/// </summary>
public static class HexLattice
{
    /// <summary>√3，避免每处重复写魔法数字。</summary>
    private const double Sqrt3 = 1.7320508075688772;

    /// <summary>外接半径：中心到六个顶点的距离（六边形宽度 = 半径 × √3）。</summary>
    /// <param name="cell">格子宽度。</param>
    /// <returns>外接半径。</returns>
    private static double Radius(int cell) => cell / Sqrt3;

    /// <summary>六边形高度（尖顶时上下顶点之间的距离）。</summary>
    /// <param name="cell">格子宽度。</param>
    /// <returns>高度。</returns>
    private static double Height(int cell) => Radius(cell) * 2.0;

    /// <summary>行距：同一列相邻两行的中心间距。</summary>
    /// <param name="cell">格子宽度。</param>
    /// <returns>行距。</returns>
    public static double RowStep(int cell) => Radius(cell) * 1.5;

    /// <summary>格子中心的世界坐标。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>格子中心。</returns>
    public static Point Center(int col, int row, int cell) =>
        new(
            col * (double)cell + (row % 2) * (cell / 2.0),
            row * RowStep(cell));

    /// <summary>
    /// 世界坐标落在哪个六边形格里：先按行列步长约出候选，再在 3×3 邻域里取中心最近的一格，
    /// 比直接四舍五入稳（尖顶六边形的列边界是斜的，纯四舍五入会在缝上跳格）。
    /// </summary>
    /// <param name="x">世界 X。</param>
    /// <param name="y">世界 Y。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>格子行列（X = 列，Y = 行）。</returns>
    public static Point IndexAt(double x, double y, int cell)
    {
        if (cell <= 0)
        {
            return new Point(0, 0);
        }

        var rowStep = RowStep(cell);
        var row = (int)Math.Round(y / rowStep, MidpointRounding.AwayFromZero);
        var col = (int)Math.Round((x - (row % 2) * (cell / 2.0)) / cell, MidpointRounding.AwayFromZero);

        var best = new Point(col, row);
        var bestDistance = double.MaxValue;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var candidateCol = col + dx;
                var candidateRow = row + dy;
                var center = Center(candidateCol, candidateRow, cell);
                var distance = (center.X - x) * (center.X - x) + (center.Y - y) * (center.Y - y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new Point(candidateCol, candidateRow);
                }
            }
        }

        return best;
    }

    /// <summary>六边形格的外接矩形（宽 cell、高 2×半径）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>外接矩形。</returns>
    public static Rect Bounds(int col, int row, int cell)
    {
        var center = Center(col, row, cell);
        var height = Height(cell);
        return new Rect(center.X - cell / 2.0, center.Y - height / 2.0, cell, height);
    }

    /// <summary>六边形的六个顶点（尖顶：上、右上、右下、下、左下、左上）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>六个顶点。</returns>
    public static Point[] Vertices(int col, int row, int cell)
    {
        var center = Center(col, row, cell);
        var radius = Radius(cell);
        var points = new Point[6];
        for (var i = 0; i < 6; i++)
        {
            // 从正上方（-90°）起每 60° 一个顶点
            var angle = -Math.PI / 2.0 + i * Math.PI / 3.0;
            points[i] = new Point(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle));
        }

        return points;
    }

    /// <summary>铺满地图需要的列数：奇数行右移半格，最右一列会探出半格，故多算一格。</summary>
    /// <param name="width">地图宽（像素）。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>列数。</returns>
    public static int Cols(double width, int cell) =>
        cell <= 0 || width <= 0 ? 0 : (int)Math.Ceiling(width / cell) + 1;

    /// <summary>铺满地图需要的行数：行距为 1.5 倍外接半径，比正方格行数略多。</summary>
    /// <param name="height">地图高（像素）。</param>
    /// <param name="cell">格子宽度。</param>
    /// <returns>行数。</returns>
    public static int Rows(double height, int cell) =>
        cell <= 0 || height <= 0 ? 0 : (int)Math.Ceiling(height / RowStep(cell)) + 1;

    /// <summary>判断格子中心是否落在地图矩形内（铺满时边缘格子会被裁掉半边）。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子宽度。</param>
    /// <param name="width">地图宽。</param>
    /// <param name="height">地图高。</param>
    /// <returns>中心在地图内返回 true。</returns>
    public static bool InsideMap(int col, int row, int cell, double width, double height)
    {
        var center = Center(col, row, cell);
        return center.X >= 0 && center.X <= width && center.Y >= 0 && center.Y <= height;
    }
}
