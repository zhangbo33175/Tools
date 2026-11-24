/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineBinding.cs
 * author:    云毅
 * created:   2026
 * descrip:   把已导入的 Spine 资产绑定为物件的唯一视觉源
 ***************************************************************/

using System;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 绑定

/// <summary>
/// Spine 物件绑定入口。主窗口和动画预览窗共用同一口径：
/// 视觉来源切到 Spine，清掉旧的序列帧名，避免资产缺失时回退到旧贴图或旧序列帧。
/// </summary>
public static class SpineBinding
{
    #region 公开方法

    /// <summary>把资产绑定到物件，并启用动画播放。</summary>
    /// <param name="target">目标物件。</param>
    /// <param name="asset">已导入的 Spine 资产。</param>
    // ---TODO 动画替换：Spine 成功后统一清掉序列帧名，视觉源只认 AnimationKind。
    public static void Apply(PlacedObject target, SpineAnimationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(asset);

        target.AnimationKind = AnimationKinds.Spine;
        target.SpineAsset = asset.Name;
        target.SpineAnimation = asset.DefaultAnimation;
        target.SpineWalkAnimation = ResolveWalkAnimation(asset);
        target.AnimEnabled = true;

        // 旧序列帧名留着会让「Spine 资产缺失」时误回退到上一套帧动画；
        // ImagePath 保留为素材来源，但 Spine 模式下不再参与绘制。
        target.AnimName = string.Empty;
    }

    /// <summary>从骨架动画里选行走动作：优先 walk，其次 move / run，最后回落到标准 walk 名。</summary>
    /// <param name="asset">Spine 资产。</param>
    /// <returns>行走动画名。</returns>
    public static string ResolveWalkAnimation(SpineAnimationAsset asset)
    {
        foreach (var name in asset.Animations)
        {
            if (string.Equals(name, MapToolDefaults.DefaultMoveAnimName, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        foreach (var name in asset.Animations)
        {
            if (name.Contains("move", StringComparison.OrdinalIgnoreCase)
                || name.Contains("run", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return MapToolDefaults.DefaultMoveAnimName;
    }

    #endregion
}

#endregion
