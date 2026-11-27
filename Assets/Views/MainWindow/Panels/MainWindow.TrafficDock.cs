using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MapEditorTool;

/// <summary>
/// 交通规则悬浮窗：默认浮在画布上，标题栏拖动，靠近画布左 / 右边缘吸附停靠，
/// 停靠后可拖手柄调宽。和收集物面板同一套交互，但面板本身独立、不占主工作区。
/// </summary>
public partial class MainWindow
{
    private void WireTrafficRuleDock()
    {
        TrafficRulePanelHost.Width = TrafficPanelWidth;
        TrafficRulePanelHost.Height = TrafficPanelFloatHeight;

        TrafficRuleDragArea.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(TrafficRuleDragArea).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var point = e.GetPosition(TrafficRuleOverlay);
            _trafficDragging = true;
            _trafficDragMoved = false;
            _trafficDragOffset = new Point(
                point.X - Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost),
                point.Y - Avalonia.Controls.Canvas.GetTop(TrafficRulePanelHost));
            e.Pointer.Capture(TrafficRuleDragArea);
            e.Handled = true;
        };

        TrafficRuleDragArea.PointerCaptureLost += (_, _) =>
        {
            _trafficDragging = false;
            _trafficDragMoved = false;
            TrafficRuleDockHint.IsVisible = false;
        };

        TrafficRuleDragArea.PointerMoved += (_, e) =>
        {
            if (!_trafficDragging
                || !e.GetCurrentPoint(TrafficRuleDragArea).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            var point = e.GetPosition(TrafficRuleOverlay);
            if (!_trafficDragMoved
                && Math.Abs(point.X - _trafficDragOffset.X - Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost)) < 4
                && Math.Abs(point.Y - _trafficDragOffset.Y - Avalonia.Controls.Canvas.GetTop(TrafficRulePanelHost)) < 4)
            {
                return;
            }

            if (!_trafficDragMoved)
            {
                _trafficDragMoved = true;
                UndockTrafficPanelForDrag();
            }

            var areaWidth = TrafficOverlayWidth();
            var areaHeight = TrafficOverlayHeight();
            var left = Math.Clamp(point.X - _trafficDragOffset.X, 8,
                Math.Max(8, areaWidth - TrafficRulePanelHost.Width - 8));
            var top = Math.Clamp(point.Y - _trafficDragOffset.Y, 8,
                Math.Max(8, areaHeight - TrafficRulePanelHost.Height - 8));
            Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost, left);
            Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost, top);

            var side = left <= CollectibleDockSnapThreshold
                ? CollectibleDockSide.Left
                : left + TrafficRulePanelHost.Width >= areaWidth - CollectibleDockSnapThreshold
                    ? CollectibleDockSide.Right
                    : CollectibleDockSide.None;
            ShowTrafficDockHint(side);
        };

        TrafficRuleDragArea.PointerReleased += (_, e) =>
        {
            if (!_trafficDragging)
            {
                return;
            }

            e.Handled = true;
            var moved = _trafficDragMoved;
            _trafficDragging = false;
            _trafficDragMoved = false;
            e.Pointer.Capture(null);
            TrafficRuleDockHint.IsVisible = false;

            if (!moved)
            {
                return;
            }

            var areaWidth = TrafficOverlayWidth();
            var areaHeight = TrafficOverlayHeight();
            var left = Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost);
            var top = Avalonia.Controls.Canvas.GetTop(TrafficRulePanelHost);
            if (left <= CollectibleDockSnapThreshold)
            {
                DockTrafficPanel(CollectibleDockSide.Left);
            }
            else if (left + TrafficRulePanelHost.Width >= areaWidth - CollectibleDockSnapThreshold)
            {
                DockTrafficPanel(CollectibleDockSide.Right);
            }
            else
            {
                _trafficDockSide = CollectibleDockSide.None;
                Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost, Math.Max(8, left));
                Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost,
                    Math.Clamp(top, 8, Math.Max(8, areaHeight - TrafficRulePanelHost.Height - 8)));
                UpdateTrafficPanelChrome();
            }
        };

        TrafficRuleResizeHandle.PointerPressed += (_, e) =>
        {
            if (_trafficDockSide == CollectibleDockSide.None
                || !e.GetCurrentPoint(TrafficRuleResizeHandle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _trafficResizing = true;
            _trafficResizeStartX = e.GetPosition(TrafficRuleOverlay).X;
            _trafficResizeStartWidth = TrafficRulePanelHost.Width;
            e.Pointer.Capture(TrafficRuleResizeHandle);
            e.Handled = true;
        };

        TrafficRuleResizeHandle.PointerCaptureLost += (_, _) => _trafficResizing = false;

        TrafficRuleResizeHandle.PointerMoved += (_, e) =>
        {
            if (!_trafficResizing
                || !e.GetCurrentPoint(TrafficRuleResizeHandle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            var delta = e.GetPosition(TrafficRuleOverlay).X - _trafficResizeStartX;
            var areaWidth = TrafficOverlayWidth();
            var wanted = _trafficDockSide == CollectibleDockSide.Right
                ? _trafficResizeStartWidth - delta
                : _trafficResizeStartWidth + delta;
            var max = Math.Min(TrafficPanelMaxDockWidth, Math.Max(TrafficPanelMinDockWidth, areaWidth - 220));
            var width = Math.Clamp(wanted, TrafficPanelMinDockWidth, max);
            TrafficRulePanelHost.Width = width;
            _trafficPanelDockWidth = width;
            if (_trafficDockSide == CollectibleDockSide.Right)
            {
                Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost, Math.Max(0, areaWidth - width));
            }
        };

        BtnTrafficDock.Click += (_, _) =>
        {
            if (_trafficDockSide != CollectibleDockSide.None)
            {
                FloatTrafficPanel();
                SetMessage("交通规则已改为浮动窗；拖动标题栏可移动，靠近边缘会吸附停靠");
                return;
            }

            var areaWidth = TrafficOverlayWidth();
            var center = Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost) + TrafficRulePanelHost.Width / 2;
            DockTrafficPanel(center <= areaWidth / 2
                ? CollectibleDockSide.Left
                : CollectibleDockSide.Right);
            SetMessage("交通规则已停靠；再次点「停靠」可恢复浮动");
        };

        BtnTrafficClose.Click += (_, _) =>
        {
            HideTrafficRuleDock();
            SetMessage("交通规则面板已关闭；已配置的规则与地图数据仍保留");
        };

        TrafficRuleOverlay.SizeChanged += (_, _) => RelayoutTrafficRuleDock();
    }

    /// <summary>顶栏「交通规则」入口：打开悬浮窗并回到场景编辑，保证地图同屏可见。</summary>
    private void OpenTrafficRuleDock()
    {
        if (_config is null)
        {
            SetMessage("请先打开或新建一张地图，再编辑交通规则");
            return;
        }

        TrafficRuleOverlay.IsVisible = true;
        TrafficRulePanelHost.IsVisible = true;
        _trafficPanel?.RefreshAll();
        UpdateActiveTabVisual();

        if (_trafficDockSide == CollectibleDockSide.None)
        {
            var areaWidth = TrafficOverlayWidth();
            var width = Math.Min(_trafficPanelDockWidth, Math.Max(320, areaWidth - 32));
            var height = Math.Min(TrafficPanelFloatHeight, Math.Max(300, TrafficOverlayHeight() - 40));
            TrafficRulePanelHost.Width = width;
            TrafficRulePanelHost.Height = height;
            Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost, Math.Max(16, areaWidth - width - 24));
            Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost, 16);
            UpdateTrafficPanelChrome();

            // 首次打开时画布可能还没量好尺寸；下一帧按真实可用高度再收一次，
            // 否则面板会顶到画布底边被裁掉圆角与最后一张卡片。
            Dispatcher.UIThread.Post(RelayoutTrafficRuleDock, DispatcherPriority.Loaded);
        }
        else
        {
            DockTrafficPanel(_trafficDockSide);
        }
    }

    private void HideTrafficRuleDock()
    {
        TrafficRulePanelHost.IsVisible = false;
        TrafficRuleOverlay.IsVisible = false;
        TrafficRuleDockHint.IsVisible = false;
        UpdateActiveTabVisual();
    }

    private void DockTrafficPanel(CollectibleDockSide side)
    {
        _trafficDockSide = side;
        var areaWidth = TrafficOverlayWidth();
        var areaHeight = TrafficOverlayHeight();
        var maxWidth = Math.Min(TrafficPanelMaxDockWidth, Math.Max(240, areaWidth - 200));
        var width = Math.Clamp(_trafficPanelDockWidth, TrafficPanelMinDockWidth, maxWidth);
        _trafficPanelDockWidth = width;
        TrafficRulePanelHost.Width = width;
        TrafficRulePanelHost.Height = Math.Max(260, areaHeight);
        Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost, 0);
        Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost,
            side == CollectibleDockSide.Right ? Math.Max(0, areaWidth - width) : 0);
        TrafficRulePanelHost.CornerRadius = new CornerRadius(0);
        TrafficRulePanelHost.BoxShadow = default;
        TrafficRulePanelHost.BorderThickness = side == CollectibleDockSide.Right
            ? new Thickness(1, 0, 0, 0)
            : new Thickness(0, 0, 1, 0);
        TrafficRuleResizeHandle.HorizontalAlignment = side == CollectibleDockSide.Right
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        TrafficRuleResizeHandle.IsVisible = true;
        TrafficRuleDockHint.IsVisible = false;
        UpdateTrafficPanelChrome();
        UpdateActiveTabVisual();
    }

    private void FloatTrafficPanel()
    {
        _trafficDockSide = CollectibleDockSide.None;
        var areaWidth = TrafficOverlayWidth();
        var areaHeight = TrafficOverlayHeight();
        var width = Math.Min(_trafficPanelDockWidth, Math.Max(320, areaWidth - 32));
        var height = Math.Min(TrafficPanelFloatHeight, Math.Max(320, areaHeight - 32));
        TrafficRulePanelHost.Width = width;
        TrafficRulePanelHost.Height = height;
        TrafficRulePanelHost.CornerRadius = new CornerRadius(4);
        TrafficRulePanelHost.BoxShadow = TrafficPanelShadow;
        TrafficRulePanelHost.BorderThickness = new Thickness(1);
        Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost, Math.Max(8,
            Math.Min(Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost), areaWidth - width - 8)));
        Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost, Math.Max(8,
            Math.Min(Avalonia.Controls.Canvas.GetTop(TrafficRulePanelHost), areaHeight - height - 8)));
        TrafficRuleResizeHandle.IsVisible = false;
        UpdateTrafficPanelChrome();
        UpdateActiveTabVisual();
    }

    /// <summary>拖出停靠：保留当前宽度，恢复浮动外观并钳制到画布内。</summary>
    private void UndockTrafficPanelForDrag()
    {
        if (_trafficDockSide == CollectibleDockSide.None)
        {
            return;
        }

        _trafficDockSide = CollectibleDockSide.None;
        TrafficRulePanelHost.CornerRadius = new CornerRadius(4);
        TrafficRulePanelHost.BoxShadow = TrafficPanelShadow;
        TrafficRulePanelHost.BorderThickness = new Thickness(1);
        TrafficRulePanelHost.Height = Math.Min(TrafficPanelFloatHeight,
            Math.Max(320, TrafficOverlayHeight() - 32));
        TrafficRuleResizeHandle.IsVisible = false;
        UpdateTrafficPanelChrome();
    }

    private void RelayoutTrafficRuleDock()
    {
        if (!TrafficRulePanelHost.IsVisible)
        {
            return;
        }

        var areaWidth = TrafficOverlayWidth();
        var areaHeight = TrafficOverlayHeight();
        if (_trafficDockSide == CollectibleDockSide.None)
        {
            var width = Math.Min(TrafficRulePanelHost.Width, Math.Max(320, areaWidth - 16));
            var height = Math.Min(TrafficRulePanelHost.Height, Math.Max(260, areaHeight - 24));
            TrafficRulePanelHost.Width = width;
            TrafficRulePanelHost.Height = height;
            Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost,
                Math.Clamp(Avalonia.Controls.Canvas.GetLeft(TrafficRulePanelHost), 8, Math.Max(8, areaWidth - width - 8)));
            Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost,
                Math.Clamp(Avalonia.Controls.Canvas.GetTop(TrafficRulePanelHost), 8, Math.Max(8, areaHeight - height - 8)));
            return;
        }

        var maxWidth = Math.Min(TrafficPanelMaxDockWidth, Math.Max(240, areaWidth - 200));
        var dockWidth = Math.Clamp(TrafficRulePanelHost.Width, TrafficPanelMinDockWidth, maxWidth);
        TrafficRulePanelHost.Width = dockWidth;
        TrafficRulePanelHost.Height = Math.Max(260, areaHeight);
        Avalonia.Controls.Canvas.SetTop(TrafficRulePanelHost, 0);
        Avalonia.Controls.Canvas.SetLeft(TrafficRulePanelHost,
            _trafficDockSide == CollectibleDockSide.Right ? Math.Max(0, areaWidth - dockWidth) : 0);
    }

    private void UpdateTrafficPanelChrome()
    {
        TrafficRuleHint.Text = _trafficDockSide switch
        {
            CollectibleDockSide.Left => "已停靠左侧 · 拖标题栏可移出",
            CollectibleDockSide.Right => "已停靠右侧 · 拖标题栏可移出",
            _ => "拖到画布左右边缘可停靠",
        };
        BtnTrafficDock.Content = _trafficDockSide == CollectibleDockSide.None ? "停靠" : "浮动";
    }

    private void ShowTrafficDockHint(CollectibleDockSide side)
    {
        if (side == CollectibleDockSide.None)
        {
            TrafficRuleDockHint.IsVisible = false;
            return;
        }

        var areaWidth = TrafficOverlayWidth();
        var areaHeight = TrafficOverlayHeight();
        TrafficRuleDockHint.Width = 4;
        TrafficRuleDockHint.Height = areaHeight;
        Avalonia.Controls.Canvas.SetTop(TrafficRuleDockHint, 0);
        Avalonia.Controls.Canvas.SetLeft(TrafficRuleDockHint,
            side == CollectibleDockSide.Left ? 0 : Math.Max(0, areaWidth - 4));
        TrafficRuleDockHint.IsVisible = true;
    }

    private double TrafficOverlayWidth()
    {
        var width = TrafficRuleOverlay.Bounds.Width;
        if (width <= 0)
        {
            width = MapCanvasHost.Bounds.Width;
        }
        return width > 0 ? width : 1160;
    }

    private double TrafficOverlayHeight()
    {
        var height = TrafficRuleOverlay.Bounds.Height;
        if (height <= 0)
        {
            height = MapCanvasHost.Bounds.Height;
        }
        return height > 0 ? height : 640;
    }
}

