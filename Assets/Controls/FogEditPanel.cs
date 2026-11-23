/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FogEditPanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   迷雾编辑面板：笔刷设置 / 绘制开关 / 迷雾区域列表
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 迷雾编辑面板，结构对齐 Unity 版 FogEditModule：
/// 顶栏「添加迷雾区域 / 清空迷雾」，左侧笔刷设置（笔刷大小、迷雾透明度、开始绘制、快捷键说明），
/// 右侧迷雾区域列表。窗口独立运行时没有 Unity 的 Scene 视图可画，
/// 因此面板内挂了一块迷雾画布承担绘制视图。
/// </summary>
public sealed class FogEditPanel : UserControl
{
    #region 常量与画刷

    private const double SettingsWidth = 300.0;

    private static readonly IBrush PanelBorderBrush = UiTheme.PanelBorder;

    #endregion

    #region 字段

    private readonly Action<string> _notify;
    private readonly StackPanel _regionHost = new();
    private readonly TextBlock _regionHint = new();
    private readonly TextBlock _regionTitle = new();
    private readonly TextBlock _brushValue = new();
    private readonly TextBlock _opacityValue = new();
    private readonly Button _drawButton = new();
    private readonly MapCanvas _canvas = new();

    private MapConfigData? _config;
    private MapFogDocument? _fog;
    private MapCanvas? _sceneCanvas;
    private FogRegionData? _selectedRegion;
    private bool _drawing;
    private bool _loading;

    #endregion

    #region 事件

    /// <summary>绘制开关变化。</summary>
    public event EventHandler? DrawingChanged;

    /// <summary>迷雾数据变化。</summary>
    public event EventHandler? FogChanged;

    /// <summary>请求把迷雾恢复到上次保存的状态。</summary>
    public event EventHandler? ResetRequested;

    #endregion

    #region 属性

    /// <summary>是否处于绘制状态。</summary>
    public bool Drawing => _drawing;

    #endregion

    #region 构造

    /// <summary>初始化迷雾编辑面板。</summary>
    /// <param name="notify">状态栏提示回调。</param>
    public FogEditPanel(Action<string> notify)
    {
        _notify = notify;
        Content = BuildLayout();
    }

    #endregion

    #region 绑定

    /// <summary>把面板接到地图配置、迷雾文档与场景画布。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="fog">迷雾文档。</param>
    /// <param name="sceneCanvas">场景画布（与迷雾画布共享数据）。</param>
    public void Attach(MapConfigData config, MapFogDocument fog, MapCanvas sceneCanvas)
    {
        _config = config;
        _fog = fog;
        _sceneCanvas = sceneCanvas;

        _canvas.Config = config;
        _canvas.Fog = fog;
        _canvas.FogDrawing = _drawing;
        _canvas.FogBrushSize = 100;
        _canvas.FogOpacity = 0.8;
        _canvas.Assets = sceneCanvas.Assets;
        _canvas.FogChanged += (_, _) => OnFogEdited();

        RefreshAll();
        _canvas.FitToView();
        _canvas.ClearFogHistory();
    }

    /// <summary>刷新区域列表与统计。</summary>
    public void RefreshAll()
    {
        if (_config is not null)
        {
            _canvas.Config = _config;
        }

        _canvas.Fog = _fog;
        RefreshRegionList();
        SyncCanvasSettings();
    }

    #endregion

    #region 布局

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = UiTheme.Form.SubtleSurface,
        };

        root.Children.Add(BuildToolbar());

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{SettingsWidth},8,*"),
            Margin = new Thickness(4, 4, 4, 4),
        };

        body.Children.Add(BuildSettingsPanel());

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("*,220"),
        };

        var canvasBorder = new Border
        {
            Background = UiTheme.MapView.FogNeutral,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _canvas,
        };

        right.Children.Add(canvasBorder);

        var listCard = BuildRegionCard();
        Grid.SetRow(listCard, 1);
        listCard.Margin = new Thickness(0, 4, 0, 0);
        right.Children.Add(listCard);

        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        Grid.SetRow(body, 1);
        root.Children.Add(body);
        return root;
    }

    private Control BuildToolbar()
    {
        var title = new TextBlock
        {
            Text = "迷雾编辑模式",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var undoButton = new Button { Content = "撤销一笔", Padding = new Thickness(10, 4) };
        undoButton.Click += (_, _) => UndoLastStroke();
        right.Children.Add(undoButton);

        var resetButton = new Button { Content = "重置迷雾", Padding = new Thickness(10, 4) };
        resetButton.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        right.Children.Add(resetButton);

        var addButton = new Button { Content = "添加迷雾区域", Padding = new Thickness(10, 4) };
        addButton.Click += (_, _) => AddRegion();
        right.Children.Add(addButton);

        var clearButton = new Button
        {
            Content = "清空迷雾",
            Padding = new Thickness(10, 4),
            Background = UiTheme.Error,
            Foreground = UiTheme.White,
        };
        clearButton.Click += (_, _) => ClearRegions();
        right.Children.Add(clearButton);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = UiTheme.Form.SubtleSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5),
            Child = grid,
        };
    }

    private Control BuildSettingsPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(6), Spacing = 6 };

        stack.Children.Add(new TextBlock
        {
            Text = "笔刷设置",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
        });

        var brushSlider = new Slider
        {
            Minimum = 16,
            Maximum = 400,
            Value = 100,
            TickFrequency = 8,
            IsSnapToTickEnabled = false,
        };

        var brushRow = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*,44") };
        brushRow.Children.Add(new TextBlock
        {
            Text = "笔刷大小",
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0),
        });
        Grid.SetColumn(brushSlider, 1);
        brushRow.Children.Add(brushSlider);
        _brushValue.Text = "100";
        _brushValue.Foreground = UiTheme.TextSecondary;
        _brushValue.VerticalAlignment = VerticalAlignment.Center;
        _brushValue.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_brushValue, 2);
        brushRow.Children.Add(_brushValue);
        stack.Children.Add(brushRow);

        brushSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _loading)
            {
                return;
            }

            _brushValue.Text = ((int)brushSlider.Value).ToString();
            _canvas.FogBrushSize = brushSlider.Value;
            if (_selectedRegion is not null)
            {
                _selectedRegion.BrushSize = brushSlider.Value;
            }
        };

        var opacitySlider = new Slider
        {
            Minimum = 0.05,
            Maximum = 1.0,
            Value = 0.8,
            TickFrequency = 0.05,
        };

        var opacityRow = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*,44") };
        opacityRow.Children.Add(new TextBlock
        {
            Text = "迷雾透明度",
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0),
        });
        Grid.SetColumn(opacitySlider, 1);
        opacityRow.Children.Add(opacitySlider);
        _opacityValue.Text = "0.80";
        _opacityValue.Foreground = UiTheme.TextSecondary;
        _opacityValue.VerticalAlignment = VerticalAlignment.Center;
        _opacityValue.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_opacityValue, 2);
        opacityRow.Children.Add(_opacityValue);
        stack.Children.Add(opacityRow);

        opacitySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _loading)
            {
                return;
            }

            _opacityValue.Text = opacitySlider.Value.ToString("0.00");
            _canvas.FogOpacity = opacitySlider.Value;
            if (_selectedRegion is not null && _fog is not null)
            {
                _selectedRegion.Opacity = opacitySlider.Value;
                _canvas.Refresh();
                FogChanged?.Invoke(this, EventArgs.Empty);
            }
        };

        _drawButton.Content = "开始绘制";
        _drawButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _drawButton.Padding = new Thickness(8, 5);
        _drawButton.Click += (_, _) => ToggleDrawing();
        stack.Children.Add(_drawButton);

        var shortcuts = new Border
        {
            Background = UiTheme.Card,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = "左键拖拽：绘制迷雾", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "右键拖拽：擦除迷雾", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "右键拖动 / 中键 / 空格+左键：平移画布", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                    new TextBlock { Text = "滚轮：缩放画布", Foreground = UiTheme.TextSecondary, FontSize = 11 },
                },
            },
        };
        stack.Children.Add(shortcuts);

        var stackHost = new ScrollViewer { Content = stack };

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = stackHost,
        };
    }

    private Control BuildRegionCard()
    {
        _regionHint.Text = "点击「开始绘制」后，在地图上左键拖拽绘制迷雾区域，右键拖拽擦除；\n" +
                           "点「添加迷雾区域」可先建一个空区域，再在其中涂抹。";
        _regionHint.Foreground = UiTheme.TextSecondary;
        _regionHint.TextWrapping = TextWrapping.Wrap;
        _regionHint.Margin = new Thickness(4, 4);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _regionHost,
        };

        _regionTitle.Text = "迷雾区域列表";
        _regionTitle.Foreground = UiTheme.AccentDark;
        _regionTitle.FontWeight = FontWeight.Bold;
        _regionTitle.Margin = new Thickness(0, 0, 0, 4);

        // 标题固定、列表占满剩余高度：用 StackPanel 会给列表无限高度，行数一多就冲出卡片
        var body = new Grid
        {
            Margin = new Thickness(8),
            RowSpacing = 4,
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(_regionTitle, 0);
        body.Children.Add(_regionTitle);

        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        _regionHost.Margin = new Thickness(4);
        scroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        var scrollArea = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14") };
        scrollArea.Children.Add(scroll);
        var scrollBar = new ClassicScrollBar { Target = scroll };
        Grid.SetColumn(scrollBar, 1);
        scrollArea.Children.Add(scrollBar);
        var listFrame = new Border
        {
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = scrollArea,
        };
        Grid.SetRow(listFrame, 1);
        body.Children.Add(listFrame);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = body,
            ClipToBounds = true,
        };
    }

    #endregion

    #region 区域操作

    private void AddRegion()
    {
        if (_fog is null)
        {
            return;
        }

        var region = new FogRegionData
        {
            Name = $"迷雾区域 {_fog.Regions.Count + 1}",
            Opacity = _canvas.FogOpacity,
            BrushSize = _canvas.FogBrushSize,
        };

        _fog.Regions.Add(region);
        _selectedRegion = region;
        _canvas.ActiveFogRegion = region;
        RefreshRegionList();
        _notify($"已添加 {region.Name}，在画布上涂抹即可填充");
    }

    private void ClearRegions()
    {
        if (_fog is null || _fog.Regions.Count == 0)
        {
            _notify("当前地图没有迷雾区域");
            return;
        }

        _fog.Regions.Clear();
        _selectedRegion = null;
        _canvas.ActiveFogRegion = null;
        RefreshRegionList();
        _canvas.Refresh();
        _sceneCanvas?.Refresh();
        FogChanged?.Invoke(this, EventArgs.Empty);
        _notify("已清空迷雾");
    }

    private void RefreshRegionList()
    {
        _regionHost.Children.Clear();

        if (_fog is null || _fog.Regions.Count == 0)
        {
            _regionTitle.Text = "迷雾区域列表（0 个区域）";
            _regionHost.Children.Add(_regionHint);
            return;
        }

        _regionTitle.Text = $"迷雾区域列表（{_fog.Regions.Count} 个区域 · 共 {_fog.Regions.Sum(item => item.CellCount)} 格）";

        foreach (var region in new List<FogRegionData>(_fog.Regions))
        {
            _regionHost.Children.Add(BuildRegionRow(region));
        }
    }

    private Control BuildRegionRow(FogRegionData region)
    {
        var name = new TextBlock
        {
            Text = region.Name,
            Foreground = UiTheme.TextPrimary,
            FontWeight = FontWeight.SemiBold,
        };

        var desc = new TextBlock
        {
            Text = $"{region.CellCount} 格 · 不透明度 {region.Opacity:0.00} · 笔刷 {region.BrushSize:0}px",
            Foreground = UiTheme.TextSecondary,
            FontSize = 11,
        };

        var text = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { name, desc },
        };

        var delete = new Button
        {
            Content = "删除",
            Padding = new Thickness(6, 2),
            Background = UiTheme.Error,
            Foreground = UiTheme.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        delete.Click += (_, _) =>
        {
            _fog?.Regions.Remove(region);
            if (ReferenceEquals(_selectedRegion, region))
            {
                _selectedRegion = null;
                _canvas.ActiveFogRegion = null;
            }

            RefreshRegionList();
            _canvas.Refresh();
            _sceneCanvas?.Refresh();
            FogChanged?.Invoke(this, EventArgs.Empty);
            _notify($"已删除 {region.Name}");
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,60") };
        grid.Children.Add(text);
        Grid.SetColumn(delete, 1);
        grid.Children.Add(delete);

        // 两行文字（名称 + 统计）需要 40px，32px 会把副标题压到边框上
        var row = new ListRowCard(40, ReferenceEquals(region, _selectedRegion))
        {
            Child = grid,
        };

        row.DoubleTapped += (_, _) => BeginRename(region, text);

        row.PointerPressed += (_, _) =>
        {
            _selectedRegion = region;
            _canvas.ActiveFogRegion = region;
            _loading = true;
            _canvas.FogBrushSize = region.BrushSize;
            _canvas.FogOpacity = region.Opacity;
            _opacityValue.Text = region.Opacity.ToString("0.00");
            _loading = false;
            RefreshRegionList();
        };

        return row;
    }

    /// <summary>撤销最后一笔迷雾涂抹（面板按钮与 Ctrl+Z 共用）。</summary>
    public void UndoLastStroke()
    {
        if (!_canvas.UndoFogStroke())
        {
            _notify("没有可撤销的迷雾笔迹");
            return;
        }

        RefreshRegionList();
        _notify("已撤销最后一笔迷雾");
    }

    /// <summary>双击区域行进入改名状态：原地换成输入框，回车或失焦提交。</summary>
    private void BeginRename(FogRegionData region, StackPanel host)
    {
        var editor = new TextBox
        {
            Text = region.Name,
            FontSize = 12,
            Padding = new Thickness(4, 2),
        };

        void Commit()
        {
            var value = editor.Text?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                region.Name = value;
            }

            RefreshRegionList();
            FogChanged?.Invoke(this, EventArgs.Empty);
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };

        editor.LostFocus += (_, _) => Commit();
        host.Children.Clear();
        host.Children.Add(editor);
        editor.Focus();
        editor.SelectAll();
    }

    #endregion

    #region 绘制控制

    private void ToggleDrawing()
    {
        _drawing = !_drawing;
        _drawButton.Content = _drawing ? "停止绘制" : "开始绘制";
        _drawButton.Background = _drawing ? UiTheme.Accent : UiTheme.RowBack;
        _drawButton.Foreground = _drawing ? UiTheme.White : UiTheme.TextPrimary;
        SyncCanvasSettings();
        DrawingChanged?.Invoke(this, EventArgs.Empty);
        _notify(_drawing ? "迷雾绘制中：左键涂抹 / 右键擦除" : "已停止迷雾绘制");
    }

    private void SyncCanvasSettings()
    {
        _canvas.FogDrawing = _drawing;
    }

    private void OnFogEdited()
    {
        RefreshRegionList();
        _sceneCanvas?.Refresh();
        FogChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
