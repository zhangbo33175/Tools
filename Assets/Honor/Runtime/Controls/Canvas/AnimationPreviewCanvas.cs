/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationPreviewCanvas.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景动画预览画布：播放序列帧并沿角色路径做移动演示
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 动画预览画布。它只读地图数据，不会改动物体坐标：
/// 序列帧按 <see cref="SpriteAnimationClip"/> 播放，位置按 <see cref="PlacedObject.PathPoints"/> 插值，
/// 用于在不影响正式场景的前提下查看动画和移动表现。
/// </summary>
public sealed class AnimationPreviewCanvas : Control
{
    #region 常量与画刷

    /// <summary>画布背景。</summary>
    private static readonly IBrush BackgroundBrush = UiTheme.PreviewView.CanvasBackdrop;

    /// <summary>参考网格线颜色。</summary>
    private static readonly IBrush GridBrush = UiTheme.PreviewView.Grid;

    /// <summary>1 像素参考网格画笔。</summary>
    private static readonly IPen MinorGridPen = new Pen(GridBrush, 1.0);

    /// <summary>路径折线画笔。</summary>
    private static readonly IPen PathPen = new Pen(UiTheme.PreviewView.Path, 2.5);

    /// <summary>路径点方块填充。</summary>
    private static readonly IBrush PathPointBrush = UiTheme.PreviewView.PathPoint;

    /// <summary>脚底阴影填充。</summary>
    private static readonly IBrush ShadowBrush = UiTheme.PreviewView.Shadow;

    /// <summary>包围盒占位填充。</summary>
    private static readonly IBrush FootprintBrush = UiTheme.PreviewView.FootprintFill;

    /// <summary>包围盒描边。</summary>
    private static readonly IPen FootprintPen = new Pen(UiTheme.PreviewView.FootprintStroke, 1.2);

    /// <summary>目标名称文字。</summary>
    private static readonly IBrush LabelBrush = UiTheme.PreviewView.Label;

    /// <summary>路径点编号等次要文字。</summary>
    private static readonly IBrush MutedBrush = UiTheme.PreviewView.Muted;

    /// <summary>贴图缺失时的占位填充。</summary>
    private static readonly IBrush EmptyBrush = UiTheme.PreviewView.Empty;

    /// <summary>路径时长计算的最小速度，避免极慢实例算出无穷大。</summary>
    private const double MinPathSpeed = 1.0;

    /// <summary>取景范围的最小边长，目标很小时仍保留上下文。</summary>
    private const double MinPreviewSpan = 180.0;

    /// <summary>取景留白相对目标长边的比例。</summary>
    private const double ViewportMarginRatio = 0.32;

    /// <summary>取景留白的基础像素，保证小目标周围也有呼吸空间。</summary>
    private const double ViewportMarginPixels = 76.0;

    /// <summary>视图缩放下限，避免路径极大时缩成不可见。</summary>
    private const double MinViewScale = 0.02;

    /// <summary>视图缩放上限，避免单个目标被放大到失真。</summary>
    private const double MaxViewScale = 2.0;

    /// <summary>参考网格的步长（格数）。</summary>
    private const int GridStepInCells = 4;

    /// <summary>路径点方块的半边长（设备无关像素）。</summary>
    private const double PathPointHalfSize = 4.0;

    /// <summary>路径点编号相对点的横向偏移。</summary>
    private const double PathLabelOffsetX = 7.0;

    /// <summary>路径点编号相对点的纵向偏移。</summary>
    private const double PathLabelOffsetY = 18.0;

    /// <summary>路径点编号字号。</summary>
    private const double PathLabelFontSize = UiMetrics.FontBody;

    /// <summary>脚底阴影宽度。</summary>
    private const double FootShadowWidth = 36.0;

    /// <summary>脚底阴影高度。</summary>
    private const double FootShadowHeight = 10.0;

    /// <summary>阴影相对脚底锚点的横向半偏移。</summary>
    private const double FootShadowOffsetX = 18.0;

    /// <summary>阴影相对脚底锚点的纵向偏移。</summary>
    private const double FootShadowOffsetY = 5.0;

    /// <summary>目标名称字号。</summary>
    private const double TargetLabelFontSize = 13.0;

    /// <summary>目标名称与包围盒顶部的间距。</summary>
    private const double TargetLabelOffsetY = 22.0;

    /// <summary>目标名称距画布顶边的最小留白。</summary>
    private const double TargetLabelTopPadding = 4.0;

    /// <summary>空状态提示字号。</summary>
    private const double HintFontSize = 14.0;

    /// <summary>空状态提示距画布边缘的最小留白。</summary>
    private const double HintInset = 12.0;

    /// <summary>路径点取景扩张的半边长，确保端点不贴边。</summary>
    private const double PathPointProbeHalfSize = 1.0;

    #endregion

    #region 字段

    /// <summary>按资源路径缓存序列帧与静态图，避免每帧重复解码。</summary>
    private readonly Dictionary<string, Bitmap?> _frameCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>当前地图数据，仅用于资源兜底查找。</summary>
    private MapConfigData? _config;

    /// <summary>贴图资源库。</summary>
    private AssetLibrary? _assets;

    /// <summary>序列帧动画目录。</summary>
    private SpriteAnimationCatalog? _catalog;

    /// <summary>Spine 资产目录。</summary>
    private SpineAnimationCatalog? _spineCatalog;

    /// <summary>当前目标的 Spine 播放器；非 Spine 目标为 null。</summary>
    private SpinePlayer? _spinePlayer;

    /// <summary>当前预览目标，只读引用，不修改其坐标。</summary>
    private PlacedObject? _target;

    /// <summary>预览经过时间，秒。</summary>
    private double _timeSeconds;

    /// <summary>世界坐标到控件坐标的当前取景矩形，由 ComputeViewport 更新。</summary>
    private Rect _viewport;

    /// <summary>世界坐标到控件坐标的缩放比，由 ComputeViewport 更新。</summary>
    private double _scale = 1.0;

    #endregion

    #region 属性

    /// <summary>当前预览目标。</summary>
    public PlacedObject? Target
    {
        get => _target;
        set
        {
            _target = value;
            _frameCache.Clear();
            _spinePlayer = null;
            _timeSeconds = 0;
            EnsureSpinePlayer();
            InvalidateVisual();
        }
    }

    /// <summary>预览经过时间，秒。窗口定时器推进它。</summary>
    public double TimeSeconds
    {
        get => _timeSeconds;
        set
        {
            _timeSeconds = Math.Max(0, value);
            InvalidateVisual();
        }
    }

    /// <summary>是否显示路径折线。</summary>
    public bool ShowPath { get; set; } = true;

    /// <summary>移动演示到终点后是否回到起点循环。</summary>
    public bool LoopMovement { get; set; } = true;

    /// <summary>移动演示基准速度，世界像素/秒（再乘实例 Speed）。</summary>
    public double MovementPixelsPerSecond { get; set; } = AnimationSpeeds.DefaultPreviewMovement;

    /// <summary>路径总长度，世界像素。</summary>
    public double PathLength => PathMath.Length(_target?.PathPoints);

    #endregion

    #region 公开方法

    /// <summary>绑定场景、资源库与动画目录。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="assets">贴图资源库。</param>
    /// <param name="catalog">动画目录。</param>
    public void SetScene(MapConfigData? config, AssetLibrary? assets, SpriteAnimationCatalog? catalog)
    {
        _config = config;
        _assets = assets;
        _catalog = catalog;
        _frameCache.Clear();
        InvalidateVisual();
    }

    /// <summary>绑定 Spine 资产目录；与 <see cref="SetScene"/> 分开调用，避免旧调用方编译失败。</summary>
    /// <param name="catalog">Spine 动画目录。</param>
    public void SetSpineCatalog(SpineAnimationCatalog? catalog)
    {
        _spineCatalog = catalog;
        _spinePlayer = null;
        EnsureSpinePlayer();
        InvalidateVisual();
    }

    /// <summary>推进预览时间；Spine 目标同时推进骨骼动画。</summary>
    /// <param name="deltaSeconds">时间增量，秒。</param>
    public void Advance(double deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        _timeSeconds += deltaSeconds;
        if (_spinePlayer is not null && _target is not null && _target.AnimEnabled)
        {
            _spinePlayer.Update(deltaSeconds, EffectiveAnimationSpeed(_target));
        }

        InvalidateVisual();
    }

    /// <summary>回到第 0 秒并重播当前动画。</summary>
    public void RestartPlayback()
    {
        _timeSeconds = 0;
        if (_spinePlayer is not null)
        {
            _spinePlayer.Restart();
        }

        InvalidateVisual();
    }

    /// <summary>解析实例的 Spine 资产；只看视觉来源，AnimEnabled 只控制是否推进时间。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>Spine 资产或 null。</returns>
    public SpineAnimationAsset? ResolveSpineAsset(PlacedObject? item)
    {
        return item is not null
            && string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            ? _spineCatalog?.Find(item.SpineAsset)
            : null;
    }

    /// <summary>当前 Spine 动画名；非 Spine 目标返回空串。</summary>
    public string CurrentSpineAnimation => _spinePlayer?.AnimationName ?? string.Empty;

    /// <summary>当前 Spine 动画时长，秒。</summary>
    public double CurrentSpineDuration => _spinePlayer?.Duration ?? 0;

    /// <summary>按当前目标创建 / 重建 Spine 播放器并同步动画名。</summary>
    private void EnsureSpinePlayer()
    {
        _spinePlayer = null;
        var asset = ResolveSpineAsset(_target);
        if (asset == null || _target is null)
        {
            return;
        }

        _spinePlayer = asset.CreatePlayer();
        _spinePlayer.EnsureAnimation(_target.SpineAnimation, _target.Loop);
    }

    /// <summary>计算实例的有效动画片段；未启用 / 未配置时返回 null。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>动画片段或 null。</param>
    public SpriteAnimationClip? ResolveClip(PlacedObject? item)
    {
        return item is { AnimEnabled: true }
            && !string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase)
            && _catalog?.Find(item.AnimName) is { } clip
            ? clip
            : null;
    }

    /// <summary>取当前预览帧号；没有片段时返回 -1。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>帧索引。</returns>
    public int CurrentFrameIndex(PlacedObject? item)
    {
        var clip = ResolveClip(item);
        return clip is null
            ? -1
            : SpriteAnimationCatalog.FrameIndex(
                clip,
                _timeSeconds,
                EffectiveAnimationSpeed(item),
                item?.Loop ?? clip.Loop);
    }

    /// <summary>路径从起点走到终点所需秒数。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>时长，秒。</returns>
    public double PathDuration(PlacedObject? item)
    {
        var length = PathMath.Length(item?.PathPoints);
        var speed = EffectiveMovementSpeed(item);
        return length <= 0 ? 0 : length / Math.Max(MinPathSpeed, speed);
    }

    #endregion

    #region 绘制

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var client = new Rect(Bounds.Size);
        context.FillRectangle(BackgroundBrush, client);
        if (_config is null || _assets is null)
        {
            DrawCenteredHint(context, client, "未加载地图");
            return;
        }

        if (_target is null)
        {
            DrawCenteredHint(context, client, "左侧选择要预览的物体");
            return;
        }

        ComputeViewport(client, _target);
        DrawGrid(context, client);
        if (ShowPath && _target.PathPoints.Count > 0)
        {
            DrawPath(context, _target);
        }

        DrawTarget(context, _target);
    }

    /// <summary>按目标包围盒 + 路径点计算取景范围。</summary>
    private void ComputeViewport(Rect client, PlacedObject target)
    {
        var world = target.Bounds;
        foreach (var point in target.PathPoints)
        {
            var rect = new Rect(
                point.X - PathPointProbeHalfSize,
                point.Y - PathPointProbeHalfSize,
                PathPointProbeHalfSize * 2,
                PathPointProbeHalfSize * 2);
            world = world.Union(rect);
        }

        var width = Math.Max(MinPreviewSpan, world.Width);
        var height = Math.Max(MinPreviewSpan, world.Height);
        var margin = Math.Max(width, height) * ViewportMarginRatio + ViewportMarginPixels;
        var view = new Rect(
            world.X - margin,
            world.Y - margin,
            width + margin * 2,
            height + margin * 2);

        var scale = Math.Min(
            client.Width / Math.Max(1, view.Width),
            client.Height / Math.Max(1, view.Height));
        scale = Math.Clamp(scale, MinViewScale, MaxViewScale);

        var drawWidth = view.Width * scale;
        var drawHeight = view.Height * scale;
        var originX = (client.Width - drawWidth) * 0.5;
        var originY = (client.Height - drawHeight) * 0.5;
        _viewport = new Rect(
            view.X - originX / scale,
            view.Y - originY / scale,
            client.Width / scale,
            client.Height / scale);
        _scale = scale;
    }

    private Point ToScreen(Point world) => new(
        (world.X - _viewport.X) * _scale,
        (world.Y - _viewport.Y) * _scale);

    private Rect ToScreen(Rect world) => new(
        (world.X - _viewport.X) * _scale,
        (world.Y - _viewport.Y) * _scale,
        world.Width * _scale,
        world.Height * _scale);

    private void DrawGrid(DrawingContext context, Rect client)
    {
        var step = MapToolDefaults.GridSize * GridStepInCells;
        var firstX = Math.Floor(_viewport.X / step) * step;
        var firstY = Math.Floor(_viewport.Y / step) * step;
        for (var x = firstX; x <= _viewport.Right; x += step)
        {
            var screenX = ToScreen(new Point(x, 0)).X;
            context.DrawLine(MinorGridPen, new Point(screenX, 0), new Point(screenX, client.Height));
        }

        for (var y = firstY; y <= _viewport.Bottom; y += step)
        {
            var screenY = ToScreen(new Point(0, y)).Y;
            context.DrawLine(MinorGridPen, new Point(0, screenY), new Point(client.Width, screenY));
        }
    }

    private void DrawPath(DrawingContext context, PlacedObject target)
    {
        Point? previous = null;
        var index = 0;
        foreach (var pathPoint in target.PathPoints)
        {
            var current = ToScreen(new Point(pathPoint.X, pathPoint.Y));
            if (previous is { } from)
            {
                context.DrawLine(PathPen, from, current);
            }

            context.FillRectangle(
                PathPointBrush,
                new Rect(
                    current.X - PathPointHalfSize,
                    current.Y - PathPointHalfSize,
                    PathPointHalfSize * 2,
                    PathPointHalfSize * 2));
            DrawText(
                context,
                $"P{index}",
                new Point(current.X + PathLabelOffsetX, current.Y - PathLabelOffsetY),
                PathLabelFontSize,
                MutedBrush);
            previous = current;
            index++;
        }
    }

    private void DrawTarget(DrawingContext context, PlacedObject target)
    {
        var foot = ComputeFootPosition(_timeSeconds, target);
        var bounds = target.Bounds;
        var worldRect = new Rect(
            foot.X - bounds.Width * 0.5,
            foot.Y - bounds.Height,
            bounds.Width,
            bounds.Height);
        var screenRect = ToScreen(worldRect);
        var footScreen = ToScreen(foot);

        // 脚底阴影与占位框：让「移动到哪」一眼可见，不依赖贴图本身是否透明
        context.FillRectangle(
            ShadowBrush,
            new Rect(
                footScreen.X - FootShadowOffsetX,
                footScreen.Y - FootShadowOffsetY,
                FootShadowWidth,
                FootShadowHeight));
        context.FillRectangle(FootprintBrush, new Rect(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height));
        context.DrawRectangle(null, FootprintPen, screenRect);

        var bitmap = ResolveBitmap(target);
        var spineAsset = ResolveSpineAsset(target);
        if (spineAsset is not null)
        {
            if (_spinePlayer is not null)
            {
                using (context.PushTransform(SpriteLayout.ContentTransform(target.Rotation, target.FlipX, screenRect)))
                {
                    SpineRenderer.Draw(context, _spinePlayer, spineAsset, screenRect, 0, false);
                }
            }
            else
            {
                // Spine 资源缺失：只画虚线占位，不再回退旧静态图。
                context.DrawRectangle(null, FootprintPen, screenRect);
            }
        }
        else if (bitmap is not null)
        {
            var destination = SpriteLayout.Fit(screenRect, bitmap.PixelSize, target.Stretch, SpriteVerticalAnchor.Bottom);
            var transform = SpriteLayout.ContentTransform(target.Rotation, target.FlipX, destination);
            using (context.PushTransform(transform))
            {
                context.DrawImage(bitmap, new Rect(bitmap.Size), destination);
            }
        }
        else
        {
            context.FillRectangle(EmptyBrush, screenRect);
        }

        DrawText(
            context,
            target.Name,
            new Point(
                screenRect.X,
                Math.Max(TargetLabelTopPadding, screenRect.Y - TargetLabelOffsetY)),
            TargetLabelFontSize,
            LabelBrush);
    }

    private Bitmap? ResolveBitmap(PlacedObject target)
    {
        // Spine 是唯一视觉源：序列帧解析为空时也不再落到静态贴图，避免背景图重新出现。
        if (string.Equals(target.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var clip = ResolveClip(target);
        if (clip is not null)
        {
            var frame = SpriteAnimationCatalog.FrameIndex(
                clip,
                _timeSeconds,
                EffectiveAnimationSpeed(target),
                target.Loop);
            var path = clip.Frames[Math.Clamp(frame, 0, clip.Frames.Count - 1)];
            if (_frameCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var frameBitmap = _assets?.Load(path);
            _frameCache[path] = frameBitmap;
            if (frameBitmap is not null)
            {
                return frameBitmap;
            }
        }

        return StaticBitmap(target);
    }

    private Bitmap? StaticBitmap(PlacedObject target)
    {
        if (_assets is null)
        {
            return null;
        }

        var cacheKey = "static|" + target.Id;
        if (_frameCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var bitmap = _assets.Load(target.ImagePath)
            ?? _assets.LoadByLayer(
                target.Name,
                target.LayerType,
                _config?.ChapterId ?? "DefaultChapter",
                _config?.SceneId ?? "Level1")
            ?? _assets.Load(target.Name);
        _frameCache[cacheKey] = bitmap;
        return bitmap;
    }

    private void DrawCenteredHint(DrawingContext context, Rect client, string text)
    {
        var formatted = CanvasText.Create(text, HintFontSize, MutedBrush);
        context.DrawText(
            formatted,
            new Point(
                Math.Max(HintInset, (client.Width - formatted.Width) * 0.5),
                Math.Max(HintInset, (client.Height - formatted.Height) * 0.5)));
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        double size,
        IBrush brush)
    {
        var formatted = CanvasText.Create(text, size, brush);
        context.DrawText(formatted, origin);
    }

    #endregion

    #region 几何与路径

    /// <summary>实例动画速度下限后的有效值；空值或非正值按 1.0 处理。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>可用于帧推进与 Spine 更新的速度。</returns>
    private static double EffectiveAnimationSpeed(PlacedObject? item) =>
        AnimationSpeeds.Effective(item?.Speed);

    /// <summary>移动演示的有效速度：基准速度乘实例 Speed，并保证非零。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>世界像素/秒。</returns>
    private double EffectiveMovementSpeed(PlacedObject? item) =>
        MovementPixelsPerSecond * EffectiveAnimationSpeed(item);

    /// <summary>取路径起点；调用前须确保 PathPoints 非空。</summary>
    /// <param name="target">地图实例。</param>
    /// <returns>起点世界坐标。</returns>
    private static Point FirstPathPoint(PlacedObject target) =>
        new(target.PathPoints[0].X, target.PathPoints[0].Y);

    private Point ComputeFootPosition(double seconds, PlacedObject target)
    {
        if (target.PathPoints.Count == 0)
        {
            return new Point(target.Bounds.Center.X, target.Bounds.Bottom);
        }

        if (target.PathPoints.Count == 1)
        {
            return FirstPathPoint(target);
        }

        var total = PathMath.Length(target.PathPoints);
        if (total <= 0)
        {
            return FirstPathPoint(target);
        }

        var speed = EffectiveMovementSpeed(target);
        var mode = LoopMovement ? PathPlaybackMode.Loop : PathPlaybackMode.Once;
        var distance = PathMath.NormalizeDistance(Math.Max(0, seconds) * speed, total, mode);
        return PathMath.PointAtDistance(target.PathPoints, distance)
            ?? FirstPathPoint(target);
    }

    #endregion
}
