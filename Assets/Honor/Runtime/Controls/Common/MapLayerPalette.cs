/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapLayerPalette.cs
 * author:    云毅
 * created:   2026
 * descrip:   八层代表色：画布占位色与列表缩略图底色共用
 ***************************************************************/

using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 图层代表色。颜色属于展示层，不放进 <see cref="MapLayerKeys"/> 数据模型；
/// 画布占位填充与物品元件列表缩略图底色都从这里取，保证同一层在列表和画布上颜色一致。
/// </summary>
public static class MapLayerPalette
{
    #region 常量

    /// <summary>未知层 / 收集物以外空值的回退灰。</summary>
    private static readonly Color Fallback = Color.FromRgb(0x8A, 0x8F, 0x9C);

    #endregion

    #region 公开接口

    /// <summary>取图层代表色；未知层回退中性灰。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>代表色。</returns>
    public static Color Of(string? layerKey) => layerKey switch
    {
        MapLayerKeys.Background => Color.FromRgb(0x0A, 0x6E, 0x96),
        MapLayerKeys.Greenery => Color.FromRgb(0x43, 0xC0, 0x8A),
        MapLayerKeys.Road => Color.FromRgb(0x9B, 0xB0, 0xC6),
        MapLayerKeys.Building => Color.FromRgb(0xE8, 0xA2, 0x3F),
        MapLayerKeys.Prop => Color.FromRgb(0x9B, 0x8B, 0xE8),
        MapLayerKeys.Character => Color.FromRgb(0xFF, 0x7F, 0xA3),
        MapLayerKeys.Foreground => Color.FromRgb(0xC7, 0x9A, 0x6B),
        MapLayerKeys.Bubble => Color.FromRgb(0xFF, 0xD2, 0x4A),
        MapLayerKeys.Collectible => Color.FromRgb(0x2F, 0xC5, 0x8B),
        _ => Fallback,
    };

    /// <summary>取图层代表色画刷；每次返回独立画刷，调用方可安全改透明度。</summary>
    /// <param name="layerKey">图层键。</param>
    /// <returns>图层代表色画刷。</returns>
    public static IBrush BrushOf(string? layerKey) => new SolidColorBrush(Of(layerKey));

    #endregion
}
