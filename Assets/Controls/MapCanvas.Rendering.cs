/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Rendering.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布绘制分区：底图、网格、精灵、选中框、路径与交通标记的可视化。
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

public sealed partial class MapCanvas : Control
{
    #region 绘制

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 画布性能采样（只读诊断，不影响正常绘制）：MAP_CANVAS_PERF=1 时每 30 帧往
        // output\Logs\canvas-perf.log 追加一行平均耗时，用来定位平移卡顿出在哪一段
        if (!RenderProfileEnabled)
        {
            RenderCore(context);
            return;
        }

        var watch = Stopwatch.StartNew();
        RenderCore(context);
        RecordRenderCost(watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>平移时把重绘合并到下一轮调度：同一轮里的多条 move 只画最后一帧位置。</summary>
    private void RequestPanRepaint()
    {
        if (_panRepaintScheduled)
        {
            return;
        }

        _panRepaintScheduled = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _panRepaintScheduled = false;
                InvalidateVisual();
            },
            DispatcherPriority.Render);
    }

    /// <summary>记录一帧绘制耗时，每 30 帧写一行到 <c>output\Logs\canvas-perf.log</c>。</summary>
    /// <param name="milliseconds">本帧绘制耗时（毫秒）。</param>
    private void RecordRenderCost(double milliseconds)
    {
        _renderCostTotal += milliseconds;
        _renderCostFrames++;
        if (_renderCostFrames < 30)
        {
            return;
        }

        var average = _renderCostTotal / _renderCostFrames;
        _renderCostTotal = 0;
        _renderCostFrames = 0;

        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "output", "Logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "canvas-perf.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  avg {average:0.0}ms/frame  objects {_config?.Objects.Count ?? 0}  zoom {_zoom:0.###}  panning {_isPanning}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 采样日志失败不影响绘制
        }
    }

    /// <summary>真正的绘制流程（平移 / 缩放 / 选中 / 覆盖层都在这里按顺序画）。</summary>
    /// <param name="context">Avalonia 绘制上下文。</param>
    private void RenderCore(DrawingContext context)
    {
        var client = new Rect(Bounds.Size);
        context.FillRectangle(BackdropBrush, client);

        var config = _config;
        if (config is null)
        {
            return;
        }

        // 世界坐标统一走缩放+平移矩阵，所有图元只需写世界坐标
        using (context.PushTransform(ViewMatrix))
        {
            context.FillRectangle(
                MapBackgroundBrush,
                new Rect(0, 0, config.Width, config.Height));

            DrawMapLayer(context, config);

            // 排序只算一次，顺便把「地表带 / 其余带」一次分好：
            // 之前地表 / 非地表各排一遍 + 各判一次带号，572 个物件等于每帧多做两趟
            // 顺序口径：遮挡带 → 立体带脚底 Y → 八层层序兜底（见 LayerOrder.Compare）
            var ordered = OrderedObjects(config).ToList();
            var groundItems = new List<PlacedObject>(ordered.Count);
            var upperItems = new List<PlacedObject>(ordered.Count);
            foreach (var item in ordered)
            {
                if (LayerBands.Of(item) == LayerBands.Ground)
                {
                    groundItems.Add(item);
                }
                else
                {
                    upperItems.Add(item);
                }
            }

            // 选中实例的占格：和贴图一起放进同一个地图裁剪层，先占格后贴图，
            // 贴图才能正常盖住占格；不能画在 PushClip 外面，否则 Avalonia 会把
            // 裁剪层整体合成到占格之上，选中绿化时整块变暗（看起来像“绿化层变色”）。
            // 裁剪本身由「裁剪越界」开关控制：关掉后整张越界图完整显示，方便检查边缘出血。
            var mapClip = new Rect(0, 0, config.Width, config.Height);
            if (ShowClipOutsideMap)
            {
                using (context.PushClip(mapClip))
                {
                    DrawMapArt(context, config, groundItems, upperItems);
                }
            }
            else
            {
                DrawMapArt(context, config, groundItems, upperItems);
            }

            // 摆放网格绘制在图元之上，保证任何背景都不会遮住格子
            if (ShowGrid && !PreviewMode)
            {
                MapGridRenderer.DrawPlacementGrid(context, config, client, _zoom, ScreenToWorld);
            }

            // 未选中旋转图元的淡占格：保持原样浮在图元之上，仅作提示；选中实例的实占格已在所有贴图之前画过
            // 拖动平移时跳过：这批占格要遍历全部物件逐个算格子，是平移卡顿的大头，松手后重新画
            if (!_isPanning && !PreviewMode)
            {
                DrawIdleOccupancy(context);
            }

            if (ShowPathGrid && !PreviewMode)
            {
                MapGridRenderer.DrawPathGrid(context, config, client, _zoom, ScreenToWorld);
            }

            // 预览模式：在美术之上轻量描出所有角色路径与当前脚底位置，便于核对移动轨迹
            if (PreviewMode && PreviewShowPaths)
            {
                DrawPreviewPaths(context, config);
            }

            if (!PreviewMode)
            {
                DrawEditingPath(context);
                DrawFog(context, config);
                DrawFogBrush(context);
                DrawSelection(context);
                DrawSelectedCell(context);
            }

            // 地图外框：始终 1 设备像素宽，让画布边界在青色底上仍清晰可见
            context.DrawRectangle(
                new Pen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), 1.0 / Math.Max(_zoom, 0.0001)),
                new Rect(0, 0, config.Width, config.Height));
        }

        // 「包围盒」：整张地图的外接矩形，纯绿实线 1 设备像素（Unity 场景工具同款）
        if (ShowBoundingBox)
        {
            DrawBoundaryBox(context, config);
        }

        if (_marqueeRect is { } marquee)
        {
            context.FillRectangle(MarqueeBrush, marquee);
            context.DrawRectangle(new Pen(AccentBrush, 1.0), marquee);
        }
    }

    /// <summary>
    /// 按已分好的地表/立体带绘制地图美术，并把选中实例的占格沉在贴图下面。
    /// 这个方法是「裁剪越界」开关的两个分支共用的：是否加地图裁剪只在调用处决定，
    /// 图元内容、顺序和占格层级都保持一致。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    /// <param name="groundItems">地表带实例。</param>
    /// <param name="upperItems">立体带 + 叠加带实例。</param>
    private void DrawMapArt(
        DrawingContext context,
        MapConfigData config,
        IReadOnlyList<PlacedObject> groundItems,
        IReadOnlyList<PlacedObject> upperItems)
    {
        DrawSelectedOccupancy(context);

        foreach (var item in groundItems)
        {
            DrawObject(context, item);
        }

        if (!PreviewMode)
        {
            DrawTrafficGround(context, config);
        }

        foreach (var item in upperItems)
        {
            DrawObject(context, item);
        }

        if (!PreviewMode)
        {
            DrawTrafficMarkers(context, config);
        }
    }

    /// <summary>
    /// 画整张地图的包围盒：在屏幕空间给地图外接矩形描一圈纯绿实线。
    /// 四条边各自画成 1 设备像素的实心条并关闭抗锯齿，缩放到任何比例都是同一条细线。
    /// </summary>
    /// <param name="context">当前绘制上下文。</param>
    /// <param name="config">地图配置，用它的宽高作为外接矩形。</param>
    private void DrawBoundaryBox(DrawingContext context, MapConfigData config)
    {
        var topLeft = ViewMatrix.Transform(new Point(0, 0));
        var bottomRight = ViewMatrix.Transform(new Point(config.Width, config.Height));
        var box = new Rect(topLeft, bottomRight);

        // 边线取整到设备像素再落笔，四条边各画 1 像素实心条，避免线宽被缩放吸收或与底色混色
        var x = Math.Round(box.X);
        var y = Math.Round(box.Y);
        var right = Math.Round(box.Right);
        var bottom = Math.Round(box.Bottom);
        var width = Math.Max(right - x, 1);
        var height = Math.Max(bottom - y, 1);

        using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
        {
            context.FillRectangle(BoundaryBoxBrush, new Rect(x, y, width, 1));
            context.FillRectangle(BoundaryBoxBrush, new Rect(x, bottom - 1, width, 1));
            context.FillRectangle(BoundaryBoxBrush, new Rect(x, y, 1, height));
            context.FillRectangle(BoundaryBoxBrush, new Rect(right - 1, y, 1, height));
        }
    }

    /// <summary>资源库：<c>image</c> 字段靠它解析成真实位图，未设置时画布退回色块。</summary>
    public AssetLibrary? Assets
    {
        get => _assets;
        set
        {
            _assets = value;
            InvalidateVisual();
        }
    }

    private IEnumerable<PlacedObject> OrderedObjects(MapConfigData config)
    {
        // 遮挡关系统一走 LayerBands：地表带在最底，立体带跨图层按脚底 Y，叠加带永远最上。
        // 显隐跟随「有效显示层」而不是归属层：手改过显示层后，顶部图层勾选与画面看到的一致。
        // 收集物单独走 collectible 显隐键，避免和普通道具共用「道具」开关。
        return LayerBands.Sort(config.Objects.Where(item =>
            !HiddenLayers.Contains(VisibilityLayer(item))
            && !HiddenTemplateIds.Contains(item.TemplateId)));
    }

    /// <summary>顶部图层勾选使用的显隐键：收集物独立成组，其余跟随有效显示层。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>显隐层键。</returns>
    private static string VisibilityLayer(PlacedObject item) =>
        item.IsCollectible ? MapLayerKeys.Collectible : LayerOrder.EffectiveLayer(item);

    /// <summary>渲染底图层占位（Unity 版为 bgName 对应的底图 Sprite，这里只画底色与名称）。</summary>
    private void DrawMapLayer(DrawingContext context, MapConfigData config)
    {
        if (!ShowMapLayer)
        {
            return;
        }

        var rect = new Rect(0, 0, config.Width, config.Height);

        // bgName 指向 Unity 工程内的底图（如 bg_1.png），命中就按地图尺寸铺满；
        // 导入 PSD 后还没点「导出切片」时，底图来自内存合成结果（_runtimeBackground）
        var background = _runtimeBackground ?? _assets?.Load(config.BgName);
        if (background is not null)
        {
            context.DrawImage(
                background,
                new Rect(0, 0, background.PixelSize.Width, background.PixelSize.Height),
                rect);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(MapLayerBrushColor), rect);
        }

        if (_zoom < 0.16 || string.IsNullOrWhiteSpace(config.BgName))
        {
            return;
        }

        var formatted = new FormattedText(
            $"底图：{config.BgName}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            28.0 / _zoom,
            LabelBrush);

        context.DrawText(formatted, new Point(32, 32));
    }

    /// <summary>
    /// 人行道 / 道路格子的外轮廓（绘制在图元之下）。只描整块区域的外边界、不填充底色，
    /// 既不遮挡底图与美术贴图，又能一眼看出刷格范围；菱形模式按 TileMap 口径描菱形台阶边。
    /// </summary>
    private void DrawTrafficGround(DrawingContext context, MapConfigData config)
    {
        var cell = (double)config.GridSize;
        var thin = Math.Max(1.2 / Math.Max(_zoom, 0.0001), 0.8);
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
        var crosswalkPen = new Pen(UiTheme.MapView.CrosswalkEdge, 2.0 / _zoom);

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
            var radius = Math.Max(cell / 3.0, 6.0 / _zoom);

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
                    new Pen(UiTheme.MapView.TrafficGuide, 1.0 / _zoom),
                    center,
                    FindCrosswalkCenter(config, light.BoundCrosswalkId));
            }
        }

        // 选中实例的停止线
        if (_selected is { } selected && (selected.StopLineX > 0 || selected.StopLineY > 0))
        {
            var stop = new Point(selected.StopLineX, selected.StopLineY);
            var stopPen = new Pen(UiTheme.MapView.StopLine, 2.0 / _zoom);
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
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x07, 0x0C, 0x14));
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

        var radius = Math.Max(FogBrushSize, 8) / 2.0;
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xD2, 0x4A)),
            new Pen(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xD2, 0x4A)), 1.6 / _zoom),
            hover,
            radius,
            radius);
    }

    /// <summary>判断实例是否参与预览路径演示：至少有两个路径点。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>可沿路径移动时为 true。</returns>
    private static bool HasPreviewPath(PlacedObject item) => item.PathPoints.Count >= 2;

    /// <summary>
    /// 预览中的绘制包围盒。带路径的实例按当前预览时间把脚底插值到路径上，
    /// 再把包围盒整体平移过去；没有路径或关闭路径演示时原样返回落盘坐标。
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>用于绘制的世界矩形。</returns>
    private Rect PreviewRenderBounds(PlacedObject item)
    {
        var bounds = item.Bounds;
        if (!PreviewMode || !PreviewShowPaths || !HasPreviewPath(item))
        {
            return bounds;
        }

        var foot = PreviewFootPoint(item);
        return foot is null
            ? bounds
            : new Rect(foot.Value.X - bounds.Width * 0.5, foot.Value.Y - bounds.Height, bounds.Width, bounds.Height);
    }

    /// <summary>按预览时钟计算实例当前脚底位置：路径点作为脚底锚点，支持循环 / 往返 / 单程。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>脚底世界坐标；路径无效时返回 null。</returns>
    private Point? PreviewFootPoint(PlacedObject item)
    {
        if (!HasPreviewPath(item))
        {
            return null;
        }

        var total = PathMath.Length(item.PathPoints);
        if (total <= 0)
        {
            return null;
        }

        var speed = PreviewWalkPixelsPerSecond * Math.Max(0.01, item.Speed);
        var walked = PathMath.NormalizeDistance(
            Math.Max(0, _previewElapsedSeconds) * speed,
            total,
            PathMath.ParseWalkMode(item.WalkMode));
        return PathMath.PointAtDistance(item.PathPoints, walked);
    }

    /// <summary>预览中描出所有角色路径、路径点序号，以及角色当前脚底位置。</summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    private void DrawPreviewPaths(DrawingContext context, MapConfigData config)
    {
        var thin = 1.0 / Math.Max(_zoom, 0.0001);
        var pathShadowPen = new Pen(UiTheme.MapView.PathShadow, 5.4 * thin);
        var pathPen = new Pen(UiTheme.MapView.Path, 2.2 * thin);
        var pointBrush = UiTheme.MapView.PathPointFill;
        var pointPen = new Pen(UiTheme.MapView.PathPointBorder, 1.0 * thin);
        var markerBrush = UiTheme.MapView.PathMarker;
        var markerPen = new Pen(UiTheme.MapView.PathMarkerBorder, 1.5 * thin);
        var labelBrush = UiTheme.White;
        var pointRadius = 4.0 * thin;
        var markerRadius = 7.0 * thin;

        foreach (var item in config.Objects.Where(HasPreviewPath))
        {
            Point? previous = null;
            var index = 0;
            foreach (var pathPoint in item.PathPoints)
            {
                var current = new Point(pathPoint.X, pathPoint.Y);
                if (previous is { } from)
                {
                    context.DrawLine(pathShadowPen, from, current);
                    context.DrawLine(pathPen, from, current);
                }

                var dot = new Rect(current.X - pointRadius, current.Y - pointRadius, pointRadius * 2, pointRadius * 2);
                context.DrawEllipse(pointBrush, pointPen, dot.Center, pointRadius, pointRadius);

                var label = new FormattedText(
                    $"P{index}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11 * thin,
                    labelBrush);
                context.DrawText(label, new Point(current.X + 6 * thin, current.Y - 17 * thin));
                previous = current;
                index++;
            }

            var foot = PreviewFootPoint(item);
            if (foot is { } marker)
            {
                var dot = new Rect(marker.X - markerRadius, marker.Y - markerRadius, markerRadius * 2, markerRadius * 2);
                context.DrawEllipse(markerBrush, markerPen, dot.Center, markerRadius, markerRadius);
            }
        }
    }

    /// <summary>取实例贴图：优先 image 字段，其次模板 ResName，找不到返回 null。</summary>
    private Bitmap? Sprite(PlacedObject item)
    {
        // 导入 PSD 后切片只在内存里：优先用预览位图，落盘后由 ClearRuntimeSprites 切回磁盘解析
        if (_runtimeSprites.TryGetValue(item.Id, out var runtimeSprite))
        {
            return runtimeSprite;
        }

        if (_assets is null)
        {
            return null;
        }

        // 同一个物件（同 id / 同路径 / 同名字 / 同层 / 同章节关卡）只解析一次：
        // 拖动时每帧都会重画 500+ 物件，之前每次都要走 File.Exists + 三级目录查找，帧率被磁盘拖死
        var key = string.Concat(
            item.Id, "|", item.ImagePath, "|", item.Name, "|", item.LayerType, "|", item.DisplayLayer,
            "|", _config?.ChapterId, "|", _config?.SceneId);
        if (_spriteCache.TryGetValue(key, out var cachedSprite))
        {
            return cachedSprite;
        }

        Bitmap? sprite = null;
        if (!string.IsNullOrWhiteSpace(item.ImagePath))
        {
            sprite = _assets.Load(item.ImagePath);
        }

        if (sprite is null && !string.IsNullOrWhiteSpace(item.Name))
        {
            // Unity 口径兜底：按「资源名 + 层键 + 章节 / 关卡」三级目录查找（本关 → 章节 Shared → 全局 Shared）。
            // 资源名统一去扩展名，避免 JSON 里写了 bg_rock_1.png 时拼成 bg_rock_1.png.png。
            var stem = Path.GetFileNameWithoutExtension(item.Name);
            sprite = _assets.LoadByLayer(stem, item.LayerType, _config?.ChapterId, _config?.SceneId)
                ?? _assets.Load(stem)
                ?? _assets.Load(item.Name);
        }

        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>清空贴图解析缓存（导入烘焙、资源根变更后调用，避免新落盘的图被旧 null 结果挡住）。</summary>
    public void ClearSpriteCache() => _spriteCache.Clear();

    /// <summary>
    /// 动画目录内容变化后调用：清空帧缓存并重新判断是否需要启动 30 FPS 检查。
    /// 导入 / 删除动画片段、切换关卡后都会走这里。
    /// </summary>
    public void RefreshAnimations()
    {
        _animationFrameCache.Clear();
        _lastAnimationSignature = long.MinValue;
        EnsureAnimationTimer();
        InvalidateVisual();
    }

    /// <summary>按当前目录与地图实例决定是否启动动画 / 预览检查定时器。</summary>
    private void EnsureAnimationTimer()
    {
        if (_animationTimer is null)
        {
            return;
        }

        var hasSpriteAnimation = _animations is not null
            && _config is not null
            && PreviewShowAnimations
            && _config.Objects.Any(_animations.IsAnimated);
        var hasSpineAnimation = _spineAnimations is not null
            && _config is not null
            && PreviewShowAnimations
            && _config.Objects.Any(IsSpineAnimated);
        var hasPreviewPath = PreviewMode
            && PreviewShowPaths
            && _config is not null
            && _config.Objects.Any(HasPreviewPath);
        var hasAnimation = hasSpriteAnimation || hasSpineAnimation || hasPreviewPath;
        if (hasAnimation && !_animationTimer.IsEnabled)
        {
            _lastSpineTick = CurrentAnimationTime;
            _lastPreviewClockSeconds = _previewClock.Elapsed.TotalSeconds;
            _animationTimer.Start();
        }
        else if (!hasAnimation && _animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
        }
    }

    /// <summary>当前动画时间：编辑器用真实时钟，预览模式用可暂停 / 调速的预览时钟。</summary>
    private double CurrentAnimationTime => _previewMode
        ? _previewElapsedSeconds
        : _animationClock.Elapsed.TotalSeconds;

    /// <summary>判断实例当前是否应走 Spine 绘制。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>有可用资产并启用动画时为 true。</returns>
    private bool IsSpineAnimated(PlacedObject item)
    {
        return PreviewShowAnimations
            && item.AnimEnabled
            && string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            && _spineAnimations?.Find(item.SpineAsset) is not null;
    }

    /// <summary>
    /// 定时器 tick：只比较「当前帧号签名」，签名变化才触发整幅重绘。
    /// 预览模式下同时推进预览时钟，角色沿路径移动也靠这里重绘。
    /// 30 FPS 检查本身不画图，静态地图或同一帧内不会产生任何绘制开销。
    /// </summary>
    private void TickAnimations()
    {
        if (_config is null)
        {
            return;
        }

        var previewMoved = UpdatePreviewClock();
        var hasSpine = UpdateSpineAnimations();
        if (_animations is null || _animations.Count == 0)
        {
            if (hasSpine || previewMoved)
            {
                InvalidateVisual();
            }

            return;
        }

        var signature = BuildAnimationSignature();
        if (signature != _lastAnimationSignature || hasSpine || previewMoved)
        {
            _lastAnimationSignature = signature;
            InvalidateVisual();
        }
    }

    /// <summary>按真实时间推进预览时钟；暂停时不前进，返回是否有需要重绘的状态变化。</summary>
    /// <returns>预览正在播放且时间前进时为 true。</returns>
    private bool UpdatePreviewClock()
    {
        if (!_previewMode)
        {
            return false;
        }

        var now = _previewClock.Elapsed.TotalSeconds;
        var delta = Math.Max(0, now - _lastPreviewClockSeconds);
        _lastPreviewClockSeconds = now;
        if (_previewPaused || delta <= 0)
        {
            return false;
        }

        // 切窗口回来可能攒出很大的间隔，单帧步长限到 0.1s，避免角色瞬移
        _previewElapsedSeconds += Math.Min(delta, 0.1) * _previewPlaybackSpeed;
        return true;
    }

    /// <summary>按真实时间推进所有 Spine 实例；返回当前是否存在需要重绘的实例。</summary>
    /// <returns>至少一个 Spine 实例在播放时为 true。</returns>
    private bool UpdateSpineAnimations()
    {
        if (_spineAnimations is null || _config is null)
        {
            return false;
        }

        var now = CurrentAnimationTime;
        var delta = now - _lastSpineTick;
        _lastSpineTick = now;
        if (delta <= 0)
        {
            return false;
        }

        // 切窗口 / 断点回来可能攒出几秒，限制单帧步长避免骨骼瞬移
        delta = Math.Min(delta, 0.1);
        var count = 0;
        foreach (var item in _config.Objects)
        {
            if (!IsSpineAnimated(item))
            {
                continue;
            }

            var player = GetSpinePlayer(item);
            if (player is null)
            {
                continue;
            }

            player.EnsureAnimation(item.SpineAnimation, item.Loop);
            player.Update(delta, Math.Max(0.01, item.Speed));
            count++;
        }

        return count > 0;
    }

    /// <summary>按实例 ID 取或创建 Spine 播放器。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>播放器；资产未命中返回 null。</returns>
    private SpinePlayer? GetSpinePlayer(PlacedObject item)
    {
        var asset = _spineAnimations?.Find(item.SpineAsset);
        if (asset == null)
        {
            return null;
        }

        if (_spinePlayers.TryGetValue(item.Id, out var player))
        {
            return player;
        }

        player = asset.CreatePlayer();
        _spinePlayers[item.Id] = player;
        return player;
    }

    /// <summary>把所有启用动画实例的当前帧号折成一个 64 位签名。</summary>
    /// <returns>没有可播放动画时为 long.MinValue；否则为稳定的帧号哈希。</returns>
    private long BuildAnimationSignature()
    {
        if (_animations is null || _config is null)
        {
            return long.MinValue;
        }

        var elapsed = CurrentAnimationTime;
        var signature = 1469598103934665603L;
        var animated = 0;
        foreach (var item in _config.Objects)
        {
            var clip = ResolveClip(item);
            if (clip is null)
            {
                continue;
            }

            var frame = SpriteAnimationCatalog.FrameIndex(
                clip,
                elapsed,
                Math.Max(0.01, item.Speed),
                item.Loop);
            signature = unchecked(signature * 31 + item.Id.GetHashCode() * 17L + frame);
            animated++;
        }

        return animated == 0 ? long.MinValue : signature;
    }

    /// <summary>按实例的 AnimEnabled + AnimName 解析可用片段。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>片段；未启用 / 未配置 / 缺帧时返回 null。</param>
    private SpriteAnimationClip? ResolveClip(PlacedObject item)
    {
        return PreviewShowAnimations && item.AnimEnabled && _animations?.Find(item.AnimName) is { } clip
            ? clip
            : null;
    }

    /// <summary>取实例当前帧位图；帧路径解析失败时返回 null，由调用方回退静态贴图。</summary>
    /// <param name="item">地图实例。</param>
    /// <param name="clip">已命中的动画片段。</param>
    /// <returns>当前帧位图。</returns>
    private Bitmap? AnimatedSprite(PlacedObject item, SpriteAnimationClip clip)
    {
        if (_assets is null || clip.Frames.Count == 0)
        {
            return null;
        }

        var frame = SpriteAnimationCatalog.FrameIndex(
            clip,
            CurrentAnimationTime,
            Math.Max(0.01, item.Speed),
            item.Loop);
        var path = clip.Frames[Math.Clamp(frame, 0, clip.Frames.Count - 1)];
        var key = string.Concat(clip.Name, "|", frame, "|", path);
        if (_animationFrameCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitmap = _assets.Load(path);
        _animationFrameCache[key] = bitmap;
        return bitmap;
    }

    /// <summary>清空序列帧缓存（动画目录变更 / 切关卡 / 资源根变更后调用）。</summary>
    public void ClearAnimationCache() => _animationFrameCache.Clear();

    /// <summary>登记一张内存贴图（导入 PSD 时用，磁盘上没有对应 PNG）。</summary>
    /// <param name="objectId">物件 ID。</param>
    /// <param name="bitmap">已解码的位图。</param>
    public void SetRuntimeSprite(string objectId, Bitmap bitmap)
    {
        _runtimeSprites[objectId] = bitmap;
        _spriteCache.Clear();
    }

    /// <summary>换掉整幅内存底图（导入 PSD 后立即预览合成底图，null 表示回到磁盘 / 底色）。</summary>
    /// <param name="bitmap">合成底图。</param>
    public void SetRuntimeBackground(Bitmap? bitmap)
    {
        _runtimeBackground = bitmap;
        InvalidateVisual();
    }

    /// <summary>清空内存贴图（点「导出切片」落盘后调用，画布改走磁盘资源）。</summary>
    public void ClearRuntimeSprites()
    {
        _runtimeSprites.Clear();
        _runtimeBackground = null;
        _spriteCache.Clear();
    }

    private void DrawObject(DrawingContext context, PlacedObject item)
    {
        // 预览中的角色若带路径，按预览时钟把脚底插值到路径上；编辑器里始终用落盘坐标
        var bounds = PreviewRenderBounds(item);

        // 占位色 / 背景特殊绘制都跟随「有效显示层」：手改到背景层的图元按背景画法走，
        // 顶部图层勾选时也能和画面一致（显示层优先，未设置才回落到归属层）。
        var displayLayer = item.IsCollectible ? MapLayerKeys.Collectible : LayerOrder.EffectiveLayer(item);
        var color = ParseColor(MapLayerKeys.Hex(displayLayer));

        // Spine 命中时完全替换静态贴图，避免骨骼与旧 PNG 叠加；未命中仍回退静态贴图。
        var spineAsset = IsSpineAnimated(item) ? _spineAnimations?.Find(item.SpineAsset) : null;
        var spinePlayer = spineAsset is null ? null : GetSpinePlayer(item);
        var staticSprite = spinePlayer is null ? Sprite(item) : null;
        var animation = spinePlayer is null ? ResolveClip(item) : null;
        var sprite = spinePlayer is not null
            ? null
            : animation is not null ? AnimatedSprite(item, animation) ?? staticSprite : staticSprite;
        var pixelSize = staticSprite is not null
            ? new Size(staticSprite.PixelSize.Width, staticSprite.PixelSize.Height)
            : sprite is null
                ? new Size(0, 0)
                : new Size(sprite.PixelSize.Width, sprite.PixelSize.Height);
        var tiled = sprite is not null && SceneDisplayAudit.ShouldTileGround(item, pixelSize);

        // 九宫格只对「勾选是否缩放」的实例生效；平铺地块、缺贴图、边距越界都自动退回普通绘制。
        var nineSlice = sprite is not null && item.Stretch && !tiled
            ? NineSlices?.Find(item.TemplateId, item.ImagePath, item.Name)
            : null;
        var useNineSlice = nineSlice is not null && nineSlice.IsUsable(pixelSize.Width, pixelSize.Height);

        // 背景层是整幅底图/地块，画半透明色块与虚线框，避免压住其他图元与网格。
        // 用手改后的有效显示层判断：把道具层图元改成背景层后，也应该走背景画法。
        if (displayLayer == MapLayerKeys.Background)
        {
            using (context.PushTransform(SpriteLayout.ContentTransform(item.Rotation, item.FlipX, bounds)))
            {
                if (spinePlayer is not null && spineAsset is not null)
                {
                    SpineRenderer.Draw(context, spinePlayer, spineAsset, bounds, 0, false);
                }
                else if (sprite is not null)
                {
                    if (tiled)
                    {
                        DrawTiled(context, sprite, bounds);
                    }
                    else
                    {
                        context.DrawImage(
                            sprite,
                            new Rect(0, 0, sprite.PixelSize.Width, sprite.PixelSize.Height),
                            bounds);
                    }
                }
                else
                {
                    // 缺贴图的背景地块（Unity 侧烘焙进合成底图的那批）：色块稍重一点，
                    // 好让「这块地有美术、但我们没像素」一眼能看出来，又不是实心地块
                    context.FillRectangle(
                        new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)),
                        bounds);
                }

                if (ShowImageBorder && !PreviewMode)
                {
                    context.DrawRectangle(
                        null,
                        new Pen(new SolidColorBrush(Color.FromArgb(0x80, color.R, color.G, color.B)), 1.5 / _zoom, DashStyle.Dash),
                        bounds);
                }
            }

            return;
        }

        var fill = new SolidColorBrush(Color.FromArgb(0xD0, color.R, color.G, color.B));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x00, 0x00, 0x00)), 1.0 / _zoom);
        var band = LayerBands.Of(item);

        // 「是否缩放」勾选时贴图按包围盒铺满；取消勾选则等比缩放到包围盒内（contain），
        // 与 Unity 场景工具的 GUI.DrawTexture(ScaleMode.ScaleToFit) 一致：不溢出、不变形。
        // 旋转 90/270 复用的切片（宽高与物件框互换）先按「交换后的框」contain，再交给旋转矩阵摆正
        var drawBounds = sprite is not null && item.Stretch is false
            ? SpriteLayout.FitForRotation(bounds, pixelSize, item.Rotation)
            : bounds;

        // 2D 立体物的脚底必须踩在包围盒底边：贴图比占格小时只做水平居中、底部对齐，
        // 不能上下居中（否则树 / 人物会浮在半空，和脚底 Y 的遮挡排序对不上）。
        // 旋转过的图元交给旋转矩阵，不再二次偏移，避免斜放物件被拉离原位。
        if (sprite is not null
            && band == LayerBands.Standing
            && Math.Abs(item.Rotation) < 0.01
            && drawBounds.Height < bounds.Height - 0.5)
        {
            drawBounds = new Rect(
                drawBounds.X,
                bounds.Bottom - drawBounds.Height,
                drawBounds.Width,
                drawBounds.Height);
        }

        // 有贴图与没贴图（色块占位）都要吃旋转与水平镜像，保证改角度后画布上有明确反馈
        using (context.PushTransform(SpriteLayout.ContentTransform(item.Rotation, item.FlipX, bounds)))
        {
            if (spinePlayer is not null && spineAsset is not null)
            {
                SpineRenderer.Draw(context, spinePlayer, spineAsset, bounds, 0, false);
            }
            else if (sprite is not null)
            {
                if (tiled)
                {
                    DrawTiled(context, sprite, bounds);
                }
                else if (useNineSlice)
                {
                    DrawNineSlice(context, sprite, drawBounds, nineSlice!);
                }
                else
                {
                    context.DrawImage(
                        sprite,
                        new Rect(0, 0, sprite.PixelSize.Width, sprite.PixelSize.Height),
                        drawBounds);
                }
            }
            else
            {
                // 没有贴图时退回图层色块，占格与编辑仍然可见
                context.FillRectangle(fill, bounds);
            }

            if (ShowImageBorder && !PreviewMode)
            {
                context.DrawRectangle(null, borderPen, sprite is null || tiled ? bounds : drawBounds);
            }
        }

        // 图片外框与占格描边受 DrawBorder 开关控制：关掉后只剩贴图本身，方便比对美术效果
        // 拖动平移时也跳过：每帧 500+ 条 Pen 描边纯属无谓开销，松手后补画
        if (ShowImageBorder && !_isPanning && !PreviewMode)
        {
            // 未旋转的图元：一条轴对齐格足迹描边，便于核对占格；旋转图元的格子级占格见 DrawSelectedOccupancy
            var cell = MapToolDefaults.GridSize;
            var footprintPen = new Pen(UiTheme.MapView.Footprint, 1.0 / _zoom);

            if (Math.Abs(item.Rotation) < 0.01)
            {
                context.DrawRectangle(
                    null,
                    footprintPen,
                    new Rect(item.X, item.Y, item.ExportWidth * cell, item.ExportHeight * cell));
            }
        }

        if (_zoom >= 0.45)
        {
            DrawLabel(context, item.Name, bounds);
        }
    }

    /// <summary>
    /// 未勾选「是否缩放」时贴图的绘制矩形：等比缩放到包围盒内居中（contain），
    /// 与 Unity 场景工具的 <c>ScaleMode.ScaleToFit</c> 口径一致——宽高比不失真、绝不溢出包围盒。
    /// </summary>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="pixelSize">贴图原始像素尺寸。</param>
    /// <param name="degrees">实例旋转角度（度）。</param>
    /// <returns>绘制矩形。</returns>
    /// <summary>
    /// 按九宫格把贴图铺到目标矩形：四角固定为素材原始像素尺寸，四条边只沿单轴拉伸，
    /// 中心按 <see cref="NineSliceSettings.CenterMode"/> 拉伸或按原始尺寸平铺。
    /// 目标比两侧边距之和还小时退回整图拉伸，避免负尺寸。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="sprite">贴图。</param>
    /// <param name="bounds">目标矩形（世界坐标）。</param>
    /// <param name="settings">九宫格配置。</param>
    private static void DrawNineSlice(
        DrawingContext context,
        Bitmap sprite,
        Rect bounds,
        NineSliceSettings settings)
    {
        var sourceWidth = sprite.PixelSize.Width;
        var sourceHeight = sprite.PixelSize.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        var left = Math.Clamp(settings.Left, 0, sourceWidth - 1);
        var right = Math.Clamp(settings.Right, 0, sourceWidth - 1 - left);
        var top = Math.Clamp(settings.Top, 0, sourceHeight - 1);
        var bottom = Math.Clamp(settings.Bottom, 0, sourceHeight - 1 - top);
        var centerWidth = sourceWidth - left - right;
        var centerHeight = sourceHeight - top - bottom;
        if (centerWidth <= 0 || centerHeight <= 0 || bounds.Width <= left + right || bounds.Height <= top + bottom)
        {
            context.DrawImage(
                sprite,
                new Rect(0, 0, sourceWidth, sourceHeight),
                bounds);
            return;
        }

        var rightEdgeX = bounds.Right - right;
        var bottomEdgeY = bounds.Bottom - bottom;

        // 四角：只平移，不缩放
        context.DrawImage(sprite, new Rect(0, 0, left, top), new Rect(bounds.X, bounds.Y, left, top));
        context.DrawImage(sprite, new Rect(sourceWidth - right, 0, right, top),
            new Rect(rightEdgeX, bounds.Y, right, top));
        context.DrawImage(sprite, new Rect(0, sourceHeight - bottom, left, bottom),
            new Rect(bounds.X, bottomEdgeY, left, bottom));
        context.DrawImage(sprite, new Rect(sourceWidth - right, sourceHeight - bottom, right, bottom),
            new Rect(rightEdgeX, bottomEdgeY, right, bottom));

        // 四条边：单轴拉伸
        context.DrawImage(
            sprite,
            new Rect(left, 0, centerWidth, top),
            new Rect(bounds.X + left, bounds.Y, bounds.Width - left - right, top));
        context.DrawImage(
            sprite,
            new Rect(left, sourceHeight - bottom, centerWidth, bottom),
            new Rect(bounds.X + left, bottomEdgeY, bounds.Width - left - right, bottom));
        context.DrawImage(
            sprite,
            new Rect(0, top, left, centerHeight),
            new Rect(bounds.X, bounds.Y + top, left, bounds.Height - top - bottom));
        context.DrawImage(
            sprite,
            new Rect(sourceWidth - right, top, right, centerHeight),
            new Rect(rightEdgeX, bounds.Y + top, right, bounds.Height - top - bottom));

        var centerBounds = new Rect(
            bounds.X + left,
            bounds.Y + top,
            bounds.Width - left - right,
            bounds.Height - top - bottom);
        var centerSource = new Rect(left, top, centerWidth, centerHeight);
        if (!string.Equals(settings.CenterMode, NineSliceModes.Tile, StringComparison.OrdinalIgnoreCase))
        {
            context.DrawImage(sprite, centerSource, centerBounds);
            return;
        }

        // 中心平铺：按素材原始像素重复；数量过多时退回一次拉伸，避免大地图上刷出几万次 DrawImage
        var columns = (int)Math.Ceiling(centerBounds.Width / centerWidth);
        var rows = (int)Math.Ceiling(centerBounds.Height / centerHeight);
        if (columns <= 0 || rows <= 0 || columns * rows > 4096)
        {
            context.DrawImage(sprite, centerSource, centerBounds);
            return;
        }

        using (context.PushClip(centerBounds))
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var x = centerBounds.X + column * centerWidth;
                    var y = centerBounds.Y + row * centerHeight;
                    context.DrawImage(sprite, centerSource, new Rect(x, y, centerWidth, centerHeight));
                }
            }
        }
    }

    /// <summary>
    /// 按贴图原始像素重复铺满包围盒（多用于被放大的草地 / 路面地块）。
    /// 平铺数量过多时退回一次拉伸，避免 4096 地图上几十万次 DrawImage 把帧率拖死。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="sprite">贴图。</param>
    /// <param name="bounds">包围盒（世界坐标）。</param>
    private static void DrawTiled(DrawingContext context, Bitmap sprite, Rect bounds)
    {
        var width = sprite.PixelSize.Width;
        var height = sprite.PixelSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var columns = (int)Math.Ceiling(bounds.Width / width);
        var rows = (int)Math.Ceiling(bounds.Height / height);
        if (columns <= 0 || rows <= 0)
        {
            return;
        }

        var source = new Rect(0, 0, width, height);
        if ((long)columns * rows > 256)
        {
            context.DrawImage(sprite, source, bounds);
            return;
        }

        using var clip = context.PushClip(bounds);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                context.DrawImage(
                    sprite,
                    source,
                    new Rect(bounds.X + column * width, bounds.Y + row * height, width, height));
            }
        }
    }

    private void DrawLabel(DrawingContext context, string text, Rect bounds)
    {
        // 拖动平移时不排版文字：每帧 500+ 段 FormattedText 是平移不跟手的主要原因之一
        if (_isPanning || string.IsNullOrEmpty(text) || bounds.Width * _zoom < 24)
        {
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11.0 / _zoom,
            LabelBrush);

        var origin = new Point(
            bounds.X + (bounds.Width - formatted.Width / _zoom) / 2.0,
            bounds.Y + (bounds.Height - formatted.Height / _zoom) / 2.0);

        if (formatted.Width / _zoom <= bounds.Width)
        {
            context.DrawText(formatted, origin);
        }
    }

    private void DrawSelection(DrawingContext context)
    {
        var item = _selected;
        if (item is null || _config is null)
        {
            return;
        }

        var bounds = item.Bounds;
        var handle = 8.0 / _zoom;

        // 选中框、四角手柄与碰撞 / 阴影区域都是实例的局部矩形，必须和贴图吃同一个旋转，
        // 否则改角度后会出现「图转了、框还是正的」这种错位。
        // 默认再裁到地图矩形：`bg_grass_2` 这类贴边的整块绿化只露出一条边，
        // 完整包围盒会把几百像素的虚线框画到蓝色画布底上，看起来像一块“暗色错误区”。
        // 「裁剪越界」关闭时不要裁，完整画出实例包围盒与四角手柄，便于检查边缘出血。
        var mapClip = new Rect(0, 0, _config.Width, _config.Height);

        // 选中框内容拆成本地函数：裁剪开/关只切换 PushClip，内容与碰撞/阴影逻辑只写一份。
        void DrawContent()
        {
            context.DrawRectangle(null, new Pen(AccentBrush, 2.0 / _zoom), bounds);

            // 绿化层的 collision/shadow 是 PSD 批量导入时统一带上的 true，画出来只会把
            // 整块 605×351 的草地压成灰暗色块；它不是可编辑的碰撞体，选中时只保留外框与占格。
            // 用手改后的有效显示层判断，显示层改为绿化时同样不铺调试填充。
            var greenery = LayerOrder.EffectiveLayer(item) == MapLayerKeys.Greenery;

            // 碰撞 / 阴影区域：只对当前选中实例描绘，便于在画布上对准「碰撞区域 / 阴影区域」的数值
            if (item.CollisionEnabled && !greenery)
            {
                DrawEditableArea(context, item.CollisionBounds, CollisionAreaFillBrush, CollisionAreaStrokeBrush);
            }

            if (item.ShadowEnabled && !greenery)
            {
                DrawEditableArea(context, item.ShadowBounds, ShadowAreaFillBrush, ShadowAreaStrokeBrush);
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

        DrawOccupancyCells(context, item, emphasized: true);
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
    private void DrawOccupancyCells(DrawingContext context, PlacedObject item, bool emphasized)
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

        var thin = 1.0 / Math.Max(_zoom, 0.0001);
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
        var fill = emphasized ? OccupancyFillBrush : null;
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

    /// <summary>绘制选中实例的可编辑区域（碰撞 / 阴影）：半透明填充 + 虚线描边，线宽按缩放保持设备像素。</summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="area">区域矩形（世界坐标）。</param>
    /// <param name="fill">填充画刷。</param>
    /// <param name="stroke">描边画刷。</param>
    private void DrawEditableArea(DrawingContext context, Rect area, IBrush fill, IBrush stroke)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        context.FillRectangle(fill, area);
        context.DrawRectangle(null, new Pen(stroke, 1.5 / _zoom, DashStyle.Dash), area);
    }

    /// <summary>绘制空白处点选的格子：按绘制形状填充半透明绿色并描边，随缩放保持 1.5 设备像素线宽。</summary>    /// <param name="context">绘制上下文。</param>
    private void DrawSelectedCell(DrawingContext context)
    {
        if (_selectedCell is not { } selected || _config is null)
        {
            return;
        }

        var size = (double)_config.GridSize;
        var thin = 1.5 / Math.Max(_zoom, 0.0001);

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

        var thin = 1.0 / _zoom;
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

    private static Color ParseColor(string? hex)
    {
        return !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color)
            ? color
            : Color.FromRgb(0x8A, 0x8F, 0x9C);
    }

    #endregion
}
