/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景编辑画布：菱形网格 / 缩放平移 / 摆放 / 拖动 / 路径绘制
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 场景地图画布。世界坐标以像素为单位，原点位于地图左上角，
/// 格子由 <see cref="MapToolDefaults.GridSize"/> 划分，支持菱形与正交两种网格形状。
/// </summary>
public sealed partial class MapCanvas : Control
{
    #region 常量与画刷

    private const double FitMargin = 24.0;
    private const double ZoomStep = 1.15;

    // 画布配色跟随 TexturePacker 风格：浅灰工作区外框 + 青色画布 + 绿色常规网格 + 蓝色路径网格
    private static readonly IBrush BackdropBrush = UiTheme.MapView.Backdrop;
    private static readonly IBrush MapBackgroundBrush = UiTheme.MapView.Surface;
    private static readonly Color MapLayerBrushColor = UiTheme.MapView.Layer;
    private static readonly IBrush MarqueeBrush = UiTheme.MapView.Marquee;
    private static readonly IBrush AccentBrush = UiTheme.MapView.Accent;
    private static readonly IBrush HandleBrush = UiTheme.MapView.Handle;
    private static readonly IBrush LabelBrush = UiTheme.MapView.Label;

    // 空白处点选格子：绿色半透明填充 + 亮绿描边，与白色网格/选中框区分开
    private static readonly IBrush SelectedCellFillBrush = UiTheme.MapView.SelectedCellFill;
    private static readonly IBrush SelectedCellStrokeBrush = UiTheme.MapView.SelectedCellStroke;

    // 选中实例的可编辑区域：碰撞=橙红、阴影=紫，半透明填充 + 实线描边
    private static readonly IBrush CollisionAreaFillBrush = UiTheme.MapView.CollisionFill;
    private static readonly IBrush CollisionAreaStrokeBrush = UiTheme.MapView.CollisionStroke;
    private static readonly IBrush ShadowAreaFillBrush = UiTheme.MapView.ShadowFill;
    private static readonly IBrush ShadowAreaStrokeBrush = UiTheme.MapView.ShadowStroke;

    // 旋转后的占格 / 碰撞区域：琥珀色格子，矩形模式画正方格、菱形模式画菱形格
    private static readonly IBrush OccupancyFillBrush = UiTheme.MapView.OccupancyFill;
    private static readonly IBrush OccupancyStrokeBrush = UiTheme.MapView.OccupancyStroke;
    private static readonly IBrush OccupancyIdleStrokeBrush = UiTheme.MapView.OccupancyIdleStroke;

    // 摆放网格与路径网格统一由 MapGridRenderer 绘制，颜色见 UiTheme.MapGrid
    /// <summary>「包围盒」= 整张地图的外接矩形，纯绿实线，与 Unity 场景工具口径一致。</summary>
    private static readonly IBrush BoundaryBoxBrush = UiTheme.MapView.Boundary;
    private static readonly IPen PathPen = new Pen(UiTheme.MapView.Path, 2.0);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor GrabCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor PanCursor = new(StandardCursorType.Hand);
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    #endregion

    #region 字段

    private double _zoom = 0.2;
    private Vector _offset = new(30, 30);
    private Matrix _inverse = Matrix.Identity;

    private MapConfigData? _config;
    private AssetLibrary? _assets;
    private PlacedObject? _selected;
    private PlacedObject? _dragged;
    private Vector _dragGrab;

    /// <summary>路径绘制开关。必须只由 <see cref="PathDrawing"/> 读写，避免 UI 里再存一份 _pathEditing。</summary>
    private bool _pathDrawing;

    private bool _isPanning;
    private Point _panAnchor;
    private bool _spaceDown;

    /// <summary>MAP_PATH_DEBUG=1 时把路径点击的命中条件写进 output\Logs\path-edit.log，默认不产生 IO。</summary>
    private static readonly bool PathDebugEnabled =
        Environment.GetEnvironmentVariable("MAP_PATH_DEBUG") == "1";
    private readonly Dictionary<string, Bitmap?> _spriteCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 只在内存里的贴图（导入 PSD 时按图层像素自裁得到，磁盘上还没有文件）。
    /// 键是物件 <see cref="PlacedObject.Id"/>；点「导出切片」落盘后清空，改走磁盘解析。
    /// </summary>
    private readonly Dictionary<string, Bitmap> _runtimeSprites = new(StringComparer.Ordinal);

    /// <summary>只在内存里的整幅底图（导入 PSD 后的合成结果，尚未落盘）。</summary>
    private Bitmap? _runtimeBackground;

    /// <summary>序列帧动画目录；为空或未命中时画布使用静态贴图。</summary>
    private SpriteAnimationCatalog? _animations;

    /// <summary>Spine 资产目录；仅当实例 <see cref="PlacedObject.AnimationKind"/> 为 spine 时参与绘制。</summary>
    private SpineAnimationCatalog? _spineAnimations;

    /// <summary>画布上的 Spine 播放器，按实例 ID 隔离骨骼状态。</summary>
    private readonly Dictionary<string, SpinePlayer> _spinePlayers = new(StringComparer.Ordinal);

    /// <summary>上一次推进 Spine 动画的时间点，避免掉帧后一帧跳太远。</summary>
    private double _lastSpineTick;

    /// <summary>序列帧位图缓存：键为「动画名 + 帧号 + 路径」，避免 30 FPS 重绘反复解码同一张 PNG。</summary>
    private readonly Dictionary<string, Bitmap?> _animationFrameCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>动画重绘时钟：从画布创建开始计时，帧号按绝对时间计算，切换选中不会重置画面节奏。</summary>
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();

    /// <summary>上一轮动画签名的帧号哈希；签名不变时不做全画布重绘。</summary>
    private long _lastAnimationSignature = long.MinValue;

    /// <summary>动画重绘定时器（30 FPS 检查，只在帧号真的变化时才 InvalidateVisual）。</summary>
    private DispatcherTimer? _animationTimer;

    /// <summary>地图预览模式：只读取地图数据，隐藏编辑器辅助层，由预览时钟推进动画与路径演示。</summary>
    private bool _previewMode;

    /// <summary>预览播放是否暂停；暂停时动画与路径位置都冻结在当前位置。</summary>
    private bool _previewPaused;

    /// <summary>预览播放速度倍率，仅影响预览时钟，不改地图里的实例 Speed。</summary>
    private double _previewPlaybackSpeed = 1.0;

    /// <summary>预览经过时间（秒），由 <see cref="TickAnimations"/> 按真实时间推进。</summary>
    private double _previewElapsedSeconds;

    /// <summary>预览时钟；与编辑器动画时钟分开，退出预览后编辑器动画节奏不受影响。</summary>
    private readonly Stopwatch _previewClock = new();

    /// <summary>上一帧预览时钟读数，用于计算增量。</summary>
    private double _lastPreviewClockSeconds;

    /// <summary>是否开启绘制耗时采样（环境变量 <c>MAP_CANVAS_PERF=1</c>）。</summary>
    private static readonly bool RenderProfileEnabled =
        Environment.GetEnvironmentVariable("MAP_CANVAS_PERF") == "1";

    private static double _renderCostTotal;
    private static int _renderCostFrames;

    /// <summary>平移重绘是否已排队：Windows 一次拖动会送来 100+ 条 move 事件，合并成一帧画一次。</summary>
    private bool _panRepaintScheduled;

    private bool _isMarquee;
    private Point _marqueeAnchor;
    private Rect? _marqueeRect;
    private bool _initialFitPending = true;

    // 空白处点选的格子（绿色高亮）
    private Point? _selectedCell;

    // 迷雾笔刷状态
    private bool _fogPainting;
    private FogStrokeRecord? _fogRecord;
    private readonly List<FogStrokeRecord> _fogHistory = new();
    private Point? _fogHover;

    private bool _fogErasing;
    private FogRegionData? _fogStroke;

    #endregion

    #region 构造

    /// <summary>初始化画布。</summary>
    public MapCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = ArrowCursor;

        // 30 FPS 只做「帧号是否变化」的轻量检查；没有动画时定时器保持停止，
        // 有动画时才启动，避免静态地图上白跑定时器。
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _animationTimer.Tick += (_, _) => TickAnimations();
    }

    #endregion

    #region 事件

    /// <summary>视图缩放或平移变化。</summary>
    public event EventHandler? ViewChanged;

    /// <summary>选中物体变化。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>物体集合或属性变化。</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>路径绘制开关变化。按钮文案 / 角色切换都订阅它，避免和 <see cref="PathDrawing"/> 脱节。</summary>
    public event EventHandler? PathDrawingChanged;

    /// <summary>路径绘制时左键点到非地面物件（会被忽略）的通知，参数为面向用户的提示。</summary>
    public event EventHandler<string>? PathPointRejected;

    /// <summary>路径点新增 / 撤销 / 被去重或越界拦下时的通知，参数为面向用户的提示。</summary>
    public event EventHandler<string>? PathPointFeedback;

    /// <summary>指针状态变化，参数为世界坐标。</summary>
    public event EventHandler<Point>? PointerWorldChanged;

    /// <summary>空白处点选格子变化，参数为格子行列（X = 列，Y = 行）；取消时参数为 null。</summary>
    public event EventHandler<Point?>? CellSelectionChanged;

    /// <summary>迷雾数据变化。</summary>
    public event EventHandler? FogChanged;

    #endregion

    #region 属性

    /// <summary>绑定的地图配置。</summary>
    public MapConfigData? Config
    {
        get => _config;
        set
        {
            _config = value;
            _selected = null;
            _spriteCache.Clear();
            _runtimeSprites.Clear();
            _runtimeBackground = null;
            _animationFrameCache.Clear();
            _lastAnimationSignature = long.MinValue;
            _previewElapsedSeconds = 0;
            _previewClock.Restart();
            _lastPreviewClockSeconds = 0;
            _lastSpineTick = CurrentAnimationTime;
            _spinePlayers.Clear();
            SetSelectedCell(null);
            RebuildInverse();
            EnsureAnimationTimer();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>当前选中的物体。</summary>
    public PlacedObject? Selected => _selected;

    /// <summary>空白处点选到的格子行列（X = 列，Y = 行），null 表示没有点选格子。</summary>
    public Point? SelectedCell => _selectedCell;

    /// <summary>当前缩放倍率。</summary>
    public double Zoom => _zoom;

    /// <summary>当前平移量（屏幕像素），供切换关卡时保存 / 恢复视口。</summary>
    public Vector ViewOffset => _offset;

    /// <summary>当前视口状态：缩放倍率 + 平移量。</summary>
    public (double Zoom, Vector Offset) ViewState => (_zoom, _offset);

    /// <summary>当前缩放百分比整数值。</summary>
    public int ZoomPercent => (int)Math.Round(_zoom * 100);

    /// <summary>待摆放的模板，null 表示未进入摆放。</summary>
    public ObjectTemplate? PendingTemplate { get; set; }

    /// <summary>待摆放的收集物素材；和 <see cref="PendingTemplate"/> 互斥，点击画布时优先按收集物落位。</summary>
    public ObjectTemplate? PendingCollectibleTemplate { get; set; }

    /// <summary>当前待摆放收集物的层级：under = 物件下，over = 物件上。</summary>
    public string PendingCollectibleLayer { get; set; } = CollectibleLayers.Default;

    /// <summary>是否处于摆放模式。</summary>
    public bool PlacementEnabled { get; set; } = true;

    /// <summary>是否处于路径绘制模式。状态只以画布为准，界面按钮订阅 <see cref="PathDrawingChanged"/> 回显。</summary>
    public bool PathDrawing
    {
        get => _pathDrawing;
        set
        {
            if (_pathDrawing == value)
            {
                return;
            }

            _pathDrawing = value;
            if (value)
            {
                // 进入路径编辑默认打开路径网格：4 格宽的蓝色辅助线直接对应当前可落点的网格。
                // 退出时保留当前勾选状态，用户仍可在顶部「路径网格」里手动关闭。
                ShowPathGrid = true;
            }

            InvalidateVisual();
            PathDrawingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>是否显示摆放网格层。</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>是否显示路径网格层。</summary>
    public bool ShowPathGrid { get; set; }

    /// <summary>是否显示整张地图的包围盒外框（绿色 1 设备像素实线）。</summary>
    public bool ShowBoundingBox { get; set; }

    /// <summary>是否给每个图元画外框与占格描边（场景设置卡里的 DrawBorder 开关）。</summary>
    public bool ShowImageBorder { get; set; } = true;

    /// <summary>是否显示底图层（Unity 版 bgName 对应的底图）。</summary>
    public bool ShowMapLayer { get; set; } = true;

    /// <summary>
    /// 是否按地图包围盒裁掉越界美术。默认开启：边角上超出 4096 的大地块只画地图内部分；
    /// 关闭后可看到完整越界图片（包含选中框），用于检查边缘出血和美术摆放。
    /// </summary>
    public bool ShowClipOutsideMap { get; set; } = true;

    /// <summary>
    /// 地图预览模式：关闭选中框、占格、网格、路径编辑等编辑器辅助层，
    /// 只按地图数据绘制底图、物件、动画与路径演示；鼠标仅保留缩放和平移。
    /// </summary>
    public bool PreviewMode
    {
        get => _previewMode;
        set
        {
            if (_previewMode == value)
            {
                return;
            }

            _previewMode = value;
            if (value)
            {
                _selected = null;
                _dragged = null;
                _isMarquee = false;
                _marqueeRect = null;
                _pathDrawing = false;
                _previewElapsedSeconds = 0;
                _previewClock.Restart();
                _lastPreviewClockSeconds = 0;
                _lastSpineTick = 0;
                _spinePlayers.Clear();
                _lastAnimationSignature = long.MinValue;
            }
            else
            {
                _previewClock.Stop();
                _lastSpineTick = _animationClock.Elapsed.TotalSeconds;
            }

            EnsureAnimationTimer();
            InvalidateVisual();
        }
    }

    /// <summary>预览中是否播放序列帧 / Spine 动画；关闭后只用静态贴图。</summary>
    public bool PreviewShowAnimations { get; set; } = true;

    /// <summary>预览中是否显示角色路径并让角色沿路径移动；关闭后角色停在原摆放位置。</summary>
    public bool PreviewShowPaths { get; set; } = true;

    /// <summary>预览是否暂停；暂停时冻结动画帧与路径位置。</summary>
    public bool PreviewPaused
    {
        get => _previewPaused;
        set
        {
            if (_previewPaused == value)
            {
                return;
            }

            _previewPaused = value;
            _lastPreviewClockSeconds = _previewClock.Elapsed.TotalSeconds;
            InvalidateVisual();
        }
    }

    /// <summary>预览播放速度倍率（0.25～4.0），只影响预览时钟。</summary>
    public double PreviewPlaybackSpeed
    {
        get => _previewPlaybackSpeed;
        set => _previewPlaybackSpeed = Math.Clamp(value, 0.25, 4.0);
    }

    /// <summary>预览已经播放的秒数，供窗口状态栏显示。</summary>
    public double PreviewElapsedSeconds => _previewElapsedSeconds;

    /// <summary>预览中带路径、会参与移动演示的实例数量。</summary>
    public int PreviewPathObjectCount => _config?.Objects.Count(HasPreviewPath) ?? 0;

    /// <summary>重置预览时钟并重新开始动画 / 路径演示。</summary>
    public void RestartPreview()
    {
        _previewElapsedSeconds = 0;
        _previewClock.Restart();
        _lastPreviewClockSeconds = 0;
        _lastSpineTick = 0;
        _spinePlayers.Clear();
        _lastAnimationSignature = long.MinValue;
        InvalidateVisual();
    }

    /// <summary>预览中角色沿路径移动的基准速度，世界像素/秒。</summary>
    private const double PreviewWalkPixelsPerSecond = 96.0;

    /// <summary>各功能层显示开关，键为图层键。</summary>
    public HashSet<string> HiddenLayers { get; } = new();

    /// <summary>被元件列表「显示」开关隐藏的模板 ID 集合。</summary>
    public HashSet<string> HiddenTemplateIds { get; } = new();

    /// <summary>
    /// 九宫格配置目录：按实例的模板 ID / 图片名命中。仅在实例勾选「是否缩放」时参与绘制，
    /// 未配置或边距越界时自动退回普通拉伸，保证 577 个既有实例的显示不变。
    /// </summary>
    public NineSliceCatalog? NineSlices { get; set; }

    /// <summary>
    /// 序列帧动画目录。画布只按实例 <see cref="PlacedObject.AnimName"/> 查这个目录，
    /// <see cref="PlacedObject.AnimEnabled"/> 为 false 或目录里没有可用片段时保持静态贴图。
    /// 赋新目录会清空帧缓存并重启动画时钟检查，不会修改任何地图数据。
    /// </summary>
    public SpriteAnimationCatalog? Animations
    {
        get => _animations;
        set
        {
            _animations = value;
            _animationFrameCache.Clear();
            _lastAnimationSignature = long.MinValue;
            EnsureAnimationTimer();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Spine 资产目录。实例的 <see cref="PlacedObject.AnimationKind"/> 为 <see cref="AnimationKinds.Spine"/>
    /// 且资产名命中时才走骨骼绘制；否则继续走静态贴图或序列帧，不会把旧实例画成空白。
    /// </summary>
    public SpineAnimationCatalog? SpineAnimations
    {
        get => _spineAnimations;
        set
        {
            _spineAnimations = value;
            _spinePlayers.Clear();
            _lastSpineTick = CurrentAnimationTime;
            EnsureAnimationTimer();
            InvalidateVisual();
        }
    }

    /// <summary>迷雾笔刷模式，开启后左键绘制、右键擦除。</summary>
    public bool FogDrawing { get; set; }

    /// <summary>迷雾笔刷大小（世界像素直径）。</summary>
    public double FogBrushSize { get; set; } = 100.0;

    /// <summary>迷雾透明度 0～1。</summary>
    public double FogOpacity { get; set; } = 0.8;

    /// <summary>绑定的迷雾文档。</summary>
    public MapFogDocument? Fog { get; set; }

    /// <summary>当前绘制目标区域：非空时笔刷涂抹进该区域，否则每笔新建一个区域。</summary>
    public FogRegionData? ActiveFogRegion { get; set; }

    #endregion

    #region 公开方法

    /// <summary>首次布局完成后自动缩放居中整张地图，避免启动时画布只占一角。</summary>
    /// <param name="finalSize">布局结果尺寸。</param>
    /// <returns>实际使用尺寸。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_initialFitPending && finalSize.Width > 0 && finalSize.Height > 0)
        {
            _initialFitPending = false;
            Dispatcher.UIThread.Post(FitToView);
        }

        return result;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (PreviewMode)
        {
            _previewClock.Restart();
            _lastPreviewClockSeconds = 0;
        }

        EnsureAnimationTimer();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _animationTimer?.Stop();
        _previewClock.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>缩放并居中到整张地图。</summary>
    public void FitToView()
    {
        if (_config is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var scaleX = (Bounds.Width - FitMargin * 2) / _config.Width;
        var scaleY = (Bounds.Height - FitMargin * 2) / _config.Height;
        _zoom = Math.Clamp(Math.Min(scaleX, scaleY), MapToolDefaults.MinZoom, MapToolDefaults.AutoFitMaxZoom);
        _offset = new Vector(
            (Bounds.Width - _config.Width * _zoom) / 2.0,
            (Bounds.Height - _config.Height * _zoom) / 2.0);

        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按百分比设置缩放，锚点为画布中心。</summary>
    /// <param name="zoom">目标倍率。</param>
    public void SetZoom(double zoom)
    {
        var anchor = new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);
        var before = ScreenToWorld(anchor);
        _zoom = Math.Clamp(zoom, MapToolDefaults.MinZoom, MapToolDefaults.MaxZoom);
        _offset = new Vector(anchor.X - before.X * _zoom, anchor.Y - before.Y * _zoom);
        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 直接恢复一份视口状态（切换关卡回来时用）：缩放钳到合法范围，平移量原样恢复。
    /// 与 <see cref="FitToView"/> 的区别是不重新居中，能保住用户离开该关卡时的缩放与平移。
    /// </summary>
    /// <param name="zoom">缩放倍率。</param>
    /// <param name="offset">平移量（屏幕像素）。</param>
    public void SetView(double zoom, Vector offset)
    {
        _zoom = Math.Clamp(zoom, MapToolDefaults.MinZoom, MapToolDefaults.MaxZoom);
        _offset = offset;
        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>选中指定物体。</summary>
    /// <param name="target">目标物体，null 表示取消选择。</param>
    public void Select(PlacedObject? target)
    {
        if (ReferenceEquals(_selected, target))
        {
            return;
        }

        _selected = target;
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>外部修改数据后请求重绘。</summary>
    public void Refresh() => InvalidateVisual();

    /// <summary>
    /// 外部（路径撤销 / 清空 / 逐点删除）改了数据后广播一次文档变化，
    /// 让属性卡与路径列表走和画布内加点同一条刷新链路。
    /// </summary>
    public void NotifyDocumentChanged() => DocumentChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>点击事件的世界坐标换算，供状态栏使用。</summary>
    /// <param name="screen">屏幕坐标。</param>
    /// <returns>世界坐标。</returns>
    public Point ToWorld(Point screen) => ScreenToWorld(screen);

    #endregion

    #region 坐标换算

    private Matrix ViewMatrix => Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_offset.X, _offset.Y);

    private void RebuildInverse() => _inverse = ViewMatrix.Invert();

    private Point ScreenToWorld(Point screen) => screen.Transform(_inverse);

    /// <summary>把世界坐标对齐到格子左上角（菱形模式下即菱形外接矩形的左上角）。</summary>
    private Point SnapToCell(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        if (IsDiamondMode)
        {
            var index = DiamondLattice.IndexAt(world.X, world.Y, cell);
            return DiamondLattice.Bounds((int)index.X, (int)index.Y, cell).TopLeft;
        }

        return new Point(
            Math.Floor(world.X / cell) * cell,
            Math.Floor(world.Y / cell) * cell);
    }

    /// <summary>世界坐标落在哪个格子（X = 列，Y = 行）。</summary>
    private Point CellIndexOf(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        return IsDiamondMode
            ? DiamondLattice.IndexAt(world.X, world.Y, cell)
            : new Point(
                Math.Floor(world.X / cell),
                Math.Floor(world.Y / cell));
    }

    /// <summary>判断格子是否落在地图内（画布外空白处不参与点选）。</summary>
    private bool IsCellInsideMap(Point index)
    {
        var config = _config;
        if (config is null)
        {
            return false;
        }

        var cell = config.GridSize;
        if (IsDiamondMode)
        {
            return DiamondLattice.InsideMap((int)index.X, (int)index.Y, cell, config.Width, config.Height);
        }

        var rect = new Rect(index.X * cell, index.Y * cell, cell, cell);
        return rect.Intersects(new Rect(0, 0, config.Width, config.Height));
    }

    /// <summary>世界坐标落在哪个格子（X = 列，Y = 行），供状态栏等外部只读查询。</summary>
    /// <param name="world">世界坐标。</param>
    /// <returns>格子行列。</returns>
    public Point IndexOfWorld(Point world) => CellIndexOf(world);

    /// <summary>格子中心的像素坐标。</summary>
    /// <param name="index">格子行列。</param>
    /// <returns>格子中心。</returns>
    private Point CellCenter(Point index)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        return IsDiamondMode
            ? DiamondLattice.Center((int)index.X, (int)index.Y, cell)
            : new Point(index.X * cell + cell / 2.0, index.Y * cell + cell / 2.0);
    }

    /// <summary>
    /// 路径点吸附口径：矩形网格贴格子左上角（与 Unity <c>MapToolEditorUtils.SnapToGrid</c> 一致，
    /// 落盘后 GridX/GridY 正好等于行列号）；菱形网格取其中心，避免路径点落在菱形外接框的空白角。
    /// </summary>
    private Point SnapPathPoint(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        if (IsDiamondMode)
        {
            var diamond = DiamondLattice.IndexAt(world.X, world.Y, cell);
            return DiamondLattice.Center((int)diamond.X, (int)diamond.Y, cell);
        }

        return new Point(
            Math.Floor(world.X / cell) * cell,
            Math.Floor(world.Y / cell) * cell);
    }

    /// <summary>是否处于菱形（TileMap 45°）模式。</summary>
    private bool IsDiamondMode => _config?.DrawShape == DrawShapes.Diamond;

    #endregion

}

