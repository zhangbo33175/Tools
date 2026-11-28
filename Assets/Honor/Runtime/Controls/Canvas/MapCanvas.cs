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

    /// <summary>线条反算到屏幕像素时允许的最小缩放，避免缩放到 0 时除零。</summary>
    private const double MinRenderableZoom = 0.0001;

    // 画布配色跟随「工作区深色外框 + 青蓝地图面 + 白色网格 + 浅青路径网格」的统一语汇
    private static readonly IBrush BackdropBrush = UiTheme.MapView.Backdrop;
    private static readonly IBrush MapBackgroundBrush = UiTheme.MapView.Surface;

    /// <summary>无底图时的「地图层」填充：上浅下深一档的竖向渐变，避免整块纯色显得平。</summary>
    private static readonly IBrush MapLayerBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(UiTheme.MapView.Layer, 0),
            new GradientStop(UiTheme.MapView.LayerDeep, 1),
        },
    };
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

    // 占格：三种形状统一用游戏内落位格配色（半透明绿填充 + 亮绿描边 + 格内十字）。
    // Outline 专门画在图元之上，保证 1 格大小的贴图把整格盖住时仍能看见占格边界。
    private static readonly IBrush OccupancyFillBrush = UiTheme.MapView.OccupancyFill;
    private static readonly IBrush OccupancyStrokeBrush = UiTheme.MapView.OccupancyStroke;
    private static readonly IBrush OccupancyIdleStrokeBrush = UiTheme.MapView.OccupancyIdleStroke;
    private static readonly IBrush OccupancyCrossBrush = UiTheme.MapView.OccupancyCross;
    private static readonly IBrush OccupancyOutlineBrush = UiTheme.MapView.OccupancyOutline;

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

    /// <summary>按下时物件左上角的世界坐标；松手时与当前坐标比较得出一次「移动」操作。</summary>
    private Point _dragOrigin;
    private Vector _dragGrab;

    /// <summary>路径绘制开关。必须只由 <see cref="PathDrawing"/> 读写，避免 UI 里再存一份 _pathEditing。</summary>
    private bool _pathDrawing;

    /// <summary>
    /// 当前选中的路径点序号：路径列表与画布共用同一份，-1 表示没有选中。
    /// 只经 <see cref="SelectPathPoint"/> 修改，保证重绘与事件成对发生。
    /// </summary>
    private int _selectedPathPoint = -1;

    /// <summary>正在拖动的路径点序号；-1 表示没有在拖动。</summary>
    private int _pathPointDrag = -1;

    /// <summary>路径点拖动起点（世界坐标），松手时用来判断这一次是否真的移动过。</summary>
    private Point _pathPointDragFrom;

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

    /// <summary>选中的路径点变化（画布拖动与路径列表点击都会触发），订阅方负责刷新列表高亮。</summary>
    public event EventHandler? PathPointSelectionChanged;

    /// <summary>指针状态变化，参数为世界坐标。</summary>
    public event EventHandler<Point>? PointerWorldChanged;

    /// <summary>空白处点选格子变化，参数为格子行列（X = 列，Y = 行）；取消时参数为 null。</summary>
    public event EventHandler<Point?>? CellSelectionChanged;

    /// <summary>迷雾数据变化。</summary>
    public event EventHandler? FogChanged;

    /// <summary>
    /// 一次拖动在松手时结算。携带物件与「按下坐标 → 松手坐标」，窗口层据此压入撤销栈；
    /// 拖动过程中的中间坐标不单独上报，避免一次拖动记成几十步。
    /// </summary>
    public event EventHandler<ObjectMoveEventArgs>? ObjectMoveCompleted;

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
        // 换物件 / 取消选中时路径点选中态必须一起清，否则列表会高亮一个已不存在的序号。
        SelectPathPoint(-1);
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把视口平移到指定物体并保证可见：保持当前缩放，过小时抬到 0.3，避免从整图视角跳过去只看到一个小点。
    /// 不改选中状态，调用方需要时自己再调 <see cref="Select"/>。
    /// </summary>
    /// <param name="target">目标物体。</param>
    public void FocusObject(PlacedObject target)
    {
        if (target is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var center = ClampIntoMap(target.Bounds.Center);
        _zoom = Math.Clamp(Math.Max(_zoom, 0.3), MapToolDefaults.MinZoom, MapToolDefaults.MaxZoom);
        _offset = new Vector(
            Bounds.Width / 2.0 - center.X * _zoom,
            Bounds.Height / 2.0 - center.Y * _zoom);

        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把视口平移到世界坐标点，保持当前缩放。
    /// <para>红绿灯这类规则只存坐标、没有对应地图物件的对象用它定位，不会改变选中状态。</para>
    /// </summary>
    /// <param name="x">世界坐标 X。</param>
    /// <param name="y">世界坐标 Y。</param>
    public void FocusPoint(double x, double y)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _zoom = Math.Clamp(Math.Max(_zoom, 0.3), MapToolDefaults.MinZoom, MapToolDefaults.MaxZoom);
        var center = ClampIntoMap(new Point(x, y));
        _offset = new Vector(
            Bounds.Width / 2.0 - center.X * _zoom,
            Bounds.Height / 2.0 - center.Y * _zoom);

        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把定位中心夹进地图矩形内。载具起点可能在图外（如 x=-462），
    /// 直接居中会把整张图推到屏幕另一侧、只剩空白，所以定位点一律夹回图内。
    /// </summary>
    /// <param name="point">世界坐标点。</param>
    /// <returns>夹回图内的点。</returns>
    private Point ClampIntoMap(Point point)
    {
        if (_config is null || _config.Width <= 0 || _config.Height <= 0)
        {
            return point;
        }

        return new Point(
            Math.Clamp(point.X, 0, _config.Width),
            Math.Clamp(point.Y, 0, _config.Height));
    }

    /// <summary>外部修改数据后请求重绘。</summary>
    public void Refresh() => InvalidateVisual();

    /// <summary>
    /// 外部（路径撤销 / 清空 / 逐点删除）改了数据后广播一次文档变化，
    /// 让属性卡与路径列表走和画布内加点同一条刷新链路。
    /// </summary>
    public void NotifyDocumentChanged() => DocumentChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 数据改动后的统一收尾：画面重绘 + 广播文档变化。
    /// <para>两者总是成对出现——只重绘会让属性卡/列表停在旧值，只广播会看到旧画面，
    /// 所以外部改完数据一律调这里，不要再手写 <see cref="Refresh"/> + <see cref="NotifyDocumentChanged"/>。</para>
    /// </summary>
    public void CommitEdit()
    {
        Refresh();
        NotifyDocumentChanged();
    }

    /// <summary>点击事件的世界坐标换算，供状态栏使用。</summary>
    /// <param name="screen">屏幕坐标。</param>
    /// <returns>世界坐标。</returns>
    public Point ToWorld(Point screen) => ScreenToWorld(screen);

    #endregion

    #region 坐标换算

    private Matrix ViewMatrix => Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_offset.X, _offset.Y);

    private void RebuildInverse() => _inverse = ViewMatrix.Invert();

    private Point ScreenToWorld(Point screen) => screen.Transform(_inverse);

    /// <summary>
    /// 把世界坐标对齐到格子。矩形模式贴格子左上角；菱形 / 六边形模式让「一个格子大小的贴图」
    /// 正中坐在格心上，这样贴图四角留白对称、格子的尖角自然探出到贴图外侧，和游戏内的落位一致。
    /// </summary>
    private Point SnapToCell(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        if (IsLatticeMode)
        {
            var center = LatticeCenter(LatticeIndexAt(world, cell), cell);
            return new Point(center.X - cell / 2.0, center.Y - cell / 2.0);
        }

        return new Point(
            Math.Floor(world.X / cell) * cell,
            Math.Floor(world.Y / cell) * cell);
    }

    /// <summary>世界坐标落在哪个格子（X = 列，Y = 行）。</summary>
    private Point CellIndexOf(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        if (IsLatticeMode)
        {
            return LatticeIndexAt(world, cell);
        }

        return new Point(
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
        if (IsLatticeMode)
        {
            return IsHexMode
                ? HexLattice.InsideMap((int)index.X, (int)index.Y, cell, config.Width, config.Height)
                : DiamondLattice.InsideMap((int)index.X, (int)index.Y, cell, config.Width, config.Height);
        }

        var rect = new Rect(index.X * cell, index.Y * cell, cell, cell);
        return rect.Intersects(new Rect(0, 0, config.Width, config.Height));
    }

    /// <summary>世界坐标落在哪个格子（X = 列，Y = 行），供状态栏等外部只读查询。</summary>
    /// <param name="world">世界坐标。</param>
    /// <returns>格子行列。</returns>
    public Point IndexOfWorld(Point world) => CellIndexOf(world);

    /// <summary>格子中心的像素坐标。</summary>
    private Point CellCenter(Point index)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        return IsLatticeMode
            ? LatticeCenter(index, cell)
            : new Point(index.X * cell + cell / 2.0, index.Y * cell + cell / 2.0);
    }

    /// <summary>
    /// 路径点吸附口径：矩形网格贴格子左上角（与 Unity <c>MapToolEditorUtils.SnapToGrid</c> 一致，
    /// 落盘后 GridX/GridY 正好等于行列号）；菱形 / 六边形网格取其中心，避免路径点落在格外的空白角。
    /// </summary>
    private Point SnapPathPoint(Point world)
    {
        var cell = _config?.GridSize ?? MapToolDefaults.GridSize;
        if (IsLatticeMode)
        {
            return LatticeCenter(LatticeIndexAt(world, cell), cell);
        }

        return new Point(
            Math.Floor(world.X / cell) * cell,
            Math.Floor(world.Y / cell) * cell);
    }

    /// <summary>是否处于菱形（TileMap 45°）模式，含 Isometric Z as Y（画布几何相同）。</summary>
    private bool IsDiamondMode => DrawShapes.IsDiamond(_config?.DrawShape);

    /// <summary>是否处于六边形（蜂窝）模式。</summary>
    private bool IsHexMode => DrawShapes.IsHex(_config?.DrawShape);

    /// <summary>是否菱形 / 六边形这类「贴图中心坐格心」的格子模式。</summary>
    private bool IsLatticeMode => IsDiamondMode || IsHexMode;

    /// <summary>按当前形状把世界坐标换算成格行列。</summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>格行列。</returns>
    private Point LatticeIndexAt(Point world, int cell) => IsHexMode
        ? HexLattice.IndexAt(world.X, world.Y, cell)
        : DiamondLattice.IndexAt(world.X, world.Y, cell);

    /// <summary>按当前形状取格心。</summary>
    /// <param name="index">格行列。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>格心世界坐标。</returns>
    private Point LatticeCenter(Point index, int cell) => IsHexMode
        ? HexLattice.Center((int)index.X, (int)index.Y, cell)
        : DiamondLattice.Center((int)index.X, (int)index.Y, cell);

    #endregion
}
