/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateSyncService.cs
 * author:    云毅
 * created:   2026
 * descrip:   把元件模板的最新属性同步到已摆放实例
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 模板 → 实例的属性同步。仅同步「模板决定」的业务字段，保留实例自身的坐标、占格尺寸、
/// 名称标签（Tag）、旋转与路径等摆放期数据。
/// </summary>
public static class TemplateSyncService
{
    #region 公开方法

    /// <summary>把模板属性同步到所有引用该模板的实例。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="templates">模板集合。</param>
    /// <returns>被更新的实例数量。</returns>
    public static int Apply(MapConfigData config, IReadOnlyList<ObjectTemplate> templates)
    {
        var index = templates.ToDictionary(template => template.TemplateId, template => template);
        var updated = 0;

        foreach (var instance in config.Objects)
        {
            if (!index.TryGetValue(instance.TemplateId, out var template))
            {
                continue;
            }

            instance.Type = template.Type;
            instance.LayerType = template.LayerKey;
            instance.DisplayLayer = template.DisplayLayer;
            instance.ImagePath = template.ImagePath;
            instance.AnimName = template.AnimName;
            instance.Loop = template.Loop;
            instance.Speed = template.Speed;
            instance.Interactable = template.Interactable;
            instance.InteractType = TemplateInteractTypes.ToInstanceCode(template.InteractType);
            instance.DialogBubble = template.DialogBubble;
            instance.BubbleId = template.BubbleId;
            instance.SortOrder = template.SortOrder;
            instance.YSortEnabled = template.YSortEnabled;
            instance.YSortOffset = template.YSortOffset;
            instance.CollisionEnabled = template.CollisionEnabled;
            instance.FacingDirection = template.FacingDirection;
            instance.ShadowEnabled = template.ShadowEnabled;
            instance.IsCollectible = template.IsCollectible;
            instance.RespawnTime = template.RespawnTime;
            instance.IsMovable = template.IsMovable;
            instance.MoveAnimName = template.MoveAnimName;
            instance.IsTeleport = template.IsTeleport;
            instance.TeleportTargetScene = template.TeleportTargetScene;
            instance.TeleportTargetX = template.TeleportTargetX;
            instance.TeleportTargetY = template.TeleportTargetY;
            updated++;
        }

        return updated;
    }

    #endregion
}
