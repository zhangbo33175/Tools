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
using System.Globalization;
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

    private static readonly IBrush PreviewBackBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
    private static readonly IBrush CheckerDarkBrush = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
    private static readonly IBrush CheckerLightBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x35, 0x35));
    private static readonly IBrush SliceFillBrush = new SolidColorBrush(Color.FromArgb(0x24, 0x4C, 0xD9, 0x64));
    private static readonly IBrush CenterSliceFillBrush = new SolidColorBrush(Color.FromArgb(0x14, 0x4C, 0xD9, 0x64));
    private static readonly IBrush GuideBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xD9, 0x64));
    private static readonly IBrush ActiveGuideBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A));
    private static readonly IBrush InvalidGuideBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x53, 0x3D));
    private static readonly IBrush HandleFillBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xD9, 0x64));
    private static readonly IBrush ActiveHandleFillBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A));
    private static readonly IBrush FrameBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly IBrush HintBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x8A));

    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HorizontalCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor VerticalCursor = new(StandardCursorType.SizeNorthSouth);

    #endregion

    #region 字段

    private Bitmap? _image;
    private NineSliceSettings? _settings;
    private Rect _imageRect;
    private double _imageScale = 1.0;
    private NineSliceEdge _draggingEdge = NineSliceEdge.None;
    private NineSliceEdge _hoverEdge = NineSliceEdge.None;

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
        MinHeight = 220;
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
        var availableHeight = Math.Max(1.0, view.Height - ImagePadding * 2 - 22);
        _imageScale = Math.Min(availableWidth / pixelWidth, availableHeight / pixelHeight);
        if (_imageScale <= MinScale)
        {
            _imageScale = MinScale;
        }

        var drawWidth = pixelWidth * _imageScale;
        var drawHeight = pixelHeight * _imageScale;
        _imageRect = new Rect(
            (view.Width - drawWidth) / 2.0,
            (view.Height - drawHeight - 22) / 2.0,
            drawWidth,
            drawHeight);

        context.DrawImage(
            _image,
            new Rect(0, 0, pixelWidth, pixelHeight),
            _imageRect);

        context.DrawRectangle(null, new Pen(FrameBrush, 1.0), _imageRect);

        DrawSliceOverlay(context, pixelWidth, pixelHeight);
        DrawGuides(context, pixelWidth, pixelHeight);
        DrawFooter(context, view, pixelWidth, pixelHeight);
    }

    /// <summary>画深灰棋盘格，避免透明 PNG 的透明区域和背景混在一起。</summary>
    private static void DrawCheckerboard(DrawingContext context, Rect view)
    {
        context.FillRectangle(PreviewBackBrush, view);
        const double cell = 14.0;
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
                    CheckerDarkBrush,
                    new Rect(column * cell, row * cell, cell, cell));
            }
        }
    }

    /// <summary>九个区域按绿色半透明填充；中心区域更浅，便于分辨四条边与中心。</summary>
    private void DrawSliceOverlay(DrawingContext context, int pixelWidth, int pixelHeight)
    {
        if (_settings is null)
        {
            return;
        }

        var rect = _imageRect;
        var left = Math.Clamp(_settings.Left, 0, pixelWidth);
        var right = Math.Clamp(_settings.Right, 0, pixelWidth);
        var top = Math.Clamp(_settings.Top, 0, pixelHeight);
        var bottom = Math.Clamp(_settings.Bottom, 0, pixelHeight);

        var xLeft = rect.X + left * _imageScale;
        var xRight = rect.Right - right * _imageScale;
        var yTop = rect.Y + top * _imageScale;
        var yBottom = rect.Bottom - bottom * _imageScale;

        if (xRight < xLeft)
        {
            xRight = xLeft;
        }

        if (yBottom < yTop)
        {
            yBottom = yTop;
        }

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

                var fill = row == 1 && column == 1 ? CenterSliceFillBrush : SliceFillBrush;
                context.FillRectangle(fill, cell);
                context.DrawRectangle(null, new Pen(GuideBrush, 0.7), cell);
            }
        }
    }

    /// <summary>画四条绿色参考线与四枚中点拖拽手柄；当前悬停 / 拖动线用黄色高亮。</summary>
    private void DrawGuides(DrawingContext context, int pixelWidth, int pixelHeight)
    {
        if (_settings is null)
        {
            return;
        }

        var rect = _imageRect;
        var left = Math.Clamp(_settings.Left, 0, pixelWidth);
        var right = Math.Clamp(_settings.Right, 0, pixelWidth);
        var top = Math.Clamp(_settings.Top, 0, pixelHeight);
        var bottom = Math.Clamp(_settings.Bottom, 0, pixelHeight);

        var xLeft = rect.X + left * _imageScale;
        var xRight = rect.Right - right * _imageScale;
        var yTop = rect.Y + top * _imageScale;
        var yBottom = rect.Bottom - bottom * _imageScale;

        var invalid = _settings.Enabled
            && (_settings.Left + _settings.Right >= pixelWidth - 1
                || _settings.Top + _settings.Bottom >= pixelHeight - 1);
        var guideBrush = invalid ? InvalidGuideBrush : GuideBrush;

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
        var width = active ? 2.4 : 1.3;
        var pen = new Pen(active ? ActiveGuideBrush : brush, width);
        context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
    }

    /// <summary>画中点手柄：拖动线时可以从任意位置抓，手柄只是视觉提示。</summary>
    private static void DrawHandle(DrawingContext context, Point center, bool active)
    {
        var fill = active ? ActiveHandleFillBrush : HandleFillBrush;
        var radius = active ? 5.0 : 3.6;
        context.DrawEllipse(fill, new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), 1.0), center, radius, radius);
    }

    /// <summary>底部显示素材尺寸与当前边距；边距越界时直接提示不生效。</summary>
    private void DrawFooter(DrawingContext context, Rect view, int pixelWidth, int pixelHeight)
    {
        if (_settings is null)
        {
            return;
        }

        var invalid = _settings.Left + _settings.Right >= pixelWidth - 1
            || _settings.Top + _settings.Bottom >= pixelHeight - 1;
        var text = invalid
            ? $"边距越界：左+右需 < {pixelWidth - 1}，上+下需 < {pixelHeight - 1}"
            : $"{pixelWidth}×{pixelHeight} 像素　L {_settings.Left}  R {_settings.Right}  T {_settings.Top}  B {_settings.Bottom}　拖动绿线调整";
        var brush = invalid ? ErrorBrush : HintBrush;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11.0,
            brush);
        context.DrawText(formatted, new Point(10, view.Height - 18));
    }

    /// <summary>空图时在预览中心放一行说明。</summary>
    private static void DrawCenteredHint(DrawingContext context, Rect view, string text)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11.0,
            HintBrush);
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
        var leftX = _imageRect.X + Math.Clamp(_settings.Left, 0, pixelWidth) * _imageScale;
        var rightX = _imageRect.Right - Math.Clamp(_settings.Right, 0, pixelWidth) * _imageScale;
        var topY = _imageRect.Y + Math.Clamp(_settings.Top, 0, pixelHeight) * _imageScale;
        var bottomY = _imageRect.Bottom - Math.Clamp(_settings.Bottom, 0, pixelHeight) * _imageScale;

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
