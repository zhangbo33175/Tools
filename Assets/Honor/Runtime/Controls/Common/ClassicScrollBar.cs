/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ClassicScrollBar.cs
 * author:    云毅
 * created:   2026
 * descrip:   经典风格竖向滚动条：箭头按钮 + 可拖拽滑块
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 经典风格竖向滚动条：常显的上下箭头按钮 + 可拖拽滑块，绑定到一个 <see cref="ScrollViewer"/>。
/// 与系统滚动条不同，它始终占位显示（列表没有滚动内容时滑块铺满整条），
/// 用于给左侧章节列表、资源列表这类「看起来不可滚」的区域补上明确的滚动暗示。
/// </summary>
public sealed class ClassicScrollBar : UserControl
{
    #region 常量

    /// <summary>箭头按钮高度。</summary>
    private const double ArrowHeight = 15;

    /// <summary>滑块最小高度，避免超长列表里滑块细到没法拖。</summary>
    private const double MinThumbHeight = 26;

    /// <summary>滚动条总宽度。</summary>
    public const double BarWidth = 14;

    /// <summary>点击箭头一次滚动的像素。</summary>
    private const double LineStep = 48;

    #endregion

    #region 依赖属性

    /// <summary>被驱动的滚动视图。</summary>
    public static readonly StyledProperty<ScrollViewer?> TargetProperty =
        AvaloniaProperty.Register<ClassicScrollBar, ScrollViewer?>(nameof(Target));

    /// <summary>被驱动的滚动视图。</summary>
    public ScrollViewer? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    #endregion

    #region 字段

    private readonly Border _thumb;
    private readonly Panel _track;
    private readonly RepeatButton _upButton;
    private readonly RepeatButton _downButton;

    private ScrollViewer? _hooked;
    private bool _draggingThumb;
    private double _dragStartPointerY;
    private double _dragStartOffsetY;

    #endregion

    /// <summary>构造一个经典竖向滚动条。</summary>
    public ClassicScrollBar()
    {
        Width = BarWidth;
        MinWidth = BarWidth;

        _thumb = new Border
        {
            Height = MinThumbHeight,
            MinHeight = MinThumbHeight,
            CornerRadius = new CornerRadius(2),
            Background = UiTheme.ScrollBar.ThumbFill,
            BorderBrush = UiTheme.ScrollBar.ThumbBorder,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand),
            IsHitTestVisible = true,
        };

        _track = new Panel
        {
            Margin = new Thickness(2, 2),
            ClipToBounds = true,
            Background = UiTheme.ScrollBar.Track,
        };
        _track.Children.Add(_thumb);

        _upButton = CreateArrowButton(isUp: true);
        _downButton = CreateArrowButton(isUp: false);

        var rows = new Grid
        {
            RowDefinitions = new RowDefinitions($"{ArrowHeight},*,{ArrowHeight}"),
        };
        Grid.SetRow(_upButton, 0);
        Grid.SetRow(_track, 1);
        Grid.SetRow(_downButton, 2);
        rows.Children.Add(_upButton);
        rows.Children.Add(_track);
        rows.Children.Add(_downButton);

        Content = new Border
        {
            Background = UiTheme.ScrollBar.Surface,
            BorderBrush = UiTheme.ScrollBar.Border,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = rows,
        };

        WireInteraction();
    }

    #region 交互

    /// <summary>绑定箭头、滑块拖拽与轨道翻页。</summary>
    private void WireInteraction()
    {
        _upButton.Click += (_, _) => ScrollBy(-LineStep);
        _downButton.Click += (_, _) => ScrollBy(LineStep);

        _track.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(_track).Properties.IsLeftButtonPressed is false)
            {
                return;
            }

            var y = e.GetPosition(_track).Y;
            var thumbTop = _thumb.Margin.Top;
            var page = _hooked?.Viewport.Height ?? LineStep * 2;
            ScrollBy(y < thumbTop ? -page : page);
            e.Handled = true;
        };

        _thumb.PointerPressed += (_, e) =>
        {
            if (_hooked is null || e.GetCurrentPoint(_thumb).Properties.IsLeftButtonPressed is false)
            {
                return;
            }

            _draggingThumb = true;
            _dragStartPointerY = e.GetPosition(this).Y;
            _dragStartOffsetY = _hooked.Offset.Y;

            // 用滚动条自身接住指针：滑块被指针盖住时，移动事件仍会派发到捕获者，拖拽不会丢
            e.Pointer.Capture(this);
            e.Handled = true;
        };

        PointerMoved += (_, e) => DragThumb(e.GetPosition(this).Y);
        PointerReleased += (_, e) => EndDrag(e);

        _track.SizeChanged += (_, _) => UpdateThumb();
    }

    /// <summary>按像素滚动。</summary>
    private void ScrollBy(double delta)
    {
        if (_hooked is null)
        {
            return;
        }

        SetOffsetY(_hooked, _hooked.Offset.Y + delta);
    }

    /// <summary>拖拽滑块：把指针位移按「滑块可走行程 / 可滚距离」换算成新的偏移。</summary>
    /// <param name="pointerY">指针在当前滚动条坐标系里的 Y。</param>
    private void DragThumb(double pointerY)
    {
        if (_draggingThumb is false || _hooked is null)
        {
            return;
        }

        var freeTrack = _track.Bounds.Height - _thumb.Height;
        var scrollable = ScrollableHeight(_hooked);
        if (freeTrack <= 0 || scrollable <= 0)
        {
            return;
        }

        var delta = pointerY - _dragStartPointerY;
        SetOffsetY(_hooked, _dragStartOffsetY + delta / freeTrack * scrollable);
    }

    /// <summary>结束拖拽并释放指针捕获。</summary>
    /// <param name="e">释放事件参数。</param>
    private void EndDrag(PointerReleasedEventArgs e)
    {
        if (_draggingThumb is false)
        {
            return;
        }

        _draggingThumb = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>设置纵向偏移并夹紧到合法区间。</summary>
    private static void SetOffsetY(ScrollViewer viewer, double offsetY)
    {
        var max = ScrollableHeight(viewer);
        var target = Math.Clamp(offsetY, 0, max);
        viewer.Offset = new Vector(viewer.Offset.X, target);
    }

    /// <summary>可滚动的纵向距离。</summary>
    private static double ScrollableHeight(ScrollViewer viewer)
        => Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);

    #endregion

    #region 滑块同步

    /// <summary>刷新滑块尺寸与位置。</summary>
    private void UpdateThumb()
    {
        var viewer = _hooked;
        if (viewer is null)
        {
            _thumb.IsVisible = false;
            return;
        }

        var trackHeight = _track.Bounds.Height;
        if (trackHeight <= 0)
        {
            return;
        }

        var extent = viewer.Extent.Height;
        var viewport = viewer.Viewport.Height;

        // 没有内容或没有视口时，滑块铺满整条，表示「整段内容都看得见」
        _thumb.IsVisible = true;
        if (extent <= 0 || viewport <= 0 || extent <= viewport)
        {
            _thumb.Height = trackHeight;
            _thumb.Margin = new Thickness(0, 0, 0, 0);
            return;
        }

        var thumbHeight = Math.Clamp(trackHeight * viewport / extent, MinThumbHeight, trackHeight);
        _thumb.Height = thumbHeight;

        var scrollable = ScrollableHeight(viewer);
        var freeTrack = trackHeight - thumbHeight;
        var top = scrollable <= 0 || freeTrack <= 0
            ? 0
            : Math.Clamp(viewer.Offset.Y / scrollable, 0, 1) * freeTrack;
        _thumb.Margin = new Thickness(0, top, 0, 0);
    }

    /// <summary>挂接滚动视图的偏移 / 视口 / 范围变化。</summary>
    private void Attach(ScrollViewer? viewer)
    {
        if (_hooked is not null)
        {
            _hooked.PropertyChanged -= OnTargetPropertyChanged;
            _hooked.SizeChanged -= OnTargetSizeChanged;
        }

        _hooked = viewer;

        if (_hooked is not null)
        {
            _hooked.PropertyChanged += OnTargetPropertyChanged;
            _hooked.SizeChanged += OnTargetSizeChanged;
        }

        UpdateThumb();
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty
            || e.Property == ScrollViewer.ExtentProperty
            || e.Property == ScrollViewer.ViewportProperty)
        {
            UpdateThumb();
        }
    }

    private void OnTargetSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateThumb();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TargetProperty)
        {
            Attach(change.GetNewValue<ScrollViewer?>());
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 控件本身尺寸变化也要重算滑块（例如面板被拉伸）
        SizeChanged += (_, _) => UpdateThumb();
        UpdateThumb();
    }

    #endregion

    #region 箭头按钮

    /// <summary>构造一个带三角箭头的重复按钮。</summary>
    private static RepeatButton CreateArrowButton(bool isUp)
    {
        var geometry = isUp
            ? Geometry.Parse("M 0,4 L 4,0 L 8,4 Z")
            : Geometry.Parse("M 0,0 L 8,0 L 4,4 Z");

        var arrow = new Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            Fill = UiTheme.ScrollBar.Arrow,
            Width = 8,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new RepeatButton
        {
            Height = ArrowHeight,
            MinHeight = ArrowHeight,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Background = UiTheme.ScrollBar.ArrowSurface,
            BorderBrush = UiTheme.ScrollBar.ArrowBorder,
            BorderThickness = new Thickness(0, isUp ? 0 : 1, 0, isUp ? 1 : 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = arrow,
        };
    }

    #endregion
}
