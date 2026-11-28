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

    /// <summary>菱形网格开始抽稀的屏幕间距（像素）：低于该值斜线会糊成一片斜纹。</summary>
    private const double DiamondMinimumSpacing = 12.0;

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
                if (DrawShapes.IsDiamond(config.DrawShape))
                {
                    DrawDiamondGrid(context, map, cell, DiamondGridFactor(cell, zoom), thin);
                }
                else if (DrawShapes.IsHex(config.DrawShape))
                {
                    DrawHexGrid(context, map, view, (int)cell, HexGridStep(cell, zoom), thin);
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

    /// <summary>
    /// 菱形网格：两组斜率 ±0.5 的斜线，相位与 <see cref="DiamondLattice"/> 的真实菱形边一致，
    /// 抽稀只取「真实边」的子集，所以任何缩放下放大出来的菱形都与实际格子严丝合缝。
    /// <para>
    /// 细线每格一条，每 <see cref="MajorGridEvery"/> 条换一条主线；主细线在同一次遍历里选笔，
    /// 不重复描线，密度只有「细线一遍 + 主线一遍」的一半，不会糊成一片斜纹。
    /// </para>
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="map">地图矩形。</param>
    /// <param name="cell">格子边长（菱形的半高）。</param>
    /// <param name="factor">抽稀倍数（1 / 2 / 4 / 8）。</param>
    /// <param name="thin">线宽（世界坐标）。</param>
    private static void DrawDiamondGrid(DrawingContext context, Rect map, double cell, int factor, double thin)
    {
        var minor = new Pen(UiTheme.MapGrid.DiamondMinorLine, thin);
        var major = new Pen(UiTheme.MapGrid.DiamondMajorLine, thin);

        // 真实菱形边的截距相位固定为 -cell/2 + k*cell：抽稀只跳着取这些边，相位不跟着 spacing 变，
        // 否则放大后的菱形网会与实际格子错位，看起来像一层斜纹布。
        var phase = -cell / 2.0;
        var spacing = cell * factor;

        DrawHalfSlopeFamily(context, map, minor, major, phase, spacing, rising: true, MajorGridEvery);
        DrawHalfSlopeFamily(context, map, minor, major, phase, spacing, rising: false, MajorGridEvery);
    }

    /// <summary>按截距区间画一族斜率 ±0.5 的平行线（2:1 等距菱形边方向）。</summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="map">地图矩形。</param>
    /// <param name="minor">细线画笔。</param>
    /// <param name="major">主线画笔。</param>
    /// <param name="phase">截距相位。</param>
    /// <param name="spacing">截距步长（世界像素）。</param>
    /// <param name="rising">true 为「\」方向（y = 0.5x + b），false 为「/」方向（y = -0.5x + b）。</param>
    /// <param name="majorEvery">每多少条换一次主线；0 表示全部走细线。</param>
    private static void DrawHalfSlopeFamily(
        DrawingContext context,
        Rect map,
        Pen minor,
        Pen major,
        double phase,
        double spacing,
        bool rising,
        int majorEvery)
    {
        // 「\」族覆盖矩形需要 b ∈ [-W/2, H]；「/」族需要 b ∈ [0, H + W/2]
        var min = rising ? -map.Width / 2.0 : 0;
        var max = rising ? map.Height : map.Height + map.Width / 2.0;

        var start = (int)Math.Floor((min - phase) / spacing) - 1;
        var end = (int)Math.Ceiling((max - phase) / spacing) + 1;
        for (var i = start; i <= end; i++)
        {
            var intercept = phase + i * spacing;
            var pen = majorEvery > 0 && i % majorEvery == 0 ? major : minor;
            if (rising)
            {
                context.DrawLine(pen, new Point(0, intercept), new Point(map.Width, intercept + map.Width / 2.0));
            }
            else
            {
                context.DrawLine(pen, new Point(0, intercept), new Point(map.Width, intercept - map.Width / 2.0));
            }
        }
    }

    #endregion

    #region 六边形

    /// <summary>
    /// 六边形网格：只遍历视口覆盖的格子（外扩一圈），按 <paramref name="step"/> 抽稀后
    /// 把六边形轮廓合并进「细线 / 主线」两条几何各画一次。
    /// 逐格 DrawLine 在上万格时会拖慢重绘，合并成一条几何后只需一次提交。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="map">地图矩形。</param>
    /// <param name="view">视口在世界坐标下的可见矩形。</param>
    /// <param name="cell">格子宽度（像素）。</param>
    /// <param name="step">抽稀步长（1 / 2 / 4 / 8，行列同时跳）。</param>
    /// <param name="thin">线宽（世界坐标）。</param>
    private static void DrawHexGrid(DrawingContext context, Rect map, Rect view, int cell, int step, double thin)
    {
        if (cell <= 0 || step <= 0)
        {
            return;
        }

        var rowStep = HexLattice.RowStep(cell);
        var maxRow = (int)Math.Ceiling(map.Height / rowStep) + 1;
        var maxCol = (int)Math.Ceiling(map.Width / cell) + 1;

        var firstCol = Math.Max(0, (int)Math.Floor(view.X / cell) - 1);
        var lastCol = Math.Min(maxCol, (int)Math.Ceiling(view.Right / cell) + 1);
        var firstRow = Math.Max(0, (int)Math.Floor(view.Y / rowStep) - 1);
        var lastRow = Math.Min(maxRow, (int)Math.Ceiling(view.Bottom / rowStep) + 1);

        var minor = new StreamGeometry();
        var major = new StreamGeometry();
        using (var minorContext = minor.Open())
        using (var majorContext = major.Open())
        {
            for (var row = firstRow - firstRow % step; row <= lastRow; row += step)
            {
                for (var col = firstCol - firstCol % step; col <= lastCol; col += step)
                {
                    var vertices = HexLattice.Vertices(col, row, cell);
                    var target = col % MajorGridEvery == 0 && row % MajorGridEvery == 0
                        ? majorContext
                        : minorContext;

                    target.BeginFigure(vertices[0], isFilled: false);
                    for (var i = 1; i < vertices.Length; i++)
                    {
                        target.LineTo(vertices[i]);
                    }

                    target.EndFigure(true);
                }
            }
        }

        context.DrawGeometry(null, new Pen(UiTheme.MapGrid.DiamondMinorLine, thin), minor);
        context.DrawGeometry(null, new Pen(UiTheme.MapGrid.DiamondMajorLine, thin), major);
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

    /// <summary>
    /// 菱形网格抽稀倍数：保证屏幕上相邻斜线的间距不小于 <see cref="DiamondMinimumSpacing"/>，
    /// 否则 4000px 的地图在 10% 缩放下会变成一整块斜纹。
    /// </summary>
    /// <param name="cell">格子边长。</param>
    /// <param name="zoom">当前缩放。</param>
    /// <returns>1 / 2 / 4 / 8。</returns>
    private static int DiamondGridFactor(double cell, double zoom)
    {
        var factor = 1;
        while (factor < 8 && cell * zoom * factor < DiamondMinimumSpacing)
        {
            factor *= 2;
        }

        return factor;
    }

    /// <summary>
    /// 六边形网格抽稀步长：保证屏幕上单个六边形宽度不小于 8px。
    /// 蜂窝不像斜线族那样会连成一片，阈值可以比菱形放宽一档，低缩放下也能看出格子在哪儿。
    /// </summary>
    /// <param name="cell">格子宽度。</param>
    /// <param name="zoom">当前缩放。</param>
    /// <returns>1 / 2 / 4 / 8。</returns>
    private static int HexGridStep(double cell, double zoom)
    {
        const double minimumSpacing = 8.0;
        var step = 1;
        while (step < 8 && cell * zoom * step < minimumSpacing)
        {
            step *= 2;
        }

        return step;
    }

    /// <summary>1 个设备像素对应的世界线宽。</summary>
    private static double DevicePixelWidth(double zoom) => 1.0 / Math.Max(zoom, 0.0001);

    #endregion
}
