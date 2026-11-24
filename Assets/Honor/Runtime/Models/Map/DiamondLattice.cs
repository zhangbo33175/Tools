/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DiamondLattice.cs
 * author:    云毅
 * created:   2026
 * descrip:   菱形格（45° TileMap）几何换算
 ***************************************************************/

using System;
using Avalonia;

namespace MapEditorTool.Models;

/// <summary>
/// 菱形格（TileMap 的 45° 角网格）几何换算。
/// 一个格子就是一个菱形 tile：宽高均为 <c>GridSize</c>（对角线等长 → 斜线正好 45°），
/// 相邻行在水平方向错开半格，格子首尾相接、铺满整张地图。
/// 行 <c>row</c> 的格子中心：<c>x = col * cell + (row % 2) * cell / 2</c>，<c>y = row * cell / 2</c>。
/// </summary>
public static class DiamondLattice
{
    /// <summary>格子中心的世界坐标。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长（菱形对角线长）。</param>
    /// <returns>格子中心。</returns>
    public static Point Center(int col, int row, int cell) =>
        new(col * cell + (row % 2) * (cell / 2.0), row * (cell / 2.0));

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

        var half = cell / 2.0;
        var row = (int)Math.Round(y / half, MidpointRounding.AwayFromZero);
        var col = (int)Math.Round((x - (row % 2) * half) / cell, MidpointRounding.AwayFromZero);
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
        var half = cell / 2.0;
        return new Rect(center.X - half, center.Y - half, cell, cell);
    }

    /// <summary>菱形格的四个顶点。</summary>
    /// <param name="col">列号。</param>
    /// <param name="row">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>上、右、下、左四个顶点。</returns>
    public static Point[] Vertices(int col, int row, int cell)
    {
        var center = Center(col, row, cell);
        var half = cell / 2.0;
        return
        [
            new Point(center.X, center.Y - half),
            new Point(center.X + half, center.Y),
            new Point(center.X, center.Y + half),
            new Point(center.X - half, center.Y),
        ];
    }

    /// <summary>铺满地图需要的列数：半格错行时最右一列会探出半格，故多算一格。</summary>
    /// <param name="width">地图宽（像素）。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>列数。</summary>
    public static int Cols(double width, int cell) =>
        cell <= 0 || width <= 0 ? 0 : (int)Math.Ceiling(width / cell) + 1;

    /// <summary>铺满地图需要的行数：行距为半格，行数约为正交格行数的两倍。</summary>
    /// <param name="height">地图高（像素）。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>行数。</param>
    public static int Rows(double height, int cell) =>
        cell <= 0 || height <= 0 ? 0 : (int)Math.Ceiling(height / (cell / 2.0)) + 1;

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
}
