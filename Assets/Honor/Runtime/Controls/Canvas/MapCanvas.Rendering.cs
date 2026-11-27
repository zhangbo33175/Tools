/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Rendering.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布绘制总入口：性能采样、底图、边界、图层顺序与整图绘制
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

/// <summary>画布绘制总入口：性能采样、底图、边界、图层顺序与整图绘制。</summary>
public sealed partial class MapCanvas : Control
{
    #region 绘制总入口与底图

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

        EditorLogs.Append(
            "canvas-perf.log",
            $"{DateTime.Now:HH:mm:ss.fff}  avg {average:0.0}ms/frame  objects {_config?.Objects.Count ?? 0}  zoom {_zoom:0.###}  panning {_isPanning}{Environment.NewLine}");
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

            // 交通仿真开启时叠一层相位灯头与「等灯」标记，让规则状态一眼可见
            if (PreviewMode && _trafficPreviewEnabled)
            {
                DrawTrafficSimOverlay(context, config);
            }

            if (!PreviewMode)
            {
                // 未登记配置表的资源先框出来：盖在美术之上、选中框之下，一眼能扫到缺口
                DrawUnregisteredMarks(context, config);
                DrawEditingPath(context);
                DrawFog(context, config);
                DrawFogBrush(context);
                DrawSelection(context);
                DrawSelectedCell(context);
            }

            // 地图外框：始终 1 设备像素宽，让画布边界在青色底上仍清晰可见
            context.DrawRectangle(
                new Pen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), Hairline),
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
        else if (_trafficPreviewEnabled)
        {
            // 预览交通时把斑马线画出来：否则看不出车停在停止线前还是压进横道
            DrawPreviewCrosswalks(context, config);
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
        // 预览模式传当前脚底基准：角色沿路径移动时，树 / 建筑的遮挡跟着角色当前位置实时变化。
        // 编辑器与预览共用同一排序基准：取当前渲染包围盒底边；路径角色移动时按新脚底参与 2D 遮挡，
        // 不把角色强制置顶，避免树木 / 建筑前后关系失真。
        Func<PlacedObject, double> baseline = VisualSortBaseline;
        return LayerBands.Sort(config.Objects.Where(item =>
            !HiddenLayers.Contains(VisibilityLayer(item))
            && !HiddenTemplateIds.Contains(item.TemplateId)), baseline);
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

        var formatted = CanvasText.Create($"底图：{config.BgName}", 28.0 * Hairline, LabelBrush);

        context.DrawText(formatted, new Point(32, 32));
    }

    #endregion
}



