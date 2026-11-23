/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ScrollRail.cs
 * author:    云毅
 * created:   2026
 * descrip:   左侧列表用的自绘竖向滚动条
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MapEditorTool.Controls;

#region 竖向滚动条

/// <summary>
/// 预览窗左侧列表的竖向滚动条。Avalonia Fluent 主题的滚动条在长列表里会被压缩成
/// 几像素高的滑块，鼠标很难抓住；本控件使用固定 14×28 以上的命中区，并把拖动位置
/// 直接映射到目标 <see cref="ScrollViewer"/> 的纵向偏移。
/// </summary>
public sealed class ScrollRail : Control
{
    #region 常量与画刷

    private const double TrackWidth = 4.0;
    private const double ThumbWidth = 10.0;
    private const double MinThumbHeight = 28.0;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xEC, 0xF2));
    private static readonly IBrush ThumbBrush = new SolidColorBrush(Color.FromRgb(0xB7, 0xC8, 0xD6));
    private static readonly IBrush ThumbHoverBrush = new SolidColorBrush(Color.FromRgb(0x8F, 0xB4, 0xC9));
    private static readonly IBrush ThumbActiveBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x87, 0xBE));

    #endregion

    #region 字段

    private ScrollViewer? _target;
    private bool _dragging;
    private double _thumbHeight = MinThumbHeight;

    #endregion

    #region 构造

    /// <summary>构造竖向滚动条。</summary>
    public ScrollRail()
    {
        Width = 14;
        MinWidth = 14;
        Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, "拖动滚动场景物体列表");
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        PointerEntered += (_, _) => InvalidateVisual();
        PointerExited += (_, _) => InvalidateVisual();
        PointerCaptureLost += (_, _) =>
        {
            _dragging = false;
            InvalidateVisual();
        };
        SizeChanged += (_, _) => InvalidateVisual();
    }

    #endregion

    #region 绑定

    /// <summary>绑定到列表内部的滚动容器。</summary>
    public void Attach(ScrollViewer target)
    {
        if (ReferenceEquals(_target, target))
        {
            return;
        }

        Detach();
        _target = target;
        _target.PropertyChanged += OnTargetPropertyChanged;
        SyncFromTarget();
    }

    /// <summary>解除绑定。</summary>
    public void Detach()
    {
        if (_target is null)
        {
            return;
        }

        _target.PropertyChanged -= OnTargetPropertyChanged;
        _target = null;
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty ||
            e.Property == ScrollViewer.ExtentProperty ||
            e.Property == ScrollViewer.ViewportProperty)
        {
            SyncFromTarget();
        }
    }

    /// <summary>根据列表的当前偏移 / 内容高度 / 视口高度刷新滑块。</summary>
    private void SyncFromTarget()
    {
        if (_target is null)
        {
            return;
        }

        var extent = _target.Extent.Height;
        var viewport = _target.Viewport.Height;
        IsVisible = extent > viewport + 1 && Bounds.Height > 0;
        InvalidateVisual();
    }

    #endregion

    #region 绘制

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_target is null || Bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        var trackHeight = Bounds.Height;
        var track = new Rect(
            (Bounds.Width - TrackWidth) * 0.5,
            0,
            TrackWidth,
            trackHeight);
        context.DrawRectangle(TrackBrush, null, track, TrackWidth * 0.5, TrackWidth * 0.5);

        var extent = Math.Max(1, _target.Extent.Height);
        var viewport = Math.Max(1, _target.Viewport.Height);
        var ratio = Math.Clamp(viewport / extent, 0, 1);
        _thumbHeight = Math.Clamp(trackHeight * ratio, MinThumbHeight, trackHeight);

        var maxOffset = Math.Max(1, extent - viewport);
        var value = Math.Clamp(_target.Offset.Y / maxOffset, 0, 1);
        var thumbTop = (trackHeight - _thumbHeight) * value;
        var thumb = new Rect(
            (Bounds.Width - ThumbWidth) * 0.5,
            thumbTop,
            ThumbWidth,
            _thumbHeight);
        var brush = _dragging
            ? ThumbActiveBrush
            : IsPointerOver
                ? ThumbHoverBrush
                : ThumbBrush;
        context.DrawRectangle(brush, null, thumb, 5.0, 5.0);
    }

    #endregion

    #region 交互

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(this);
        SetFromY(e.GetPosition(this).Y);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is false)
        {
            return;
        }

        SetFromY(e.GetPosition(this).Y);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is false)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_target is null)
        {
            return;
        }

        var maxOffset = Math.Max(0, _target.Extent.Height - _target.Viewport.Height);
        if (maxOffset <= 0)
        {
            return;
        }

        var step = Math.Max(24, _target.Viewport.Height * 0.12);
        var offset = Math.Clamp(_target.Offset.Y - e.Delta.Y * step, 0, maxOffset);
        _target.Offset = new Vector(_target.Offset.X, offset);
        e.Handled = true;
    }

    /// <summary>按指针 Y 计算滑块中心，并同步滚动目标。</summary>
    private void SetFromY(double y)
    {
        if (_target is null || Bounds.Height <= MinThumbHeight)
        {
            return;
        }

        var extent = Math.Max(1, _target.Extent.Height);
        var viewport = Math.Max(1, _target.Viewport.Height);
        var ratio = Math.Clamp(viewport / extent, 0, 1);
        var thumbHeight = Math.Clamp(Bounds.Height * ratio, MinThumbHeight, Bounds.Height);

        var usable = Math.Max(1, Bounds.Height - thumbHeight);
        var value = Math.Clamp((y - thumbHeight * 0.5) / usable, 0, 1);
        var maxOffset = Math.Max(1, extent - viewport);
        _target.Offset = new Vector(_target.Offset.X, value * maxOffset);
        InvalidateVisual();
    }

    #endregion
}

#endregion
