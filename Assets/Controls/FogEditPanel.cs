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
/// 状态与绑定位于本文件，布局、区域列表、绘制控制分别位于 Layout / Regions / Drawing partial。
/// </summary>
public sealed partial class FogEditPanel : UserControl
{
    #region 常量与默认值

    /// <summary>左侧笔刷设置栏的固定宽度（设备无关像素）。</summary>
    private const double SettingsWidth = 300.0;

    /// <summary>笔刷直径缺省值，与 Unity FogEditModule 的初始笔刷口径保持一致。</summary>
    private const double DefaultBrushSize = 100.0;

    /// <summary>迷雾不透明度缺省值（0–1），0 为完全透明、1 为完全不透明。</summary>
    private const double DefaultOpacity = 0.8;

    /// <summary>笔刷大小滑块下限（像素）。</summary>
    private const double MinBrushSize = 16.0;

    /// <summary>笔刷大小滑块上限（像素）。</summary>
    private const double MaxBrushSize = 400.0;

    /// <summary>笔刷大小滑块的刻度步长（像素）。</summary>
    private const double BrushTick = 8.0;

    /// <summary>透明度滑块下限，保留少量可见度，避免滑到 0 后误以为没有迷雾。</summary>
    private const double MinOpacity = 0.05;

    /// <summary>透明度滑块上限（完全不透明）。</summary>
    private const double MaxOpacity = 1.0;

    /// <summary>透明度滑块的刻度步长。</summary>
    private const double OpacityTick = 0.05;

    /// <summary>设置栏与区域列表共用的细分隔线颜色。</summary>
    private static readonly IBrush PanelBorderBrush = UiTheme.PanelBorder;

    #endregion

    #region 字段

    /// <summary>状态栏提示回调，由主窗注入；面板不直接依赖主窗。</summary>
    private readonly Action<string> _notify;

    /// <summary>区域列表容器，刷新时整体重建行，避免残留旧行。</summary>
    private readonly StackPanel _regionHost = new();

    /// <summary>无区域时的操作提示文本。</summary>
    private readonly TextBlock _regionHint = new();

    /// <summary>区域列表标题，同时显示区域数量与总格数。</summary>
    private readonly TextBlock _regionTitle = new();

    /// <summary>笔刷大小数值标签，与滑块实时同步。</summary>
    private readonly TextBlock _brushValue = new();

    /// <summary>迷雾透明度数值标签，保留两位小数。</summary>
    private readonly TextBlock _opacityValue = new();

    /// <summary>开始 / 停止绘制按钮，文案与配色随绘制状态切换。</summary>
    private readonly Button _drawButton = new();

    /// <summary>面板内嵌的迷雾画布，独立运行时承担绘制视图。</summary>
    private readonly MapCanvas _canvas = new();

    /// <summary>当前地图配置，用于把迷雾画布接到同一份地图数据。</summary>
    private MapConfigData? _config;

    /// <summary>当前迷雾文档，区域列表与画布共享同一实例。</summary>
    private MapFogDocument? _fog;

    /// <summary>主场景画布，仅用于复用资源索引并在迷雾变化后刷新场景。</summary>
    private MapCanvas? _sceneCanvas;

    /// <summary>当前在区域列表中选中的区域；为空表示没有选中行。</summary>
    private FogRegionData? _selectedRegion;

    /// <summary>是否处于迷雾绘制状态，决定画布是否接收涂抹/擦除手势。</summary>
    private bool _drawing;

    /// <summary>刷新保护位：重建控件值时置位，防止滑块回调把初始化值写回数据。</summary>
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
        _canvas.FogBrushSize = DefaultBrushSize;
        _canvas.FogOpacity = DefaultOpacity;
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
}
