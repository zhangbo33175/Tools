/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorTemplateModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   物体模板模型：公共属性、交互与层级字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MapEditorTool.Models;

#region 模板与实例

/// <summary>
/// 物体模板。同一类物体的公共属性只配置一次，放置时通过 <see cref="TemplateId"/> 引用。
/// </summary>
public sealed class ObjectTemplate : IResourceRowData
{
    /// <summary>模板唯一 ID。</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>模板显示名称。</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>物体类型。</summary>
    public MapObjectType Type { get; set; } = MapObjectType.Prop;

    /// <summary>素材资源名。</summary>
    public string ResName { get; set; } = string.Empty;

    /// <summary>预览图路径。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>是否内置模板（内置不可删除）。</summary>
    public bool IsBuiltIn { get; set; } = true;

    /// <summary>默认动画名。</summary>
    public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;

    /// <summary>默认循环播放。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>默认播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>默认可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>默认交互类型。</summary>
    public string InteractType { get; set; } = InteractTypes.None;

    /// <summary>默认气泡文本。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>默认气泡配置 ID。</summary>
    public int BubbleId { get; set; }

    /// <summary>渲染层级排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>显示层键，空串表示跟随类型归属层。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>启用 Y 轴深度排序。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>Y 轴深度排序偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>启用碰撞。</summary>
    public bool CollisionEnabled { get; set; } = true;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = Facings.Down;

    /// <summary>显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>是否可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>移动动画名。</summary>
    public string MoveAnimName { get; set; } = "walk";

    /// <summary>循环行驶离开地图后的重启延迟，秒。</summary>
    public double LoopRestartDelay { get; set; } = MapToolDefaults.DefaultLoopRestartDelay;

    /// <summary>离开地图时隐藏。</summary>
    public bool HideWhenOffMap { get; set; } = MapToolDefaults.DefaultHideWhenOffMap;

    /// <summary>可收集。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>重生时间。</summary>
    public double RespawnTime { get; set; }

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>传送目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>传送目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>传送目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>交通类别键。</summary>
    public string TrafficCategory { get; set; } = "none";

    /// <summary>默认占格宽，单位格子。</summary>
    public int DefaultWidth { get; set; } = 1;

    /// <summary>默认占格高，单位格子。</summary>
    public int DefaultHeight { get; set; } = 1;

    /// <summary>
    /// 九宫格配置。仅在该素材被「是否缩放」拉伸时参与绘制：
    /// 四角保持原尺寸，四条边单轴填充，中心按 <see cref="NineSliceSettings.CenterMode"/> 填充。
    /// </summary>
    public NineSliceSettings NineSlice { get; set; } = new();

    /// <summary>显式指定的图层键，留空时按类型推导。</summary>
    public string LayerTypeKey { get; set; } = string.Empty;

    /// <summary>归属图层键，优先取显式配置，否则按类型推导。</summary>
    public string LayerKey => !string.IsNullOrEmpty(LayerTypeKey)
        ? LayerTypeKey
        : Type switch
        {
            MapObjectType.Building => MapLayerKeys.Building,
            MapObjectType.Character => MapLayerKeys.Character,
            _ => MapLayerKeys.Prop,
        };
}

#endregion
