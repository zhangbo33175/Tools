/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteAnimationCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   序列帧动画目录：按 anim 名称索引帧序列，统一帧索引换算规则
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 序列帧动画目录。地图 JSON 只保存 <c>anim</c> 名称，具体帧文件由这里按名称解析；
/// 未命中 / 未启用 / 缺帧时画布自动回退静态贴图，不会把实例画成空白。
/// </summary>
public sealed class SpriteAnimationCatalog
{
    #region 字段

    private readonly Dictionary<string, SpriteAnimationClip> _clips =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string>? _names;

    #endregion

    #region 属性

    /// <summary>已注册的动画数量。</summary>
    public int Count => _clips.Count;

    /// <summary>按名称排序的动画名列表，供下拉框与预览列表使用。</summary>
    public IReadOnlyList<string> Names =>
        _names ??= _clips.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>全部动画片段（按名称排序）。</summary>
    public IReadOnlyList<SpriteAnimationClip> Clips =>
        _clips.Values.OrderBy(clip => clip.Name, StringComparer.OrdinalIgnoreCase).ToList();

    #endregion

    #region 公开方法

    /// <summary>清空目录。加载配置或导入失败时使用。</summary>
    public void Clear()
    {
        _clips.Clear();
        _names = null;
    }

    /// <summary>注册一个片段；同名覆盖，空名称 / 无帧的片段忽略。</summary>
    /// <param name="clip">动画片段。</param>
    public void Register(SpriteAnimationClip? clip)
    {
        if (clip is null || !clip.IsUsable)
        {
            return;
        }

        _clips[clip.Name.Trim()] = clip.Clone();
        _names = null;
    }

    /// <summary>按动画名查找片段。</summary>
    /// <param name="name">动画名（对应 JSON 的 <c>anim</c>）。</param>
    /// <returns>片段；找不到或不可用时返回 null。</returns>
    public SpriteAnimationClip? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _clips.TryGetValue(name.Trim(), out var clip) && clip.IsUsable ? clip : null;
    }

    /// <summary>判断实例当前是否会播放帧动画。</summary>
    /// <param name="item">地图实例。</param>
    /// <returns>已启用动画且能解析到可用片段时为 true。</returns>
    public bool IsAnimated(PlacedObject? item) =>
        item is { AnimEnabled: true } && Find(item.AnimName) is not null;

    /// <summary>
    /// 按经过时间换算当前帧号。循环片段取模，非循环片段钳到最后一帧。
    /// </summary>
    /// <param name="clip">动画片段。</param>
    /// <param name="elapsedSeconds">从开始播放到现在的秒数。</param>
    /// <param name="speed">播放速度倍率。</param>
    /// <param name="loopOverride">非空时覆盖片段的循环设置（预览弹窗用）。</param>
    /// <returns>帧索引，始终落在有效范围内。</returns>
    public static int FrameIndex(
        SpriteAnimationClip clip,
        double elapsedSeconds,
        double speed = 1.0,
        bool? loopOverride = null)
    {
        if (clip is null || clip.Frames.Count == 0)
        {
            return 0;
        }

        var frames = clip.Frames.Count;
        var fps = AnimationSpeeds.Clamp(clip.Fps);
        var rate = AnimationSpeeds.Clamp(speed);
        var index = (int)Math.Floor(Math.Max(0, elapsedSeconds) * rate * fps);
        var loop = loopOverride ?? clip.Loop;

        return loop
            ? ((index % frames) + frames) % frames
            : Math.Clamp(index, 0, frames - 1);
    }

    /// <summary>片段的单帧时长，秒。</summary>
    /// <param name="clip">动画片段。</param>
    /// <returns>单帧时长；Fps 非法时按 10 FPS 兜底。</returns>
    public static double FrameDuration(SpriteAnimationClip clip) =>
        1.0 / AnimationSpeeds.Clamp(clip.Fps);

    /// <summary>片段的完整循环时长，秒。</summary>
    /// <param name="clip">动画片段。</param>
    /// <returns>全部帧播放一次的时长。</returns>
    public static double Duration(SpriteAnimationClip clip) =>
        clip.Frames.Count <= 0 ? 0 : clip.Frames.Count / AnimationSpeeds.Clamp(clip.Fps);

    #endregion
}
