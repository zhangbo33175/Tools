/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemFieldNames.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件配置表字段键与列名别名：CSV / xlsm 共用口径
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Services;

#region 字段定义

/// <summary>表格式。</summary>
public enum ItemTableKind
{
    /// <summary>逗号分隔文本（两行表头：列名 / 类型）。</summary>
    Csv,

    /// <summary>Excel 宏工作簿（四行表头：标题 / 列名 / 类型 / 备注）。</summary>
    Xlsm,
}

/// <summary>配置表字段键与列名别名（CSV 中文列名 / Unity xlsm 英文列名共用一套读写逻辑）。</summary>
internal static class ItemFieldNames
{
    /// <summary>ID。</summary>
    public const string Id = "Id";

    /// <summary>图片名。</summary>
    public const string Icon = "Icon";

    /// <summary>预制体名。</summary>
    public const string ResName = "ResName";

    /// <summary>模板名称。</summary>
    public const string TemplateName = "TemplateName";

    /// <summary>默认动画。</summary>
    public const string AnimName = "AnimName";

    /// <summary>是否循环。</summary>
    public const string Loop = "Loop";

    /// <summary>播放速度。</summary>
    public const string Speed = "Speed";

    /// <summary>是否可交互。</summary>
    public const string Interactable = "Interactable";

    /// <summary>交互类型。</summary>
    public const string InteractType = "InteractType";

    /// <summary>渲染层级。</summary>
    public const string SortOrder = "SortOrder";

    /// <summary>是否有碰撞。</summary>
    public const string Collision = "CollisionEnabled";

    /// <summary>对话气泡。</summary>
    public const string DialogBubble = "DialogBubble";

    /// <summary>初始朝向。</summary>
    public const string FacingDirection = "FacingDirection";

    /// <summary>显示层 / 层级档（空 = 跟随归属层）。</summary>
    public const string DisplayLayer = "DisplayLayer";

    /// <summary>是否显示阴影。</summary>
    public const string Shadow = "ShadowEnabled";

    /// <summary>是否收集物。</summary>
    public const string Collectible = "IsCollectible";

    /// <summary>重生时间。</summary>
    public const string RespawnTime = "RespawnTime";

    /// <summary>交通类别。</summary>
    public const string TrafficCategory = "TrafficCategory";

    /// <summary>启用 YSort。</summary>
    public const string YSortEnabled = "YSortEnabled";

    /// <summary>YSort 偏移。</summary>
    public const string YSortOffset = "YSortOffset";

    /// <summary>可移动。</summary>
    public const string Movable = "IsMovable";

    /// <summary>循环重启延迟。</summary>
    public const string LoopRestartDelay = "LoopRestartDelay";

    /// <summary>离开地图隐藏。</summary>
    public const string HideWhenOffMap = "HideWhenOffMap";

    /// <summary>是否传送门。</summary>
    public const string Teleport = "IsTeleport";

    /// <summary>目标场景。</summary>
    public const string TeleportScene = "TeleportTargetScene";

    /// <summary>目标 X。</summary>
    public const string TeleportX = "TeleportTargetX";

    /// <summary>目标 Y。</summary>
    public const string TeleportY = "TeleportTargetY";

    /// <summary>是否启用九宫格。</summary>
    public const string NineSliceEnabled = "NineSliceEnabled";

    /// <summary>九宫格左边距。</summary>
    public const string NineSliceLeft = "NineSliceLeft";

    /// <summary>九宫格右边距。</summary>
    public const string NineSliceRight = "NineSliceRight";

    /// <summary>九宫格上边距。</summary>
    public const string NineSliceTop = "NineSliceTop";

    /// <summary>九宫格下边距。</summary>
    public const string NineSliceBottom = "NineSliceBottom";

    /// <summary>九宫格中心填充方式。</summary>
    public const string NineSliceCenterMode = "NineSliceCenterMode";

    /// <summary>字段 ↔ 列名别名表。</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        [Id] = new[] { "ID", "TemplateId" },
        [Icon] = new[] { "Icon", "ImagePath", "图片名称" },
        [ResName] = new[] { "ResName", "资源ResDef的名字" },
        [TemplateName] = new[] { "模板名称", "TemplateName", "物件名称" },
        [AnimName] = new[] { "默认动画", "AnimName" },
        [Loop] = new[] { "是否循环", "Loop", "是否循环播放" },
        [Speed] = new[] { "播放速度", "Speed", "动画播放速度" },
        [Interactable] = new[] { "是否可交互", "Interactable" },
        [InteractType] = new[] { "交互类型", "InteractType" },
        [SortOrder] = new[] { "渲染层级", "SortOrder" },
        [Collision] = new[] { "是否有碰撞", "CollisionEnabled" },
        [DialogBubble] = new[] { "对话气泡", "DialogBubble" },
        [FacingDirection] = new[] { "初始朝向", "FacingDirection" },
        [DisplayLayer] = new[] { "显示层", "层级档", "显示层级", "DisplayLayer" },
        [Shadow] = new[] { "是否显示阴影", "ShadowEnabled" },
        [Collectible] = new[] { "是否收集物", "IsCollectible" },
        [RespawnTime] = new[] { "重生时间", "RespawnTime" },
        [TrafficCategory] = new[] { "交通类别", "TrafficCategory" },
        [YSortEnabled] = new[] { "启用YSort", "YSortEnabled" },
        [YSortOffset] = new[] { "YSort偏移", "YSortOffset" },
        [Movable] = new[] { "可移动", "IsMovable" },
        [LoopRestartDelay] = new[] { "循环重启延迟", "LoopRestartDelay" },
        [HideWhenOffMap] = new[] { "离开地图隐藏", "HideWhenOffMap" },
        [Teleport] = new[] { "是否传送门", "IsTeleport" },
        [TeleportScene] = new[] { "目标场景", "TeleportTargetScene" },
        [TeleportX] = new[] { "目标X", "TeleportTargetX" },
        [TeleportY] = new[] { "目标Y", "TeleportTargetY" },
        [NineSliceEnabled] = new[] { "九宫格", "是否九宫格", "NineSliceEnabled", "NeedNineSlice" },
        [NineSliceLeft] = new[] { "九宫格左边距", "左边距", "NineSliceLeft" },
        [NineSliceRight] = new[] { "九宫格右边距", "右边距", "NineSliceRight" },
        [NineSliceTop] = new[] { "九宫格上边距", "上边距", "NineSliceTop" },
        [NineSliceBottom] = new[] { "九宫格下边距", "下边距", "NineSliceBottom" },
        [NineSliceCenterMode] = new[] { "九宫格中心", "中心填充", "NineSliceCenterMode" },
    };

    /// <summary>按数字列写回的字段（xlsm 里写成数值而不是文本）。</summary>
    public static readonly IReadOnlySet<string> NumericFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Speed, SortOrder, RespawnTime, YSortOffset, LoopRestartDelay, TeleportX, TeleportY,
        NineSliceLeft, NineSliceRight, NineSliceTop, NineSliceBottom,
    };

    /// <summary>类型行 / 备注行里出现的词，用来跳过表头。</summary>
    public static readonly IReadOnlySet<string> TypeTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "int", "string", "bool", "float", "double", "long", "备注", "序号", "类型", "说明",
    };
}

#endregion

