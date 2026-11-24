/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapEmptyStateOverlay.cs
 * author:    云毅
 * created:   2026
 * descrip:   空地图占位层：蓝色渐变网格、MT 等距立方体与右侧操作引导。
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 没有地图存档时的画布占位层。
/// <para>
/// 该层只负责视觉提示，不参与画布命中与编辑：底层 <see cref="MapCanvas"/> 仍可正常缩放、平移和落位。
/// 是否显示由主窗口按「当前没有地图路径且没有已放置物件」统一控制，避免空白关卡与已有存档互相干扰。
/// </para>
/// </summary>
public sealed class MapEmptyStateOverlay : Control
{
    #region 视觉常量

    /// <summary>空态背景网格步长（屏幕像素），比编辑网格更大，作为装饰层而非可编辑格。</summary>
    private const double GridStep = 64.0;

    /// <summary>右侧引导区距画布边缘的留白。</summary>
    private const double GuideInset = 34.0;

    /// <summary>等距立方体的基准宽度；实际绘制按视口短边缩放。</summary>
    private const double CubeBaseWidth = 220.0;

    /// <summary>立方体正面高度（从顶面中心到底边）。</summary>
    private const double CubeBaseHeight = 216.0;

    /// <summary>立方体与标题整体在视口中的目标中心比例。</summary>
    private const double VerticalCenterRatio = 0.50;

    /// <summary>等距立方体缩放的参考视口短边。</summary>
    private const double ScaleReferenceSize = 650.0;

    /// <summary>缩放下限，防止小窗口压扁立方体。</summary>
    private const double ScaleMin = 0.72;

    /// <summary>缩放上限，防止大窗口让装饰元素过度膨胀。</summary>
    private const double ScaleMax = 1.15;

    private static readonly IBrush ViewportBrush = MakeGradient(
        Color.FromRgb(0x0D, 0x8C, 0xB6),
        Color.FromRgb(0x00, 0x5E, 0x8D),
        0.5, 0, 0.5, 1);

    private static readonly IPen GridPen = ArgbPen(0x1C, 0xFF, 0xFF, 0xFF, 1.0);

    private static readonly IBrush CubeTopBrush = MakeGradient(
        Color.FromRgb(0xC5, 0xF8, 0xFA),
        Color.FromRgb(0x42, 0xB8, 0xD2),
        0.1, 0.0, 0.9, 1.0);

    private static readonly IBrush CubeLeftBrush = MakeGradient(
        Color.FromRgb(0x10, 0x70, 0x94),
        Color.FromRgb(0x0A, 0x4F, 0x6D),
        0.0, 0.0, 1.0, 1.0);

    private static readonly IBrush CubeRightBrush = MakeGradient(
        Color.FromRgb(0x0D, 0x61, 0x82),
        Color.FromRgb(0x06, 0x3A, 0x53),
        0.0, 0.0, 1.0, 1.0);

    private static readonly IPen CubeEdgePen = ArgbPen(0xD8, 0xFF, 0xFF, 0xFF, 1.2);

    private static readonly IBrush CubeShadowBrush = ArgbBrush(0x38, 0x00, 0x00, 0x00);

    private static readonly IBrush PrimaryTextBrush = Brushes.White;

    private static readonly IBrush GuideTextBrush = ArgbBrush(0xE8, 0xFF, 0xFF, 0xFF);

    private static readonly IBrush GuideMutedBrush = ArgbBrush(0xC8, 0xEF, 0xFB, 0xFF);

    private static readonly IBrush TextShadowBrush = ArgbBrush(0x44, 0x00, 0x00, 0x00);

    private static readonly IPen ArrowPen = ArgbPen(0xE6, 0xFF, 0xFF, 0xFF, 1.8);

    private static readonly Typeface LatinBoldTypeface = new(
        "Segoe UI", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal);

    private static readonly Typeface GuideTypeface = new(
        "Microsoft YaHei UI", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);

    private static readonly Typeface TagTypeface = new(
        "Segoe UI", FontStyle.Normal, FontWeight.SemiBold, FontStretch.Normal);

    private static readonly (string Text, Color Color)[] GuideTags =
    {
        ("JSON", Color.FromRgb(0xE0, 0x6C, 0x2E)),
        ("PSD", Color.FromRgb(0x2E, 0x86, 0xBE)),
        ("CSV", Color.FromRgb(0x2F, 0xA8, 0x6A))
    };

    #endregion

    #region 布局测量

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 占位层跟随画布完整铺满，不参与自适应尺寸计算。
        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    #endregion

    #region 绘制

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var view = new Rect(Bounds.Size);
        if (view.Width < 1 || view.Height < 1)
        {
            return;
        }

        DrawBackdrop(context, view);
        var cubeTop = DrawCube(context, view);
        DrawGuide(context, view, cubeTop);
    }

    /// <summary>绘制蓝色渐变底与 64px 装饰网格。</summary>
    private static void DrawBackdrop(DrawingContext context, Rect view)
    {
        context.FillRectangle(ViewportBrush, view);

        for (var x = GridStep; x < view.Width; x += GridStep)
        {
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, view.Height));
        }

        for (var y = GridStep; y < view.Height; y += GridStep)
        {
            context.DrawLine(GridPen, new Point(0, y), new Point(view.Width, y));
        }
    }

    /// <summary>绘制等距立方体、MT 标识与 Map Tool 标题。</summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="view">画布占位层区域。</param>
    /// <returns>立方体顶面在屏幕上的 Y 坐标，供引导箭头定位。</returns>
    private double DrawCube(DrawingContext context, Rect view)
    {
        var scale = ViewScale(view);
        var centerX = view.Width * 0.5;
        var cubeTop = view.Height * VerticalCenterRatio - 150.0 * scale;

        Point P(double x, double y) => new(
            centerX + (x - CubeBaseWidth * 0.5) * scale,
            cubeTop + y * scale);

        // 地面阴影：先于三个面绘制，避免压住立方体底边。
        context.DrawEllipse(
            CubeShadowBrush,
            null,
            P(CubeBaseWidth * 0.5, 232),
            86 * scale,
            10 * scale);

        var topFace = BuildPolygon(
            P(110, 0), P(220, 52), P(110, 104), P(0, 52));
        var leftFace = BuildPolygon(
            P(0, 52), P(110, 104), P(110, 216), P(0, 164));
        var rightFace = BuildPolygon(
            P(110, 104), P(220, 52), P(220, 164), P(110, 216));

        context.DrawGeometry(CubeTopBrush, CubeEdgePen, topFace);
        context.DrawGeometry(CubeLeftBrush, CubeEdgePen, leftFace);
        context.DrawGeometry(CubeRightBrush, CubeEdgePen, rightFace);

        // 中缝与顶面棱线：补出 TexturePacker 风格的低多边形体积感。
        context.DrawLine(CubeEdgePen, P(110, 0), P(110, 104));
        context.DrawLine(CubeEdgePen, P(0, 52), P(110, 104));
        context.DrawLine(CubeEdgePen, P(220, 52), P(110, 104));

        DrawCenteredText(
            context,
            "MT",
            centerX,
            cubeTop + 119 * scale,
            46 * scale,
            LatinBoldTypeface,
            PrimaryTextBrush,
            shadowOffset: 2.0);

        var titleSize = Math.Clamp(32 * scale, 24, 38);
        DrawCenteredText(
            context,
            "Map Tool",
            centerX,
            cubeTop + 278 * scale,
            titleSize,
            LatinBoldTypeface,
            PrimaryTextBrush,
            shadowOffset: 2.0);

        return cubeTop;
    }

    /// <summary>绘制右上角操作引导：无地图说明、支持格式与基础快捷键。</summary>
    private static void DrawGuide(DrawingContext context, Rect view, double cubeTop)
    {
        var right = view.Width - GuideInset;
        var y = Math.Max(34.0, view.Height * 0.05);

        y = DrawRightText(context, "No map loaded", right, y, 13, GuideTypeface, GuideTextBrush);
        y = DrawRightText(context, "Open Map or New Level", right, y + 3, 16, LatinBoldTypeface, PrimaryTextBrush);
        y = DrawRightText(
            context,
            "也可以从章节树切换关卡，或用 --open-map 载入地图",
            right,
            y + 6,
            10.5,
            GuideTypeface,
            GuideTextBrush);

        var tagTop = y + 8;
        DrawRightAlignedTags(context, right, tagTop);

        y = DrawRightText(
            context,
            "Delete 删除选中 · 滚轮缩放 · 右键拖动平移",
            right,
            tagTop + 26,
            10,
            GuideTypeface,
            GuideMutedBrush);

        DrawGuideArrow(context, view, right, y, cubeTop);
    }

    /// <summary>绘制右侧引导指向立方体的弧形箭头。</summary>
    private static void DrawGuideArrow(
        DrawingContext context,
        Rect view,
        double right,
        double guideBottom,
        double cubeTop)
    {
        var scale = ViewScale(view);
        var start = new Point(
            Math.Max(view.Width * 0.5 + 260 * scale, right - 330),
            guideBottom + 14);
        var end = new Point(
            view.Width * 0.5 + 148 * scale,
            cubeTop + 18 * scale);

        var c1 = new Point(start.X - 34, start.Y + 38);
        var c2 = new Point(start.X - 132, start.Y + 76);
        var previous = start;

        // 分段采样三次贝塞尔，避免为了一个装饰箭头引入额外几何缓存。
        const int segments = 14;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (double)segments;
            var point = CubicPoint(start, c1, c2, end, t);
            context.DrawLine(ArrowPen, previous, point);
            previous = point;
        }

        // 末段采样点就是箭头尖端，箭头方向要取前一段的采样点，否则零长度会直接跳过。
        var beforeTip = CubicPoint(start, c1, c2, end, 1 - 1.0 / segments);
        DrawArrowHead(context, beforeTip, end);
    }

    /// <summary>三次贝塞尔采样。</summary>
    private static Point CubicPoint(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
        var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
        return new Point(x, y);
    }

    /// <summary>按末端切线方向绘制实心箭头。</summary>
    private static void DrawArrowHead(DrawingContext context, Point from, Point tip)
    {
        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            return;
        }

        var ux = dx / length;
        var uy = dy / length;
        var baseX = tip.X - ux * 17;
        var baseY = tip.Y - uy * 17;
        var px = -uy * 8;
        var py = ux * 8;

        var head = BuildPolygon(
            tip,
            new Point(baseX + px, baseY + py),
            new Point(baseX - px, baseY - py));

        context.DrawGeometry(PrimaryTextBrush, null, head);
    }

    /// <summary>绘制右对齐文本并返回下一行的顶部 Y 坐标。</summary>
    private static double DrawRightText(
        DrawingContext context,
        string text,
        double right,
        double top,
        double size,
        Typeface typeface,
        IBrush brush)
    {
        var formatted = CanvasText.Create(text, size, brush, typeface);
        var x = right - formatted.Width;
        context.DrawText(formatted, new Point(x, top));
        return top + formatted.Height;
    }

    /// <summary>居中绘制带轻微投影的文本。</summary>
    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        double centerX,
        double top,
        double size,
        Typeface typeface,
        IBrush brush,
        double shadowOffset)
    {
        var formatted = CanvasText.Create(text, size, brush, typeface);
        var x = centerX - formatted.Width * 0.5;

        if (shadowOffset > 0)
        {
            var shadow = CanvasText.Create(text, size, TextShadowBrush, typeface);
            context.DrawText(shadow, new Point(x + shadowOffset, top + shadowOffset));
        }

        context.DrawText(formatted, new Point(x, top));
    }

    /// <summary>右下对齐绘制支持格式标签，颜色与图一的格式胶囊一致。</summary>
    private static void DrawRightAlignedTags(DrawingContext context, double right, double top)
    {
        const double gap = 4.0;
        const double height = 18.0;
        const double padding = 5.0;

        var widths = new double[GuideTags.Length];
        var texts = new FormattedText[GuideTags.Length];
        var total = 0.0;
        for (var i = 0; i < GuideTags.Length; i++)
        {
            texts[i] = CanvasText.Create(GuideTags[i].Text, 9.5, PrimaryTextBrush, TagTypeface);
            widths[i] = texts[i].Width + padding * 2;
            total += widths[i] + (i > 0 ? gap : 0);
        }

        var x = right - total;
        for (var i = 0; i < GuideTags.Length; i++)
        {
            var fill = new SolidColorBrush(GuideTags[i].Color);
            context.DrawRectangle(fill, null, new Rect(x, top, widths[i], height));
            context.DrawText(texts[i], new Point(x + padding, top + (height - texts[i].Height) * 0.5));
            x += widths[i] + gap;
        }
    }

    // ---TODO 重构：空态绘制统一走局部缩放、渐变与 ARGB 原语，避免各处重复手写。
    /// <summary>按视口短边计算装饰元素缩放，统一上下限。</summary>
    private static double ViewScale(Rect view) =>
        Math.Clamp(Math.Min(view.Width, view.Height) / ScaleReferenceSize, ScaleMin, ScaleMax);

    /// <summary>创建两段线性渐变，统一起止点与停止位写法。</summary>
    private static IBrush MakeGradient(
        Color from,
        Color to,
        double startX,
        double startY,
        double endX,
        double endY) =>
        new LinearGradientBrush
        {
            StartPoint = new RelativePoint(startX, startY, RelativeUnit.Relative),
            EndPoint = new RelativePoint(endX, endY, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(from, 0.0),
                new GradientStop(to, 1.0)
            },
        };

    /// <summary>创建带透明度的实色画刷。</summary>
    private static IBrush ArgbBrush(byte alpha, byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));

    /// <summary>创建带透明度的实色画笔。</summary>
    private static IPen ArgbPen(byte alpha, byte red, byte green, byte blue, double thickness) =>
        new Pen(ArgbBrush(alpha, red, green, blue), thickness);

    /// <summary>将四点闭合为可填充的流式几何。</summary>
    private static StreamGeometry BuildPolygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var writer = geometry.Open())
        {
            writer.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
            {
                writer.LineTo(points[i]);
            }

            writer.EndFigure(true);
        }

        return geometry;
    }

    #endregion
}
