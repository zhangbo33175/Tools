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
            // 选中框与占格框画在同一圈「落位格 + 格数」上：物件包围盒是按 PSD 像素算的，
            // 和格子块常差十几个像素，两条线一内一外会看成「占格没对准物件」。
            // 吸附到格块后只剩一条框，框角手柄也正好落在格角上，拖拽就是对格调整。
            var frame = ResolveSelectionFrame(item, bounds);
            context.DrawRectangle(null, new Pen(AccentBrush, 2.0 * Hairline), frame);

            // 绿化层的 collision/shadow 是 PSD 批量导入时统一带上的 true，画出来只会把
            // 整块 605×351 的草地压成灰暗色块；它不是可编辑的碰撞体，选中时只保留外框与占格。
            // 用手改后的有效显示层判断，显示层改为绿化时同样不铺调试填充。
            var greenery = LayerOrder.EffectiveLayer(item) == MapLayerKeys.Greenery;

            // 碰撞 / 阴影区域：只对当前选中实例描绘，便于在画布上对准「碰撞区域 / 阴影区域」的数值。
            // Spine 动画本身透明，若继续铺半透明填充，会在角色背后露出一整块「背景图」；
            // 因此 Spine 物件只保留虚线轮廓，不画填充。
            // 区域铺满整个包围盒时（碰撞宽高留 0 的默认值就是这样）只画虚线框：铺满没有任何信息量，
            // 一块 356×299 的路面被红紫两层各铺一遍会变成脏粉色，反而盖住美术。
            var spineVisual = UsesSpineVisual(item);
            if (item.CollisionEnabled && !greenery)
            {
                var area = item.CollisionBounds;
                DrawEditableArea(
                    context,
                    area,
                    spineVisual || CoversBounds(area, bounds) ? null : CollisionAreaFillBrush,
                    CollisionAreaStrokeBrush);
            }

            if (item.ShadowEnabled && !greenery)
            {
                var area = item.ShadowBounds;
                DrawEditableArea(
                    context,
                    area,
                    spineVisual || CoversBounds(area, bounds) ? null : ShadowAreaFillBrush,
                    ShadowAreaStrokeBrush);
            }

            // 手柄跟着外扩后的框走，和框线保持成一组
            foreach (var corner in new[] { frame.TopLeft, frame.TopRight, frame.BottomRight, frame.BottomLeft })
            {
                context.FillRectangle(
                    HandleBrush,
                    new Rect(corner.X - handle / 2.0, corner.Y - handle / 2.0, handle, handle));
            }
        }

        if (ShowClipOutsideMap)
        {
            using (context.PushClip(mapClip))
            using (context.PushTransform(SpriteLayout.ContentTransform(EffectiveRotationOf(item), item.FlipX, bounds)))
            {
                DrawContent();
            }
        }
        else
        {
            using (context.PushTransform(SpriteLayout.ContentTransform(EffectiveRotationOf(item), item.FlipX, bounds)))
            {
                DrawContent();
            }
        }
    }

    /// <summary>
    /// 选中框的落点：未旋转的矩形地图吸附到「落位格 + 格数」的格块上，和占格框共用一条边；
    /// 菱形 / 六边形 / 带旋转的物件仍按包围盒画（格块轴对齐，硬套上去会和转过的贴图错开）。
    /// </summary>
    /// <param name="item">实例。</param>
    /// <param name="bounds">实例包围盒。</param>
    /// <returns>外扩 3 设备像素后的选中框矩形（世界坐标）。</returns>
    private Rect ResolveSelectionFrame(PlacedObject item, Rect bounds)
    {
        var outset = 3.0 * Hairline;
        var target = bounds;

        if (_config is { } config
            && !DrawShapes.IsDiamond(config.DrawShape)
            && !DrawShapes.IsHex(config.DrawShape)
            && Math.Abs(EffectiveRotationOf(item)) < 0.01)
        {
            var cell = (double)(config.GridSize > 0 ? config.GridSize : MapToolDefaults.GridSize);
            if (cell > 0 && bounds.Width > 0 && bounds.Height > 0)
            {
                // 与 RectFootprintCells / 导出同一口径：落位格向下取整，格数按尺寸向上取整
                var cols = Math.Max(1, (int)Math.Ceiling(bounds.Width / cell - 1e-6));
                var rows = Math.Max(1, (int)Math.Ceiling(bounds.Height / cell - 1e-6));
                target = new Rect(
                    Math.Floor(bounds.X / cell) * cell,
                    Math.Floor(bounds.Y / cell) * cell,
                    cols * cell,
                    rows * cell);
            }
        }

        return new Rect(
            target.X - outset,
            target.Y - outset,
            target.Width + outset * 2,
            target.Height + outset * 2);
    }

    /// <summary>
    /// 编辑区（碰撞 / 阴影）是否正好铺满整个包围盒：宽高留 0 的默认值就是这个状态，
    /// 这种区域铺半透明填充等于给整块贴图刷一层色，只有真正缩进或偏移的区域才值得铺色。
    /// </summary>
    /// <param name="area">碰撞或阴影区域（世界像素）。</param>
    /// <param name="bounds">实例包围盒。</param>
    /// <returns>四边都在半像素容差内重合时返回 true。</returns>
    private static bool CoversBounds(Rect area, Rect bounds)
    {
        const double tolerance = 0.5;
        return Math.Abs(area.X - bounds.X) <= tolerance
            && Math.Abs(area.Y - bounds.Y) <= tolerance
            && Math.Abs(area.Right - bounds.Right) <= tolerance
            && Math.Abs(area.Bottom - bounds.Bottom) <= tolerance;
    }

    /// <summary>
    /// 占格绘图里还画十字的最大格数：道具、单格装饰这类 1~4 格的小件靠十字确认落位，
    /// 12×10 这种大块再逐格画十字会把 120 个格糊成一张密度翻倍的网，看不出「占了几格」。
    /// </summary>
    private const int MaxCrossCells = 4;

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
            if (ReferenceEquals(item, _selected) || Math.Abs(EffectiveRotationOf(item)) < 0.01)
            {
                continue;
            }

            DrawOccupancyCells(context, item, emphasized: false);
        }
    }

    /// <summary>
    /// 实例的格子级占格（地面层）：只铺半透明填充 + 一档细描边，让物件看起来像「站在格子上」。
    /// 强调用的亮线、格线与十字全部交给顶层的 <see cref="DrawSelectedOccupancyOutline"/>，
    /// 两层都描边等于把同一条格线画两遍，线会变粗变亮（原来选中大件时就是这样糊成一片绿网）。
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

        var cells = OccupiedCells(item, cell, config.DrawShape);
        if (cells.Count == 0)
        {
            return;
        }

        // 注意：这里刻意不使用 PushClip —— 一旦用 PushClip 包住占格，Avalonia 会把这批图元
        // 合成到贴图之上，占格又会浮在美术表面；越界格子改为逐格判断裁掉。
        var stroke = new Pen(OccupancyIdleStrokeBrush, Hairline);
        var fill = emphasized && !suppressFill ? OccupancyFillBrush : null;
        if (fill is null && !emphasized)
        {
            // 未选中（带旋转的提示态）只留一圈淡描边，不铺色
            DrawOccupancyLayer(context, cells, cell, config.DrawShape, null, stroke, null);
            return;
        }

        DrawOccupancyLayer(context, cells, cell, config.DrawShape, fill, stroke, null);
    }

    /// <summary>
    /// 选中实例占格的顶层：画在所有图元之后，保证贴图盖住整格时仍看得出占了哪几格。
    /// 矩形大地块只描「整块外框 + 淡淡的内部格线」，小件才逐格描边加十字——
    /// 这样 12×10 一眼就是一块 12×10，而不是 24×20 的网格。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    private void DrawSelectedOccupancyOutline(DrawingContext context)
    {
        if (_selected is not { } item || _config is not { } config)
        {
            return;
        }

        var cell = (double)(config.GridSize > 0 ? config.GridSize : MapToolDefaults.GridSize);
        if (cell <= 0)
        {
            return;
        }

        var cells = OccupiedCells(item, cell, config.DrawShape);
        if (cells.Count == 0)
        {
            return;
        }

        // 顶层占格：只描边，不铺填充（填充会盖住美术本体）
        var frame = new Pen(OccupancyOutlineBrush, 2.0 * Hairline);

        if (!DrawShapes.IsDiamond(config.DrawShape) && !DrawShapes.IsHex(config.DrawShape))
        {
            DrawRectOccupancyGrid(
                context,
                cells,
                cell,
                new Rect(0, 0, config.Width, config.Height),
                frame,
                new Pen(OccupancyGridLineBrush, Hairline),
                OccupancyCrossBrush);
            return;
        }

        DrawOccupancyLayer(
            context,
            cells,
            cell,
            config.DrawShape,
            null,
            frame,
            cells.Count <= MaxCrossCells ? OccupancyCrossBrush : null);
    }

    /// <summary>
    /// 矩形地图的顶层占格：整块画一条外框，内部格线改成「一条长线」而不是每格四条边，
    /// 相邻格共用的边只画一次，接缝不会叠成两倍粗细；格数少时再补格内十字确认单格落位。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="cells">格子行列集合（矩形块，行列连续）。</param>
    /// <param name="cell">格子边长。</param>
    /// <param name="mapRect">地图范围，用于裁掉越界部分。</param>
    /// <param name="frame">整块外框画笔。</param>
    /// <param name="grid">内部格线画笔。</param>
    /// <param name="cross">格内十字画刷；只在格数不多于 <see cref="MaxCrossCells"/> 时生效。</param>
    private void DrawRectOccupancyGrid(
        DrawingContext context,
        List<(int Col, int Row)> cells,
        double cell,
        Rect mapRect,
        IPen frame,
        IPen grid,
        IBrush? cross)
    {
        var minCol = int.MaxValue;
        var maxCol = int.MinValue;
        var minRow = int.MaxValue;
        var maxRow = int.MinValue;
        foreach (var (col, row) in cells)
        {
            minCol = Math.Min(minCol, col);
            maxCol = Math.Max(maxCol, col);
            minRow = Math.Min(minRow, row);
            maxRow = Math.Max(maxRow, row);
        }

        var area = new Rect(minCol * cell, minRow * cell, (maxCol - minCol + 1) * cell, (maxRow - minRow + 1) * cell);
        area = area.Intersect(mapRect);
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        // 内部格线：只画块内那几条，并且裁到可视范围内，地图边上的半格不会拖出框外
        for (var col = minCol + 1; col <= maxCol; col++)
        {
            var x = col * cell;
            if (x <= area.X + 0.5 || x >= area.Right - 0.5)
            {
                continue;
            }

            context.DrawLine(grid, new Point(x, area.Y), new Point(x, area.Bottom));
        }

        for (var row = minRow + 1; row <= maxRow; row++)
        {
            var y = row * cell;
            if (y <= area.Y + 0.5 || y >= area.Bottom - 0.5)
            {
                continue;
            }

            context.DrawLine(grid, new Point(area.X, y), new Point(area.Right, y));
        }

        if (cross is not null && cells.Count <= MaxCrossCells)
        {
            var crossPen = new Pen(cross, Hairline);
            foreach (var (col, row) in cells)
            {
                var rect = new Rect(col * cell, row * cell, cell, cell);
                if (!rect.Intersects(area))
                {
                    continue;
                }

                context.DrawLine(crossPen, new Point(rect.X, rect.Center.Y), new Point(rect.Right, rect.Center.Y));
                context.DrawLine(crossPen, new Point(rect.Center.X, rect.Y), new Point(rect.Center.X, rect.Bottom));
            }
        }

        // 外框线宽一半画在路径外，向内缩半宽才不会压到相邻格子上
        var inset = frame.Thickness / 2.0;
        var framed = area.Deflate(inset);
        if (framed.Width > 0 && framed.Height > 0)
        {
            context.DrawRectangle(null, frame, framed);
        }
    }

    /// <summary>
    /// 按形状把一批格子画出来：矩形 / 菱形 / 六边形共用同一段填充 + 描边 + 格内十字逻辑，
    /// 免得三种形状各写一套（原来矩形与菱形就是两套分支，颜色和十字因此走偏）。
    /// 越界格子逐格裁掉（不 PushClip，原因见调用方）。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="cells">格子行列集合。</param>
    /// <param name="cell">格子边长。</param>
    /// <param name="drawShape">绘制形状。</param>
    /// <param name="fill">填充画刷；null = 只描边。</param>
    /// <param name="stroke">描边画笔。</param>
    /// <param name="cross">格内十字画刷；null = 不画十字。</param>
    private void DrawOccupancyLayer(
        DrawingContext context,
        List<(int Col, int Row)> cells,
        double cell,
        string? drawShape,
        IBrush? fill,
        IPen stroke,
        IBrush? cross)
    {
        var config = _config;
        if (config is null)
        {
            return;
        }

        var mapRect = new Rect(0, 0, config.Width, config.Height);
        var crossPen = cross is null ? null : new Pen(cross, Hairline);

        foreach (var (col, row) in cells)
        {
            if (DrawShapes.IsDiamond(drawShape))
            {
                var polygon = BuildPolygon(DiamondLattice.Vertices(col, row, (int)cell));
                if (!polygon.Bounds.Intersects(mapRect))
                {
                    continue;
                }

                context.DrawGeometry(fill, stroke, polygon);
                if (crossPen is not null)
                {
                    // 扁菱形高 half = cell/2：横贯整个 2×cell 宽、上下各半格高，正好把菱形分成四块
                    var center = DiamondLattice.Center(col, row, (int)cell);
                    context.DrawLine(crossPen, new Point(center.X - cell, center.Y), new Point(center.X + cell, center.Y));
                    context.DrawLine(crossPen, new Point(center.X, center.Y - cell / 2.0), new Point(center.X, center.Y + cell / 2.0));
                }

                continue;
            }

            if (DrawShapes.IsHex(drawShape))
            {
                var polygon = BuildPolygon(HexLattice.Vertices(col, row, (int)cell));
                if (polygon.Bounds.Intersects(mapRect))
                {
                    context.DrawGeometry(fill, stroke, polygon);
                }

                continue;
            }

            var rect = new Rect(col * cell, row * cell, cell, cell);
            if (!rect.Intersects(mapRect))
            {
                continue;
            }

            if (fill is not null)
            {
                context.FillRectangle(fill, rect);
            }

            context.DrawRectangle(null, stroke, rect);
            if (crossPen is not null)
            {
                context.DrawLine(crossPen, new Point(rect.X, rect.Center.Y), new Point(rect.Right, rect.Center.Y));
                context.DrawLine(crossPen, new Point(rect.Center.X, rect.Y), new Point(rect.Center.X, rect.Bottom));
            }
        }
    }

    /// <summary>
    /// 旋转后压到的格子集合：矩形按「落位格 + 格数」铺块（与导出 buildCells 同口径），
    /// 菱形按脚底那一排，六边形按格心落在包围盒内取样 —— 三种形状各按自己的格心与步长算，
    /// 共用一套行列数学会把六边形最下一排整排漏掉。
    /// </summary>
    /// <param name="item">实例。</param>
    /// <param name="cell">格子边长（像素）。</param>
    /// <param name="drawShape">绘制形状（矩形 / 菱形 / 六边形）。</param>
    /// <returns>格子行列集合（X = 列，Y = 行）。</returns>
    private List<(int Col, int Row)> OccupiedCells(PlacedObject item, double cell, string? drawShape)
    {
        var cells = new List<(int, int)>();
        var bounds = item.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return cells;
        }

        var quad = RotatedCorners(bounds, EffectiveRotationOf(item));

        // 等距地图里的物件是「站在格子上」的：占格取脚底那一排，而不是格心落在包围盒里的所有格。
        // 按格心判定会让一个 32×32 的贴图同时压住上下两条格心线，画出一堆互相交叉的菱形。
        if (DrawShapes.IsDiamond(drawShape))
        {
            return DiamondFootprintCells(quad, bounds, (int)cell);
        }

        // 蜂窝是斜边网格，只能按格心落框判定；矩形按「落位格 + 格数」铺块，和导出完全对齐
        return DrawShapes.IsHex(drawShape)
            ? HexFootprintCells(quad, cell)
            : RectFootprintCells(bounds, cell);
    }

    /// <summary>
    /// 矩形地图的占格：落位格（包围盒左上角所在格）+ 格数（尺寸按格子边长向上取整）。
    /// 与导出 buildCells 的「起点 + 导出格数」同一口径 —— 画布上看到几格，落盘就是几格；
    /// 原来按格心判定，48×48 这类非整格尺寸会少算一格，导出与显示当场对不上。
    /// </summary>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>占用的格子行列。</returns>
    private static List<(int Col, int Row)> RectFootprintCells(Rect bounds, double cell)
    {
        var cells = new List<(int, int)>();
        if (cell <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return cells;
        }

        var cols = Math.Max(1, (int)Math.Ceiling(bounds.Width / cell - 1e-6));
        var rows = Math.Max(1, (int)Math.Ceiling(bounds.Height / cell - 1e-6));

        // 极端尺寸的安全阀：单实例占格超过 8000 直接不画，避免一次重绘卡住界面
        if ((long)cols * rows > 8000)
        {
            return cells;
        }

        var firstCol = (int)Math.Floor(bounds.X / cell);
        var firstRow = (int)Math.Floor(bounds.Y / cell);
        for (var col = firstCol; col < firstCol + cols; col++)
        {
            for (var row = firstRow; row < firstRow + rows; row++)
            {
                cells.Add((col, row));
            }
        }

        return cells;
    }

    /// <summary>
    /// 蜂窝地图的占格：行距只有 0.866×cell、奇数行右移半格，只能按「格心落在包围盒内」取样，
    /// 并按 HexLattice 的步长推开行列范围（用正方格的 step 会漏掉最下一排）。
    /// </summary>
    /// <param name="quad">实例包围盒绕自身中心旋转后的四个角。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>占用的格子行列。</returns>
    private static List<(int Col, int Row)> HexFootprintCells(Point[] quad, double cell)
    {
        var cells = new List<(int, int)>();
        var step = (int)cell;
        if (step <= 0)
        {
            return cells;
        }

        double left = double.MaxValue, top = double.MaxValue;
        double right = double.MinValue, bottom = double.MinValue;
        foreach (var corner in quad)
        {
            left = Math.Min(left, corner.X);
            top = Math.Min(top, corner.Y);
            right = Math.Max(right, corner.X);
            bottom = Math.Max(bottom, corner.Y);
        }

        var rowStep = HexLattice.RowStep(step);
        var minCol = (int)Math.Floor(left / cell);
        // 奇数行右移半格，最右一列要多取一格才不漏
        var maxCol = (int)Math.Floor((right - 1e-6) / cell) + 1;
        var minRow = (int)Math.Floor(top / rowStep);
        var maxRow = (int)Math.Floor((bottom - 1e-6) / rowStep);

        if ((maxCol - minCol + 1) * (maxRow - minRow + 1) > 8000)
        {
            return cells;
        }

        for (var col = minCol; col <= maxCol; col++)
        {
            for (var row = minRow; row <= maxRow; row++)
            {
                if (PointInQuad(HexLattice.Center(col, row, step), quad))
                {
                    cells.Add((col, row));
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// 等距地图的占格：以物件「脚底中点」为锚取它站的那一格，再按物件宽度向左右扩展成排。
    /// 一个格子大小（cell×cell）的贴图正好落在 1 个扁菱形上；更宽的物件按每 2×cell 多占一列。
    /// </summary>
    /// <param name="quad">实例包围盒绕自身中心旋转后的四个角（左上、右上、右下、左下）。</param>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>脚底那一排的格子行列。</returns>
    private static List<(int Col, int Row)> DiamondFootprintCells(Point[] quad, Rect bounds, int cell)
    {
        var cells = new List<(int, int)>();
        if (cell <= 0)
        {
            return cells;
        }

        // 底边（左下 → 右下）的中点就是脚底；旋转后同样取这两点的中点
        var footX = (quad[3].X + quad[2].X) / 2.0;
        var footY = (quad[3].Y + quad[2].Y) / 2.0;

        // 菱形自身高 half = cell/2：把取样点再往下推半个格高，菱形「上顶点」正好落在脚底上，
        // 看起来就是物件站在格子里（和游戏内落位格一致）。直接取脚底点会落到上一行，
        // 行列相位换成奇数行，菱形会横向错开半格宽。
        var anchor = DiamondLattice.IndexAt(footX, footY + cell / 2.0, cell);
        var row = (int)anchor.Y;

        // 一个菱形横向宽 2×cell：物件每多这么宽就多占一列，居中铺开
        var span = Math.Max(1, (int)Math.Round(bounds.Width / (cell * 2.0), MidpointRounding.AwayFromZero));
        var firstCol = (int)anchor.X - (span - 1) / 2;
        for (var i = 0; i < span; i++)
        {
            cells.Add((firstCol + i, row));
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
    /// <param name="inset">四边向内收缩的世界像素数；大于 0 时落在边上或贴边的点判为外部。</param>
    /// <returns>落在内部（按 inset 收缩后）返回 true。</returns>
    private static bool PointInQuad(Point p, Point[] quad, double inset = 0.0)
    {
        var sign = 0;
        for (var i = 0; i < quad.Length; i++)
        {
            var a = quad[i];
            var b = quad[(i + 1) % quad.Length];
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var length = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

            if (Math.Abs(cross) < 1e-6 || (inset > 0 && length > 0 && Math.Abs(cross) / length <= inset))
            {
                // 正好压在边线上：要求严格内部时直接排除，否则按“无关”跳过
                if (inset > 0)
                {
                    return false;
                }

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

    /// <summary>
    /// 绘制空白处点选的格子：按当前绘制形状铺半透明绿色并描边（矩形 / 菱形 / 六边形各用各的几何），
    /// 线宽随缩放保持 1.5 设备像素，缩放很小的时候也能看清点中了哪一格。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
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

        var stroke = new Pen(SelectedCellStrokeBrush, thin, lineCap: PenLineCap.Round);

        // 三种形状都由格行列反算几何，别在这里再写一套比例：
        // 菱形 / 六边形点中的格如果是正方框，看上去就像「点到了旁边那格」。
        if (IsLatticeMode)
        {
            var vertices = IsHexMode
                ? HexLattice.Vertices((int)selected.X, (int)selected.Y, (int)size)
                : DiamondLattice.Vertices((int)selected.X, (int)selected.Y, (int)size);
            context.DrawGeometry(SelectedCellFillBrush, stroke, BuildPolygon(vertices));
            return;
        }

        var rect = new Rect(selected.X * size, selected.Y * size, size, size);
        context.FillRectangle(SelectedCellFillBrush, rect);
        context.DrawRectangle(null, stroke, rect);
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
        var selectedBrush = UiTheme.MapView.PathPointSelected;
        var selectedRing = new Pen(UiTheme.MapView.PathPointSelectedRing, thin * 2.0);

        // 与 Unity 原版一致：只画点与点之间的连线，不从角色中心额外拉一条「伪起点」，
        // 否则第一个路径点会被误读成已经走过的线段。
        Point? previous = null;
        for (var index = 0; index < item.PathPoints.Count; index++)
        {
            var point = item.PathPoints[index];
            var current = new Point(point.X, point.Y);
            if (previous is { } from)
            {
                context.DrawLine(pen, from, current);
            }

            if (index == _selectedPathPoint)
            {
                // 选中点画大一号 + 白圈 + 序号：列表行和画布上的点必须一眼能对上
                var half = thin * 6;
                var rect = new Rect(current.X - half, current.Y - half, half * 2, half * 2);
                context.FillRectangle(selectedBrush, rect);
                context.DrawRectangle(null, selectedRing, rect);

                var formatted = CanvasText.Create($"P{index}", 10.0 * thin, selectedBrush);
                context.DrawText(
                    formatted,
                    new Point(current.X + half + thin * 2, current.Y - half - formatted.Height));
            }
            else
            {
                context.FillRectangle(
                    markerBrush,
                    new Rect(current.X - thin * 4, current.Y - thin * 4, thin * 8, thin * 8));
            }

            previous = current;
        }
    }

    #endregion
}





