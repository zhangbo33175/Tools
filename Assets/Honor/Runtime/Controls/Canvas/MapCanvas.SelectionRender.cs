/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.SelectionRender.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布选中与编辑绘制：选中框、占格、区域框、当前格与路径编辑
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>画布选中与编辑绘制：选中框、占格、区域框、当前格与路径编辑。</summary>
public sealed partial class MapCanvas : Control
{
    #region 选中、占格与路径绘制

    private void DrawSelection(DrawingContext context)
    {
        var item = _selected;
        if (item is null || _config is null)
        {
            return;
        }

        var bounds = item.Bounds;
        var handle = 8.0 * Hairline;

        // 选中框、四角手柄与碰撞 / 阴影区域都是实例的局部矩形，必须和贴图吃同一个旋转，
        // 否则改角度后会出现「图转了、框还是正的」这种错位。
        // 默认再裁到地图矩形：`bg_grass_2` 这类贴边的整块绿化只露出一条边，
        // 完整包围盒会把几百像素的虚线框画到蓝色画布底上，看起来像一块“暗色错误区”。
        // 「裁剪越界」关闭时不要裁，完整画出实例包围盒与四角手柄，便于检查边缘出血。
        var mapClip = new Rect(0, 0, _config.Width, _config.Height);

        // 选中框内容拆成本地函数：裁剪开/关只切换 PushClip，内容与碰撞/阴影逻辑只写一份。
        void DrawContent()
        {
            context.DrawRectangle(null, new Pen(AccentBrush, 2.0 * Hairline), bounds);

            // 绿化层的 collision/shadow 是 PSD 批量导入时统一带上的 true，画出来只会把
            // 整块 605×351 的草地压成灰暗色块；它不是可编辑的碰撞体，选中时只保留外框与占格。
            // 用手改后的有效显示层判断，显示层改为绿化时同样不铺调试填充。
            var greenery = LayerOrder.EffectiveLayer(item) == MapLayerKeys.Greenery;

            // 碰撞 / 阴影区域：只对当前选中实例描绘，便于在画布上对准「碰撞区域 / 阴影区域」的数值。
            // Spine 动画本身透明，若继续铺半透明填充，会在角色背后露出一整块「背景图」；
            // 因此 Spine 物件只保留虚线轮廓，不画填充。
            var spineVisual = UsesSpineVisual(item);
            if (item.CollisionEnabled && !greenery)
            {
                DrawEditableArea(
                    context,
                    item.CollisionBounds,
                    spineVisual ? null : CollisionAreaFillBrush,
                    CollisionAreaStrokeBrush);
            }

            if (item.ShadowEnabled && !greenery)
            {
                DrawEditableArea(
                    context,
                    item.ShadowBounds,
                    spineVisual ? null : ShadowAreaFillBrush,
                    ShadowAreaStrokeBrush);
            }

            foreach (var corner in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft })
            {
                context.FillRectangle(
                    HandleBrush,
                    new Rect(corner.X - handle / 2.0, corner.Y - handle / 2.0, handle, handle));
            }
        }

        if (ShowClipOutsideMap)
        {
            using (context.PushClip(mapClip))
            using (context.PushTransform(SpriteLayout.ContentTransform(item.Rotation, item.FlipX, bounds)))
            {
                DrawContent();
            }
        }
        else
        {
            using (context.PushTransform(SpriteLayout.ContentTransform(item.Rotation, item.FlipX, bounds)))
            {
                DrawContent();
            }
        }
    }

    /// <summary>
    /// 选中实例的格子级占格（碰撞区域）：在所有图元贴图**之前**画一次，
    /// 整体沉到图片层级下面，只在没有美术覆盖的空隙里透出来，不会浮在美术表面。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    private void DrawSelectedOccupancy(DrawingContext context)
    {
        if (_selected is not { } item)
        {
            return;
        }

        DrawOccupancyCells(context, item, emphasized: true, suppressFill: UsesSpineVisual(item));
    }

    /// <summary>
    /// 未选中、但带旋转的实例：只画一圈淡描边占格，停在整个图元之上做提示（不铺填充色）。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    private void DrawIdleOccupancy(DrawingContext context)
    {
        var config = _config;
        if (config is null)
        {
            return;
        }

        foreach (var item in OrderedObjects(config))
        {
            if (ReferenceEquals(item, _selected) || Math.Abs(item.Rotation) < 0.01)
            {
                continue;
            }

            DrawOccupancyCells(context, item, emphasized: false);
        }
    }

    /// <summary>
    /// 实例的格子级占格（碰撞区域）：把「包围盒（含旋转）」压到的格子一格格画出来，
    /// 摆放模式=矩形时画正方格、=菱形时画菱形格。强调态加深填充（可直接当碰撞区域核对）。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="item">实例。</param>
    /// <param name="emphasized">是否强调（选中实例）。</param>
    /// <param name="suppressFill">是否只画描边不铺填充（Spine 透明动画用）。</param>
    private void DrawOccupancyCells(DrawingContext context, PlacedObject item, bool emphasized, bool suppressFill = false)
    {
        var config = _config;
        if (config is null)
        {
            return;
        }

        var cell = (double)(config.GridSize > 0 ? config.GridSize : MapToolDefaults.GridSize);
        if (cell <= 0)
        {
            return;
        }

        var thin = Hairline;
        var diamond = IsDiamondMode;

        // 注意：这里刻意不使用 PushClip —— 一旦用 PushClip 包住占格，Avalonia 会把这批图元
        // 合成到贴图之上，占格又会浮在美术表面；越界格子改为逐格判断裁掉。

        var cells = OccupiedCells(item, cell, diamond);
        if (cells.Count == 0)
        {
            return;
        }

        var stroke = emphasized
            ? new Pen(OccupancyStrokeBrush, 2.0 * thin)
            : new Pen(OccupancyIdleStrokeBrush, thin);
        var fill = emphasized && !suppressFill ? OccupancyFillBrush : null;
        var mapWidth = config.Width;
        var mapHeight = config.Height;

        foreach (var (col, row) in cells)
        {
            if (diamond)
            {
                var polygon = BuildPolygon(DiamondLattice.Vertices(col, row, (int)cell));
                if (!polygon.Bounds.Intersects(new Rect(0, 0, mapWidth, mapHeight)))
                {
                    continue;
                }

                context.DrawGeometry(fill, stroke, polygon);
                continue;
            }

            var rect = new Rect(col * cell, row * cell, cell, cell);
            if (!rect.Intersects(new Rect(0, 0, mapWidth, mapHeight)))
            {
                continue;
            }

            if (fill is not null)
            {
                context.FillRectangle(fill, rect);
            }

            context.DrawRectangle(null, stroke, rect);
        }
    }

    /// <summary>
    /// 旋转后压到的格子集合：格子中心落在旋转矩形内即算占用，与导出占格（buildCells）判定口径一致。
    /// </summary>
    /// <param name="item">实例。</param>
    /// <param name="cell">格子边长（像素）。</param>
    /// <param name="diamond">是否菱形格。</param>
    /// <returns>格子行列集合（X = 列，Y = 行）。</returns>
    private static List<(int Col, int Row)> OccupiedCells(PlacedObject item, double cell, bool diamond)
    {
        var cells = new List<(int, int)>();
        var bounds = item.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return cells;
        }

        var quad = RotatedCorners(bounds, item.Rotation);

        double left = double.MaxValue, top = double.MaxValue;
        double right = double.MinValue, bottom = double.MinValue;
        foreach (var corner in quad)
        {
            left = Math.Min(left, corner.X);
            top = Math.Min(top, corner.Y);
            right = Math.Max(right, corner.X);
            bottom = Math.Max(bottom, corner.Y);
        }

        int minCol, maxCol, minRow, maxRow;
        if (diamond)
        {
            var start = DiamondLattice.IndexAt(left, top, (int)cell);
            var end = DiamondLattice.IndexAt(right, bottom, (int)cell);
            minCol = (int)Math.Min(start.X, end.X) - 2;
            maxCol = (int)Math.Max(start.X, end.X) + 2;
            minRow = (int)Math.Min(start.Y, end.Y) - 2;
            maxRow = (int)Math.Max(start.Y, end.Y) + 2;
        }
        else
        {
            minCol = (int)Math.Floor(left / cell);
            maxCol = (int)Math.Floor((right - 1e-6) / cell);
            minRow = (int)Math.Floor(top / cell);
            maxRow = (int)Math.Floor((bottom - 1e-6) / cell);
        }

        // 极端尺寸的安全阀：单实例候选格超过 8000 直接不画，避免一次重绘卡住界面
        if ((maxCol - minCol + 1) * (maxRow - minRow + 1) > 8000)
        {
            return cells;
        }

        for (var col = minCol; col <= maxCol; col++)
        {
            for (var row = minRow; row <= maxRow; row++)
            {
                var center = diamond
                    ? DiamondLattice.Center(col, row, (int)cell)
                    : new Point(col * cell + cell / 2.0, row * cell + cell / 2.0);

                if (PointInQuad(center, quad))
                {
                    cells.Add((col, row));
                }
            }
        }

        return cells;
    }

    /// <summary>实例包围盒绕自身中心旋转后的四个角（世界坐标），旋转方向与 <see cref="ContentTransform"/> 一致。</summary>
    /// <param name="bounds">包围盒。</param>
    /// <param name="degrees">旋转角度（度，顺时针为正）。</param>
    /// <returns>左上、右上、右下、左下四个角。</returns>
    private static Point[] RotatedCorners(Rect bounds, double degrees)
    {
        var rad = degrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var c = bounds.Center;

        Point Rotate(Point p) => new(
            c.X + (p.X - c.X) * cos - (p.Y - c.Y) * sin,
            c.Y + (p.X - c.X) * sin + (p.Y - c.Y) * cos);

        return
        [
            Rotate(bounds.TopLeft),
            Rotate(bounds.TopRight),
            Rotate(bounds.BottomRight),
            Rotate(bounds.BottomLeft),
        ];
    }

    /// <summary>点是否落在凸四边形内（按同向叉积判定，顶点顺序无所谓）。</summary>
    /// <param name="p">待判定点。</param>
    /// <param name="quad">凸四边形顶点。</param>
    /// <returns>落在内部或边上返回 true。</returns>
    private static bool PointInQuad(Point p, Point[] quad)
    {
        var sign = 0;
        for (var i = 0; i < quad.Length; i++)
        {
            var a = quad[i];
            var b = quad[(i + 1) % quad.Length];
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (Math.Abs(cross) < 1e-6)
            {
                continue;
            }

            var current = cross > 0 ? 1 : -1;
            if (sign == 0)
            {
                sign = current;
            }
            else if (current != sign)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>绘制选中实例的可编辑区域（碰撞 / 阴影）：可选半透明填充 + 虚线描边，线宽按缩放保持设备像素。</summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="area">区域矩形（世界坐标）。</param>
    /// <param name="fill">填充画刷；为 null 时只画虚线轮廓。</param>
    /// <param name="stroke">描边画刷。</param>
    private void DrawEditableArea(DrawingContext context, Rect area, IBrush? fill, IBrush stroke)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        if (fill is not null)
        {
            context.FillRectangle(fill, area);
        }

        context.DrawRectangle(null, new Pen(stroke, 1.5 * Hairline, DashStyle.Dash), area);
    }

    /// <summary>绘制空白处点选的格子：按绘制形状填充半透明绿色并描边，随缩放保持 1.5 设备像素线宽。</summary>    /// <param name="context">绘制上下文。</param>
    private void DrawSelectedCell(DrawingContext context)
    {
        if (_selectedCell is not { } selected || _config is null)
        {
            return;
        }

        var size = (double)_config.GridSize;
        var thin = 1.5 * Hairline;

        // 边缘格子会探出地图半格，按地图矩形裁剪，保证高亮不画到青色底上
        using var clip = context.PushClip(new Rect(0, 0, _config.Width, _config.Height));

        if (IsDiamondMode)
        {
            var center = CellCenter(selected);
            var half = size / 2.0;
            var vertices = new[]
            {
                new Point(center.X, center.Y - half),
                new Point(center.X + half, center.Y),
                new Point(center.X, center.Y + half),
                new Point(center.X - half, center.Y),
            };

            var geometry = BuildPolygon(vertices);
            context.DrawGeometry(SelectedCellFillBrush, new Pen(SelectedCellStrokeBrush, thin, lineCap: PenLineCap.Round), geometry);
            return;
        }

        var rect = new Rect(selected.X * size, selected.Y * size, size, size);
        context.FillRectangle(SelectedCellFillBrush, rect);
        context.DrawRectangle(null, new Pen(SelectedCellStrokeBrush, thin), rect);
    }

    private void DrawEditingPath(DrawingContext context)
    {
        var item = _selected;
        if (item is null || item.PathPoints.Count == 0)
        {
            return;
        }

        var thin = Hairline;
        var pen = new Pen(UiTheme.MapView.Path, thin * 2.0);
        var markerBrush = UiTheme.MapView.PathPoint;

        // 与 Unity 原版一致：只画点与点之间的连线，不从角色中心额外拉一条「伪起点」，
        // 否则第一个路径点会被误读成已经走过的线段。
        Point? previous = null;
        foreach (var point in item.PathPoints)
        {
            var current = new Point(point.X, point.Y);
            if (previous is { } from)
            {
                context.DrawLine(pen, from, current);
            }

            context.FillRectangle(
                markerBrush,
                new Rect(current.X - thin * 4, current.Y - thin * 4, thin * 8, thin * 8));
            previous = current;
        }
    }

    #endregion
}



