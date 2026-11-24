/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NineSlicePreview.cs
 * author:    云毅
 * created:   2026
 * descrip:   九宫格预览：显示素材与绿色切片线，可拖动左 / 右 / 上 / 下四条边距
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 九宫格可视化编辑器。按素材原始像素绘制九宫格：四角保持原尺寸，四条边与中心分别着色，
/// 绿色参考线可直接拖动，拖动结果实时写回 <see cref="NineSliceSettings"/>。
/// 视觉口径参考 Unity Sprite Editor 的绿色九宫格参考线，边距单位仍是素材原始像素。
/// </summary>
public sealed class NineSlicePreview : Control
{
    #region 常量与画刷

    /// <summary>预览四周留白，让四条边距线在贴边时也有抓取空间。</summary>
    private const double ImagePadding = 18.0;

    /// <summary>九宫格线的命中半径（设备无关像素）。</summary>
    private const double GuideHitRadius = 8.0;

    /// <summary>拖动线时单个源像素在预览里的最小移动距离，避免大图一像素一跳。</summary>
    private const double MinScale = 0.0001;

    /// <summary>预览控件最小高度，保证空状态提示和底栏都能完整显示。</summary>
    private const double MinPreviewHeight = 220.0;

    /// <summary>底部说明栏预留高度，绘制素材时要把这段让出来。</summary>
    private const double FooterHeight = 22.0;

    /// <summary>底部说明文字距左边缘的内边距。</summary>
    private const double FooterTextLeftInset = 10.0;

    /// <summary>底部说明文字基线距底边的内边距。</summary>
    private const double FooterTextBottomInset = 18.0;

    /// <summary>底部说明与空状态提示的字号。</summary>
    private const double FooterFontSize = 11.0;

    /// <summary>透明棋盘格的单元边长（设备无关像素）。</summary>
    private const double CheckerCellSize = 14.0;

    /// <summary>素材外框线宽。</summary>
    private const double FrameThickness = 1.0;

    /// <summary>九宫格切片区域的描边宽度。</summary>
    private const double SliceStrokeThickness = 0.7;

    /// <summary>常规参考线宽度。</summary>
    private const double GuideThickness = 1.3;

    /// <summary>悬停 / 拖动参考线的加粗宽度。</summary>
    private const double ActiveGuideThickness = 2.4;

    /// <summary>常规拖拽手柄半径。</summary>
    private const double HandleRadius = 3.6;

    /// <summary>悬停 / 拖动时的放大的手柄半径。</summary>
    private const double ActiveHandleRadius = 5.0;

    /// <summary>拖拽手柄描边宽度。</summary>
    private const double HandleBorderThickness = 1.0;

    /// <summary>默认箭头光标，离开参考线时恢复。</summary>
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);

    /// <summary>左右边距线的水平调整光标。</summary>
    private static readonly Cursor HorizontalCursor = new(StandardCursorType.SizeWestEast);

    /// <summary>上下边距线的垂直调整光标。</summary>
    private static readonly Cursor VerticalCursor = new(StandardCursorType.SizeNorthSouth);

    #endregion

    #region 字段

    /// <summary>当前预览的素材位图；为空时绘制空状态提示。</summary>
    private Bitmap? _image;

    /// <summary>当前九宫格配置，与编辑表单共享同一实例，拖动直接回写。</summary>
    private NineSliceSettings? _settings;

    /// <summary>素材在控件内的实际绘制矩形，命中测试与坐标反算都以它为准。</summary>
    private Rect _imageRect;

    /// <summary>素材原始像素到控件设备无关像素的缩放比，每次渲染重算。</summary>
    private double _imageScale = 1.0;

    /// <summary>当前正在拖动的边距线；None 表示未拖动。</summary>
    private NineSliceEdge _draggingEdge = NineSliceEdge.None;

    /// <summary>当前鼠标悬停的边距线，用于高亮参考线与切换光标。</summary>
    private NineSliceEdge _hoverEdge = NineSliceEdge.None;

    // ---TODO 重构：绘制、参考线与命中测试共用同一套屏幕坐标。
    /// <summary>九宫格参考线在控件内的屏幕坐标；四条边共用同一套夹紧与缩放口径。</summary>
    /// <param name="Image">素材绘制矩形。</param>
    /// <param name="Left">左边距线 X。</param>
    /// <param name="Right">右边距线 X。</param>
    /// <param name="Top">上边距线 Y。</param>
    /// <param name="Bottom">下边距线 Y。</param>
    private readonly record struct GuideFrame(Rect Image, double Left, double Right, double Top, double Bottom);

    #endregion

    #region 公开属性与事件

    /// <summary>当前预览的素材；null 时显示空状态提示。</summary>
    public Bitmap? Image
    {
        get => _image;
        set
        {
            _image = value;
            InvalidateVisual();
        }
    }

    /// <summary>当前九宫格配置。预览只读取共享对象；拖动会直接修改它。</summary>
    public NineSliceSettings? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            InvalidateVisual();
        }
    }

    /// <summary>拖动四条边距线后触发，调用方据此同步数字输入框与画布。</summary>
    public event EventHandler? Changed;

    #endregion

    #region 构造

    /// <summary>初始化九宫格预览。</summary>
    public NineSlicePreview()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = ArrowCursor;
        MinHeight = MinPreviewHeight;
    }

    #endregion

    #region 渲染

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var view = new Rect(Bounds.Size);
        DrawCheckerboard(context, view);

        if (_image is null || _settings is null)
        {
            DrawCenteredHint(context, view, "当前模板没有可用图片，先设置 Icon / ResName 并确认资源能解析");
            return;
        }

        var pixelWidth = Math.Max(1, _image.PixelSize.Width);
        var pixelHeight = Math.Max(1, _image.PixelSize.Height);
        var availableWidth = Math.Max(1.0, view.Width - ImagePadding * 2);
        var availableHeight = Math.Max(1.0, view.Height - ImagePadding * 2 - FooterHeight);
        _imageScale = Math.Min(availableWidth / pixelWidth, availableHeight / pixelHeight);
        if (_imageScale <= MinScale)
        {
            _imageScale = MinScale;
        }

        var drawWidth = pixelWidth * _imageScale;
        var drawHeight = pixelHeight * _imageScale;
        _imageRect = new Rect(
            (view.Width - drawWidth) / 2.0,
            (view.Height - drawHeight - FooterHeight) / 2.0,
            drawWidth,
            drawHeight);

        context.DrawImage(
            _image,
            new Rect(0, 0, pixelWidth, pixelHeight),
            _imageRect);

        context.DrawRectangle(null, new Pen(UiTheme.NineSlice.Frame, FrameThickness), _imageRect);

        DrawSliceOverlay(context, pixelWidth, pixelHeight);
        DrawGuides(context, pixelWidth, pixelHeight);
        DrawFooter(context, view, pixelWidth, pixelHeight);
    }

    /// <summary>画深灰棋盘格，避免透明 PNG 的透明区域和背景混在一起。</summary>
    private static void DrawCheckerboard(DrawingContext context, Rect view)
    {
        context.FillRectangle(UiTheme.NineSlice.Backdrop, view);
        const double cell = CheckerCellSize;
        var rows = (int)Math.Ceiling(view.Height / cell);
        var columns = (int)Math.Ceiling(view.Width / cell);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if ((row + column) % 2 != 0)
                {
                    continue;
                }

                context.FillRectangle(
                    UiTheme.NineSlice.CheckerDark,
                    new Rect(column * cell, row * cell, cell, cell));
            }
        }
    }

    /// <summary>
    /// 把九宫格边距换算成控件内坐标。切片绘制、参考线与命中测试都走这一入口，
    /// 避免三处各自重复 <c>Clamp + Scale</c>。
    /// </summary>
    /// <param name="pixelWidth">素材原始像素宽。</param>
    /// <param name="pixelHeight">素材原始像素高。</param>
    /// <returns>屏幕坐标帧；没有配置时为 null。</returns>
    private GuideFrame? ResolveGuideFrame(int pixelWidth, int pixelHeight)
    {
        if (_settings is null)
        {
            return null;
        }

        var rect = _imageRect;
        var left = Math.Clamp(_settings.Left, 0, pixelWidth);
        var right = Math.Clamp(_settings.Right, 0, pixelWidth);
        var top = Math.Clamp(_settings.Top, 0, pixelHeight);
        var bottom = Math.Clamp(_settings.Bottom, 0, pixelHeight);
        return new GuideFrame(
            rect,
            rect.X + left * _imageScale,
            rect.Right - right * _imageScale,
            rect.Y + top * _imageScale,
            rect.Bottom - bottom * _imageScale);
    }

    /// <summary>九个区域按绿色半透明填充；中心区域更浅，便于分辨四条边与中心。</summary>
    private void DrawSliceOverlay(DrawingContext context, int pixelWidth, int pixelHeight)
    {
        if (ResolveGuideFrame(pixelWidth, pixelHeight) is not { } frame)
        {
            return;
        }

        var rect = frame.Image;
        var xLeft = frame.Left;
        var xRight = Math.Max(frame.Left, frame.Right);
        var yTop = frame.Top;
        var yBottom = Math.Max(frame.Top, frame.Bottom);

        var xs = new[] { rect.X, xLeft, xRight, rect.Right };
        var ys = new[] { rect.Y, yTop, yBottom, rect.Bottom };
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var cell = new Rect(
                    xs[column],
                    ys[row],
                    Math.Max(0, xs[column + 1] - xs[column]),
                    Math.Max(0, ys[row + 1] - ys[row]));
                if (cell.Width <= 0 || cell.Height <= 0)
                {
                    continue;
                }

                var fill = row == 1 && column == 1 ? UiTheme.NineSlice.CenterSliceFill : UiTheme.NineSlice.SliceFill;
                context.FillRectangle(fill, cell);
                context.DrawRectangle(null, new Pen(UiTheme.NineSlice.Guide, SliceStrokeThickness), cell);
            }
        }
    }

    /// <summary>画四条绿色参考线与四枚中点拖拽手柄；当前悬停 / 拖动线用黄色高亮。</summary>
    private void DrawGuides(DrawingContext context, int pixelWidth, int pixelHeight)
    {
        if (ResolveGuideFrame(pixelWidth, pixelHeight) is not { } frame)
        {
            return;
        }

        var rect = frame.Image;
        var xLeft = frame.Left;
        var xRight = frame.Right;
        var yTop = frame.Top;
        var yBottom = frame.Bottom;

        var invalid = _settings!.Enabled && HasMarginOverflow(_settings, pixelWidth, pixelHeight);
        var guideBrush = invalid ? UiTheme.NineSlice.InvalidGuide : UiTheme.NineSlice.Guide;

        DrawGuide(context, xLeft, rect.Y, xLeft, rect.Bottom, _draggingEdge == NineSliceEdge.Left || _hoverEdge == NineSliceEdge.Left, guideBrush);
        DrawGuide(context, xRight, rect.Y, xRight, rect.Bottom, _draggingEdge == NineSliceEdge.Right || _hoverEdge == NineSliceEdge.Right, guideBrush);
        DrawGuide(context, rect.X, yTop, rect.Right, yTop, _draggingEdge == NineSliceEdge.Top || _hoverEdge == NineSliceEdge.Top, guideBrush);
        DrawGuide(context, rect.X, yBottom, rect.Right, yBottom, _draggingEdge == NineSliceEdge.Bottom || _hoverEdge == NineSliceEdge.Bottom, guideBrush);

        DrawHandle(context, new Point(xLeft, rect.Y + rect.Height / 2), _draggingEdge == NineSliceEdge.Left || _hoverEdge == NineSliceEdge.Left);
        DrawHandle(context, new Point(xRight, rect.Y + rect.Height / 2), _draggingEdge == NineSliceEdge.Right || _hoverEdge == NineSliceEdge.Right);
        DrawHandle(context, new Point(rect.X + rect.Width / 2, yTop), _draggingEdge == NineSliceEdge.Top || _hoverEdge == NineSliceEdge.Top);
        DrawHandle(context, new Point(rect.X + rect.Width / 2, yBottom), _draggingEdge == NineSliceEdge.Bottom || _hoverEdge == NineSliceEdge.Bottom);
    }

    /// <summary>画一条参考线；活动线更粗，颜色切到黄色。</summary>
    private static void DrawGuide(DrawingContext context, double x1, double y1, double x2, double y2, bool active, IBrush brush)
    {
        var width = active ? ActiveGuideThickness : GuideThickness;
        var pen = new Pen(active ? UiTheme.NineSlice.ActiveGuide : brush, width);
        context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
    }

    /// <summary>画中点手柄：拖动线时可以从任意位置抓，手柄只是视觉提示。</summary>
    private static void DrawHandle(DrawingContext context, Point center, bool active)
    {
        var fill = active ? UiTheme.NineSlice.ActiveGuide : UiTheme.NineSlice.Guide;
        var radius = active ? ActiveHandleRadius : HandleRadius;
        context.DrawEllipse(fill, new Pen(UiTheme.NineSlice.HandleBorder, HandleBorderThickness), center, radius, radius);
    }

    /// <summary>底部显示素材尺寸与当前边距；边距越界时直接提示不生效。</summary>
    private void DrawFooter(DrawingContext context, Rect view, int pixelWidth, int pixelHeight)
    {
        if (_settings is null)
        {
            return;
        }

        var invalid = HasMarginOverflow(_settings, pixelWidth, pixelHeight);
        var text = invalid
            ? $"边距越界：左+右需 < {pixelWidth - 1}，上+下需 < {pixelHeight - 1}"
            : $"{pixelWidth}×{pixelHeight} 像素　L {_settings.Left}  R {_settings.Right}  T {_settings.Top}  B {_settings.Bottom}　拖动绿线调整";
        var brush = invalid ? UiTheme.NineSlice.Error : UiTheme.NineSlice.Hint;
        var formatted = CanvasText.Create(text, FooterFontSize, brush);
        context.DrawText(formatted, new Point(FooterTextLeftInset, view.Height - FooterTextBottomInset));
    }

    /// <summary>
    /// 判断左右或上下边距之和是否吃掉整张素材。九宫格要求两侧边距之和小于素材尺寸，
    /// 否则中心区域宽度/高度会变成 0，切片无法成立。
    /// </summary>
    /// <param name="settings">九宫格配置。</param>
    /// <param name="pixelWidth">素材原始宽度（像素）。</param>
    /// <param name="pixelHeight">素材原始高度（像素）。</param>
    /// <returns>true = 边距越界。</returns>
    private static bool HasMarginOverflow(NineSliceSettings settings, int pixelWidth, int pixelHeight) =>
        settings.Left + settings.Right >= pixelWidth - 1
        || settings.Top + settings.Bottom >= pixelHeight - 1;

    /// <summary>空图时在预览中心放一行说明。</summary>
    private static void DrawCenteredHint(DrawingContext context, Rect view, string text)
    {
        var formatted = CanvasText.Create(text, FooterFontSize, UiTheme.NineSlice.Hint);
        context.DrawText(
            formatted,
            new Point((view.Width - formatted.Width) / 2.0, (view.Height - formatted.Height) / 2.0));
    }

    #endregion

    #region 拖动交互

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_image is null || _settings is null)
        {
            return;
        }

        var edge = HitGuide(e.GetPosition(this));
        if (edge == NineSliceEdge.None)
        {
            return;
        }

        // 预览已显示但「需要九宫格」未勾选时，第一次拖动自动打开开关，
        // 让用户可以直接拖线，不必先去勾复选框再回来。
        if (!_settings.Enabled)
        {
            _settings.Enabled = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        _draggingEdge = edge;
        _hoverEdge = edge;
        e.Pointer.Capture(this);
        Cursor = edge is NineSliceEdge.Left or NineSliceEdge.Right ? HorizontalCursor : VerticalCursor;
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_image is null || _settings is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_draggingEdge != NineSliceEdge.None)
        {
            ApplyDrag(position);
            e.Handled = true;
            return;
        }

        var hover = HitGuide(position);
        if (hover != _hoverEdge)
        {
            _hoverEdge = hover;
            Cursor = hover switch
            {
                NineSliceEdge.Left or NineSliceEdge.Right => HorizontalCursor,
                NineSliceEdge.Top or NineSliceEdge.Bottom => VerticalCursor,
                _ => ArrowCursor,
            };
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_draggingEdge == NineSliceEdge.None)
        {
            return;
        }

        _draggingEdge = NineSliceEdge.None;
        _hoverEdge = NineSliceEdge.None;
        e.Pointer.Capture(null);
        Cursor = ArrowCursor;
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_draggingEdge != NineSliceEdge.None)
        {
            return;
        }

        if (_hoverEdge != NineSliceEdge.None)
        {
            _hoverEdge = NineSliceEdge.None;
            Cursor = ArrowCursor;
            InvalidateVisual();
        }
    }

    /// <summary>判断指针靠近哪条参考线。越靠近线越优先；同时靠近横竖两线时取距离更短的一条。</summary>
    private NineSliceEdge HitGuide(Point position)
    {
        if (_image is null || _settings is null || !_imageRect.Contains(position))
        {
            return NineSliceEdge.None;
        }

        var pixelWidth = Math.Max(1, _image.PixelSize.Width);
        var pixelHeight = Math.Max(1, _image.PixelSize.Height);
        if (ResolveGuideFrame(pixelWidth, pixelHeight) is not { } frame)
        {
            return NineSliceEdge.None;
        }

        var leftX = frame.Left;
        var rightX = frame.Right;
        var topY = frame.Top;
        var bottomY = frame.Bottom;

        var bestEdge = NineSliceEdge.None;
        var bestDistance = GuideHitRadius;
        TryPick(NineSliceEdge.Left, Math.Abs(position.X - leftX), ref bestEdge, ref bestDistance);
        TryPick(NineSliceEdge.Right, Math.Abs(position.X - rightX), ref bestEdge, ref bestDistance);
        TryPick(NineSliceEdge.Top, Math.Abs(position.Y - topY), ref bestEdge, ref bestDistance);
        TryPick(NineSliceEdge.Bottom, Math.Abs(position.Y - bottomY), ref bestEdge, ref bestDistance);
        return bestEdge;
    }

    /// <summary>距离小于当前最优且小于命中半径时更新候选。</summary>
    private static void TryPick(NineSliceEdge edge, double distance, ref NineSliceEdge bestEdge, ref double bestDistance)
    {
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestEdge = edge;
        }
    }

    /// <summary>把屏幕坐标反算成素材原始像素，并按当前边距夹紧，保证九宫格始终可用。</summary>
    private void ApplyDrag(Point position)
    {
        if (_image is null || _settings is null)
        {
            return;
        }

        var pixelWidth = Math.Max(1, _image.PixelSize.Width);
        var pixelHeight = Math.Max(1, _image.PixelSize.Height);
        var maxWidthSum = Math.Max(0, pixelWidth - 2);
        var maxHeightSum = Math.Max(0, pixelHeight - 2);
        var localX = Math.Clamp((position.X - _imageRect.X) / Math.Max(_imageScale, MinScale), 0, pixelWidth);
        var localY = Math.Clamp((position.Y - _imageRect.Y) / Math.Max(_imageScale, MinScale), 0, pixelHeight);

        switch (_draggingEdge)
        {
            case NineSliceEdge.Left:
            {
                var value = Math.Clamp((int)Math.Round(localX), 0, Math.Max(0, maxWidthSum - _settings.Right));
                if (value != _settings.Left)
                {
                    _settings.Left = value;
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
            }

            case NineSliceEdge.Right:
            {
                var value = Math.Clamp(pixelWidth - (int)Math.Round(localX), 0, Math.Max(0, maxWidthSum - _settings.Left));
                if (value != _settings.Right)
                {
                    _settings.Right = value;
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
            }

            case NineSliceEdge.Top:
            {
                var value = Math.Clamp((int)Math.Round(localY), 0, Math.Max(0, maxHeightSum - _settings.Bottom));
                if (value != _settings.Top)
                {
                    _settings.Top = value;
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
            }

            case NineSliceEdge.Bottom:
            {
                var value = Math.Clamp(pixelHeight - (int)Math.Round(localY), 0, Math.Max(0, maxHeightSum - _settings.Top));
                if (value != _settings.Bottom)
                {
                    _settings.Bottom = value;
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
            }
        }

        InvalidateVisual();
    }

    #endregion

    #region 内部枚举

    /// <summary>当前拖动 / 悬停的边距线。</summary>
    private enum NineSliceEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
    }

    #endregion
}
