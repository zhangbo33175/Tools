/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LayerBands.cs
 * author:    云毅
 * created:   2026
 * descrip:   2D 俯视地图的遮挡分层：地表 / 立体 / 叠加，判定同时看图层键与图层名
 ***************************************************************/

using System;
using System.Collections.Generic;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 2D 俯视地图的遮挡规则。
/// <para>
/// 画稿（PSD）里每个图层的位置是固定的，层序即正确遮挡关系；一旦换到可编辑地图，
/// 遮挡就必须按「离镜头远近」重排，否则会出现「建筑永远压住树」「地面贴图跑到立体物前面」。
/// </para>
/// <para>
/// 这里把实例分成三个遮挡带：
/// <list type="bullet">
/// <item>地表 0 — 背景 / 道路 / 草地 / 沙滩 / 水面 / 地毯等地块，按层序叠在地上，永远在最底；</item>
/// <item>立体 1 — 建筑 / 物件 / 角色 / 树丛等有高度的东西，<b>跨图层按「脚底 Y」排序</b>，越靠下越在前；</item>
/// <item>叠加 2 — 前景 / 气泡 / 特效，永远压在最上。</item>
/// </list>
/// 真正的绘制次序由 <see cref="LayerOrder.Compare"/> 决定：带 → 立体带脚底 Y → 八层层序兜底 → Id。
/// 八层坐标（Unity 口径 0–79）仍写进 JSON 供 Unity 侧消费，但不再单独决定画面前后。
/// </para>
/// </summary>
public static class LayerBands
{
    #region 常量

    /// <summary>地表带。</summary>
    public const int Ground = 0;

    /// <summary>立体带。</summary>
    public const int Standing = 1;

    /// <summary>叠加带。</summary>
    public const int Overlay = 2;

    /// <summary>带内层序号的区间宽度：带号 × 该值 + 带内序位 = SortOrder。</summary>
    public const int BandStride = 1000;

    /// <summary>判定「地表」的图层名关键词（含中英文，忽略大小写）。</summary>
    private static readonly string[] GroundWords =
    {
        "grass", "lawn", "moss", "ground", "floor", "tile", "carpet", "rug", "mat",
        "sand", "beach", "water", "sea", "river", "pond", "lake", "pool",
        "parking", "pavement", "sidewalk", "crosswalk", "road", "street", "path",
        "dirt", "mud", "soil", "snow", "ice", "terrain", "leaf", "leaves", "flowerbed",
        "草", "草坪", "草地", "地砖", "瓷砖", "地毯", "地垫", "沙", "沙滩", "水面", "水",
        "海", "河", "池塘", "湖", "路面", "车道", "人行道", "斑马线", "停车场",
        "泥土", "苔", "雪", "冰", "地块", "地面", "叶", "树叶", "花坛",
    };

    /// <summary>判定「立体」的图层名关键词：命中就压过地表关键词（如喷泉 / 花盆 / 路灯）。</summary>
    private static readonly string[] StandingWords =
    {
        "tree", "bush", "shrub", "flower", "plant", "pot", "lamp", "light", "sign",
        "fence", "fountain", "statue", "bench", "chair", "table", "car", "cart",
        "building", "house", "shop", "tower", "wall", "door", "window", "box", "barrel",
        "tree", "cat", "char", "npc",
        "树", "灌木", "丛", "花", "盆栽", "灯", "路牌", "围栏", "喷泉", "雕塑", "长椅",
        "桌", "椅", "车", "建筑", "房", "店", "塔", "墙", "门", "箱", "桶", "猫", "人",
    };

    #endregion

    #region 分层判定

    /// <summary>取实例所属遮挡带。</summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>0 地表 / 1 立体 / 2 叠加。</returns>
    public static int Of(PlacedObject item)
    {
        // 属性面板里手改的「遮挡带」优先级最高：改它等于把这张贴图抬到另一层
        if (item.BandOverride is >= Ground and <= Overlay)
        {
            return item.BandOverride;
        }

        // 其次「层级别」是编辑器里手改的显式分层，非空时以它为准
        var key = string.IsNullOrWhiteSpace(item.DisplayLayer) ? item.LayerType : item.DisplayLayer;
        return Of(key, item.Name);
    }

    /// <summary>把实例放到目标遮挡带的最上层：写 <see cref="Models.PlacedObject.BandOverride"/>，
    /// 再按 Unity 八层坐标把 <see cref="Models.PlacedObject.SortOrder"/> 设成「该带基线 + 层内最高档 + 1」。</summary>
    /// <param name="config">地图配置（用于找同带已有实例的档位）。</param>
    /// <param name="item">被移动的实例。</param>
    /// <param name="band">目标带（-1 = 恢复自动判定）。</param>
    public static void MoveToBand(MapConfigData config, PlacedObject item, int band)
    {
        if (band is < Ground or > Overlay)
        {
            item.BandOverride = -1;
            item.BandOverrideAuto = false;
            item.SortOrder = LayerOrder.Build(LayerOrder.EffectiveLayer(item), LayerOrder.SlotOf(item));
            return;
        }

        item.BandOverride = band;
        item.BandOverrideAuto = false;
        var slot = Math.Clamp(TopSlot(config, item, band) + 1, 0, LayerOrder.SlotCount - 1);
        item.SortOrder = LayerOrder.OrderBase(item) + slot * LayerOrder.SeqStep;
    }

    /// <summary>同带内（除自己）最高的层内档位；没有其他实例时返回 -1。</summary>
    private static int TopSlot(MapConfigData config, PlacedObject item, int band)
    {
        var top = -1;
        foreach (var other in config.Objects)
        {
            if (ReferenceEquals(other, item) || Of(other) != band)
            {
                continue;
            }

            var slot = LayerOrder.SlotOf(other);
            if (slot > top)
            {
                top = slot;
            }
        }

        return top;
    }

    /// <summary>按图层键 + 图层名判定遮挡带。</summary>
    /// <param name="layerKey">图层键（background / road / greenery / building / prop / character / foreground / bubble）。</param>
    /// <param name="name">图层名或实例名。</param>
    /// <returns>遮挡带编号。</returns>
    public static int Of(string? layerKey, string? name)
    {
        var key = (layerKey ?? string.Empty).Trim().ToLowerInvariant();
        var text = (name ?? string.Empty).Trim().ToLowerInvariant();

        if (key is MapLayerKeys.Foreground or MapLayerKeys.Bubble)
        {
            return Overlay;
        }

        if (key is MapLayerKeys.Background or MapLayerKeys.Road or MapLayerKeys.Greenery)
        {
            return Ground;
        }

        // 明确的立体组优先于名字：`prop_pond_1`、`bg_rock_1` 这类名字里带 bg_ / 水 / 地面词的物件，
        // 只要作者把它放进 prop / building / character，它就是独立立体物，不能又被名字按回地表。
        // 否则围栏（prop）会压住同一 prop 组里的池塘/岩石，形成“立柱切进画面”的假缝隙。
        // `greenery` 不在这里：它是「绿化层」整体（草地/树影/大面积花圃），默认是地表；
        // 其中独立的树/灌木由 ShouldPromoteToStanding 窄口提升到立体带。
        if (key is MapLayerKeys.Building or MapLayerKeys.Prop or MapLayerKeys.Character)
        {
            return Standing;
        }

        // 只有图层键缺失 / 不认识时才用名字兜底：
        // 先看「立体」再回升「地表」——喷泉 / 花盆 / 路灯这类名字里带水、带花，但都是有高度的立体物。
        if (ContainsWord(text, StandingWords))
        {
            return Standing;
        }

        if (text.StartsWith("bg_", StringComparison.Ordinal) || ContainsWord(text, GroundWords))
        {
            return Ground;
        }

        return Standing;
    }

    /// <summary>带的中文名，用于状态栏与日志。</summary>
    /// <param name="band">遮挡带编号。</param>
    /// <returns>地表 / 立体 / 叠加。</returns>
    public static string DisplayName(int band) => band switch
    {
        Ground => "地表",
        Standing => "立体",
        Overlay => "叠加",
        _ => "立体",
    };

    /// <summary>立体带的排序基准：包围盒下沿（脚底）加 <see cref="Models.PlacedObject.YSortOffset"/>。</summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>脚底世界 Y。</returns>
    public static double Baseline(PlacedObject item) => item.Bounds.Bottom + item.YSortOffset;

    /// <summary>
    /// 图层名是否明确指向「有高度」的美术：树、灌木、围栏、建筑、人物等。
    /// <para>
    /// 只认明确的词，避免把 <c>bg_1</c> 这类背景合并层误抬到立体带；
    /// <c>pond / water / road</c> 等平面词仍按地表处理。
    /// </para>
    /// </summary>
    /// <param name="name">图层名或实例名。</param>
    /// <returns>true = 应参与脚底 Y 排序。</returns>
    public static bool IsStandingArt(string? name)
    {
        var text = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0 || text.StartsWith("bg_", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsWord(text, StandingWords);
    }

    /// <summary>
    /// 是否是「平面美术」名：池塘、水面、地毯、草地、路面等。
    /// 只用于接缝贴合这类窄口修复，不参与主分带；<c>pond</c> 仍会因图层键是 prop 而进入立体带。
    /// </summary>
    /// <param name="name">图层名或实例名。</param>
    /// <returns>true = 平面类美术。</returns>
    public static bool IsFlatArt(string? name)
    {
        var text = (name ?? string.Empty).Trim().ToLowerInvariant();
        return !ContainsWord(text, StandingWords) && ContainsWord(text, GroundWords);
    }

    /// <summary>
    /// 地表/背景层里的「立体美术」是否需要在导入时抬到立体带。
    /// <para>
    /// PSD 里常有整组 <c>background</c> 图层，但其中单张 <c>prop_bush_3</c>、<c>tree_2</c>
    /// 不是整幅底图，而是有高度的独立美术。如果继续按图层键压在地表带，
    /// 围栏/人物会永远压住它，形成「立柱切进灌木」的硬边和假缝隙。
    /// 名称可信时只做「背景/道路/绿化 + 明确立体名」的窄口提升；
    /// 名称被 PSD 切片混进 <c>bg_</c> 模板时（如 <c>bg_rock_1_550_867</c> 实际是绿色灌木），
    /// 由刷新链路把 <see cref="ArtSilhouetteInspector"/> 的像素轮廓结果作为 <paramref name="visuallyStanding"/> 传入。
    /// </para>
    /// </summary>
    /// <param name="item">已放置实例。</param>
    /// <param name="visuallyStanding">
    /// 视觉分类器是否判定为立体植被。名称命中时可以为 <c>null</c> / false；
    /// 名称被混进 <c>bg_</c> 模板、但画面实际是树 / 灌木时必须传 true。
    /// </param>
    /// <returns>true = 建议写入 <see cref="PlacedObject.BandOverride"/> = 立体。</returns>
    public static bool ShouldPromoteToStanding(PlacedObject item, bool visuallyStanding = false)
    {
        if (item.BandOverride is >= Ground and <= Overlay)
        {
            return false;
        }

        return IsPromotableStandingArt(item, visuallyStanding);
    }

    /// <summary>
    /// 不看 <see cref="PlacedObject.BandOverride"/> 的提层候选判定：
    /// 背景 / 道路 / 绿化组里带高度语义的独立美术。属性卡用它区分「自动提层」和「手改遮挡带」。
    /// </summary>
    /// <param name="item">已放置实例。</param>
    /// <param name="visuallyStanding">
    /// 视觉分类器结果：名称不可信时（例如 <c>bg_rock_1_550_867</c> 实际是灌木）由像素轮廓补足语义。
    /// </param>
    /// <returns>true = 属于自动提层候选。</returns>
    public static bool IsPromotableStandingArt(PlacedObject item, bool visuallyStanding = false)
    {
        if (!IsPromotableLayer(item))
        {
            return false;
        }

        if (IsStandingArt(item.Name))
        {
            return true;
        }

        // 名称是 bg_ 前缀时 IsStandingArt 会主动放弃，只有「图像像植被 + 名称不是平面贴花」
        // 才允许越级；纯草皮 / 停车位 / 扁石仍留在地表带。
        return visuallyStanding && !IsFlatArt(item.Name);
    }

    /// <summary>
    /// 只看归属层是否属于「允许自动提层」的三组：背景 / 道路 / 绿化。
    /// 供刷新巡检在解码图片前做廉价预筛。
    /// </summary>
    /// <param name="item">已放置实例。</param>
    /// <returns>true = 该层组允许做自动提层判断。</returns>
    public static bool IsPromotableLayer(PlacedObject item)
    {
        var key = string.IsNullOrWhiteSpace(item.DisplayLayer) ? item.LayerType : item.DisplayLayer;
        var text = (key ?? string.Empty).Trim().ToLowerInvariant();
        return text is MapLayerKeys.Background or MapLayerKeys.Road or MapLayerKeys.Greenery;
    }

    #endregion

    #region 排序

    /// <summary>按「遮挡带 → 立体带脚底 Y → 层序兜底」排序：见 <see cref="LayerOrder"/>。</summary>
    /// <param name="items">实例集合。</param>
    /// <returns>自底向上的绘制顺序。</returns>
    public static List<PlacedObject> Sort(IEnumerable<PlacedObject> items) => LayerOrder.Sort(items);

    /// <summary>比较两个实例的渲染先后（实现见 <see cref="LayerOrder.Compare"/>）。</summary>
    /// <param name="a">实例 A。</param>
    /// <param name="b">实例 B。</param>
    /// <returns>负数表示 A 先画（在下面）。</returns>
    public static int Compare(PlacedObject a, PlacedObject b) => LayerOrder.Compare(a, b);

    /// <summary>
    /// 重排全部实例的层序号（只改 Unity 用的 sortOrder 数据）。画面前后由「带 + 脚底 Y」决定，
    /// 重排不会改变遮挡关系。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="byPicture">
    /// true = 层内按「脚底 Y」排（2D 画面优先，人物/物件越靠下越在前）；
    /// false = 层内按当前列表顺序（即导入时的 PSD 层序）。
    /// </param>
    /// <returns>各遮挡带被重排的实例数。</returns>
    public static int[] RebuildSortOrders(MapConfigData config, bool byPicture) =>
        LayerOrder.Rebuild(config, byPicture);

    #endregion

    #region 辅助

    /// <summary>
    /// 关键词匹配：英文按「分段」比较（<c>prop_car_4</c> 的 <c>car</c> 才命中，<c>carpet</c> 不会误命中 <c>car</c>），
    /// 中文按子串比较。
    /// </summary>
    private static bool ContainsWord(string text, string[] words)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var tokens = text.Split(new[] { '_', ' ', '-', '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (IsAscii(word))
            {
                foreach (var token in tokens)
                {
                    if (string.Equals(token, word, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                continue;
            }

            if (text.Contains(word, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAscii(string text)
    {
        foreach (var ch in text)
        {
            if (ch > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
