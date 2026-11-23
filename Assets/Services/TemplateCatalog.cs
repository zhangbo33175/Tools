/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   内置物体模板与示例地图数据
 ***************************************************************/

using System.Collections.Generic;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 物体模板目录。当前提供与 Unity 版 uxml 中资源列表同名的内置模板，
/// 后续可由元件配置表导入替换。
/// </summary>
public static class TemplateCatalog
{
    #region 公开方法

    /// <summary>
    /// 构建内置模板集合。
    /// </summary>
    /// <returns>模板列表。</returns>
    public static List<ObjectTemplate> CreateBuiltIn()
    {
        var templates = new List<ObjectTemplate>
        {
            // 建筑
            Build("B_001_民居", MapObjectType.Building, 4, 4, MapLayerKeys.Building),
            Build("B_002_商店", MapObjectType.Building, 4, 4, MapLayerKeys.Building),
            Build("B_003_钟楼", MapObjectType.Building, 3, 6, MapLayerKeys.Building),
            Build("B_004_城墙", MapObjectType.Building, 8, 2, MapLayerKeys.Building),

            // 人物
            Build("C_001_小猫", MapObjectType.Character, 1, 2, MapLayerKeys.Character),
            Build("C_002_商人", MapObjectType.Character, 1, 2, MapLayerKeys.Character),
            Build("C_003_村民", MapObjectType.Character, 1, 2, MapLayerKeys.Character),

            // 物件
            Build("P_001_大树", MapObjectType.Prop, 2, 3, MapLayerKeys.Prop),
            Build("P_002_小花", MapObjectType.Prop, 1, 1, MapLayerKeys.Prop),
            Build("P_003_石头", MapObjectType.Prop, 1, 1, MapLayerKeys.Prop),
            Build("P_004_木桶", MapObjectType.Prop, 1, 2, MapLayerKeys.Prop),
            Build("P_005_木箱", MapObjectType.Prop, 1, 1, MapLayerKeys.Prop),
            Build("P_006_火把", MapObjectType.Prop, 1, 2, MapLayerKeys.Prop),
            Build("P_007_灯笼", MapObjectType.Prop, 1, 2, MapLayerKeys.Prop),
            Build("P_008_栅栏", MapObjectType.Prop, 2, 1, MapLayerKeys.Prop),
            Build("P_009_桌椅", MapObjectType.Prop, 2, 1, MapLayerKeys.Prop),
            Build("P_010_雕像", MapObjectType.Prop, 2, 3, MapLayerKeys.Prop),
            Build("P_011_旗帜", MapObjectType.Prop, 1, 3, MapLayerKeys.Prop),
            Build("P_012_路标", MapObjectType.Prop, 1, 1, MapLayerKeys.Prop),
        };

        foreach (var template in templates)
        {
            template.IsBuiltIn = true;
            template.AnimName = template.Type == MapObjectType.Character ? "Idle" : "Idle";
            template.CollisionEnabled = template.Type != MapObjectType.Prop;
            template.InteractType = template.Type == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk;
        }

        return templates;
    }

    /// <summary>
    /// 构建启动时的默认地图：只有场景参数（4096 × 4096、32px 格子、矩形绘制模式、底图 bg_grass），
    /// 不预置任何物件 —— 默认地图上就是空白一片，物件全部由美术手动摆放或从地图预制体载入。
    /// </summary>
    /// <returns>地图配置。</returns>
    public static MapConfigData CreateEmptyMap()
    {
        return new MapConfigData
        {
            SceneId = "1",
            SceneName = "ChapterN",
            Width = MapToolDefaults.LevelPixelSize,
            Height = MapToolDefaults.LevelPixelSize,
            ChapterId = "DefaultChapter",
            ChapterName = "第一章",
            BgName = "bg_grass",
            DrawShape = DrawShapes.Rect,
        };
    }

    /// <summary>
    /// 按 2D 遮挡规则重排全部物体的层序号（口径见 <see cref="LayerBands"/>）：
    /// 带号 × 1000 + 带内序位。默认保留带内现有顺序（即 PSD 层序）。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="byPicture">true = 立体带改按「脚底 Y」重排（画面优先）。</param>
    /// <returns>地表 / 立体 / 叠加三带各自的实例数。</returns>
    public static int[] RefreshSortOrders(MapConfigData config, bool byPicture = false)
    {
        return LayerBands.RebuildSortOrders(config, byPicture);
    }

    #endregion

    #region 辅助

    private static ObjectTemplate Build(string id, MapObjectType type, int width, int height, string layerKey)
    {
        return new ObjectTemplate
        {
            TemplateId = id,
            TemplateName = id,
            Type = type,
            ResName = id,
            LayerTypeKey = layerKey,
            DefaultWidth = width,
            DefaultHeight = height,
        };
    }

    #endregion
}
