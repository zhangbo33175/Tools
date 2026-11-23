/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementTable.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件行模型：表字段、模板双向映射与描述
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 行模型

/// <summary>
/// 物件元件配置表的一行（<c>ItemElementConfig.csv</c>）。
/// 列口径与 Unity 侧策划表一致（标题行 + 列名行 + 类型行，之后为数据）。
/// </summary>
public sealed class ItemElementRow
{
    /// <summary>配置表行号（1 起）。</summary>
    public int RowNumber { get; set; }

    /// <summary>ID（建筑 2xxx / 人物 1xxx / 物件 3xxx）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>图片名 / 资源名（与 Map 目录下的 PNG 同名）。</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>预制体名（Unity 侧 <c>GameObject_Building</c> 等）。</summary>
    public string ResName { get; set; } = string.Empty;

    /// <summary>模板名称。</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>默认动画。</summary>
    public string AnimName { get; set; } = "Idle";

    /// <summary>是否循环。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>播放速度。</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>是否可交互。</summary>
    public bool Interactable { get; set; } = true;

    /// <summary>交互类型。</summary>
    public string InteractType { get; set; } = string.Empty;

    /// <summary>渲染层级（SortOrder）。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否有碰撞。</summary>
    public bool CollisionEnabled { get; set; }

    /// <summary>对话气泡。</summary>
    public string DialogBubble { get; set; } = string.Empty;

    /// <summary>初始朝向。</summary>
    public string FacingDirection { get; set; } = string.Empty;

    /// <summary>显示层 / 层级档（空 = 跟随归属层）。</summary>
    public string DisplayLayer { get; set; } = string.Empty;

    /// <summary>是否显示阴影。</summary>
    public bool ShadowEnabled { get; set; } = true;

    /// <summary>是否收集物。</summary>
    public bool IsCollectible { get; set; }

    /// <summary>重生时间（秒）。</summary>
    public double RespawnTime { get; set; }

    /// <summary>交通类别。</summary>
    public string TrafficCategory { get; set; } = string.Empty;

    /// <summary>启用 YSort。</summary>
    public bool YSortEnabled { get; set; } = true;

    /// <summary>YSort 偏移。</summary>
    public double YSortOffset { get; set; }

    /// <summary>可移动。</summary>
    public bool IsMovable { get; set; }

    /// <summary>循环重启延迟。</summary>
    public double LoopRestartDelay { get; set; }

    /// <summary>离开地图隐藏。</summary>
    public bool HideWhenOffMap { get; set; } = MapToolDefaults.DefaultHideWhenOffMap;

    /// <summary>是否传送门。</summary>
    public bool IsTeleport { get; set; }

    /// <summary>目标场景。</summary>
    public string TeleportTargetScene { get; set; } = string.Empty;

    /// <summary>目标 X。</summary>
    public double TeleportTargetX { get; set; }

    /// <summary>目标 Y。</summary>
    public double TeleportTargetY { get; set; }

    /// <summary>是否启用九宫格（独立版扩展；表里没有该列时由 NineSliceConfig.json 兜底）。</summary>
    public bool NineSliceEnabled { get; set; }

    /// <summary>九宫格左边距（素材原始像素）。</summary>
    public int NineSliceLeft { get; set; }

    /// <summary>九宫格右边距（素材原始像素）。</summary>
    public int NineSliceRight { get; set; }

    /// <summary>九宫格上边距（素材原始像素）。</summary>
    public int NineSliceTop { get; set; }

    /// <summary>九宫格下边距（素材原始像素）。</summary>
    public int NineSliceBottom { get; set; }

    /// <summary>九宫格中心填充方式：stretch / tile。</summary>
    public string NineSliceCenterMode { get; set; } = NineSliceModes.Stretch;

    /// <summary>原始单元格（按列下标），保存时按列回写，未识别列原样保留。</summary>
    public List<string> Cells { get; } = new();

    /// <summary>把行数据套到模板上（未配置资源选中时的建议值）。</summary>
    /// <param name="template">目标模板（通常是新建的空模板）。</param>
    public void ApplyToTemplate(ObjectTemplate template)
    {
        if (!string.IsNullOrWhiteSpace(Id) && int.TryParse(Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            template.TemplateId = Id;
        }

        if (!string.IsNullOrWhiteSpace(TemplateName))
        {
            template.TemplateName = TemplateName;
        }

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            template.ResName = Icon;
        }

        template.AnimName = string.IsNullOrWhiteSpace(AnimName) ? MapToolDefaults.DefaultAnimName : AnimName;
        template.Loop = Loop;
        template.Speed = Speed;
        template.SortOrder = SortOrder;
        template.ShadowEnabled = ShadowEnabled;
        template.IsCollectible = IsCollectible;
        template.RespawnTime = RespawnTime;
        template.Interactable = Interactable;
        template.InteractType = InteractType;
        template.CollisionEnabled = CollisionEnabled;
        template.DialogBubble = DialogBubble;
        template.FacingDirection = FacingDirection;
        template.DisplayLayer = DisplayLayer;
        template.TrafficCategory = TrafficCategory;
        template.YSortEnabled = YSortEnabled;
        template.YSortOffset = YSortOffset;
        template.IsMovable = IsMovable;
        template.LoopRestartDelay = LoopRestartDelay;
        template.HideWhenOffMap = HideWhenOffMap;
        template.IsTeleport = IsTeleport;
        template.TeleportTargetScene = TeleportTargetScene;
        template.TeleportTargetX = TeleportTargetX;
        template.TeleportTargetY = TeleportTargetY;
        template.NineSlice = new NineSliceSettings
        {
            Enabled = NineSliceEnabled,
            Left = NineSliceLeft,
            Right = NineSliceRight,
            Top = NineSliceTop,
            Bottom = NineSliceBottom,
            CenterMode = NineSliceModes.ToCode(NineSliceCenterMode),
        };
    }

    /// <summary>用模板的当前值回写这一行（编辑保存时）。</summary>
    /// <param name="template">来源模板。</param>
    public void UpdateFromTemplate(ObjectTemplate template)
    {
        Id = template.TemplateId;
        Icon = string.IsNullOrWhiteSpace(template.ResName) ? Icon : template.ResName;
        TemplateName = template.TemplateName;
        AnimName = template.AnimName;
        Loop = template.Loop;
        Speed = template.Speed;
        SortOrder = template.SortOrder;
        ShadowEnabled = template.ShadowEnabled;
        IsCollectible = template.IsCollectible;
        RespawnTime = template.RespawnTime;
        Interactable = template.Interactable;
        InteractType = template.InteractType;
        CollisionEnabled = template.CollisionEnabled;
        DialogBubble = template.DialogBubble;
        FacingDirection = template.FacingDirection;
        DisplayLayer = template.DisplayLayer;
        TrafficCategory = template.TrafficCategory;
        YSortEnabled = template.YSortEnabled;
        YSortOffset = template.YSortOffset;
        IsMovable = template.IsMovable;
        LoopRestartDelay = template.LoopRestartDelay;
        HideWhenOffMap = template.HideWhenOffMap;
        IsTeleport = template.IsTeleport;
        TeleportTargetScene = template.TeleportTargetScene;
        TeleportTargetX = template.TeleportTargetX;
        TeleportTargetY = template.TeleportTargetY;

        var nineSlice = template.NineSlice ?? new NineSliceSettings();
        NineSliceEnabled = nineSlice.Enabled;
        NineSliceLeft = Math.Max(0, nineSlice.Left);
        NineSliceRight = Math.Max(0, nineSlice.Right);
        NineSliceTop = Math.Max(0, nineSlice.Top);
        NineSliceBottom = Math.Max(0, nineSlice.Bottom);
        NineSliceCenterMode = NineSliceModes.ToCode(nineSlice.CenterMode);
    }

    /// <summary>一行摘要（供详情卡展示绑定的配置表信息）。</summary>
    /// <returns>紧凑文本。</returns>
    public string Describe()
    {
        var parts = new List<string> { $"#{Id}" };
        if (!string.IsNullOrWhiteSpace(ResName))
        {
            parts.Add(ResName);
        }

        parts.Add($"层级 {SortOrder}");
        parts.Add($"碰撞 {(CollisionEnabled ? "是" : "否")}");
        parts.Add($"交通 {TrafficCategory}");
        if (!string.IsNullOrWhiteSpace(DialogBubble))
        {
            parts.Add($"气泡 {DialogBubble}");
        }

        if (IsCollectible)
        {
            parts.Add($"收集 {RespawnTime:0.##}s 再生");
        }

        return string.Join(" · ", parts);
    }
}

#endregion
