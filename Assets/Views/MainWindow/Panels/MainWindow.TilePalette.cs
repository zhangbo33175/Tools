/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.TilePalette.cs
 * author:    云毅
 * created:   2026
 * descrip:   主界面瓦片面板停靠：顶栏入口、右侧停靠容器与打开 / 关闭联动
 ***************************************************************/
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Controls;

namespace MapEditorTool;

/// <summary>
/// 瓦片调色板在主界面里的落地：顶栏「瓦片」按钮开合，
/// 面板停靠画布右侧（可拖宽），关闭时自动退出编辑态，避免画布还停留在「左键就是刷格子」的状态。
/// </summary>
public partial class MainWindow
{
    #region 字段

    /// <summary>停靠面板宽度，默认 320：够两列缩略图 + 一条规则行。</summary>
    private const double TilePanelWidth = 320;

    /// <summary>停靠面板最小 / 最大宽度。</summary>
    private const double TilePanelMinWidth = 260;
    private const double TilePanelMaxWidth = 460;

    private Canvas? _tileOverlay;
    private Border? _tileHost;
    private TilePalettePanel? _tilePanel;
    private TextBlock? _tilePanelHint;
    private bool _tileResizing;
    private double _tileResizeStartX;
    private double _tileResizeStartWidth;
    private double _tilePanelWidth = TilePanelWidth;

    #endregion

    #region 入口

    /// <summary>挂接顶栏「瓦片」按钮。</summary>
    private void WireTilePalette()
    {
        TabTilePalette.Click += (_, _) => ToggleTilePalette();
    }

    /// <summary>开合瓦片面板。</summary>
    private void ToggleTilePalette()
    {
        if (_config is null)
        {
            SetMessage("请先打开或新建一张地图，再编辑瓦片");
            return;
        }

        EnsureTilePaletteBuilt();

        var open = _tileOverlay!.IsVisible == false;
        _tileOverlay.IsVisible = open;

        if (open)
        {
            _tilePanel!.RefreshAll();
            RelayoutTilePalette();
            SetMessage("瓦片面板已打开：选纹理 → 点「编辑」→ 在画布上左键刷、右键擦");
        }
        else
        {
            // 关闭面板必须退出编辑态，否则画布会继续把左键当画笔，用户会以为选中功能坏了
            Canvas.TileEditEnabled = false;
            SetMessage("瓦片面板已关闭，画布恢复选中 / 拖动");
        }

        UpdateTilePaletteButton(open);
    }

    /// <summary>换图后刷新面板内容（贴图清单与规则表都挂在当前地图上）。</summary>
    private void RefreshTilePalette()
    {
        if (_tileOverlay is { IsVisible: true })
        {
            _tilePanel?.RefreshAll();
        }
    }

    #endregion

    #region 构建

    /// <summary>首次打开时把覆盖层、停靠容器与面板建出来，之后只切显隐。</summary>
    private void EnsureTilePaletteBuilt()
    {
        if (_tileOverlay is not null)
        {
            return;
        }

        var parent = TrafficRuleOverlay.Parent as Grid;
        if (parent is null)
        {
            return;
        }

        _tilePanel = new TilePalettePanel(Canvas, _assets, SetMessage);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Background = UiTheme.Tiles.RuleRow,
        };

        var title = new TextBlock
        {
            Text = "瓦片",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextPrimary,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(title);

        _tilePanelHint = new TextBlock
        {
            Text = "停靠右侧",
            FontSize = 11,
            Foreground = UiTheme.TextSecondary,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(_tilePanelHint, 1);
        header.Children.Add(_tilePanelHint);

        var close = new Button
        {
            Content = "×",
            Classes = { "titleAction" },
            Width = 24,
            Height = 22,
            Padding = new Thickness(0),
        };
        ToolTip.SetTip(close, "关闭瓦片面板（同时退出编辑态）");
        close.Click += (_, _) => ToggleTilePalette();
        Grid.SetColumn(close, 2);
        header.Children.Add(close);

        var headerBand = new Border
        {
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 30,
            Child = header,
        };

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("4,*"),
        };
        Grid.SetColumnSpan(headerBand, 2);
        body.Children.Add(headerBand);

        // 左侧一条细手柄：拖它调宽，和交通 / 收集物面板的停靠手感保持一致
        var grip = new Border
        {
            Background = UiTheme.Transparent,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeWestEast),
            IsHitTestVisible = true,
        };
        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _tileResizing = true;
            _tileResizeStartX = e.GetPosition(_tileOverlay).X;
            _tileResizeStartWidth = _tileHost?.Width ?? _tilePanelWidth;
            e.Pointer.Capture(grip);
            e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!_tileResizing || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || _tileHost is null)
            {
                return;
            }

            e.Handled = true;
            var delta = _tileResizeStartX - e.GetPosition(_tileOverlay).X;
            var width = Math.Clamp(_tileResizeStartWidth + delta, TilePanelMinWidth, TilePanelMaxWidth);
            _tileHost.Width = width;
            _tilePanelWidth = width;
            RelayoutTilePalette();
        };
        grip.PointerCaptureLost += (_, _) => _tileResizing = false;

        _tileHost = new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = body,
        };

        Grid.SetRow(grip, 1);
        Grid.SetColumn(grip, 0);
        body.Children.Add(grip);

        Grid.SetRow(_tilePanel, 1);
        Grid.SetColumn(_tilePanel, 1);
        body.Children.Add(_tilePanel);

        _tileOverlay = new Canvas
        {
            Background = null,
            ClipToBounds = true,
            IsVisible = false,
        };
        _tileOverlay.Children.Add(_tileHost);
        _tileOverlay.SizeChanged += (_, _) => RelayoutTilePalette();

        parent.Children.Add(_tileOverlay);
    }

    #endregion

    #region 布局

    /// <summary>把停靠容器贴到画布右边缘，高度铺满覆盖层。</summary>
    private void RelayoutTilePalette()
    {
        if (_tileOverlay is null || _tileHost is null || !_tileOverlay.IsVisible)
        {
            return;
        }

        var areaWidth = Math.Max(_tileOverlay.Bounds.Width, MapCanvasHost.Bounds.Width);
        var areaHeight = Math.Max(_tileOverlay.Bounds.Height, MapCanvasHost.Bounds.Height);
        if (areaWidth <= 0)
        {
            areaWidth = 1160;
        }

        if (areaHeight <= 0)
        {
            areaHeight = 640;
        }

        var width = Math.Clamp(_tilePanelWidth, TilePanelMinWidth, Math.Min(TilePanelMaxWidth, Math.Max(220, areaWidth - 240)));
        _tilePanelWidth = width;
        _tileHost.Width = width;
        _tileHost.Height = areaHeight;
        Avalonia.Controls.Canvas.SetTop(_tileHost, 0);
        Avalonia.Controls.Canvas.SetLeft(_tileHost, Math.Max(0, areaWidth - width));

        if (_tilePanelHint is not null)
        {
            _tilePanelHint.Text = $"停靠右侧 · {width:0}×{areaHeight:0}";
        }
    }

    /// <summary>顶栏按钮的开合高亮：打开时压成青底，和交通规则按钮的选中观感一致。</summary>
    /// <param name="open">面板是否打开。</param>
    private void UpdateTilePaletteButton(bool open)
    {
        TabTilePalette.Background = open ? UiTheme.Tiles.EditActiveSurface : UiTheme.Card;
        TabTilePalette.Foreground = open ? UiTheme.AccentDark : UiTheme.TextPrimary;
        TabTilePalette.BorderBrush = open ? UiTheme.Tiles.EditActiveBorder : UiTheme.PanelBorder;
    }

    #endregion
}
