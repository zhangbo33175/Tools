/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdLayerRules.cs
 * author:    云毅
 * created:   2026
 * descrip:   PSD 图层规则：名称归一、层级分类、显示尺寸与整幅层判定，
 *            复刻 psd_import.py 的口径，供导入/切片/审计共用。
 ***************************************************************/

using System;
using System.Text.RegularExpressions;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>PSD 图层的纯规则集合，不依赖窗口与资源库。</summary>
public static class PsdLayerRules
{
    #region 常量

    /// <summary>整幅层的尺寸阈值，与 psd_import.py 的 FULLCANVAS_RATIO 保持一致。</summary>
    public const double FullCanvasRatio = 0.85;

    /// <summary>Photoshop 副本后缀，长后缀优先，避免「拷贝」先吃掉「拷贝 2」。</summary>
    private static readonly string[] CopySuffixes =
    {
        " 拷贝", "拷贝", " 副本", "副本", " copy", "copy", " Copy",
    };

    #endregion

    #region 名称与尺寸

    /// <summary>
    /// 图层名归一：去掉 Photoshop 的「拷贝 / 副本 / copy」后缀、结尾序号与多余空白，
    /// 便于和资源库里的 PNG 文件名对上（如「bg_road_1 拷贝」→「bg_road_1」）。
    /// </summary>
    /// <param name="name">原始图层名。</param>
    /// <returns>归一化后的名称。</returns>
    public static string NormalizeLayerName(string name)
    {
        var text = name.Replace('\u3000', ' ').Trim();
        foreach (var suffix in CopySuffixes)
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^suffix.Length].Trim();
            }
        }

        // 结尾的「(2)」「（3）」副本序号；注意不能去掉「_1」这类编号——bg_road_1 与 bg_road_2 是不同贴图
        text = Regex.Replace(text, @"\s*[\(（]\d+[\)）]$", string.Empty);
        return text.Trim();
    }

    /// <summary>把清单里的层级字符串转成图层键，未知值按道具处理。</summary>
    /// <param name="layerType">清单里的 <c>layerType</c>。</param>
    /// <returns>八层图层键之一。</returns>
    public static string NormalizeLayerType(string layerType)
    {
        var value = layerType.Trim().ToLowerInvariant();
        return value switch
        {
            MapLayerKeys.Background => MapLayerKeys.Background,
            MapLayerKeys.Greenery => MapLayerKeys.Greenery,
            MapLayerKeys.Road => MapLayerKeys.Road,
            MapLayerKeys.Building => MapLayerKeys.Building,
            MapLayerKeys.Character => MapLayerKeys.Character,
            MapLayerKeys.Foreground => MapLayerKeys.Foreground,
            MapLayerKeys.Bubble => MapLayerKeys.Bubble,
            _ => MapLayerKeys.Prop,
        };
    }

    /// <summary>
    /// 实例的世界像素尺寸。默认取 PSD 图层框；放置层的四边形（quad）只有在「按 rotation 旋转后的
    /// 轴对齐包围盒与图层框吻合」时才采信——这样 90° 旋转块能拿回宽高互换前的真实显示尺寸，
    /// 而嵌入 4096 大画布的智能对象会被拒掉。
    /// </summary>
    /// <param name="layer">PSD 图层。</param>
    /// <param name="entry">清单条目，可为 null。</param>
    /// <param name="width">输出：像素宽。</param>
    /// <param name="height">输出：像素高。</param>
    public static void ResolveDisplayPixels(
        PsdLayerInfo layer,
        PsdManifestLayer? entry,
        out double width,
        out double height)
    {
        width = layer.Width;
        height = layer.Height;

        // 容差 5% + 2px：99% 的旋转块吻合，浮点取整与贴边裁切带来的 1~2px 差异不算
        if (entry is not { QuadWidth: > 0, QuadHeight: > 0 })
        {
            return;
        }

        var radians = entry.Rotation * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var boxWidth = entry.QuadWidth * cos + entry.QuadHeight * sin;
        var boxHeight = entry.QuadWidth * sin + entry.QuadHeight * cos;
        if (Math.Abs(boxWidth - layer.Width) > layer.Width * 0.05 + 2
            || Math.Abs(boxHeight - layer.Height) > layer.Height * 0.05 + 2)
        {
            return;
        }

        width = entry.QuadWidth;
        height = entry.QuadHeight;
    }

    /// <summary>
    /// 切片安全名：Unity 侧导出切片时把图层名转成文件名（空格换下划线、拷贝/副本换 copy），
    /// 如「图层 101 拷贝 2」→「图层_101_copy_2」。
    /// </summary>
    /// <param name="name">图层名。</param>
    /// <returns>切片文件名（不含扩展名）。</returns>
    public static string SafeSliceName(string name)
    {
        var text = name.Replace('　', ' ').Trim();
        text = text.Replace(" 拷贝 ", "_copy_").Replace(" 副本 ", "_copy_");
        text = text.Replace(" 拷贝", "_copy").Replace(" 副本", "_copy");
        text = text.Replace(' ', '_');
        return text;
    }

    /// <summary>判断图层是否整幅铺满画布（用于过滤底图/地块层）。</summary>
    /// <param name="layer">PSD 图层。</param>
    /// <param name="document">PSD 文档。</param>
    /// <returns>宽高都达到画布的 85% 时返回 true。</returns>
    public static bool IsFullCanvasLayer(PsdLayerInfo layer, PsdDocument document)
    {
        if (document.Width <= 0 || document.Height <= 0)
        {
            return false;
        }

        return layer.Width >= document.Width * FullCanvasRatio
            && layer.Height >= document.Height * FullCanvasRatio;
    }

    #endregion

    #region 分类

    /// <summary>
    /// 复刻 psd_import.py 的 classify_layer 规则，按
    /// 前缀 → bg_ 第二段关键词 → 父组关键词 → 图层名关键词 的优先级判定图层类型；
    /// 图层名是 Photoshop 默认名时再补一条包围盒形状启发。
    /// </summary>
    /// <param name="layer">PSD 图层信息。</param>
    /// <param name="normalized">归一化后的图层名（去掉拷贝/副本/序号后缀）。</param>
    /// <returns>八层图层键之一。</returns>
    public static string ResolveLayerKey(PsdLayerInfo layer, string normalized)
    {
        var name = normalized.ToLowerInvariant();
        var group = layer.GroupPath.ToLowerInvariant();

        // 1. 纯背景层精确匹配
        if (name is "bg_color" or "background" or "bg")
        {
            return MapLayerKeys.Background;
        }

        // 2. 常规前缀
        if (name.StartsWith("road_"))
        {
            return MapLayerKeys.Road;
        }

        if (name.StartsWith("grass_") || name.StartsWith("plant_"))
        {
            return MapLayerKeys.Greenery;
        }

        if (name.StartsWith("building_"))
        {
            return MapLayerKeys.Building;
        }

        if (name.StartsWith("char_") || name.StartsWith("cat_"))
        {
            return MapLayerKeys.Character;
        }

        if (name.StartsWith("eff_") || name.StartsWith("effect_") || name.StartsWith("fg_"))
        {
            return MapLayerKeys.Foreground;
        }

        if (name.StartsWith("bubble_"))
        {
            return MapLayerKeys.Bubble;
        }

        if (name.StartsWith("prop_"))
        {
            return MapLayerKeys.Prop;
        }

        // 3. bg_ 前缀按第二段关键词细分（bg_grass_1 / bg_road_2 / bg_sea_1 ...）
        if (name.StartsWith("bg_"))
        {
            var rest = name.Substring(3);
            var second = rest.Length == 0 ? string.Empty : rest.Split('_')[0];

            if (Contains(second, "grass", "plant", "flower", "tree", "shrub"))
            {
                return MapLayerKeys.Greenery;
            }

            if (Contains(second, "road", "street", "sidewalk", "crosswalk", "lamp", "fence"))
            {
                return MapLayerKeys.Road;
            }

            // 无法识别的 bg_ 地块层归入背景层
            return MapLayerKeys.Background;
        }

        // 4. 父组关键词
        if (Contains(group, "road", "道路", "路面", "车道", "斑马线", "路灯", "路牌", "市政"))
        {
            return MapLayerKeys.Road;
        }

        if (Contains(group, "ground", "草地", "绿植", "植物", "花草", "灌木", "绿化", "树", "花"))
        {
            return MapLayerKeys.Greenery;
        }

        if (Contains(group, "building", "建筑"))
        {
            return MapLayerKeys.Building;
        }

        if (Contains(group, "char", "人物", "npc", "cat", "猫"))
        {
            return MapLayerKeys.Character;
        }

        if (Contains(group, "eff", "特效", "光效", "fx", "前景"))
        {
            return MapLayerKeys.Foreground;
        }

        if (Contains(group, "bubble", "气泡", "对话"))
        {
            return MapLayerKeys.Bubble;
        }

        if (Contains(group, "prop", "物件", "装饰"))
        {
            return MapLayerKeys.Prop;
        }

        // 5. 图层名关键词
        if (Contains(name, "road", "道路", "路面", "车道"))
        {
            return MapLayerKeys.Road;
        }

        if (Contains(name, "grass", "草", "plant", "绿植", "花草", "花", "灌木", "树"))
        {
            return MapLayerKeys.Greenery;
        }

        if (Contains(name, "building", "建筑", "house", "房屋", "城墙", "wall"))
        {
            return MapLayerKeys.Building;
        }

        if (Contains(name, "char", "人物", "npc", "cat", "猫"))
        {
            return MapLayerKeys.Character;
        }

        if (Contains(name, "eff", "特效", "光效", "fx", "前景"))
        {
            return MapLayerKeys.Foreground;
        }

        if (Contains(name, "bubble", "气泡", "对话", "talk", "dialog"))
        {
            return MapLayerKeys.Bubble;
        }

        // 6. 形状启发：名字全是「图层 N」时，按包围盒长宽比判道路（长条）与大面积地块
        var cellsW = layer.Width / (double)MapToolDefaults.GridSize;
        var cellsH = layer.Height / (double)MapToolDefaults.GridSize;
        if (cellsW >= 3 && cellsH <= 2)
        {
            return MapLayerKeys.Road;
        }

        if (cellsH >= 3 && cellsW <= 2)
        {
            return MapLayerKeys.Road;
        }

        return MapLayerKeys.Prop;
    }

    /// <summary>判断文本是否包含任意关键词。</summary>
    private static bool Contains(string text, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
