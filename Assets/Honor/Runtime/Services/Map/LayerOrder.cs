/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LayerOrder.cs
 * author:    云毅
 * created:   2026
 * descrip:   与 Unity MapLayerCatalog 同一套八层 SortOrder 坐标系（基线步长 10、层内 10 档）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 八层层级坐标（数据口径）与遮挡排序（画面口径）。
/// <para>
/// 数据：<c>background 0 / greenery 10 / road 20 / building 30 / prop 40 / character 50 / foreground 60 / bubble 70</c>，
/// 层内 10 个档位，坐标与 Unity <c>MapLayerCatalog.GetSortOrder</c> + <c>MapToolConstants.LayerBaselineStep</c> 对齐，
/// 继续写进地图 JSON 供 Unity 读；它不再决定画面前后。
/// </para>
/// <para>
/// 画面：<see cref="Compare"/> 先按 <see cref="LayerBands"/> 分带，立体带跨图层按脚底 Y 排，
/// 八层坐标只在同 Y 时兜底——建筑不会因为「道具层数字更大」而无条件压住后方的灌木。
/// </para>
/// </summary>
public static class LayerOrder
{
    #region 常量

    /// <summary>相邻层 SortOrder 基线步长（Unity <c>LayerBaselineStepDefault</c>）。</summary>
    public const int BaselineStep = 10;

    /// <summary>每层可分配的层内档位数（Unity <c>LayerSlotCount</c>，必须小于 <see cref="BaselineStep"/>）。</summary>
    public const int SlotCount = 10;

    /// <summary>层内档位步长（Unity <c>LayerOrderSeqStep</code>）。</summary>
    public const int SeqStep = 1;

    /// <summary>旧版「带 × 1000 + 序位」坐标系的带步长，仅用于读旧档时换算。</summary>
    public const int LegacyBandStride = 1000;

    /// <summary>旧版「带 × 100 + 序位」坐标系的带步长。</summary>
    public const int LegacyHundredStride = 100;

    #endregion

    #region 层键与基线

    /// <summary>可用层键总数（Unity 八层）。</summary>
    public static int LayerCount => MapLayerKeys.AllKeys.Length;

    /// <summary>全局 SortOrder 上界：最后一层的最高档位。</summary>
    public static int MaxSortOrder => (LayerCount - 1) * BaselineStep + (SlotCount - 1);

    /// <summary>把任意层键 / 旧键归一成八层键（ground/grass/plant → greenery，cat → character，effect → foreground）。</summary>
    /// <param name="layerKey">原始键（可为空）。</param>
    /// <returns>八层键之一，无法识别回退道具层。</returns>
    public static string Normalize(string? layerKey)
    {
        var key = (layerKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            MapLayerKeys.Background or "bg" => MapLayerKeys.Background,
            MapLayerKeys.Greenery or "ground" or "grass" or "plant" => MapLayerKeys.Greenery,
            MapLayerKeys.Road or "street" => MapLayerKeys.Road,
            MapLayerKeys.Building or "buildings" or "house" => MapLayerKeys.Building,
            MapLayerKeys.Prop or "props" or "item" => MapLayerKeys.Prop,
            MapLayerKeys.Character or "cat" or "cats" => MapLayerKeys.Character,
            MapLayerKeys.Foreground or "effect" or "fx" => MapLayerKeys.Foreground,
            MapLayerKeys.Bubble or "bubbles" => MapLayerKeys.Bubble,
            _ => MapLayerKeys.Prop,
        };
    }

    /// <summary>有效层键：手选过显示层就以它为准，否则跟随归属层（Unity <c>ResolveDisplayLayer</c>）。</summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>八层键。</returns>
    public static string EffectiveLayer(PlacedObject item) =>
        Normalize(string.IsNullOrWhiteSpace(item.DisplayLayer) ? item.LayerType : item.DisplayLayer);

    /// <summary>层基线 SortOrder：background 0 / greenery 10 / … / bubble 70。</summary>
    /// <param name="layerKey">层键（旧键会先归一）。</param>
    /// <returns>基线值。</returns>
    public static int Base(string? layerKey)
    {
        var index = Array.IndexOf(MapLayerKeys.AllKeys, Normalize(layerKey));
        return Math.Max(0, index) * BaselineStep;
    }

    /// <summary>
    /// 参与排序的基线：默认取有效层基线；手改过「遮挡带」时按带位抬到该带的代表基线
    /// （地表 0 / 立体 = 道具层 40 / 叠加 = 前景层 60），保留原来的手改语义又不越出八层坐标。
    /// </param>
    /// <param name="item">已放置实例。</param>
    /// <returns>基线值。</returns>
    public static int OrderBase(PlacedObject item)
    {
        return item.BandOverride switch
        {
            LayerBands.Ground => 0,
            LayerBands.Standing => Base(MapLayerKeys.Prop),
            LayerBands.Overlay => Base(MapLayerKeys.Foreground),
            _ => Base(EffectiveLayer(item)),
        };
    }

    /// <summary>层内档位：按实例的<b>实际参与排序的基线</b>算，钳在 0–9。
    /// 手改过遮挡带的实例（如背景灌木自动提到立体带 40）必须用 <see cref="OrderBase"/>，
    /// 不能用原图层基线，否则属性卡会把 40 档算成“第 9 档”。</summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>层内档位。</returns>
    public static int SlotOf(PlacedObject item) =>
        Math.Clamp(item.SortOrder - OrderBase(item), 0, SlotCount - 1);

    /// <summary>按「层键 + 层内档位」组装 SortOrder。</summary>
    /// <param name="layerKey">层键。</param>
    /// <param name="slot">层内档位（自动钳到 0–9）。</param>
    /// <returns>SortOrder 值。</returns>
    public static int Build(string? layerKey, int slot) =>
        Base(layerKey) + Math.Clamp(slot, 0, SlotCount - 1) * SeqStep;

    #endregion

    #region 旧档换算

    /// <summary>
    /// 把旧存档的 SortOrder 换算到八层坐标（对应 Unity <c>NormalizeLegacySortOrder</code>）：
    /// 旧「带 × 1000 / 100 + 序位」先抽出层内序位，已是 0–79 的新坐标值直接钳制。
    /// </summary>
    /// <param name="item">已放置实例（提供层键 / 带位）。</param>
    /// <param name="raw">存档里的原始值。</param>
    /// <returns>八层坐标下的 SortOrder。</returns>
    public static int Normalize(PlacedObject item, int raw)
    {
        int slot;
        if (raw >= LegacyBandStride)
        {
            slot = raw % LegacyBandStride;
        }
        else if (raw >= LegacyHundredStride)
        {
            slot = raw % LegacyHundredStride;
        }
        else if (raw >= SlotCount)
        {
            // 0–79 是新坐标：按原层基线减去，落到层内档位
            slot = raw - Base(EffectiveLayer(item));
        }
        else
        {
            slot = raw;
        }

        return OrderBase(item) + Math.Clamp(slot, 0, SlotCount - 1) * SeqStep;
    }

    #endregion

    #region 排序

    /// <summary>
    /// 按「遮挡带 → 脚底 Y → 层序兜底 → Id」排序，绘制与命中检测共用同一口径。
    /// <para>
    /// 地表带 / 叠加带保持八层层序：地块贴花、道路、前景特效的先后由设计层决定。
    /// 立体带（建筑 / 道具 / 绿化树丛 / 角色）**跨图层统一按脚底 Y** 排：越靠下越靠镜头，
    /// 建筑不会因为「道具层数字更大」而无条件压住后方的灌木，树也不会永远沉在建筑下面。
    /// 八层 <see cref="PlacedObject.SortOrder"/> 只在脚底 Y 相同时兜底，并继续作为 Unity 侧数据。
    /// </para>
    /// </summary>
    /// <param name="items">实例集合。</param>
    /// <returns>自底向上的绘制顺序。</returns>
    public static List<PlacedObject> Sort(IEnumerable<PlacedObject> items)
    {
        return Sort(items, null);
    }

    /// <summary>
    /// 按「遮挡带 → 当前脚底 Y → 层序兜底 → Id」排序。
    /// <para>params baseline 可覆盖立体带的脚底基准（预览沿路径移动时传入当前脚底），
    /// 其余带与层序规则保持不变。</para>
    /// </summary>
    /// <param name="items">实例集合。</param>
    /// <param name="baseline">脚底基准提供器；null 时使用摆放包围盒底边。</param>
    /// <returns>自底向上的绘制顺序。</returns>
    public static List<PlacedObject> Sort(
        IEnumerable<PlacedObject> items,
        Func<PlacedObject, double>? baseline)
    {
        var list = new List<PlacedObject>(items);
        list.Sort((a, b) => Compare(a, b, baseline));
        return list;
    }

    /// <summary>比较两个实例的渲染先后：先带、立体带再脚底 Y、最后层序兜底。</summary>
    /// <param name="a">实例 A。</param>
    /// <param name="b">实例 B。</param>
    /// <returns>负数表示 A 先画（在下面）。</returns>
    public static int Compare(PlacedObject a, PlacedObject b) => Compare(a, b, null);

    /// <summary>比较两个实例的渲染先后，允许预览覆盖立体带脚底基准。</summary>
    /// <param name="a">实例 A。</param>
    /// <param name="b">实例 B。</param>
    /// <param name="baseline">脚底基准提供器；null 时使用摆放包围盒底边。</param>
    /// <returns>负数表示 A 先画（在下面）。</returns>
    public static int Compare(
        PlacedObject a,
        PlacedObject b,
        Func<PlacedObject, double>? baseline)
    {
        var bandA = LayerBands.Of(a);
        var bandB = LayerBands.Of(b);
        if (bandA != bandB)
        {
            return bandA.CompareTo(bandB);
        }

        // 楼层：剖面式布景的核心排序键。上层后画，2F 楼板才会压在 1F 房间顶部，
        // 而不是被 1F 里脚底 Y 更大的家具反压过去。单层地图（全是 0）这一档直接跳过。
        if (a.Level != b.Level)
        {
            return a.Level.CompareTo(b.Level);
        }

        if (bandA == LayerBands.Standing)
        {
            // 收集物是立体带里的独立夹层：物件下 → 排在所有普通立体物之前；
            // 物件上 → 排在所有普通立体物之后、前景叠加带之前。两者内部仍按脚底 Y 排。
            var rankA = CollectibleRank(a);
            var rankB = CollectibleRank(b);
            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            var footA = Baseline(a, baseline);
            var footB = Baseline(b, baseline);
            if (footA != footB)
            {
                return footA.CompareTo(footB);
            }
        }
        else
        {
            var baseA = OrderBase(a);
            var baseB = OrderBase(b);
            if (baseA != baseB)
            {
                return baseA.CompareTo(baseB);
            }
        }

        // 层序兜底：同脚底 Y（或同带同层）时，八层坐标仍然说话
        var order = a.SortOrder.CompareTo(b.SortOrder);
        if (order != 0)
        {
            return order;
        }

        // 旧档里 SortOrder 可能相同，最后一档仍用脚底 Y，再按 Id 保证顺序稳定
        order = Baseline(a, baseline).CompareTo(Baseline(b, baseline));
        return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
    }

    /// <summary>取实例的排序脚底：预览覆盖值优先，否则用摆放包围盒底边加 Y 偏移。</summary>
    /// <param name="item">实例。</param>
    /// <param name="baseline">脚底基准提供器，可为 null。</param>
    /// <returns>排序用脚底 Y。</returns>
    private static double Baseline(PlacedObject item, Func<PlacedObject, double>? baseline) =>
        baseline is null
            ? LayerBands.Baseline(item)
            : baseline(item) + item.YSortOffset;

    /// <summary>
    /// 立体带内的收集物夹层序号：0 = 物件下，1 = 普通立体物，2 = 物件上。
    /// 只有 <see cref="PlacedObject.IsCollectible"/> 为 true 的实例才进入 0 / 2 夹层。
    /// </summary>
    /// <param name="item">实例。</param>
    /// <returns>夹层次序。</returns>
    private static int CollectibleRank(PlacedObject item)
    {
        if (!item.IsCollectible)
        {
            return 1;
        }

        return item.CollectibleOverObjects ? 2 : 0;
    }

    /// <summary>
    /// 重写 Unity 要用的层序号数据：每层（或每个手改带）内分配 0–9 档位，超出共享最高档。
    /// <para>
    /// 画面前后已经由 <see cref="Compare"/> 的「带 + 脚底 Y」决定，这里不会改变遮挡关系。
    /// 按画面重写时<b>只处理立体带</b>：建筑 / 道具 / 角色按脚底 Y 排；
    /// 地表带（背景整图、海、沙滩、草地、道路贴花）和叠加带（前景）必须保留 PSD 的层序，
    /// 否则沙滩、海面这类整幅图会被脚底 Y 翻到草地上方，把地图盖成一块纯色。
    /// </para>
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="byPicture">true = 立体带按脚底 Y 重写；false = 全带按当前列表顺序（PSD 层序）。</param>
    /// <returns>各遮挡带的实例数（地表 / 立体 / 叠加，供状态栏）。</returns>
    public static int[] Rebuild(MapConfigData config, bool byPicture)
    {
        var counts = new int[3];
        if (config.Objects.Count == 0)
        {
            return counts;
        }

        var groups = new Dictionary<int, List<PlacedObject>>();
        var order = new List<int>();
        foreach (var item in config.Objects)
        {
            var baseValue = OrderBase(item);
            if (!groups.TryGetValue(baseValue, out var bucket))
            {
                bucket = new List<PlacedObject>();
                groups[baseValue] = bucket;
                order.Add(baseValue);
            }

            bucket.Add(item);
            var band = LayerBands.Of(item);
            counts[Math.Clamp(band, 0, 2)]++;
        }

        // 原列表顺序：同脚底 Y / 同层号时的稳定兜底，避免每次刷新顺序漂移
        var indices = new Dictionary<PlacedObject, int>();
        for (var i = 0; i < config.Objects.Count; i++)
        {
            indices[config.Objects[i]] = i;
        }

        foreach (var baseValue in order)
        {
            var bucket = groups[baseValue];
            var band = LayerBands.Of(bucket[0]);

            if (byPicture)
            {
                // 只有立体带按脚底 Y 重排。地表 / 叠加带是整幅底图、地块贴花和前景特效，
                // 它们的先后是 PSD 里画好的层序（海、沙滩、草地、地毯……），不能用脚底 Y 重排：
                // 一旦按脚底 Y 重排，沙滩/海面这类整幅图会翻到草地上面，把整张地图盖成一块色块。
                if (band != LayerBands.Standing)
                {
                    continue;
                }

                bucket.Sort((a, b) =>
                {
                    var result = LayerBands.Baseline(a).CompareTo(LayerBands.Baseline(b));
                    if (result != 0)
                    {
                        return result;
                    }

                    // 同脚底 Y 时保留当前层号顺序，再按列表顺序兜底：
                    // 这样「刷新层级」是幂等的，不会把同一脚底线上的树和建筑来回翻面。
                    result = a.SortOrder.CompareTo(b.SortOrder);
                    return result != 0 ? result : indices[a].CompareTo(indices[b]);
                });
            }

            for (var slot = 0; slot < bucket.Count; slot++)
            {
                bucket[slot].SortOrder = baseValue + Math.Min(slot, SlotCount - 1) * SeqStep;
            }
        }

        return counts;
    }

    #endregion

    #region 展示

    /// <summary>层级文本：<c>40（道具）· 层内 3</c>，用于属性卡小字。</summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>一行说明。</returns>
    public static string Describe(PlacedObject item)
    {
        var layer = EffectiveLayer(item);
        return $"{OrderBase(item)}（{MapLayerKeys.DisplayName(layer)}）";
    }

    #endregion
}

