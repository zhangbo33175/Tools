/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpinePlayer.cs
 * author:    云毅
 * created:   2026
 * descrip:   单个 Spine 实例的骨骼播放状态
 ***************************************************************/

using System;
using Spine;

namespace MapEditorTool.Services;

#region 播放器

/// <summary>
/// 单个 Spine 实例的播放器。每个放置物 / 预览目标各自持有一个，
/// 避免多个实例共享骨骼状态导致互相串帧。
/// </summary>
public sealed class SpinePlayer
{
    private readonly SpineAnimationAsset _asset;
    private readonly Skeleton _skeleton;
    private readonly AnimationState _state;
    private string _animationName = string.Empty;
    private bool _loop;

    /// <summary>创建播放器，并立即摆到 Setup Pose。</summary>
    /// <param name="asset">所属资产。</param>
    internal SpinePlayer(SpineAnimationAsset asset)
    {
        _asset = asset;
        _skeleton = new Skeleton(asset.Data);
        var stateData = new AnimationStateData(asset.Data)
        {
            DefaultMix = 0.15f,
        };
        _state = new AnimationState(stateData);
        _skeleton.SetToSetupPose();
        _skeleton.UpdateWorldTransform();
    }

    /// <summary>当前播放的动画名。</summary>
    public string AnimationName => _animationName;

    /// <summary>当前动画总时长（秒）。</summary>
    public float Duration
    {
        get
        {
            var entry = _state.GetCurrent(0);
            return entry?.Animation?.Duration ?? 0f;
        }
    }

    /// <summary>当前轨道时间（秒），用于预览窗显示播放进度。</summary>
    public float TrackTime => _state.GetCurrent(0)?.TrackTime ?? 0f;

    /// <summary>当前动画是否循环。</summary>
    public bool Loop => _loop;

    /// <summary>骨架对象，供渲染器读取 DrawOrder / Slots。</summary>
    public Skeleton Skeleton => _skeleton;

    /// <summary>确保播放指定动画；名字为空时使用资产默认动画。</summary>
    /// <param name="animationName">骨架动画名。</param>
    /// <param name="loop">是否循环。</param>
    public void EnsureAnimation(string? animationName, bool loop)
    {
        var resolved = ResolveAnimationName(animationName);
        if (string.Equals(_animationName, resolved, StringComparison.Ordinal) && _loop == loop)
        {
            return;
        }

        _animationName = resolved;
        _loop = loop;
        _state.ClearTracks();
        _skeleton.SetToSetupPose();
        _state.SetAnimation(0, resolved, loop);
        _state.Apply(_skeleton);
        _skeleton.UpdateWorldTransform();
    }

    /// <summary>按时间增量推进动画并应用到骨架。</summary>
    /// <param name="deltaSeconds">真实时间增量。</param>
    /// <param name="speed">播放速度倍率。</param>
    public void Update(double deltaSeconds, double speed)
    {
        if (deltaSeconds <= 0 || speed <= 0)
        {
            return;
        }

        _state.Update((float)(deltaSeconds * speed));
        _state.Apply(_skeleton);
        _skeleton.UpdateWorldTransform();
    }

    /// <summary>重新从第 0 秒播放当前动画。</summary>
    public void Restart()
    {
        var entry = _state.GetCurrent(0);
        if (entry == null)
        {
            EnsureAnimation(_asset.DefaultAnimation, true);
            return;
        }

        entry.TrackTime = 0f;
        _state.Apply(_skeleton);
        _skeleton.UpdateWorldTransform();
    }

    /// <summary>把骨架摆回 Setup Pose，用于停止播放后的静态显示。</summary>
    public void ResetToSetupPose()
    {
        _state.ClearTracks();
        _skeleton.SetToSetupPose();
        _skeleton.UpdateWorldTransform();
        _animationName = string.Empty;
    }

    /// <summary>解析动画名：显式名字优先，否则资产默认，最后兜底第一个。</summary>
    /// <param name="animationName">显式名字。</param>
    /// <returns>有效动画名。</returns>
    private string ResolveAnimationName(string? animationName)
    {
        if (!string.IsNullOrWhiteSpace(animationName))
        {
            foreach (var name in _asset.Animations)
            {
                if (string.Equals(name, animationName, StringComparison.Ordinal))
                {
                    return name;
                }
            }
        }

        foreach (var name in _asset.Animations)
        {
            if (string.Equals(name, _asset.DefaultAnimation, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return _asset.Animations.Count > 0 ? _asset.Animations[0] : string.Empty;
    }
}

#endregion
