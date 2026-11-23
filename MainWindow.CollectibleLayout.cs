/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.CollectibleLayout.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口收集物浮窗布局：显示/隐藏、停靠、拖动与边界约束。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{

    /// <summary>收集物是否已有计划或实例；决定顶部入口显示“添加收集”还是“收集物”。</summary>
    private bool HasCollectibleData()
    {
        return _config.Collectibles.Kinds.Count > 0
            || _config.Objects.Any(item => item.IsCollectible);
    }

    /// <summary>
    /// 同步顶部收集物入口和浮窗：没有计划/实例时隐藏面板；已有数据时刷新内容。
    /// 左侧资源栏不再承载收集物，面板位置由用户拖动或吸附决定。
    /// </summary>
    private void SyncCollectibleEntry()
    {
        var hasData = RefreshCollectibleEntry();
        if (!hasData)
        {
            return;
        }

        if (CollectiblePanelHost.IsVisible)
        {
            BuildCollectiblePanelList();
        }
    }

    /// <summary>
    /// 只刷新顶部入口文案和面板显隐，不重建列表。
    /// DocumentChanged 会频繁触发，重建列表会打断拖拽和选中态。
    /// </summary>
    private bool RefreshCollectibleEntry()
    {
        var hasData = HasCollectibleData();
        BtnAddCollectible.Content = hasData ? "收集物" : "添加收集";
        ToolTip.SetTip(BtnAddCollectible, hasData
            ? "打开 / 收起收集物面板；面板内「重设」可重新配置目标和素材"
            : "两步向导：选择目标数量与收集物素材；完成后在画布右侧打开收集物面板");

        if (!hasData)
        {
            HideCollectiblePanel();
        }

        return hasData;
    }

    /// <summary>显示收集物面板；默认浮出，可拖动到左 / 右边缘停靠，停靠时画布让位。</summary>
    private void ShowCollectiblePanel(CollectibleDockSide side)
    {
        if (!HasCollectibleData())
        {
            return;
        }

        CollectiblePanelHost.IsVisible = true;
        BuildCollectiblePanelList();
        Dispatcher.UIThread.Post(() =>
        {
            if (side == CollectibleDockSide.None)
            {
                FloatCollectiblePanel();
            }
            else
            {
                DockCollectiblePanel(side);
            }
        });
    }

    /// <summary>收起面板只影响 UI，不删除收集计划；同时恢复画布宽度并退出待摆放状态。</summary>
    private void HideCollectiblePanel()
    {
        CollectiblePanelHost.IsVisible = false;
        _collectiblePanelDockSide = CollectibleDockSide.None;
        _collectiblePanelDragging = false;
        _collectiblePanelDragMoved = false;
        _collectiblePanelResizing = false;
        CollectibleDockHint.IsVisible = false;
        ApplyFloatingPanelStyle();
        MapCanvasHost.Margin = new Thickness(0);
        Canvas.PendingCollectibleTemplate = null;
    }

    private void ToggleCollectiblePanel()
    {
        if (CollectiblePanelHost.IsVisible)
        {
            HideCollectiblePanel();
            SetMessage("收集物面板已收起；再次点顶部「收集物」可打开。");
        }
        else
        {
            ShowCollectiblePanel(CollectibleDockSide.None);
        }
    }

    /// <summary>浮到画布右侧，保留标题栏和阴影，模拟 TextureSettings 那种工具浮窗。</summary>
    private void FloatCollectiblePanel()
    {
        if (!CollectiblePanelHost.IsVisible)
        {
            return;
        }

        _collectiblePanelDockSide = CollectibleDockSide.None;
        ApplyFloatingPanelStyle();
        MapCanvasHost.Margin = new Thickness(0);

        var areaWidth = OverlayWidth();
        var areaHeight = OverlayHeight();
        var width = Math.Min(CollectiblePanelWidth, Math.Max(300, areaWidth - 20));
        var preferredHeight = CollectiblePanelPreferredHeight();
        var height = Math.Min(preferredHeight, Math.Max(CollectiblePanelMinHeight, areaHeight - 16));
        CollectiblePanelHost.Width = width;
        CollectiblePanelHost.Height = height;
        Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, Math.Max(8, areaWidth - width - 16));
        Avalonia.Controls.Canvas.SetTop(CollectiblePanelHost, Math.Max(8, Math.Min(16, areaHeight - height - 8)));
        CollectibleDockHint.IsVisible = false;
        UpdateCollectiblePanelChrome();
    }

    /// <summary>浮动高度按素材数量估算：1 种素材保持紧凑，12 种时最多拉到 600。</summary>
    private double CollectiblePanelPreferredHeight()
    {
        var content = 196 + _config.Collectibles.Kinds.Count * 40;
        return Math.Clamp(content, CollectiblePanelMinHeight, CollectiblePanelFloatHeight);
    }

    /// <summary>贴到画布左 / 右边缘；面板占满画布高度，画布按宽度让位。</summary>
    private void DockCollectiblePanel(CollectibleDockSide side)
    {
        if (!CollectiblePanelHost.IsVisible || side == CollectibleDockSide.None)
        {
            return;
        }

        _collectiblePanelDockSide = side;
        ApplyCollectibleDockLayout();
        UpdateCollectiblePanelChrome();
    }

    /// <summary>停靠布局的唯一入口：面板贴边、画布让位、宽度限幅都在这里算，避免 resize 递归。</summary>
    private void ApplyCollectibleDockLayout()
    {
        if (!CollectiblePanelHost.IsVisible)
        {
            return;
        }

        if (_collectiblePanelDockSide == CollectibleDockSide.None)
        {
            ApplyFloatingPanelStyle();
            MapCanvasHost.Margin = new Thickness(0);
            return;
        }

        var areaWidth = OverlayWidth();
        var areaHeight = OverlayHeight();
        var maxWidth = Math.Max(
            CollectiblePanelMinDockWidth,
            Math.Min(CollectiblePanelMaxDockWidth, areaWidth - CollectiblePanelMinCanvasWidth));
        var width = Math.Clamp(_collectiblePanelDockWidth, CollectiblePanelMinDockWidth, maxWidth);
        _collectiblePanelDockWidth = width;

        CollectiblePanelHost.Width = width;
        CollectiblePanelHost.Height = Math.Max(240, areaHeight);
        CollectiblePanelHost.CornerRadius = new CornerRadius(0);
        CollectiblePanelHost.BoxShadow = default;
        CollectiblePanelResizeHandle.IsVisible = true;

        if (_collectiblePanelDockSide == CollectibleDockSide.Right)
        {
            Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, Math.Max(0, areaWidth - width));
            CollectiblePanelHost.BorderThickness = new Avalonia.Thickness(1, 0, 0, 0);
            MapCanvasHost.Margin = new Avalonia.Thickness(0, 0, width, 0);
            CollectiblePanelResizeHandle.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, 0);
            CollectiblePanelHost.BorderThickness = new Avalonia.Thickness(0, 0, 1, 0);
            MapCanvasHost.Margin = new Avalonia.Thickness(width, 0, 0, 0);
            CollectiblePanelResizeHandle.HorizontalAlignment = HorizontalAlignment.Right;
        }

        Avalonia.Controls.Canvas.SetTop(CollectiblePanelHost, 0);
        CollectibleDockHint.IsVisible = false;
    }

    /// <summary>恢复浮动外观；停靠时去掉圆角、阴影和悬浮边距。</summary>
    private void ApplyFloatingPanelStyle()
    {
        CollectiblePanelHost.CornerRadius = new CornerRadius(4);
        CollectiblePanelHost.BorderThickness = new Avalonia.Thickness(1);
        CollectiblePanelHost.BoxShadow = CollectiblePanelShadow;
        CollectiblePanelResizeHandle.IsVisible = false;
    }

    private void UpdateCollectiblePanelChrome()
    {
        var docked = _collectiblePanelDockSide != CollectibleDockSide.None;
        BtnCollectibleDock.Content = docked ? "浮动" : "吸附";
        ToolTip.SetTip(BtnCollectibleDock, docked
            ? "恢复为可拖动浮窗"
            : "吸附到画布左 / 右边缘，画布自动让位");
        CollectiblePanelHint.Text = docked
            ? (_collectiblePanelDockSide == CollectibleDockSide.Left ? "已吸附左侧" : "已吸附右侧")
            : "拖到边缘可吸附";
    }

    private double OverlayWidth()
    {
        var width = CollectibleOverlay.Bounds.Width;
        if (width <= 0)
        {
            width = Canvas.Bounds.Width;
        }

        return width > 0 ? width : 900;
    }

    private double OverlayHeight()
    {
        var height = CollectibleOverlay.Bounds.Height;
        if (height <= 0)
        {
            height = Canvas.Bounds.Height;
        }

        return height > 0 ? height : 620;
    }

    /// <summary>窗口缩放后重新贴合：停靠时重算宽度和画布边距，浮动时拉回可视区域。</summary>
    private void ClampCollectiblePanel()
    {
        if (!CollectiblePanelHost.IsVisible)
        {
            return;
        }

        if (_collectiblePanelDockSide != CollectibleDockSide.None)
        {
            ApplyCollectibleDockLayout();
            return;
        }

        var areaWidth = OverlayWidth();
        var areaHeight = OverlayHeight();
        var width = Math.Min(CollectiblePanelHost.Width, Math.Max(240, areaWidth - 16));
        var height = Math.Min(CollectiblePanelPreferredHeight(), Math.Max(220, areaHeight - 16));
        CollectiblePanelHost.Width = width;
        CollectiblePanelHost.Height = height;
        var left = Math.Clamp(Avalonia.Controls.Canvas.GetLeft(CollectiblePanelHost), 8, Math.Max(8, areaWidth - width - 8));
        var top = Math.Clamp(Avalonia.Controls.Canvas.GetTop(CollectiblePanelHost), 8, Math.Max(8, areaHeight - height - 8));
        Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, left);
        Avalonia.Controls.Canvas.SetTop(CollectiblePanelHost, top);
    }

    /// <summary>拖动靠近边缘时显示 4px 停靠提示条。</summary>
    private void ShowCollectibleDockHint(CollectibleDockSide side)
    {
        if (side == CollectibleDockSide.None)
        {
            CollectibleDockHint.IsVisible = false;
            return;
        }

        var areaWidth = OverlayWidth();
        var areaHeight = OverlayHeight();
        CollectibleDockHint.Width = 4;
        CollectibleDockHint.Height = areaHeight;
        Avalonia.Controls.Canvas.SetTop(CollectibleDockHint, 0);
        Avalonia.Controls.Canvas.SetLeft(CollectibleDockHint, side == CollectibleDockSide.Left
            ? 0
            : Math.Max(0, areaWidth - 4));
        CollectibleDockHint.IsVisible = true;
    }

    /// <summary>从停靠状态开始拖动时先恢复浮动外观，避免把整条侧栏拖成 800px 高的浮窗。</summary>
    private void UndockCollectiblePanelForDrag()
    {
        if (_collectiblePanelDockSide == CollectibleDockSide.None)
        {
            return;
        }

        _collectiblePanelDockSide = CollectibleDockSide.None;
        MapCanvasHost.Margin = new Avalonia.Thickness(0);
        ApplyFloatingPanelStyle();
        CollectiblePanelHost.Height = Math.Min(
            CollectiblePanelPreferredHeight(),
            Math.Max(320, OverlayHeight() - 16));
        UpdateCollectiblePanelChrome();
    }

    /// <summary>标题栏拖动：靠近左 / 右边缘显示提示并吸附；停靠边缘另提供宽度拖动。</summary>
}

