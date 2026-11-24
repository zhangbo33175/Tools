/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlaybackSpeedSlider.cs
 * author:    云毅
 * created:   2026
 * descrip:   预览窗用的自绘速度滑动条
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MapEditorTool.Controls;

#region 速度滑动条

/// <summary>
/// 预览窗速度滑动条。相比主题 Slider，它使用更大的滑块与更宽的可拖动轨道，
/// 在预览窗的小尺寸工具栏里更容易拖动；值变化通过 <see cref="ValueChanged"/> 回传。
/// </summary>
public sealed class PlaybackSpeedSlider : Control
{
    #region 常量与画刷

    private const double TrackHeight = 5.0;
    private const double ThumbRadius = 7.0;
    private const double TrackRadius = 2.5;

    /// <summary>滑块描边，颜色跟随主题强调蓝，粗细属于控件几何参数。</summary>
    private static readonly IPen ThumbPen = new Pen(UiTheme.PlaybackSlider.Fill, 2.0);

    #endregion

    #region 属性

    /// <summary>最小值。</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<PlaybackSpeedSlider, double>(nameof(Minimum), 0.25);

    /// <summary>最大值。</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<PlaybackSpeedSlider, double>(nameof(Maximum), 3.0);

    /// <summary>当前值。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PlaybackSpeedSlider, double>(nameof(Value), 1.0);

    /// <summary>最小值。</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>最大值。</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>当前值。</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    /// <summary>值变化回调，参数为新的速度倍率。</summary>
    public event Action<double>? ValueChanged;

    #endregion

    #region 字段

    private bool _dragging;

    #endregion

    #region 构造

    /// <summary>构造速度滑动条。</summary>
    public PlaybackSpeedSlider()
    {
        Height = 24;
        MinHeight = 24;
        Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, "拖动改变预览播放速度");
        AutomationProperties.SetName(this, "播放速度");

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragging = false;
    }

    #endregion

    #region 绘制

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        if (width <= ThumbRadius * 2)
        {
            return;
        }

        // 透明底层保证整条控件矩形都可命中，避免只有 5px 轨道能拖动。
        context.DrawRectangle(UiTheme.Transparent, null, new Rect(Bounds.Size));

        var trackTop = (Bounds.Height - TrackHeight) * 0.5;
        var track = new Rect(ThumbRadius, trackTop, width - ThumbRadius * 2, TrackHeight);
        context.DrawRectangle(
            IsEnabled ? UiTheme.PlaybackSlider.Track : UiTheme.PlaybackSlider.DisabledTrack,
            null,
            track,
            TrackRadius,
            TrackRadius);

        var ratio = Ratio();
        var fillWidth = Math.Max(TrackHeight, track.Width * ratio);
        context.DrawRectangle(
            IsEnabled ? UiTheme.PlaybackSlider.Fill : UiTheme.PlaybackSlider.DisabledFill,
            null,
            new Rect(track.X, track.Y, fillWidth, TrackHeight),
            TrackRadius,
            TrackRadius);

        var thumbCenter = new Point(track.X + track.Width * ratio, Bounds.Height * 0.5);
        context.DrawEllipse(
            IsEnabled ? UiTheme.PlaybackSlider.Thumb : UiTheme.PlaybackSlider.DisabledTrack,
            IsEnabled ? ThumbPen : null,
            thumbCenter,
            ThumbRadius,
            ThumbRadius);
    }

    #endregion

    #region 交互

    /// <summary>按下轨道或滑块时立即吸附并开始拖动。</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(this);
        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    /// <summary>按住后跟随指针横坐标更新速度。</summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is false)
        {
            return;
        }

        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    /// <summary>松开或失去捕获时结束拖动。</summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is false)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>把指针 X 映射为值。</summary>
    private void SetFromPointer(double x)
    {
        var usable = Math.Max(1.0, Bounds.Width - ThumbRadius * 2);
        var ratio = Math.Clamp((x - ThumbRadius) / usable, 0.0, 1.0);
        var range = Math.Max(0.0001, Maximum - Minimum);
        var value = Minimum + ratio * range;
        Value = Math.Round(value * 100.0) / 100.0;
    }

    /// <summary>当前值在 0–1 区间的比例。</summary>
    private double Ratio()
    {
        var range = Maximum - Minimum;
        return range <= 0.0001
            ? 0
            : Math.Clamp((Value - Minimum) / range, 0.0, 1.0);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            InvalidateVisual();
            ValueChanged?.Invoke(Value);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PlaybackSpeedSliderAutomationPeer(this);
    }

    #endregion
}

#region 自动化对等体

/// <summary>
/// 暴露 RangeValue 模式，让 UI 自动化（UIA）可以读写速度值，
/// 也方便辅助技术识别这是一个滑条而不是普通自定义控件。
/// </summary>
internal sealed class PlaybackSpeedSliderAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    private readonly PlaybackSpeedSlider _owner;

    public PlaybackSpeedSliderAutomationPeer(PlaybackSpeedSlider owner)
        : base(owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    public bool IsReadOnly => _owner.IsEnabled is false;

    /// <inheritdoc />
    public double LargeChange => 0.5;

    /// <inheritdoc />
    public double Maximum => _owner.Maximum;

    /// <inheritdoc />
    public double Minimum => _owner.Minimum;

    /// <inheritdoc />
    public double SmallChange => 0.05;

    /// <inheritdoc />
    public double Value => _owner.Value;

    /// <inheritdoc />
    public void SetValue(double value)
    {
        if (IsReadOnly)
        {
            return;
        }

        var oldValue = _owner.Value;
        _owner.Value = value;
        RaisePropertyChangedEvent(
            RangeValuePatternIdentifiers.ValueProperty,
            oldValue,
            _owner.Value);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Slider;
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(PlaybackSpeedSlider);
    }
}

#endregion

#endregion
