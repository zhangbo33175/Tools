/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.AnimationRender.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布预览与动画：路径演示包围盒、序列帧缓存、Spine 播放与运行时贴图
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

/// <summary>画布预览与动画：路径演示包围盒、序列帧缓存、Spine 播放与运行时贴图。</summary>
public sealed partial class MapCanvas : Control
{
    #region 路径预览与动画
    /// <summary>判断实例是否参与预览路径演示：至少有两个路径点。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>可沿路径移动时为 true。</returns>
    private static bool HasPreviewPath(PlacedObject item) => PathMath.HasPath(item.PathPoints);

    /// <summary>
    /// 预览中的绘制包围盒。带路径的实例按当前预览时间把脚底插值到路径上，
    /// 再把包围盒整体平移过去；没有路径或关闭路径演示时原样返回落盘坐标。
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>用于绘制的世界矩形。</returns>
    private Rect PreviewRenderBounds(PlacedObject item)
    {
        var bounds = item.Bounds;
        if (!PreviewMode || !PreviewShowPaths || !HasPreviewPath(item))
        {
            return bounds;
        }

        var foot = PreviewFootPoint(item);
        return foot is null
            ? bounds
            : new Rect(foot.Value.X - bounds.Width * 0.5, foot.Value.Y - bounds.Height, bounds.Width, bounds.Height);
    }

    /// <summary>按预览时钟计算实例当前脚底位置：路径点作为脚底锚点，支持循环 / 往返 / 单程。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>脚底世界坐标；路径无效时返回 null。</returns>
    private Point? PreviewFootPoint(PlacedObject item)
    {
        if (!HasPreviewPath(item))
        {
            return null;
        }

        // 交通仿真开启时用仿真里程：它带刹车曲线与信号等待，位置不再与时间线性相关
        if (_trafficPreviewEnabled && _trafficSim is not null)
        {
            var simulated = _trafficSim.PositionOf(item.Id);
            if (simulated is not null)
            {
                return simulated;
            }
        }

        var total = PathMath.Length(item.PathPoints);
        if (total <= 0)
        {
            return null;
        }

        var speed = AnimationSpeeds.DefaultPreviewMovement * AnimationSpeeds.Clamp(item.Speed);
        var walked = PathMath.NormalizeDistance(
            Math.Max(0, _previewElapsedSeconds) * speed,
            total,
            PathMath.ParseWalkMode(item.WalkMode));
        return PathMath.PointAtDistance(item.PathPoints, walked);
    }

    /// <summary>
    /// 实例在预览中是否处于「行走」状态：带路径、开启路径演示且没有走到单程终点。
    /// <para>暂停不改变动作归属，恢复播放时直接继续原动作；循环 / 往返路径始终视为行走。</para>
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>应播放行走动作时为 true。</returns>
    private bool IsPreviewPathMoving(PlacedObject item)
    {
        if (!PreviewMode || !PreviewShowPaths || !HasPreviewPath(item))
        {
            return false;
        }

        // 交通仿真接管时以角色真实速度为准：等灯 / 排队时速度为 0，
        // 播待机而不是原地踏步（与游戏里停在停止线外的状态一致）。
        if (_trafficPreviewEnabled && _trafficSim?.AgentOf(item.Id) is { } simulated)
        {
            return simulated.Speed > 0.05 && !simulated.Arrived;
        }

        var total = PathMath.Length(item.PathPoints);
        if (total <= 0)
        {
            return false;
        }

        if (PathMath.ParseWalkMode(item.WalkMode) != PathPlaybackMode.Once)
        {
            return true;
        }

        var speed = AnimationSpeeds.DefaultPreviewMovement * AnimationSpeeds.Clamp(item.Speed);
        return Math.Max(0, _previewElapsedSeconds) * speed < total;
    }

    /// <summary>
    /// 画面排序脚底：直接取当前渲染包围盒的下沿。
    /// <para>预览中的路径角色会随路径插值移动，因此排序基准也跟着移动；
    /// 严格按 2D 规则参与树木 / 建筑 / 道具的脚底 Y 遮挡，不做角色置顶。</para>
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>排序用脚底世界 Y。</returns>
    // ---TODO 遮挡：排序与绘制共用同一包围盒底边，路径角色移动时不会因为基准不一致而穿帮或永远置顶。
    private double VisualSortBaseline(PlacedObject item) => PreviewRenderBounds(item).Bottom;

    /// <summary>
    /// 预览行走时的实际水平镜像：按路径当前八方向决定朝左还是朝右，再叠加实例自身的「水平翻转」。
    /// <para>默认美术按朝左绘制；初始朝向标成 right 的美术反向处理，避免所有资源都靠方向字段手工纠正。</para>
    /// <para>资产里存在方向动作（如 <c>walk_up_right</c>）时，方向已经画在动画里，不再额外镜像。</para>
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <returns>本次绘制是否水平镜像。</returns>
    // ---TODO 朝向：预览沿路径移动时按八方向自动翻面，静态摆放仍沿用实例自身的 FlipX。
    private bool ResolveRenderFlipX(PlacedObject item)
    {
        var flip = item.FlipX;
        var direction = PreviewMoveDirection8(item);
        if (direction is null)
        {
            return flip;
        }

        if (PreviewShowAnimations && item.AnimEnabled && IsPreviewPathMoving(item)
            && HasDirectionalWalk(item, direction.Value))
        {
            return flip;
        }

        var baseFacesRight = string.Equals(item.FacingDirection, Facings.Right, StringComparison.OrdinalIgnoreCase);
        var invert = baseFacesRight
            ? MoveDirectionResolver.IsLeft(direction.Value)
            : MoveDirectionResolver.IsRight(direction.Value);
        return flip ^ invert;
    }

    /// <summary>预览路径上的八方向；路径在端点或静止时返回 null。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>当前八方向；无有效方向时为 null。</returns>
    private MoveDirection8? PreviewMoveDirection8(PlacedObject item)
    {
        var direction = PreviewMoveDirection(item);
        return direction is null
            ? null
            : MoveDirectionResolver.FromVector(direction.Value.X, direction.Value.Y);
    }

    /// <summary>预览路径上的当前切向；往返模式后半程自动取反。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>归一化方向向量；路径无效时为 null。</returns>
    private Point? PreviewMoveDirection(PlacedObject item)
    {
        if (!PreviewMode || !PreviewShowPaths || !HasPreviewPath(item))
        {
            return null;
        }

        var total = PathMath.Length(item.PathPoints);
        if (total <= 0)
        {
            return null;
        }

        var speed = AnimationSpeeds.DefaultPreviewMovement * AnimationSpeeds.Clamp(item.Speed);
        var raw = Math.Max(0, _previewElapsedSeconds) * speed;
        var mode = PathMath.ParseWalkMode(item.WalkMode);
        var walked = PathMath.NormalizeDistance(raw, total, mode);
        var direction = PathMath.DirectionAtDistance(item.PathPoints, walked);
        if (direction is null)
        {
            return null;
        }

        // 往返模式后半程沿原路返回，路段本身仍指向前进方向，需要取反。
        if (mode == PathPlaybackMode.PingPong && raw % (total * 2) > total)
        {
            direction = new Point(-direction.Value.X, -direction.Value.Y);
        }

        return direction;
    }

    /// <summary>当前方向是否存在对应的 Spine / 序列帧行走动作。</summary>
    /// <param name="item">地图实例。</param>
    /// <param name="direction">八方向。</param>
    /// <returns>命中方向动作时为 true。</returns>
    private bool HasDirectionalWalk(PlacedObject item, MoveDirection8 direction)
    {
        if (!UsesSpineVisual(item) && ResolveClip(item) is null)
        {
            return false;
        }

        var available = AvailableAnimationNames(item);
        if (available is null || available.Count == 0)
        {
            return false;
        }

        var walkAction = AnimationActionResolver.Resolve(item, true);
        return DirectionalAnimationResolver.HasDirectional(walkAction, direction, available);
    }

    /// <summary>取实例当前视觉源可用的动作名。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>Spine 骨架动画名或序列帧目录名；视觉源缺失时为 null。</returns>
    private IReadOnlyList<string>? AvailableAnimationNames(PlacedObject item)
    {
        if (string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase))
        {
            return _spineAnimations?.Find(item.SpineAsset)?.Animations;
        }

        return _animations?.Names;
    }

    /// <summary>预览中描出所有角色路径、路径点序号，以及角色当前脚底位置。</summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    private void DrawPreviewPaths(DrawingContext context, MapConfigData config)
    {
        var thin = Hairline;
        var pathShadowPen = new Pen(UiTheme.MapView.PathShadow, 5.4 * thin);
        var pathPen = new Pen(UiTheme.MapView.Path, 2.2 * thin);
        var pointBrush = UiTheme.MapView.PathPointFill;
        var pointPen = new Pen(UiTheme.MapView.PathPointBorder, 1.0 * thin);
        var markerBrush = UiTheme.MapView.PathMarker;
        var markerPen = new Pen(UiTheme.MapView.PathMarkerBorder, 1.5 * thin);
        var labelBrush = UiTheme.White;
        var pointRadius = 4.0 * thin;
        var markerRadius = 7.0 * thin;

        foreach (var item in config.Objects.Where(HasPreviewPath))
        {
            Point? previous = null;
            var index = 0;
            foreach (var pathPoint in item.PathPoints)
            {
                var current = new Point(pathPoint.X, pathPoint.Y);
                if (previous is { } from)
                {
                    context.DrawLine(pathShadowPen, from, current);
                    context.DrawLine(pathPen, from, current);
                }

                var dot = new Rect(current.X - pointRadius, current.Y - pointRadius, pointRadius * 2, pointRadius * 2);
                context.DrawEllipse(pointBrush, pointPen, dot.Center, pointRadius, pointRadius);

                var label = CanvasText.Create($"P{index}", 11 * thin, labelBrush);
                context.DrawText(label, new Point(current.X + 6 * thin, current.Y - 17 * thin));
                previous = current;
                index++;
            }

            var foot = PreviewFootPoint(item);
            if (foot is { } marker)
            {
                var dot = new Rect(marker.X - markerRadius, marker.Y - markerRadius, markerRadius * 2, markerRadius * 2);
                context.DrawEllipse(markerBrush, markerPen, dot.Center, markerRadius, markerRadius);
            }
        }
    }

    /// <summary>取实例贴图：优先 image 字段，其次模板 ResName，找不到返回 null。</summary>
    private Bitmap? Sprite(PlacedObject item)
    {
        // 导入 PSD 后切片只在内存里：优先用预览位图，落盘后由 ClearRuntimeSprites 切回磁盘解析
        if (_runtimeSprites.TryGetValue(item.Id, out var runtimeSprite))
        {
            return runtimeSprite;
        }

        if (_assets is null)
        {
            return null;
        }

        // 同一个物件（同 id / 同路径 / 同名字 / 同层 / 同章节关卡）只解析一次：
        // 拖动时每帧都会重画 500+ 物件，之前每次都要走 File.Exists + 三级目录查找，帧率被磁盘拖死
        var key = string.Concat(
            item.Id, "|", item.ImagePath, "|", item.Name, "|", item.LayerType, "|", item.DisplayLayer,
            "|", _config?.ChapterId, "|", _config?.SceneId);
        if (_spriteCache.TryGetValue(key, out var cachedSprite))
        {
            return cachedSprite;
        }

        Bitmap? sprite = null;
        if (!string.IsNullOrWhiteSpace(item.ImagePath))
        {
            sprite = _assets.Load(item.ImagePath);
        }

        if (sprite is null && !string.IsNullOrWhiteSpace(item.Name))
        {
            // Unity 口径兜底：按「资源名 + 层键 + 章节 / 关卡」三级目录查找（本关 → 章节 Shared → 全局 Shared）。
            // 资源名统一去扩展名，避免 JSON 里写了 bg_rock_1.png 时拼成 bg_rock_1.png.png。
            var stem = Path.GetFileNameWithoutExtension(item.Name);
            sprite = _assets.LoadByLayer(stem, item.LayerType, _config?.ChapterId, _config?.SceneId)
                ?? _assets.Load(stem)
                ?? _assets.Load(item.Name);
        }

        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>清空贴图解析缓存（导入烘焙、资源根变更后调用，避免新落盘的图被旧 null 结果挡住）。</summary>
    public void ClearSpriteCache() => _spriteCache.Clear();

    /// <summary>
    /// 动画目录内容变化后调用：清空帧缓存并重新判断是否需要启动 30 FPS 检查。
    /// 导入 / 删除动画片段、切换关卡后都会走这里。
    /// </summary>
    public void RefreshAnimations()
    {
        _animationFrameCache.Clear();
        _lastAnimationSignature = long.MinValue;
        EnsureAnimationTimer();
        InvalidateVisual();
    }

    /// <summary>按当前目录与地图实例决定是否启动动画 / 预览检查定时器。</summary>
    private void EnsureAnimationTimer()
    {
        if (_animationTimer is null)
        {
            return;
        }

        var hasSpriteAnimation = _animations is not null
            && _config is not null
            && PreviewShowAnimations
            && _config.Objects.Any(_animations.IsAnimated);
        var hasSpineAnimation = _spineAnimations is not null
            && _config is not null
            && PreviewShowAnimations
            && _config.Objects.Any(IsSpineAnimated);
        var hasPreviewPath = PreviewMode
            && PreviewShowPaths
            && _config is not null
            && _config.Objects.Any(HasPreviewPath);
        var hasAnimation = hasSpriteAnimation || hasSpineAnimation || hasPreviewPath;
        if (hasAnimation && !_animationTimer.IsEnabled)
        {
            _lastSpineTick = CurrentAnimationTime;
            _lastPreviewClockSeconds = _previewClock.Elapsed.TotalSeconds;
            _animationTimer.Start();
        }
        else if (!hasAnimation && _animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
        }
    }

    /// <summary>当前动画时间：编辑器用真实时钟，预览模式用可暂停 / 调速的预览时钟。</summary>
    private double CurrentAnimationTime => _previewMode
        ? _previewElapsedSeconds
        : _animationClock.Elapsed.TotalSeconds;

    /// <summary>物件是否以 Spine 作为视觉来源：只看动画来源，不看资产是否加载成功。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>AnimationKind 为 spine 时为 true。</returns>
    private static bool UsesSpineVisual(PlacedObject item) =>
        string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase);

    /// <summary>判断实例当前是否应推进 Spine 播放；视觉来源与是否播放分开。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>有可用资产、启用动画且允许预览动画时为 true。</returns>
    private bool IsSpineAnimated(PlacedObject item)
    {
        return PreviewShowAnimations
            && item.AnimEnabled
            && UsesSpineVisual(item)
            && _spineAnimations?.Find(item.SpineAsset) is not null;
    }

    /// <summary>
    /// 定时器 tick：只比较「当前帧号签名」，签名变化才触发整幅重绘。
    /// 预览模式下同时推进预览时钟，角色沿路径移动也靠这里重绘。
    /// 30 FPS 检查本身不画图，静态地图或同一帧内不会产生任何绘制开销。
    /// </summary>
    private void TickAnimations()
    {
        if (_config is null)
        {
            return;
        }

        var previewMoved = UpdatePreviewClock();
        var hasSpine = UpdateSpineAnimations();
        if (_animations is null || _animations.Count == 0)
        {
            if (hasSpine || previewMoved)
            {
                InvalidateVisual();
            }

            return;
        }

        var signature = BuildAnimationSignature();
        if (signature != _lastAnimationSignature || hasSpine || previewMoved)
        {
            _lastAnimationSignature = signature;
            InvalidateVisual();
        }
    }

    /// <summary>按真实时间推进预览时钟；暂停时不前进，返回是否有需要重绘的状态变化。</summary>
    /// <returns>预览正在播放且时间前进时为 true。</returns>
    private bool UpdatePreviewClock()
    {
        if (!_previewMode)
        {
            return false;
        }

        var now = _previewClock.Elapsed.TotalSeconds;
        var delta = Math.Max(0, now - _lastPreviewClockSeconds);
        _lastPreviewClockSeconds = now;
        if (_previewPaused || delta <= 0)
        {
            return false;
        }

        // 切窗口回来可能攒出很大的间隔，单帧步长限到 0.1s，避免角色瞬移
        var step = Math.Min(delta, 0.1) * _previewPlaybackSpeed;
        _previewElapsedSeconds += step;
        AdvanceTrafficSimulation(step);
        return true;
    }

    /// <summary>按真实时间推进所有 Spine 实例；返回当前是否存在需要重绘的实例。</summary>
    /// <returns>至少一个 Spine 实例在播放时为 true。</returns>
    private bool UpdateSpineAnimations()
    {
        if (_spineAnimations is null || _config is null)
        {
            return false;
        }

        var now = CurrentAnimationTime;
        var delta = now - _lastSpineTick;
        _lastSpineTick = now;
        if (delta <= 0)
        {
            return false;
        }

        // 切窗口 / 断点回来可能攒出几秒，限制单帧步长避免骨骼瞬移
        delta = Math.Min(delta, 0.1);
        var count = 0;
        foreach (var item in _config.Objects)
        {
            if (!IsSpineAnimated(item))
            {
                continue;
            }

            var player = GetSpinePlayer(item);
            if (player is null)
            {
                continue;
            }

            // 动作切换：沿路径移动时走行走动作，停住 / 单程终点回待机动作；
            // 行走动作强制循环，避免 Loop=false 的待机配置让角色走一半就停帧。
            // 资产提供 walk_up / walk_down_right 等方向动作时优先使用，否则回落到通用 walk。
            var moving = IsPreviewPathMoving(item);
            var action = AnimationActionResolver.Resolve(item, moving);
            if (moving && PreviewMoveDirection8(item) is { } direction)
            {
                action = DirectionalAnimationResolver.Resolve(action, direction, AvailableAnimationNames(item));
            }

            player.EnsureAnimation(action, moving || item.Loop);
            player.Update(delta, AnimationSpeeds.Clamp(item.Speed));
            count++;
        }

        return count > 0;
    }

    /// <summary>按实例 ID 取或创建 Spine 播放器。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>播放器；资产未命中返回 null。</returns>
    private SpinePlayer? GetSpinePlayer(PlacedObject item)
    {
        var asset = _spineAnimations?.Find(item.SpineAsset);
        if (asset == null)
        {
            return null;
        }

        if (_spinePlayers.TryGetValue(item.Id, out var player))
        {
            return player;
        }

        player = asset.CreatePlayer();
        _spinePlayers[item.Id] = player;
        return player;
    }

    /// <summary>把所有启用动画实例的当前帧号折成一个 64 位签名。</summary>
    /// <returns>没有可播放动画时为 long.MinValue；否则为稳定的帧号哈希。</returns>
    private long BuildAnimationSignature()
    {
        if (_animations is null || _config is null)
        {
            return long.MinValue;
        }

        var elapsed = CurrentAnimationTime;
        var signature = 1469598103934665603L;
        var animated = 0;
        foreach (var item in _config.Objects)
        {
            var clip = ResolveClip(item);
            if (clip is null)
            {
                continue;
            }

            var frame = SpriteAnimationCatalog.FrameIndex(
                clip,
                elapsed,
                AnimationSpeeds.Clamp(item.Speed),
                IsAnimationLooping(item));
            signature = unchecked(signature * 31 + item.Id.GetHashCode() * 17L + frame);
            animated++;
        }

        return animated == 0 ? long.MinValue : signature;
    }

    /// <summary>按实例的 AnimEnabled + AnimName 解析可用片段。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>片段；未启用 / 未配置 / 缺帧时返回 null。</param>
    private SpriteAnimationClip? ResolveClip(PlacedObject item)
    {
        if (!PreviewShowAnimations || !item.AnimEnabled || UsesSpineVisual(item) || _animations is null)
        {
            return null;
        }

        var moving = IsPreviewPathMoving(item);
        var action = AnimationActionResolver.Resolve(item, moving);
        if (moving && PreviewMoveDirection8(item) is { } direction)
        {
            action = DirectionalAnimationResolver.Resolve(action, direction, _animations.Names);
        }

        return _animations.Find(action)
            ?? (moving ? _animations.Find(item.AnimName) : null);
    }

    /// <summary>预览中行走动作强制循环；待机动作仍按实例的「循环」开关。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>当前动作是否循环。</returns>
    private bool IsAnimationLooping(PlacedObject item) => IsPreviewPathMoving(item) || item.Loop;

    /// <summary>取实例当前帧位图；帧路径解析失败时返回 null，由调用方回退静态贴图。</summary>
    /// <param name="item">地图实例。</param>
    /// <param name="clip">已命中的动画片段。</param>
    /// <returns>当前帧位图。</returns>
    private Bitmap? AnimatedSprite(PlacedObject item, SpriteAnimationClip clip)
    {
        if (_assets is null || clip.Frames.Count == 0)
        {
            return null;
        }

        var frame = SpriteAnimationCatalog.FrameIndex(
            clip,
            CurrentAnimationTime,
            AnimationSpeeds.Clamp(item.Speed),
            IsAnimationLooping(item));
        var path = clip.Frames[Math.Clamp(frame, 0, clip.Frames.Count - 1)];
        var key = string.Concat(clip.Name, "|", frame, "|", path);
        if (_animationFrameCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitmap = _assets.Load(path);
        _animationFrameCache[key] = bitmap;
        return bitmap;
    }

    /// <summary>清空序列帧缓存（动画目录变更 / 切关卡 / 资源根变更后调用）。</summary>
    public void ClearAnimationCache() => _animationFrameCache.Clear();

    /// <summary>登记一张内存贴图（导入 PSD 时用，磁盘上没有对应 PNG）。</summary>
    /// <param name="objectId">物件 ID。</param>
    /// <param name="bitmap">已解码的位图。</param>
    public void SetRuntimeSprite(string objectId, Bitmap bitmap)
    {
        _runtimeSprites[objectId] = bitmap;
        _spriteCache.Clear();
    }

    /// <summary>换掉整幅内存底图（导入 PSD 后立即预览合成底图，null 表示回到磁盘 / 底色）。</summary>
    /// <param name="bitmap">合成底图。</param>
    public void SetRuntimeBackground(Bitmap? bitmap)
    {
        _runtimeBackground = bitmap;
        InvalidateVisual();
    }

    /// <summary>清空内存贴图（点「导出切片」落盘后调用，画布改走磁盘资源）。</summary>
    public void ClearRuntimeSprites()
    {
        _runtimeSprites.Clear();
        _runtimeBackground = null;
        _spriteCache.Clear();
    }

    #endregion
}


