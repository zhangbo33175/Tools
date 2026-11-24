/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationActionResolver.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件动作动画选择：待机 / 行走共用一套口径，序列帧与 Spine 各自取对应字段
 ***************************************************************/

using System;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 动作选择

/// <summary>
/// 物件动作动画选择。地图预览按「是否在沿路径移动」在待机动作与行走动作之间切换，
/// 序列帧取 <see cref="PlacedObject.MoveAnimName"/>，Spine 取 <see cref="PlacedObject.SpineWalkAnimation"/>；
/// 行走字段为空时回落到待机动作，避免旧档没有配置行走动作时直接不动。
/// </summary>
public static class AnimationActionResolver
{
    #region 公开方法

    /// <summary>按当前运动状态解析应播放的动画名。</summary>
    /// <param name="item">地图实例。</param>
    /// <param name="moving">是否正在沿路径移动。</param>
    /// <returns>动画名；字段都为空时返回空串，由播放器使用资产默认动画。</returns>
    public static string Resolve(PlacedObject item, bool moving)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase))
        {
            return moving
                ? FirstNonEmpty(item.SpineWalkAnimation, item.SpineAnimation)
                : item.SpineAnimation;
        }

        return moving
            ? FirstNonEmpty(item.MoveAnimName, item.AnimName)
            : item.AnimName;
    }

    #endregion

    #region 辅助

    /// <summary>取第一个非空文本。</summary>
    /// <param name="primary">首选值。</param>
    /// <param name="fallback">回落值。</param>
    /// <returns>首选值或回落值。</returns>
    private static string FirstNonEmpty(string? primary, string? fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback ?? string.Empty : primary.Trim();

    #endregion
}

#endregion
