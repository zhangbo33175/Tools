/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.TrafficRender.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布交通绘制：人行道/道路格子外轮廓、横道与红绿灯标记、雾效与笔刷
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

/// <summary>画布交通绘制：人行道/道路轮廓、横道、红绿灯、雾效与笔刷。</summary>
public sealed partial class MapCanvas : Control
{
    /// <summary>迷雾笔刷光标的最小直径，避免滑条最小值时圈不可见。</summary>
    private const double MinFogBrushDiameter = 8.0;

    #region 交通地面与标记
    /// <summary>
    /// 人行道 / 道路格子的外轮廓（绘制在图元之下）。只描整块区域的外边界、不填充底色，
    /// 既不遮挡底图与美术贴图，又能一眼看出刷格范围；菱形模式按 TileMap 口径描菱形台阶边。
    /// </summary>
    private void DrawTrafficGround(DrawingContext context, MapConfigData config)
    {
        var cell = (double)config.GridSize;
        var thin = Math.Max(1.2 * Hairline, 0.8);
        var sidewalkPen = new Pen(UiTheme.MapView.Sidewalk, thin);
        var roadPen = new Pen(UiTheme.MapView.Road, thin);
        var diamond = config.DrawShape == DrawShapes.Diamond;

        var sidewalkBoundary = BuildCellsBoundary(config.SidewalkCells, cell, diamond);
        if (sidewalkBoundary is not null)
        {
            context.DrawGeometry(null, sidewalkPen, sidewalkBoundary);
        }

        var roadBoundary = BuildCellsBoundary(config.RoadCells, cell, diamond);
        if (roadBoundary is not null)
        {
            context.DrawGeometry(null, roadPen, roadBoundary);
        }
    }

    /// <summary>
    /// 求一组格子并集的外轮廓：每格的四条边登记一次，被相邻格共用的内部边成对抵消，
    /// 剩下的就是整块区域的边界线。逐条边渲染，不做路径拼接，避免自交导致奇偶填充异常。
    /// </summary>
    /// <param name="cells">格子集合（X = 列，Y = 行）。</param>
    /// <param name="cell">格子边长（世界像素）。</param>
    /// <param name="diamond">是否按菱形格计算边。</param>
    /// <returns>外轮廓几何；格子为空时返回 null。</returns>
    private static StreamGeometry? BuildCellsBoundary(IReadOnlyList<MapGridCellData> cells, double cell, bool diamond)
    {
        if (cells.Count == 0)
        {
            return null;
        }

        // 端点取整到 0.01 像素再配对，保证相邻格共用的边能被准确抵消
        var edges = new Dictionary<(long, long, long, long), (Point, Point)>();

        foreach (var item in cells)
        {
            var corners = diamond
                ? DiamondLattice.Vertices(item.X, item.Y, (int)cell)
                : new[]
                {
                    new Point(item.X * cell, item.Y * cell),
                    new Point((item.X + 1) * cell, item.Y * cell),
                    new Point((item.X + 1) * cell, (item.Y + 1) * cell),
                    new Point(item.X * cell, (item.Y + 1) * cell),
                };

            for (var i = 0; i < 4; i++)
            {
                var from = corners[i];
                var to = corners[(i + 1) % 4];
                var key = EdgeKey(from, to);
                if (!edges.Remove(key))
                {
                    edges[key] = (from, to);
                }
            }
        }

        if (edges.Count == 0)
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var (from, to) in edges.Values)
            {
                ctx.BeginFigure(from, false);
                ctx.LineTo(to);
                ctx.EndFigure(false);
            }
        }

        return geometry;
    }

    /// <summary>无向边的主键：两端点各自取整后按字典序归一，使 (A,B) 与 (B,A) 得到同一个键。</summary>
    /// <param name="from">起点。</param>
    /// <param name="to">终点。</param>
    /// <returns>四元组主键。</returns>
    private static (long, long, long, long) EdgeKey(Point from, Point to)
    {
        long ax = (long)Math.Round(from.X * 100), ay = (long)Math.Round(from.Y * 100);
        long bx = (long)Math.Round(to.X * 100), by = (long)Math.Round(to.Y * 100);
        var aFirst = ax < bx || (ax == bx && ay <= by);
        return aFirst ? (ax, ay, bx, by) : (bx, by, ax, ay);
    }

    /// <summary>斑马线、红绿灯、停止线等交通标记（绘制在图元之上）。</summary>
    private void DrawTrafficMarkers(DrawingContext context, MapConfigData config)
    {
        var cell = (double)config.GridSize;
        var stripeBrush = UiTheme.MapView.Crosswalk;
        var crosswalkPen = new Pen(UiTheme.MapView.CrosswalkEdge, 2.0 * Hairline);

        var objectIds = config.Objects
            .Select(instance => instance.Id)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var crosswalk in config.Crosswalks)
        {
            // 横道已经是地图上的普通物件时，由物件贴图负责显示，不再叠画斑马条纹
            if (objectIds.Contains(crosswalk.Id))
            {
                continue;
            }

            var a = new Point(crosswalk.PointA.X, crosswalk.PointA.Y);
            var b = new Point(crosswalk.PointB.X, crosswalk.PointB.Y);

            // 横道主体：按横穿方向铺斑马条纹（条纹垂直于行走方向）
            var stripeWidth = crosswalk.Width > 0 ? crosswalk.Width : MapToolDefaults.GridSize;

            if (crosswalk.Direction == TrafficDirections.Vertical)
            {
                var top = Math.Min(a.Y, b.Y);
                var bottom = Math.Max(a.Y, b.Y);
                var centerX = (a.X + b.X) / 2.0;

                for (var y = top; y < bottom; y += cell * 2)
                {
                    context.FillRectangle(
                        stripeBrush,
                        new Rect(centerX - stripeWidth / 2.0, y, stripeWidth, Math.Min(cell, bottom - y)));
                }
            }
            else
            {
                var left = Math.Min(a.X, b.X);
                var right = Math.Max(a.X, b.X);
                var centerY = (a.Y + b.Y) / 2.0;

                for (var x = left; x < right; x += cell * 2)
                {
                    context.FillRectangle(
                        stripeBrush,
                        new Rect(x, centerY - stripeWidth / 2.0, Math.Min(cell, right - x), stripeWidth));
                }
            }

            context.DrawLine(crosswalkPen, a, b);
        }

        foreach (var light in config.TrafficLights)
        {
            // 灯体已经是地图上的普通物件时，由物件贴图负责显示，不再叠画三色假灯
            if (objectIds.Contains(light.Id))
            {
                continue;
            }

            var center = new Point(light.PosX, light.PosY);
            var radius = Math.Max(cell / 3.0, 6.0 * Hairline);

            context.FillRectangle(
                UiTheme.MapView.TrafficBody,
                new Rect(center.X - radius, center.Y - radius * 2.4, radius * 2, radius * 4.8));

            context.FillRectangle(UiTheme.MapView.TrafficRed,
                new Rect(center.X - radius * 0.7, center.Y - radius * 2.0, radius * 1.4, radius * 1.2));
            context.FillRectangle(UiTheme.MapView.TrafficYellow,
                new Rect(center.X - radius * 0.7, center.Y - radius * 0.6, radius * 1.4, radius * 1.2));
            context.FillRectangle(UiTheme.MapView.TrafficGreen,
                new Rect(center.X - radius * 0.7, center.Y + radius * 0.8, radius * 1.4, radius * 1.2));

            if (light.BoundCrosswalkId.Length > 0)
            {
                context.DrawLine(
                    new Pen(UiTheme.MapView.TrafficGuide, Hairline),
                    center,
                    FindCrosswalkCenter(config, light.BoundCrosswalkId));
            }
        }

        // 选中实例的停止线
        if (_selected is { } selected && (selected.StopLineX > 0 || selected.StopLineY > 0))
        {
            var stop = new Point(selected.StopLineX, selected.StopLineY);
            var stopPen = new Pen(UiTheme.MapView.StopLine, 2.0 * Hairline);
            context.DrawLine(stopPen, new Point(stop.X - cell, stop.Y), new Point(stop.X + cell, stop.Y));
            context.DrawLine(stopPen, stop, new Point(stop.X, stop.Y - cell));
        }
    }

    private static Point FindCrosswalkCenter(MapConfigData config, string crosswalkId)
    {
        foreach (var crosswalk in config.Crosswalks)
        {
            if (crosswalk.Id == crosswalkId)
            {
                return new Point(
                    (crosswalk.PointA.X + crosswalk.PointB.X) / 2.0,
                    (crosswalk.PointA.Y + crosswalk.PointB.Y) / 2.0);
            }
        }

        return new Point(0, 0);
    }

    /// <summary>把四个顶点合成闭合多边形几何。</summary>
    /// <param name="vertices">顶点序列。</param>
    /// <returns>闭合几何。</returns>
    private static StreamGeometry BuildPolygon(Point[] vertices)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(vertices[0], true);
            for (var i = 1; i < vertices.Length; i++)
            {
                ctx.LineTo(vertices[i]);
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>迷雾覆盖层：按区域浓度叠加黑色。</summary>
    private void DrawFog(DrawingContext context, MapConfigData config)
    {
        var fog = Fog;
        if (fog is null || fog.Regions.Count == 0)
        {
            return;
        }

        var cell = (double)config.GridSize;
        foreach (var region in fog.Regions)
        {
            var alpha = (byte)Math.Clamp((int)Math.Round(region.Opacity * 255), 0, 255);
            var overlay = UiTheme.MapView.FogOverlay;
            var brush = new SolidColorBrush(Color.FromArgb(alpha, overlay.R, overlay.G, overlay.B));
            foreach (var fogCell in region.Cells)
            {
                context.FillRectangle(
                    brush,
                    new Rect(fogCell.X * cell, fogCell.Y * cell, cell, cell));
            }
        }
    }

    /// <summary>迷雾笔刷光标：绘制状态下在指针处画出笔刷范围圈。</summary>
    private void DrawFogBrush(DrawingContext context)
    {
        if (!FogDrawing || _fogHover is not { } hover)
        {
            return;
        }

        var radius = Math.Max(FogBrushSize, MinFogBrushDiameter) / 2.0;
        context.DrawEllipse(
            UiTheme.MapView.FogBrushFill,
            new Pen(UiTheme.MapView.FogBrushStroke, 1.6 * Hairline),
            hover,
            radius,
            radius);
    }

    #endregion
}



