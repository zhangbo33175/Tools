/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AnimationModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   动画模型：动画来源常量与序列帧片段
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MapEditorTool.Models;

/// <summary>
/// 实例动画来源。静态贴图是缺省值；序列帧走 <see cref="SpriteAnimationCatalog"/>，
/// Spine 走导入的 <c>SpineAnimations.json</c> 目录并按骨架动画名播放。
/// </summary>
public static class AnimationKinds
{
    /// <summary>静态贴图 / 序列帧（缺省）。</summary>
    public const string Sprite = "sprite";

    /// <summary>Spine 骨骼动画。</summary>
    public const string Spine = "spine";

    /// <summary>下拉框显示项。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[] { "序列帧", "Spine" };

    /// <summary>代码值转显示文案。</summary>
    /// <param name="code">代码值。</param>
    /// <returns>显示文案；未知值按序列帧处理。</param>
    public static string ToDisplay(string? code) =>
        string.Equals(code, Spine, StringComparison.OrdinalIgnoreCase) ? "Spine" : "序列帧";

    /// <summary>显示文案转代码值。</summary>
    /// <param name="display">显示文案。</param>
    /// <returns>代码值。</returns>
    public static string ToCode(string? display) =>
        string.Equals(display, "Spine", StringComparison.OrdinalIgnoreCase) ? Spine : Sprite;
}

/// <summary>
/// 序列帧动画片段。名字与地图 JSON 的 <c>anim</c> 字段同口径，
/// <see cref="Frames"/> 按播放顺序保存 PNG/JPG 路径（绝对路径或 Unity 工程内相对路径均可）。
/// </summary>
public sealed class SpriteAnimationClip
{
    /// <summary>动画名，对应 <see cref="PlacedObject.AnimName"/> / JSON 的 <c>anim</c>。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>播放帧率。</summary>
    public double Fps { get; set; } = 10.0;

    /// <summary>是否循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>帧图片路径，按数组顺序播放。</summary>
    public List<string> Frames { get; set; } = new();

    /// <summary>帧数（不参与序列化，避免配置里出现冗余字段）。</summary>
    [JsonIgnore]
    public int FrameCount => Frames.Count;

    /// <summary>片段是否可用于画布播放。</summary>
    [JsonIgnore]
    public bool IsUsable => !string.IsNullOrWhiteSpace(Name) && Frames.Count > 0 && Fps > 0;

    /// <summary>复制一份片段，避免 UI 编辑时改到目录里的对象。</summary>
    /// <returns>片段副本。</returns>
    public SpriteAnimationClip Clone() => new()
    {
        Name = Name,
        Fps = Fps,
        Loop = Loop,
        Frames = new List<string>(Frames),
    };
}
