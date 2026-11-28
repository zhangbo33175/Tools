/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.axaml.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口装配：资源面板 / 场景画布 / 底部属性区
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

/// <summary>
/// 场景地图编辑器主窗口。按 Unity 版 SenceMapTool 的面板结构复刻：
/// 左侧章节与资源列表、顶部模式 Tab、场景工具条与图层开关、画布、状态栏、底部属性区。
/// </summary>
public partial class MainWindow : Window
{
    #region 字段

    private enum CollectibleDockSide
    {
        None,
        Left,
        Right,
    }

    private MapConfigData _config = new();
    private List<ObjectTemplate> _templates = new();

    /// <summary>四方向出图索引：模板集合变化后重建，画布据此决定「换图」还是「转图」。</summary>
    private FacingImageCatalog _facingImages = FacingImageCatalog.Empty;
    private MapObjectType _activeType = MapObjectType.Prop;
    private ObjectTemplate? _pendingTemplate;

    /// <summary>收集物浮窗拖拽状态与左 / 右停靠状态。</summary>
    private CollectibleDockSide _collectiblePanelDockSide;
    private bool _collectiblePanelDragging;
    private bool _collectiblePanelDragMoved;
    private Point _collectiblePanelDragOffset;
    private bool _collectiblePanelResizing;
    private double _collectiblePanelResizeStartX;
    private double _collectiblePanelResizeStartWidth;
    private const double CollectiblePanelWidth = 360;
    private const double CollectiblePanelFloatHeight = 600;
    private const double CollectiblePanelMinHeight = 300;
    private const double CollectiblePanelMinDockWidth = 300;
    private const double CollectiblePanelMaxDockWidth = 520;
    private const double CollectiblePanelMinCanvasWidth = 320;
    private const double CollectibleDockSnapThreshold = 28;
    private double _collectiblePanelDockWidth = CollectiblePanelWidth;
    private static readonly BoxShadows CollectiblePanelShadow = BoxShadows.Parse("0 8 24 0 #55000000");

    /// <summary>收集物面板素材拖拽状态：按下时记录素材与起点，移动超过阈值后松手即按落点摆放。</summary>
    private ObjectTemplate? _collectibleDragTemplate;
    private Point _collectibleDragStart;
    private bool _collectibleDragMoved;

    /// <summary>收集物面板头部统计与各素材已放数量的实时文本，DocumentChanged 时只刷文字不重建列表。</summary>
    private TextBlock? _collectibleGoalValue;
    private TextBlock? _collectiblePlacedValue;
    private TextBlock? _collectibleRemainValue;
    private readonly Dictionary<string, TextBlock> _collectibleCountTexts = new(StringComparer.Ordinal);

    private TemplateStore _templateStore = null!;
    private EditorSettingsStore _settingsStore = null!;
    private SpriteAnimationStore _spriteAnimationStore = null!;
    private SpriteAnimationCatalog _spriteAnimations = null!;
    private SpineAnimationStore _spineAnimationStore = null!;
    private SpineAnimationCatalog _spineAnimations = null!;
    private AnimationPreviewWindow? _animationPreviewWindow;
    private MapPreviewWindow? _mapPreviewWindow;
    private TrafficDemoWindow? _trafficDemoWindow;
    private ItemElementPanel? _itemPanel;
    private FogEditPanel? _fogPanel;
    private TrafficRulePanel? _trafficPanel;

    /// <summary>交通规则悬浮窗的拖动 / 停靠 / 调宽状态，行为与收集物面板一致。</summary>
    private CollectibleDockSide _trafficDockSide;
    private bool _trafficDragging;
    private bool _trafficDragMoved;
    private Point _trafficDragOffset;
    private bool _trafficResizing;
    private double _trafficResizeStartX;
    private double _trafficResizeStartWidth;
    private const double TrafficPanelWidth = 640;
    private const double TrafficPanelFloatHeight = 720;
    private const double TrafficPanelMinDockWidth = 520;
    private const double TrafficPanelMaxDockWidth = 980;
    private double _trafficPanelDockWidth = TrafficPanelWidth;
    private static readonly BoxShadows TrafficPanelShadow = BoxShadows.Parse("0 14 36 0 #66000000");
    private ResourceOrganizePanel? _resourcePanel;
    private StoryEditorPanel? _storyPanel;
    private MapFogDocument _fogDoc = new();

    /// <summary>属性面板「编辑」勾选：控制右栏「物体编辑」分组是否显示。</summary>
    private bool _showObjectEdit = true;
    private bool _suppressLightSync;
    private int _mode;

    private string _saveDirectory = string.Empty;
    private AssetLibrary _assets = null!;

    /// <summary>地图物件配置表（ItemElementConfig.csv）；资源整理按图片名绑定行数据。</summary>
    private ItemConfigSet? _itemTable;
    private string _currentMapPath = string.Empty;
    private bool _suppressSync;
    private bool _suppressBandMove;

    /// <summary>每个关卡离开时的视口（缩放 + 平移），切回来恢复原视角，避免整张图跳到居中缩放。</summary>
    private readonly Dictionary<string, (double Zoom, Vector Offset)> _levelViews = new();

    /// <summary>当前挂进资源根的关卡目录；换关卡时先摘掉，防止同名切片串图。</summary>
    private string? _levelResourceRoot;

    /// <summary>最近一次导入的 PSD 文档（「导出切片」按它重新解码像素，与当前窗口里的物件一一对应）。</summary>
    private PsdDocument? _psdDocument;

    /// <summary>最近一次导入的 PSD 源路径（切片文件名、底图命名、清单校验都用它）。</summary>
    private string _psdSourcePath = string.Empty;

    /// <summary>最近一次导入时 PSD 的文件长度（「更新地图」判断源文件是否改过）。</summary>
    private long _psdImportLength;

    /// <summary>最近一次导入时 PSD 的 UTC 修改时间 ticks（同上，长度相同也能抓到改时间的情况）。</summary>
    private long _psdImportTicks;

    /// <summary>最近一次导入用到的 Unity 侧 <c>psd.json</c> 清单（图层判定口径，导出复用）。</summary>
    private PsdManifest? _psdManifest;

    /// <summary>导入时判定为「并入底图」的整幅图层（导出时合成 <c>Background\bg_x.png</c>）。</summary>
    private List<PsdLayerInfo> _psdBackgroundLayers = new();

    /// <summary>PSD 图层序号 → 导入出来的物件（导出切片后按记录回写路径 / 尺寸 / 旋转）。</summary>
    private readonly Dictionary<int, PlacedObject> _psdObjectByLayer = new();

    /// <summary>当前关卡 <c>slices.json</c> 的路径缓存；切换关卡时按路径失效重读。</summary>
    private string _sliceIndexPath = string.Empty;

    /// <summary>PSD 切片索引行（文件 / 左上角 / 序号），属性区用它在无配置表 ID 时回退数字 ID。</summary>
    private readonly List<(string File, double Left, double Top, int Index)> _sliceIndexRows = new();

    /// <summary>导入时的内存预览结果（含未落盘的 PNG 字节与去重统计）。</summary>
    private PsdSliceSet? _psdPreview;

    /// <summary>章节表 + 主关卡表读出来的章节目录（左侧树的数据源）。</summary>
    private ChapterCatalog _chapters = new();

    /// <summary>折叠起来的章节 ID（默认全部展开，和 Unity 的章节树一致）。</summary>
    private readonly HashSet<string> _collapsedChapters = new();

    /// <summary>当前选中的章节 / 关卡（用于树上的蓝底高亮）。</summary>
    private string _selectedChapterId = string.Empty;
    private string _selectedLevelId = string.Empty;

    /// <summary>选中关卡后按 <c>Map_{章节}m{关卡}.json</c> 落盘；为空时回退「场景 ID.json」的老口径。</summary>
    private string _saveFileOverride = string.Empty;

    // TexturePacker 风格配色统一由 UiTheme 提供；本类不再维护局部画刷副本。

    #endregion

    #region 构造

    /// <summary>初始化窗口并挂载示例数据。</summary>
    public MainWindow()
    {
        InitializeComponent();

        _saveDirectory = ResolveDefaultSaveDirectory();
        var configDirectory = EditorPaths.Config;
        _templateStore = new TemplateStore(configDirectory);
        _templates = _templateStore.LoadOrCreate(() => TemplateCatalog.CreateBuiltIn());

        // 四方向出图索引：模板一变就重建，画布只认「模板 + 朝向」不再摸模板集合
        _facingImages = new FacingImageCatalog();
        _facingImages.Rebuild(_templates);
        _config = TemplateCatalog.CreateEmptyMap();

        // 动画目录与地图数据分离：地图 JSON 只保存动画名 / Spine 资产名，帧路径和 Atlas 路径集中在 output\Config
        _spriteAnimationStore = new SpriteAnimationStore(configDirectory);
        _spriteAnimations = _spriteAnimationStore.Load();
        _spineAnimationStore = new SpineAnimationStore(configDirectory);
        _spineAnimations = new SpineAnimationCatalog();
        _spineAnimationStore.LoadInto(_spineAnimations);

        // 地图配置（对应 Unity 的 EditorPrefs 那套）：六条路径 + 基线步长
        _settingsStore = new EditorSettingsStore(configDirectory);
        EditorSettings.Apply(_settingsStore.LoadOrCreate());

        // 资源库指向 output\Config，resRoots.txt 里声明的 Unity 工程根用于解析 image 字段
        _assets = new AssetLibrary(configDirectory);
        ApplyPreviewDirectories();

        // 地图物件配置表（CSV）：资源整理按图片名绑行数据
        _itemTable = LoadItemElementTable();

        // 自裁切片目录：导入后地图里的 image 指向这里（按遮挡带分子文件夹），重开已保存地图时也要能解析到
        var sliceDirectory = ResolveResourceDirectory();
        if (Directory.Exists(sliceDirectory))
        {
            _assets.AddSearchRoots(new[] { sliceDirectory });
        }

        Canvas.Assets = _assets;
        Canvas.FacingImages = _facingImages;
        // 画布按配置表把「未登记资源」框出来：先挂表，后续刷新 / 补齐时同步
        Canvas.ItemTable = _itemTable;

        Canvas.Config = _config;
        Canvas.Fog = _fogDoc;
        Canvas.NineSlices = _templateStore.LoadNineSlices();
        Canvas.Animations = _spriteAnimations;
        Canvas.SpineAnimations = _spineAnimations;

        // 章节 / 关卡树来自地图配置里的两张表（Unity 口径：TableChapters + TableMainLevels）
        LoadChapterCatalog();
        BuildChapters();
        BuildResourceList();

        WireTabs();
        WireToolbar();
        WireCollectiblePanel();
        WireLayerToggles();
        WireSceneFields();
        WireObjectEdit();
        WireProperties();
        WireTrafficPanel();
        WireAreaAndTransform();
        UpdateObjectEditVisibility();
        WireCanvas();
        WireUndo();
        WireItemElementPanel();
        WireFogPanel();
        WireTrafficRulePanel();
        WireResourcePanel();
        WireStoryPanel();
        WireTilePalette();
        WireLevels();
        WireRooms();
        WireWallSnap();
        WireSceneAttrScroll();

        RefreshSceneFields();
        RefreshStatus();
       SwitchMode(0);

        // --open-map <路径>：直接打开某张地图（可用于对照 Unity 工程里的真实数据）
        var args = Environment.GetCommandLineArgs();

        // --grid-settings：启动即弹出网格设置，用于快速核对界面与实时预览
        if (Array.IndexOf(args, "--grid-settings") >= 0)
        {
            Dispatcher.UIThread.Post(async () => await OpenGridSettingsAsync(), DispatcherPriority.Background);
        }

        TraceStartup($"ctor args({args.Length}): {string.Join(" | ", args)}");
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--open-map")
            {
                var target = Path.GetFullPath(args[i + 1]);
                TraceStartup($"--open-map -> {target}");
                LoadMapFile(target);
                TraceStartup($"--open-map done, objects={_config.Objects.Count}, lights={_config.TrafficLights.Count}, path={_currentMapPath}");
                continue;   // 不再 break：--open-map 之后可能还跟着 --preview-room
            }

            // --preview-room <房间ID>：启动即进入「进屋预览」，用于核对 rooms[] 与 hideOnEnter 的落盘结果
            if (args[i] == "--preview-room")
            {
                var roomId = args[i + 1];
                if (_config.FindRoom(roomId) is { } room)
                {
                    Canvas.RoomPreviewId = room.Id;
                    Canvas.Refresh();
                    TraceStartup($"--preview-room -> {room.Id} ({room.DisplayName})");
                }

                continue;
            }

            if (args[i] == "--import-psd")
            {
                TryImportPsd(Path.GetFullPath(args[i + 1]));
                break;
            }
        }

        Canvas.Select(null);

        WindowChromeTheme.Attach(this);
        Opened += (_, _) => Canvas.FitToView();
        Canvas.SizeChanged += (_, _) => Canvas.FitToView();
        KeyDown += OnWindowKeyDown;
    }

    /// <summary>
    /// 场景编辑下的交通工具快捷键：F1 人行道刷格、F2 道路刷格、F3 画横道；
    /// F4 把当前选中物件标记/取消标记为红绿灯（一次性动作，不再进入放灯模式）。
    /// 刷格工具再按一次退出。
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // 迷雾模式：Ctrl+Z 撤销最后一笔涂抹
        if (_mode == 2 && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
        {
            _fogPanel?.UndoLastStroke();
            e.Handled = true;
            return;
        }

        if (_mode != 0)
        {
            return;
        }

        if (HandleUndoShortcut(e))
        {
            e.Handled = true;
            return;
        }

        // 楼层切换：PageUp 上楼 / PageDown 下楼（文本框里翻页时不抢键）
        if (e.Key is Key.PageUp or Key.PageDown && e.KeyModifiers == KeyModifiers.None
            && FocusManager?.GetFocusedElement() is not TextBox)
        {
            ChangeLevel(e.Key == Key.PageUp ? 1 : -1);
            e.Handled = true;
            return;
        }

        Action? action = e.Key switch
        {
            Key.F1 => ToggleSelectedSidewalkMark,
            Key.F2 => ToggleSelectedRoadMark,
            Key.F3 => ToggleSelectedCrosswalkMark,
            Key.F4 => ToggleSelectedTrafficLight,
            _ => null,
        };

        if (action is null)
        {
            return;
        }

        action();
        e.Handled = true;
    }

    #endregion



    #region 辅助

    /// <summary>
    /// 启动期诊断日志：只在设置环境变量 <c>MAPEDITOR_TRACE_STARTUP</c> 时写
    /// <c>%TEMP%\MapEditorTool_startup.log</c>，用于排查「--open-map 打开了别的地图」这类只在启动阶段出现的问题。
    /// 分布式的面板/文件域也会调用它，所以做成静态方法，避免多次实例状态耦合。
    /// </summary>
    /// <param name="message">一行诊断信息（调用方自带上下文）。</param>
    /// <summary>启动期诊断日志开关：只有设置了 <c>MAPEDITOR_TRACE_STARTUP</c> 才为 true，便于在高频绘制处先判断再拼字符串。</summary>
    internal static bool TraceStartupEnabled { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAPEDITOR_TRACE_STARTUP"));

    internal static void TraceStartup(string message)
    {
        if (!TraceStartupEnabled)
        {
            return;
        }

        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "MapEditorTool_startup.log");
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch
        {
            // 诊断日志不能影响主流程
        }
    }


    private void SetMessage(string message) => WriteMessage(message, error: false);

    private void SetMessageError(string message) => WriteMessage(message, error: true);

    private void WriteMessage(string message, bool error)
    {
        // 左侧 SceneHint 只有一百多像素，长句必被省略号截断；完整信息同时写到状态栏右侧的 OpHint。
        SceneHint.Text = message;
        OpHint.Text = message;
        ToolTip.SetTip(SceneHint, message);
        ToolTip.SetTip(OpHint, message);
        OpHint.Foreground = error
            ? UiTheme.Chrome.MessageErrorText
            : UiTheme.Chrome.MessageInfoText;
    }

    /// <summary>
    /// 工具条中间那行保存提示：保存成功绿字、失败红字、载入后灰字显示当前文件。
    /// 状态栏左侧的 SceneHint 只有一百多像素，长绝对路径会被省略成“看不出变化”，所以单独放这里。
    /// </summary>
    /// <param name="text">提示文本。</param>
    /// <param name="ok">true = 成功（绿），false = 失败（红）。</param>
    /// <param name="neutral">true = 中性提示（灰），优先级低于 ok。</param>
    private void SetSaveHint(string text, bool ok = false, bool neutral = false)
    {
        SaveHint.Text = text;
        SaveHint.Foreground = ok
            ? UiTheme.Chrome.SaveSuccessText
            : neutral
                ? UiTheme.Chrome.SaveNeutralText
                : UiTheme.Error;
    }

    /// <summary>保存按钮临时变文案，给“点了没反应”一个直接可见的反馈。</summary>
    /// <param name="ok">true = 已保存，false = 保存失败。</param>
    private async void FlashSaveButton(bool ok)
    {
        BtnSave.Content = ok ? "已保存" : "保存失败";
        await Task.Delay(1200);
        BtnSave.Content = "保存";
    }

    private void BindText(TextBox box, Action<string> apply)
    {
        UiFactory.CommitOnEnter(box, () => apply(box.Text ?? string.Empty));
        box.LostFocus += (_, _) => apply(box.Text ?? string.Empty);
    }

    /// <summary>绑定格子坐标的单个轴输入框：回车 / 失焦后按 32px 一格写回世界坐标。</summary>
    /// <param name="box">X 或 Y 输入框。</param>
    /// <param name="isX">true 写 X，false 写 Y。</param>
    private void BindGridCellField(TextBox box, bool isX)
    {
        BindText(box, text =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cell))
            {
                SyncProperties();
                return;
            }

            var target = Canvas.Selected;
            if (isX)
            {
                target.X = cell * MapToolDefaults.GridSize;
            }
            else
            {
                target.Y = cell * MapToolDefaults.GridSize;
            }

            Canvas.Refresh();
            SyncProperties();
        });
    }

    /// <summary>可编辑下拉框的提交：选中项立即生效，手输文本在回车 / 失焦时生效。</summary>
    /// <param name="combo">下拉框。</param>
    /// <param name="apply">文本提交动作。</param>
    private void BindComboText(ComboBox combo, Action<string> apply)
    {
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string selected)
            {
                apply(selected);
            }
        };

        UiFactory.CommitOnEnter(combo, () => apply(combo.Text ?? string.Empty));
        combo.LostFocus += (_, _) => apply(combo.Text ?? string.Empty);
    }

    private void BindNumber(TextBox box, Action<double> apply)
    {
        void Commit()
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                apply(value);
            }
        }

        UiFactory.CommitOnEnter(box, Commit);
        box.LostFocus += (_, _) => Commit();
    }

    #region 拖动改值（Unity 式 scrub）

    /// <summary>起拖阈值（屏幕像素）：小于该位移仍按普通单击处理，不抢文本编辑。</summary>
    private const double ScrubThreshold = 4.0;

    /// <summary>
    /// 按住左键横向拖动改数值（Unity 数值框的手感）：1px 位移 = 1 个 <paramref name="step"/>，
    /// 上拖也按正值算；按住 Shift 步长 ×10。松手即提交，未拖动过的单击照常进入文本编辑。
    /// </summary>
    /// <param name="box">显示数值的输入框（拖动的同时把新值写回这里）。</param>
    /// <param name="handle">拖动把手：标签或输入框本身。</param>
    /// <param name="apply">值变化后的提交动作。</param>
    /// <param name="step">每像素位移对应的数值增量。</param>
    /// <param name="min">下限。</param>
    /// <param name="max">上限。</param>
    private void BindScrub(TextBox box, Control handle, Action<double> apply, double step = 1.0,
        double min = double.MinValue, double max = double.MaxValue)
    {
        double startValue = 0;
        Point anchor = default;
        bool armed = false;
        bool dragging = false;

        void Reset()
        {
            armed = false;
            dragging = false;
            handle.Cursor = new Cursor(StandardCursorType.SizeWestEast);
            box.Cursor = new Cursor(StandardCursorType.Ibeam);
        }

        void OnPressed(object? _, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(box).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return;
            }

            anchor = e.GetPosition(this);
            startValue = value;
            armed = true;
            dragging = false;

            if (!ReferenceEquals(handle, box))
            {
                e.Pointer.Capture(handle);
            }
        }

        void OnMoved(object? _, PointerEventArgs e)
        {
            if (!armed)
            {
                return;
            }

            if (!e.GetCurrentPoint(box).Properties.IsLeftButtonPressed)
            {
                Reset();
                return;
            }

            var point = e.GetPosition(this);
            double dx = point.X - anchor.X;
            double dy = anchor.Y - point.Y;

            if (!dragging)
            {
                if (Math.Abs(dx) < ScrubThreshold && Math.Abs(dy) < ScrubThreshold)
                {
                    return;
                }

                dragging = true;
                handle.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                if (box.SelectionStart != box.SelectionEnd)
                {
                    box.SelectionStart = box.SelectionEnd = box.CaretIndex;
                }
            }

            // 阈值内的前 4px 不计数，避免「按下就跳一下」
            double delta = Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
            delta -= Math.Sign(delta) * ScrubThreshold;
            double factor = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            double target = startValue + Math.Round(delta) * step * factor;
            target = Math.Clamp(target, min, max);

            box.Text = EditorNumberText.CompactOrWhole(target);
            apply(target);
            e.Handled = true;
        }

        void OnReleased(object? _, PointerReleasedEventArgs e)
        {
            if (!armed && !dragging)
            {
                return;
            }

            if (dragging)
            {
                e.Handled = true;
            }

            Reset();
            e.Pointer.Capture(null);
        }

        handle.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, true);

        if (!ReferenceEquals(handle, box))
        {
            box.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
            box.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
            box.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, true);
        }

        handle.Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    #endregion

    private static string ReadCombo(ComboBox box, string fallback)
    {
        if (box.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? fallback;
        }

        return box.SelectedItem?.ToString() ?? fallback;
    }

    private static string TypeName(MapObjectType type) => type switch
    {
        MapObjectType.Building => "建筑",
        MapObjectType.Character => "人物",
        _ => "物件",
    };

    private static int InteractIndex(string interactType) => interactType switch
    {
        InteractTypes.Click => 1,
        InteractTypes.Dialog => 2,
        InteractTypes.Enter => 3,
        InteractTypes.Pickup => 4,
        _ => 0,
    };

    /// <summary>交互下拉显示名 → 实例侧交互码。</summary>
    private static string InteractCodeFromDisplay(string display) => display switch
    {
        "点击" => InteractTypes.Click,
        "对话" => InteractTypes.Dialog,
        "进入" => InteractTypes.Enter,
        "收集" => InteractTypes.Pickup,
        _ => InteractTypes.None,
    };

    private static int FacingIndex(string facing) => facing switch
    {
        Facings.Up => 0,
        Facings.Down => 1,
        Facings.Left => 2,
        Facings.Right => 3,
        _ => 1,
    };

    /// <summary>朝向下拉显示名 → 朝向键。</summary>
    private static string FacingCodeFromDisplay(string display) => display switch
    {
        "上" => Facings.Up,
        "左" => Facings.Left,
        "右" => Facings.Right,
        _ => Facings.Down,
    };

    /// <summary>
    /// 「场景属性」卡片的滚动条按需显示：内容装得下就整条收起，避免在窄卡片里常驻一条空轨道。
    /// <para>换算走 ScrollViewer 的 Extent / Viewport：两者差 0.5px 以上才认为需要滚动，
    /// 免得布局取整的亚像素误差让滚动条闪一下。</para>
    /// </summary>
    private void WireSceneAttrScroll()
    {
        void Sync() => SceneAttrScrollBar.IsVisible =
            SceneAttrScroll.Extent.Height - SceneAttrScroll.Viewport.Height > 0.5;

        SceneAttrScroll.ScrollChanged += (_, _) => Sync();
        SceneAttrScroll.SizeChanged += (_, _) => Sync();
        Dispatcher.UIThread.Post(Sync, DispatcherPriority.Background);
    }

    #endregion
}



