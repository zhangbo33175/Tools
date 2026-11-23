/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Properties.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布状态属性：视口、选中、摆放、图层显隐与动画预览
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class MapCanvas
{
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

    /// <summary>
    /// 1 个屏幕像素对应的世界线宽。所有不随缩放变粗的描边都从这里取值，
    /// 避免各绘制分区各自做一次「1 / 缩放」反算。
    /// </summary>
    private double Hairline => 1.0 / Math.Max(_zoom, MinRenderableZoom);

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
}
