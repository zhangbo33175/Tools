/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapGridRenderer.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图网格渲染器：统一正交/菱形摆放网格与路径网格的绘制、
 *            抽稀和裁剪规则，MapCanvas 只负责传入视图状态。
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 地图网格绘制。无状态、无业务引用，只依赖地图配置、缩放与屏幕→世界换算。
/// </summary>
public static class MapGridRenderer
{
    #region 常量

    /// <summary>每 N 条摆放网格线加粗一次，方便数格子。</summary>
    public const int MajorGridEvery = 8;

    /// <summary>路径网格的格宽：普通格子的 4 倍。</summary>
    public const int PathGridEveryCells = 4;

    /// <summary>网格线在屏幕上的最小可见间距，低于该值按 2 的幂抽稀。</summary>
    private const double MinimumScreenSpacing = 2.0;

    /// <summary>正交网格开始抽稀的缩放阈值。</summary>
    private const double OrthogonalStep55 = 0.55;

    /// <summary>正交网格第二档抽稀阈值。</summary>
    private const double OrthogonalStep28 = 0.28;

    /// <summary>正交网格第三档抽稀阈值。</summary>
    private const double OrthogonalStep14 = 0.14;

    #endregion

    #region 摆放网格

    /// <summary>
    /// 绘制摆放网格。网格尺寸只由地图宽高与格子边长决定，绘制范围以视口与地图矩形的交集为界。
    /// 菱形模式按 TileMap 口径逐格绘制，不做每 N 格加粗。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    /// <param name="client">画布可视矩形（屏幕坐标）。</param>
    /// <param name="zoom">当前缩放倍率。</param>
    /// <param name="screenToWorld">屏幕坐标转世界坐标。</param>
    public static void DrawPlacementGrid(
        DrawingContext context,
        MapConfigData config,
        Rect client,
        double zoom,
        Func<Point, Point> screenToWorld)
    {
        var cell = (double)config.GridSize;
        if (cell <= 0 || config.Width <= 0 || config.Height <= 0)
        {
            return;
        }

        var thin = DevicePixelWidth(zoom);
        var map = new Rect(0, 0, config.Width, config.Height);
        var view = new Rect(screenToWorld(client.TopLeft), screenToWorld(client.BottomRight)).Intersect(map);
        if (view.Width > 0 && view.Height > 0)
        {
            using (context.PushClip(view))
            {
                if (config.DrawShape == DrawShapes.Diamond)
                {
                    DrawDiamondGrid(context, map, cell * DiamondGridFactor(cell, zoom), thin);
                }
                else
                {
                    DrawOrthogonalGrid(context, map, cell * OrthogonalStep(zoom), thin);
                }
            }
        }

        // 地图外边框：闭合右/下边缘，避免网格铺满后看起来缺一条边
        context.DrawRectangle(
            null,
            new Pen(UiTheme.MapGrid.MapFrameLine, 1.5 / Math.Max(zoom, 0.0001)),
            map);
    }

    #endregion

    #region 路径网格

    /// <summary>
    /// 绘制路径编辑网格：按 <see cref="PathGridEveryCells"/> 个普通格为一步铺蓝色辅助线，
    /// 只遍历当前视口可见范围。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    /// <param name="client">画布可视矩形（屏幕坐标）。</param>
    /// <param name="zoom">当前缩放倍率。</param>
    /// <param name="screenToWorld">屏幕坐标转世界坐标。</param>
    public static void DrawPathGrid(
        DrawingContext context,
        MapConfigData config,
        Rect client,
        double zoom,
        Func<Point, Point> screenToWorld)
    {
        var cell = config.GridSize * PathGridEveryCells;
        if (cell <= 0)
        {
            return;
        }

        var pen = new Pen(UiTheme.MapGrid.PathLine, DevicePixelWidth(zoom));
        var topLeft = screenToWorld(client.TopLeft);
        var bottomRight = screenToWorld(client.BottomRight);

        for (var x = Math.Max(0, Math.Floor(topLeft.X / cell) * cell);
             x <= Math.Min(config.Width, bottomRight.X);
             x += cell)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, config.Height));
        }

        for (var y = Math.Max(0, Math.Floor(topLeft.Y / cell) * cell);
             y <= Math.Min(config.Height, bottomRight.Y);
             y += cell)
        {
            context.DrawLine(pen, new Point(0, y), new Point(config.Width, y));
        }
    }

    #endregion

    #region 正交与菱形

    /// <summary>正交网格：横竖两组平行线，从 (0,0) 起铺满地图矩形。</summary>
    private static void DrawOrthogonalGrid(DrawingContext context, Rect map, double spacing, double thin)
    {
        for (var i = 0; ; i++)
        {
            var x = i * spacing;
            if (x > map.Width)
            {
                break;
            }

            context.DrawLine(GridPen(i, thin), new Point(x, 0), new Point(x, map.Height));
        }

        for (var i = 0; ; i++)
        {
            var y = i * spacing;
            if (y > map.Height)
            {
                break;
            }

            context.DrawLine(GridPen(i, thin), new Point(0, y), new Point(map.Width, y));
        }
    }

    /// <summary>菱形网格：两组 45° 斜线，相位偏移半格，与 TileMap 格点对齐。</summary>
    private static void DrawDiamondGrid(DrawingContext context, Rect map, double spacing, double thin)
    {
        var pen = new Pen(UiTheme.MapGrid.MinorLine, thin);
        var phase = spacing / 2.0;

        DrawDiagonalFamily(context, map, pen, phase, spacing, -map.Height, map.Width, rising: true);
        DrawDiagonalFamily(context, map, pen, phase, spacing, 0, map.Width + map.Height, rising: false);
    }

    /// <summary>按截距区间画一族 45° 平行线。</summary>
    private static void DrawDiagonalFamily(
        DrawingContext context,
        Rect map,
        Pen pen,
        double phase,
        double spacing,
        double minIntercept,
        double maxIntercept,
        bool rising)
    {
        var start = (int)Math.Floor((minIntercept - phase) / spacing) - 1;
        var end = (int)Math.Ceiling((maxIntercept - phase) / spacing) + 1;
        for (var i = start; i <= end; i++)
        {
            DrawDiagonal(context, map, phase + i * spacing, pen, rising);
        }
    }

    /// <summary>画一条贯穿地图矩形的 45° 斜线，超出部分由裁剪丢弃。</summary>
    private static void DrawDiagonal(DrawingContext context, Rect map, double intercept, Pen pen, bool rising)
    {
        if (rising)
        {
            context.DrawLine(pen, new Point(0, intercept), new Point(map.Width, map.Width + intercept));
            return;
        }

        context.DrawLine(pen, new Point(0, intercept), new Point(map.Width, intercept - map.Width));
    }

    #endregion

    #region 网格参数

    /// <summary>取网格线画笔：每 <see cref="MajorGridEvery"/> 条使用强线。</summary>
    private static Pen GridPen(int index, double thin)
    {
        var brush = index % MajorGridEvery == 0
            ? UiTheme.MapGrid.MajorLine
            : UiTheme.MapGrid.MinorLine;
        return new Pen(brush, thin);
    }

    /// <summary>正交网格按缩放抽稀，保证屏幕线距不至于糊成一片。</summary>
    private static int OrthogonalStep(double zoom)
    {
        if (zoom >= OrthogonalStep55)
        {
            return 1;
        }

        if (zoom >= OrthogonalStep28)
        {
            return 2;
        }

        return zoom >= OrthogonalStep14 ? 4 : 8;
    }

    /// <summary>菱形网格抽稀倍数：屏幕上边长至少 2 像素，否则按 2 的幂加倍。</summary>
    private static int DiamondGridFactor(double cell, double zoom)
    {
        var factor = 1;
        while (factor < 8 && cell * zoom * factor < MinimumScreenSpacing)
        {
            factor *= 2;
        }

        return factor;
    }

    /// <summary>1 个设备像素对应的世界线宽。</summary>
    private static double DevicePixelWidth(double zoom) => 1.0 / Math.Max(zoom, 0.0001);

    #endregion
}
