/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  OcclusionAdjustmentStrategy.cs
 * author:    云毅
 * created:   2026
 * descrip:   遮挡调整策略的唯一收敛入口：导入、刷新画面、刷新层级共用同一套规则
 ***************************************************************/

using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 一次遮挡调整的结果。只统计真正写入配置的改动：
/// 背景组立体美术提层、平面件接缝贴合。
/// </summary>
public sealed record OcclusionAdjustmentResult(int BandPromoted, int SeamSnapped)
{
    /// <summary>本轮是否有写入型调整。</summary>
    public bool Any => BandPromoted > 0 || SeamSnapped > 0;

    /// <summary>状态栏用的简短描述；没有调整时返回空串。</summary>
    public string Summary
    {
        get
        {
            if (!Any)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (BandPromoted > 0)
            {
                parts.Add($"遮挡 自动提层 {BandPromoted}");
            }

            if (SeamSnapped > 0)
            {
                parts.Add($"接缝 贴合 {SeamSnapped}");
            }

            return string.Join("｜", parts);
        }
    }
}

/// <summary>
/// 2D 遮挡调整策略。把散落在导入、刷新画面、刷新层级里的自动修复收敛到这里：
/// <list type="number">
/// <item>显式 <c>BandOverride</c> / 图层键 → 三带（由 <see cref="LayerBands"/> 判定，不在这里重复）；</item>
/// <item>背景 / 道路 / 绿化组里的独立树、灌木、围栏、建筑、人物 → 提升到立体带；</item>
/// <item>池塘 / 水面 / 地毯等大平面件与上方立体件 1–24px 的窄缝 → 上移贴合；</item>
/// <item>最后才由 <see cref="LayerOrder"/> 按「带 → 立体脚底 Y → 层序」排序。</item>
/// </list>
/// <para>
/// 策略只做高置信度、可撤销的窄口修复：不删实例、不改尺寸 / 旋转 / 拉伸、不改用户手改的
/// <c>BandOverride</c>；不把两个立体物之间的自然间距当成接缝。
/// </para>
/// </summary>
public static class OcclusionAdjustmentStrategy
{
    #region 公开入口

    /// <summary>
    /// 对整张地图执行一轮遮挡调整。
    /// </summary>
    /// <param name="config">当前地图配置；会写入 <c>BandOverride</c> 与少量平面件 <c>Y</c>。</param>
    /// <param name="visualResolver">
    /// 按实例返回已解码切片的可选委托。刷新层级时由 <see cref="SceneDisplayAudit"/> 接入
    /// <see cref="AssetLibrary"/> 缓存位图，用于识别命名与画面不一致的背景植被；
    /// 传入 null 时只按名称提层（PSD 导入等无图片的场景）。
    /// </param>
    /// <returns>本轮实际写入的数量。</returns>
    public static OcclusionAdjustmentResult Apply(
        MapConfigData config,
        Func<PlacedObject, Bitmap?>? visualResolver = null)
    {
        var promoted = 0;
        foreach (var item in config.Objects)
        {
            // 先做层组预筛：明确立体名 / 明确平面名都不需要读图，只有名称有歧义
            // （例如 bg_rock_1_550_867 实际是灌木）才解码切片。
            var needsVisual = visualResolver is not null
                && LayerBands.IsPromotableLayer(item)
                && !LayerBands.IsStandingArt(item.Name)
                && !LayerBands.IsFlatArt(item.Name);
            var bitmap = needsVisual ? visualResolver!(item) : null;
            if (TryPromoteBackgroundStanding(item, bitmap))
            {
                promoted++;
            }
        }

        var snapped = SnapFlatSeams(config);
        return new OcclusionAdjustmentResult(promoted, snapped);
    }

    /// <summary>
    /// 导入 PSD 时为单个实例套用同一分带策略。
    /// </summary>
    /// <param name="item">新建的 PSD 实例。</param>
    /// <param name="visual">
    /// 已解码切片；名称与画面不一致时（例如 <c>bg_rock_1_550_867</c> 实际是绿色灌木），
    /// 由 <see cref="ArtSilhouetteInspector"/> 补足立体语义。导入阶段没有图片时传 null。
    /// </param>
    /// <returns>true = 已提升到立体带。</returns>
    public static bool TryPromoteBackgroundStanding(PlacedObject item, Bitmap? visual = null)
    {
        var visuallyStanding = ArtSilhouetteInspector.IsStandingVegetation(visual);
        if (!LayerBands.ShouldPromoteToStanding(item, visuallyStanding))
        {
            return false;
        }

        item.BandOverride = LayerBands.Standing;
        item.BandOverrideAuto = true;
        return true;
    }

    #endregion

    #region 接缝贴合

    /// <summary>
    /// 贴合「平面件 + 上方立体件」之间的窄缝：池塘贴着围栏、地毯贴着摊位这类小间距，
    /// 作者在 PSD 里通常是对齐的，导入后如果只差 1–24px，会把围栏底边悬空成一条绿色缝隙。
    /// <para>只动满足全部条件的实例：下方物件是平面名（pond/water/rug…）、尺寸 ≥120×80 且 ≤256、
    /// 与上方立体件水平重叠 ≥50%、垂直间隙 1–24px；不碰小草丛，也不碰两个立体物之间的自然间距。</para>
    /// </summary>
    private static int SnapFlatSeams(MapConfigData config)
    {
        var snapped = 0;
        foreach (var lower in config.Objects)
        {
            if (LayerBands.Of(lower) != LayerBands.Standing
                || !LayerBands.IsFlatArt(lower.Name)
                || lower.PixelWidth < 120
                || lower.PixelHeight < 80
                || lower.PixelWidth > 256
                || lower.PixelHeight > 256)
            {
                continue;
            }

            var lowerBounds = lower.Bounds;
            var bestGap = double.MaxValue;
            PlacedObject? bestUpper = null;
            foreach (var upper in config.Objects)
            {
                if (ReferenceEquals(upper, lower) || LayerBands.Of(upper) != LayerBands.Standing)
                {
                    continue;
                }

                var upperBounds = upper.Bounds;
                var gap = lowerBounds.Top - upperBounds.Bottom;
                if (gap <= 0 || gap > 24)
                {
                    continue;
                }

                var overlap = Math.Min(lowerBounds.Right, upperBounds.Right)
                    - Math.Max(lowerBounds.Left, upperBounds.Left);
                if (overlap < Math.Min(lowerBounds.Width, upperBounds.Width) * 0.5)
                {
                    continue;
                }

                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestUpper = upper;
                }
            }

            if (bestUpper is null)
            {
                continue;
            }

            // 上方件的脚底与下方件的顶边对齐；只上移这一段空隙，不缩放、不改 X。
            lower.Y -= (int)Math.Round(bestGap);
            snapped++;
        }

        return snapped;
    }

    #endregion
}
