/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.EdgeSnap.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布斜边吸附：等距（菱形）下把墙体贴到菱形共边中点并给出朝向档位
 ***************************************************************/

using System;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class MapCanvas
{
    #region 斜边吸附

    /// <summary>
    /// 斜边吸附开关：开启后落位 / 拖动会把物件的「脚底中点」贴到菱形格四条共边的中点，
    /// 同时按贴合的那条边给出 90° 档位朝向——剖面式布景的外墙、隔断、屋檐都靠它对齐。
    /// <para>只在菱形几何（diamond / diamondz）下生效；矩形与六边形保持原来的格心吸附。</para>
    /// </summary>
    public bool EdgeSnapEnabled { get; set; }

    /// <summary>当前几何是否支持斜边吸附。</summary>
    public bool CanEdgeSnap => IsDiamondMode;

    /// <summary>最近一次斜边吸附贴到的是哪条边（0 左上 / 1 右上 / 2 右下 / 3 左下），未吸附为 -1。</summary>
    public int LastSnappedEdge { get; private set; } = -1;

    /// <summary>
    /// 把世界坐标吸附到最近的一条菱形共边：X/Y 取「脚底中点贴边」的左上角，
    /// 旋转取该边对应的 90° 档位。
    /// </summary>
    /// <param name="world">鼠标世界坐标。</param>
    /// <param name="widthCells">物件占地列数。</param>
    /// <param name="heightCells">物件占地行数。</param>
    /// <param name="rotation">输出：朝向角度（0 / 90 / 180 / 270）。</param>
    /// <returns>吸附后的左上角世界坐标。</returns>
    private Point SnapToEdge(Point world, int widthCells, int heightCells, out double rotation)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        var index = DiamondLattice.IndexAt(world.X, world.Y, cell);
        var col = (int)index.X;
        var row = (int)index.Y;
        var edge = DiamondLattice.NearestEdge(world, col, row, cell);
        var anchor = DiamondLattice.EdgeMidpoint(col, row, cell, edge);

        LastSnappedEdge = edge;
        rotation = DiamondLattice.EdgeRotation(edge);

        // 墙是「站着」的：脚底中点对齐边中点，所以左上角 = 锚点 - (半宽, 全高)
        var width = Math.Max(1, widthCells) * (double)cell;
        var height = Math.Max(1, heightCells) * (double)cell;
        return new Point(anchor.X - width / 2.0, anchor.Y - height);
    }

    /// <summary>
    /// 把当前选中物件重新贴到最近的斜边（属性面板「贴斜边」按钮、快捷键用）。
    /// 已经在边上的物件重复点击不会漂移：它以自身脚底中点重新求最近的边。
    /// </summary>
    /// <returns>true = 发生了吸附；没有选中、非菱形几何或坐标没变化时返回 false。</returns>
    public bool ResnapSelectedToEdge()
    {
        if (_selected is not { } item || !CanEdgeSnap)
        {
            return false;
        }

        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        var foot = new Point(item.X + item.Width * cell / 2.0, item.Y + item.Height * (double)cell);
        var target = SnapToEdge(foot, item.Width, item.Height, out var rotation);
        if (Math.Abs(target.X - item.X) < 0.01
            && Math.Abs(target.Y - item.Y) < 0.01
            && Math.Abs(item.Rotation - rotation) < 0.01)
        {
            return false;
        }

        item.X = target.X;
        item.Y = target.Y;
        item.Rotation = rotation;
        CommitEdit();
        return true;
    }

    #endregion
}
