/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationSpeeds.cs
 * author:    云毅
 * created:   2026
 * descrip:   动画与预览移动速度的统一下限、基准与夹紧入口
 ***************************************************************/

using System;

namespace MapEditorTool.Services;

/// <summary>
/// 动画、序列帧与预览移动共用的速度规则。
/// <para>
/// 只定义“速度不能为零”和预览移动基准，不参与具体播放或时间推进。
/// </para>
/// </summary>
public static class AnimationSpeeds
{
    #region 常量与换算

    /// <summary>速度下限，避免零速或负速导致除零与时间不推进。</summary>
    public const double MinFactor = 0.01;

    /// <summary>预览移动的默认基准速度（世界像素/秒）。</summary>
    public const double DefaultPreviewMovement = 96.0;

    /// <summary>把速度夹到可播放下限；用于已有数值。</summary>
    /// <param name="speed">原始速度倍率。</param>
    /// <returns>不小于 <see cref="MinFactor"/> 的速度。</returns>
    // ---TODO 重构：动画与预览速度下限统一走此入口，不再各写 Math.Max(0.01, ...)。
    public static double Clamp(double speed) => Math.Max(MinFactor, speed);

    /// <summary>把可空速度夹到可播放下限；null 使用回退值。</summary>
    /// <param name="speed">原始速度倍率；null 表示未配置。</param>
    /// <param name="fallback">未配置时的回退速度。</param>
    /// <returns>不小于 <see cref="MinFactor"/> 的速度。</returns>
    public static double Effective(double? speed, double fallback = 1.0) =>
        Clamp(speed ?? fallback);

    #endregion
}
