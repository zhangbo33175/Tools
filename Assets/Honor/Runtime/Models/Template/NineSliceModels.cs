/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NineSliceModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   九宫格模型：边距、中心模式与显示名/代码互转
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Models;

/// <summary>
/// 九宫格（Nine-Slice）配置：按素材像素给出左 / 右 / 上 / 下四条边距，
/// 拉伸绘制时四角不参与缩放，边与中心分别填充，避免边框和圆角被拉变形。
/// </summary>
public sealed class NineSliceSettings
{
    /// <summary>边距下限：负值不允许写入配置表 / JSON。</summary>
    public const int MinInset = 0;

    /// <summary>是否启用九宫格。</summary>
    public bool Enabled { get; set; }

    /// <summary>左边距（素材原始像素）。</summary>
    public int Left { get; set; }

    /// <summary>右边距（素材原始像素）。</summary>
    public int Right { get; set; }

    /// <summary>上边距（素材原始像素）。</summary>
    public int Top { get; set; }

    /// <summary>下边距（素材原始像素）。</summary>
    public int Bottom { get; set; }

    /// <summary>中心区域填充方式，见 <see cref="NineSliceModes"/>。</summary>
    public string CenterMode { get; set; } = NineSliceModes.Stretch;

    /// <summary>四条边距是否都非负且左+右 / 上+下没有越过素材尺寸。</summary>
    /// <param name="pixelWidth">素材原始像素宽。</param>
    /// <param name="pixelHeight">素材原始像素高。</param>
    /// <returns>可以按九宫格绘制时为 true。</returns>
    public bool IsUsable(double pixelWidth, double pixelHeight)
    {
        return Enabled
            && Left >= 0
            && Right >= 0
            && Top >= 0
            && Bottom >= 0
            && Left + Right < pixelWidth - 1
            && Top + Bottom < pixelHeight - 1;
    }

    /// <summary>
    /// 把四条边距夹到非负，返回自身便于在对象初始化后链式调用。
    /// 表数据、JSON 与模板映射都走这一处，避免各写一份 <c>Math.Max(0, …)</c>。
    /// </summary>
    /// <returns>当前实例。</returns>
    public NineSliceSettings ClampMargins()
    {
        Left = ClampInset(Left);
        Right = ClampInset(Right);
        Top = ClampInset(Top);
        Bottom = ClampInset(Bottom);
        return this;
    }

    /// <summary>把单个边距夹到 <see cref="MinInset"/>，供逐列解析场景复用。</summary>
    /// <param name="value">原始边距。</param>
    /// <returns>非负边距。</returns>
    public static int ClampInset(int value) => Math.Max(MinInset, value);

    /// <summary>按四条边距与中心模式创建配置：边距统一夹到非负，中心模式统一转代码值。</summary>
    /// <param name="enabled">是否启用九宫格。</param>
    /// <param name="left">左边距。</param>
    /// <param name="right">右边距。</param>
    /// <param name="top">上边距。</param>
    /// <param name="bottom">下边距。</param>
    /// <param name="centerMode">中心填充方式，显示名或代码值均可。</param>
    /// <returns>归一后的九宫格配置。</returns>
    public static NineSliceSettings FromMargins(
        bool enabled,
        int left,
        int right,
        int top,
        int bottom,
        string? centerMode) =>
        new NineSliceSettings
        {
            Enabled = enabled,
            Left = left,
            Right = right,
            Top = top,
            Bottom = bottom,
            CenterMode = NineSliceModes.ToCode(centerMode),
        }.ClampMargins();

    /// <summary>复制一份配置，避免模板之间共享同一个可变对象。</summary>
    /// <returns>配置副本。</returns>
    public NineSliceSettings Clone() => new()
    {
        Enabled = Enabled,
        Left = Left,
        Right = Right,
        Top = Top,
        Bottom = Bottom,
        CenterMode = CenterMode,
    };
}

/// <summary>九宫格中心区域的填充方式。</summary>
public static class NineSliceModes
{
    /// <summary>中心区域拉伸（默认，适合纯色 / 渐变）。</summary>
    public const string Stretch = "stretch";

    /// <summary>中心区域按素材原始尺寸平铺（适合纹理 / 网格）。</summary>
    public const string Tile = "tile";

    /// <summary>下拉框显示项。</summary>
    public static readonly IReadOnlyList<string> DisplayChoices = new[] { "拉伸", "平铺" };

    /// <summary>代码值转显示文案。</summary>
    /// <param name="code">代码值。</param>
    /// <returns>显示文案。</returns>
    public static string ToDisplay(string? code) =>
        string.Equals(code, Tile, StringComparison.OrdinalIgnoreCase) ? "平铺" : "拉伸";

    /// <summary>
    /// 显示文案或代码值转代码值。配置表存代码、UI 存显示名，
    /// 两条路径都走这里，必须保证 <c>tile</c> 往返后仍是 <c>tile</c>。
    /// </summary>
    /// <param name="display">显示文案「平铺 / 拉伸」或代码值 <c>tile / stretch</c>。</param>
    /// <returns>代码值。</returns>
    public static string ToCode(string? display) =>
        string.Equals(display, "平铺", StringComparison.Ordinal)
        || string.Equals(display, Tile, StringComparison.OrdinalIgnoreCase)
            ? Tile
            : Stretch;
}

