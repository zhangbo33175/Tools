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

    /// <summary>新建迷雾区域的默认名称前缀。</summary>
    private const string DefaultRegionNamePrefix = "迷雾区域 ";

    /// <summary>区域行高度：名称与统计两行文字需要 40px，32px 会压到边框。</summary>
    private const double RegionRowHeight = 40.0;

    /// <summary>区域行删除按钮列宽。</summary>
    private const double RegionDeleteColumnWidth = 60.0;

    /// <summary>区域行统计文字字号。</summary>
    private const double RegionDescFontSize = 11.0;

    /// <summary>区域行名称与统计之间的间距。</summary>
    private const double RegionTextSpacing = 1.0;

    /// <summary>区域行删除按钮内边距。</summary>
    private static readonly Thickness RegionDeletePadding = new(6, 2);

    /// <summary>区域行删除按钮文字。</summary>
    private const string RegionDeleteText = "删除";

    /// <summary>双击区域行后重命名输入框的字号。</summary>
    private const double RenameEditorFontSize = 12.0;

    /// <summary>重命名输入框的内边距。</summary>
    private static readonly Thickness RenameEditorPadding = new(4, 2);

    /// <summary>顶部工具栏标题。</summary>
    private const string PanelTitle = "迷雾编辑模式";

    /// <summary>撤销按钮文案。</summary>
    private const string UndoButtonText = "撤销一笔";

    /// <summary>重置按钮文案。</summary>
    private const string ResetButtonText = "重置迷雾";

    /// <summary>添加区域按钮文案。</summary>
    private const string AddRegionButtonText = "添加迷雾区域";

    /// <summary>清空按钮文案。</summary>
    private const string ClearRegionsButtonText = "清空迷雾";

    /// <summary>绘制按钮的未绘制状态文案。</summary>
    private const string StartDrawingText = "开始绘制";

    /// <summary>绘制按钮的绘制中状态文案。</summary>
    private const string StopDrawingText = "停止绘制";

    /// <summary>左侧设置区的标题。</summary>
    private const string SettingsTitleText = "笔刷设置";

    /// <summary>笔刷大小行标签。</summary>
    private const string BrushSizeLabel = "笔刷大小";

    /// <summary>迷雾透明度行标签。</summary>
    private const string OpacityLabel = "迷雾透明度";

    /// <summary>区域列表标题前缀。</summary>
    private const string RegionListTitle = "迷雾区域列表";

    /// <summary>无区域时的画布操作提示。</summary>
    private const string RegionHintText =
        "点击「开始绘制」后，在地图上左键拖拽绘制迷雾区域，右键拖拽擦除；\n" +
        "点「添加迷雾区域」可先建一个空区域，再在其中涂抹。";

    /// <summary>左栏快捷键说明，按行渲染。</summary>
    private static readonly string[] ShortcutHints =
    {
        "左键拖拽：绘制迷雾",
        "右键拖拽：擦除迷雾",
        "右键拖动 / 中键 / 空格+左键：平移画布",
        "滚轮：缩放画布",
    };

    /// <summary>主体左右分栏之间的间距。</summary>
    private const double BodyColumnGap = 8.0;

    /// <summary>主体外边距。</summary>
    private static readonly Thickness BodyMargin = new(4);

    /// <summary>右侧区域列表固定高度。</summary>
    private const double RegionListHeight = 220.0;

    /// <summary>工具栏按钮内边距。</summary>
    private static readonly Thickness ToolbarButtonPadding = new(10, 4);

    /// <summary>工具栏按钮间距。</summary>
    private const double ToolbarButtonSpacing = 6.0;

    /// <summary>工具栏内边距。</summary>
    private static readonly Thickness ToolbarPadding = new(8, 5);

    /// <summary>设置区内边距。</summary>
    private static readonly Thickness SettingsMargin = new(6);

    /// <summary>设置区行间距。</summary>
    private const double SettingsSpacing = 8.0;

    /// <summary>笔刷卡片 / 快捷键卡片的圆角。</summary>
    private const double SettingsCardCornerRadius = 8.0;

    /// <summary>笔刷卡片 / 快捷键卡片的内边距。</summary>
    private static readonly Thickness SettingsCardPadding = new(14, 12, 14, 14);

    /// <summary>两个设置项之间的间距。</summary>
    private const double SettingBlockSpacing = 14.0;

    /// <summary>设置项标签字号。</summary>
    private const double SettingLabelFontSize = 11.5;

    /// <summary>设置项数值标签字号。</summary>
    private const double SettingValueFontSize = 11.5;

    /// <summary>数值胶囊圆角。</summary>
    private static readonly CornerRadius SettingValueCornerRadius = new(4);

    /// <summary>数值胶囊内边距。</summary>
    private static readonly Thickness SettingValuePadding = new(7, 2);

    /// <summary>滑块上边距：紧贴标签行，两端轻微负边距抵消模板留白。</summary>
    private static readonly Thickness SettingSliderMargin = new(-3, 6, -3, 0);

    /// <summary>绘制按钮内边距。</summary>
    private static readonly Thickness DrawButtonPadding = new(8, 6);

    /// <summary>绘制按钮圆角：与全局按钮圆角 5 对齐。</summary>
    private const double DrawButtonCornerRadius = 5.0;

    /// <summary>绘制按钮高度：比普通工具栏按钮高一档，作为左栏的主行动。</summary>
    private const double DrawButtonHeight = 32.0;

    /// <summary>设置区、区域列表两张卡片的圆角，与全局卡片圆角 8 对齐。</summary>
    private const double PanelCardCornerRadius = 8.0;

    /// <summary>快捷键卡片标题。</summary>
    private const string ShortcutTitleText = "快捷键";

    /// <summary>按键胶囊圆角：与输入框 6 那档错开，取按钮 5。</summary>
    private const double ShortcutChipCornerRadius = 5.0;

    /// <summary>按键胶囊内边距。</summary>
    private static readonly Thickness ShortcutChipPadding = new(6, 1);

    /// <summary>按键胶囊与说明文字之间的间距。</summary>
    private const double HintKeyGap = 8.0;

    /// <summary>快捷键卡片标题的下边距。</summary>
    private static readonly Thickness HintTitleMargin = new(0, 0, 0, 6);

    /// <summary>快捷键标题下方细分隔线的边距。</summary>
    private static readonly Thickness HintDividerMargin = new(0, 0, 0, 7);

    /// <summary>快捷说明卡片圆角。</summary>
    private static readonly CornerRadius HintCornerRadius = new(3);

    /// <summary>快捷说明卡片内边距。</summary>
    private static readonly Thickness HintPadding = new(8, 6);

    /// <summary>快捷说明行间距。</summary>
    private const double HintSpacing = 3.0;

    /// <summary>快捷说明与区域统计文字的字号。</summary>
    private const double HintFontSize = 11.0;

    /// <summary>区域列表表体与卡片边缘的留白。</summary>
    private static readonly Thickness RegionBodyMargin = new(8);

    /// <summary>区域标题的下边距。</summary>
    private static readonly Thickness RegionTitleMargin = new(0, 0, 0, 4);

    /// <summary>区域标题与列表之间的行间距。</summary>
    private const double RegionBodyRowSpacing = 4.0;

    /// <summary>区域列表内边距。</summary>
    private static readonly Thickness RegionListMargin = new(4);

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
