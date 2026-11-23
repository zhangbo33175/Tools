/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateStore.Json.cs
 * author:    云毅
 * created:   2026
 * descrip:   模板仓库 JSON 通道：兼容载荷、字段映射与读取
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>模板仓库的 JSON 读取与兼容载荷映射。</summary>
public sealed partial class TemplateStore
{
    /// <summary>模板占格的最小合法边长。</summary>
    private const int MinGridExtent = 1;

    /// <summary>九宫格四边内缩的最小合法值。</summary>
    private const int MinNineSliceInset = 0;

    #region JSON

    private static List<ObjectTemplate> TryLoadJson(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<ObjectTemplate>();
            }

            var payload = JsonSerializer.Deserialize<List<TemplatePayload>>(File.ReadAllText(path, Encoding.UTF8));
            if (payload is null)
            {
                return new List<ObjectTemplate>();
            }

            return payload
                .Where(item => !string.IsNullOrWhiteSpace(item.TemplateId))
                .Select(item => item.ToModel())
                .ToList();
        }
        catch (Exception)
        {
            // 配置损坏时回落到内置目录，不阻塞编辑器启动
            return new List<ObjectTemplate>();
        }
    }

    /// <summary>九宫格旁路文件：一个版本号 + 若干按素材别名登记的条目。</summary>
    private sealed class NineSliceFilePayload
    {
        [JsonPropertyName("Version")] public int Version { get; set; } = 1;

        [JsonPropertyName("Entries")] public List<NineSliceEntryPayload> Entries { get; set; } = new();
    }

    /// <summary>九宫格条目：键别名 + 四条边距 + 中心填充方式。</summary>
    private sealed class NineSliceEntryPayload
    {
        [JsonPropertyName("TemplateId")] public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("ResName")] public string ResName { get; set; } = string.Empty;

        [JsonPropertyName("ImagePath")] public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("TemplateName")] public string TemplateName { get; set; } = string.Empty;

        [JsonPropertyName("Enabled")] public bool Enabled { get; set; }

        [JsonPropertyName("Left")] public int Left { get; set; }

        [JsonPropertyName("Right")] public int Right { get; set; }

        [JsonPropertyName("Top")] public int Top { get; set; }

        [JsonPropertyName("Bottom")] public int Bottom { get; set; }

        [JsonPropertyName("CenterMode")] public string CenterMode { get; set; } = NineSliceModes.Stretch;
    }

    /// <summary>JSON 传输对象：属性名与 Unity 配置表列名保持一致，追加独立版扩展列。</summary>
    private sealed class TemplatePayload
    {
        [JsonPropertyName("TemplateId")] public string TemplateId { get; set; } = string.Empty;
        [JsonPropertyName("TemplateName")] public string TemplateName { get; set; } = string.Empty;
        [JsonPropertyName("ResName")] public string ResName { get; set; } = string.Empty;
        [JsonPropertyName("ImagePath")] public string ImagePath { get; set; } = string.Empty;
        [JsonPropertyName("AnimName")] public string AnimName { get; set; } = MapToolDefaults.DefaultAnimName;
        [JsonPropertyName("Loop")] public bool Loop { get; set; } = true;
        [JsonPropertyName("Speed")] public double Speed { get; set; } = 1.0;
        [JsonPropertyName("SortOrder")] public int SortOrder { get; set; }
        [JsonPropertyName("DisplayLayer")] public string DisplayLayer { get; set; } = string.Empty;
        [JsonPropertyName("DialogBubble")] public string DialogBubble { get; set; } = string.Empty;
        [JsonPropertyName("BubbleId")] public int BubbleId { get; set; }
        [JsonPropertyName("ShadowEnabled")] public bool ShadowEnabled { get; set; } = true;
        [JsonPropertyName("IsCollectible")] public bool IsCollectible { get; set; }
        [JsonPropertyName("RespawnTime")] public double RespawnTime { get; set; }
        [JsonPropertyName("TrafficCategory")] public string TrafficCategory { get; set; } = TemplateTrafficCategories.None;
        [JsonPropertyName("Interactable")] public bool Interactable { get; set; } = true;
        [JsonPropertyName("InteractType")] public string InteractType { get; set; } = TemplateInteractTypes.None;
        [JsonPropertyName("CollisionEnabled")] public bool CollisionEnabled { get; set; } = true;
        [JsonPropertyName("FacingDirection")] public string FacingDirection { get; set; } = Facings.Down;
        [JsonPropertyName("YSortEnabled")] public bool YSortEnabled { get; set; } = true;
        [JsonPropertyName("YSortOffset")] public double YSortOffset { get; set; }
        [JsonPropertyName("IsMovable")] public bool IsMovable { get; set; }
        [JsonPropertyName("MoveAnimName")] public string MoveAnimName { get; set; } = "walk";
        [JsonPropertyName("LoopRestartDelay")] public double LoopRestartDelay { get; set; } = MapToolDefaults.DefaultLoopRestartDelay;
        [JsonPropertyName("HideWhenOffMap")] public bool HideWhenOffMap { get; set; } = MapToolDefaults.DefaultHideWhenOffMap;
        [JsonPropertyName("IsTeleport")] public bool IsTeleport { get; set; }
        [JsonPropertyName("TeleportTargetScene")] public string TeleportTargetScene { get; set; } = string.Empty;
        [JsonPropertyName("TeleportTargetX")] public double TeleportTargetX { get; set; }
        [JsonPropertyName("TeleportTargetY")] public double TeleportTargetY { get; set; }

        /// <summary>独立版扩展：默认占格宽。</summary>
        [JsonPropertyName("GridWidth")] public int GridWidth { get; set; } = 1;

        /// <summary>独立版扩展：默认占格高。</summary>
        [JsonPropertyName("GridHeight")] public int GridHeight { get; set; } = 1;

        /// <summary>独立版扩展：显式图层键。</summary>
        [JsonPropertyName("LayerTypeKey")] public string LayerTypeKey { get; set; } = string.Empty;

        /// <summary>独立版扩展：是否启用九宫格。</summary>
        [JsonPropertyName("NineSliceEnabled")] public bool NineSliceEnabled { get; set; }

        /// <summary>独立版扩展：九宫格左边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceLeft")] public int NineSliceLeft { get; set; }

        /// <summary>独立版扩展：九宫格右边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceRight")] public int NineSliceRight { get; set; }

        /// <summary>独立版扩展：九宫格上边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceTop")] public int NineSliceTop { get; set; }

        /// <summary>独立版扩展：九宫格下边距（素材原始像素）。</summary>
        [JsonPropertyName("NineSliceBottom")] public int NineSliceBottom { get; set; }

        /// <summary>独立版扩展：九宫格中心填充方式。</summary>
        [JsonPropertyName("NineSliceCenterMode")] public string NineSliceCenterMode { get; set; } = NineSliceModes.Stretch;

        /// <summary>独立版扩展：类型（Unity 侧按 ResName 前缀推断）。</summary>
        [JsonPropertyName("ObjectType")] public string ObjectType { get; set; } = string.Empty;

        /// <summary>模型转传输对象。</summary>
        public static TemplatePayload FromModel(ObjectTemplate template) => new()
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            ResName = template.ResName,
            ImagePath = template.ImagePath,
            AnimName = template.AnimName,
            Loop = template.Loop,
            Speed = template.Speed,
            SortOrder = template.SortOrder,
            DisplayLayer = template.DisplayLayer,
            DialogBubble = template.DialogBubble,
            BubbleId = template.BubbleId,
            ShadowEnabled = template.ShadowEnabled,
            IsCollectible = template.IsCollectible,
            RespawnTime = template.RespawnTime,
            TrafficCategory = template.TrafficCategory,
            Interactable = template.Interactable,
            InteractType = template.InteractType,
            CollisionEnabled = template.CollisionEnabled,
            FacingDirection = template.FacingDirection,
            YSortEnabled = template.YSortEnabled,
            YSortOffset = template.YSortOffset,
            IsMovable = template.IsMovable,
            MoveAnimName = template.MoveAnimName,
            LoopRestartDelay = template.LoopRestartDelay,
            HideWhenOffMap = template.HideWhenOffMap,
            IsTeleport = template.IsTeleport,
            TeleportTargetScene = template.TeleportTargetScene,
            TeleportTargetX = template.TeleportTargetX,
            TeleportTargetY = template.TeleportTargetY,
            GridWidth = template.DefaultWidth,
            GridHeight = template.DefaultHeight,
            LayerTypeKey = template.LayerTypeKey,
            ObjectType = ObjectTypeNames.Of(template.Type),
            NineSliceEnabled = template.NineSlice.Enabled,
            NineSliceLeft = template.NineSlice.Left,
            NineSliceRight = template.NineSlice.Right,
            NineSliceTop = template.NineSlice.Top,
            NineSliceBottom = template.NineSlice.Bottom,
            NineSliceCenterMode = template.NineSlice.CenterMode,
        };

        /// <summary>传输对象转模型。</summary>
        public ObjectTemplate ToModel() => new()
        {
            TemplateId = TemplateId,
            TemplateName = string.IsNullOrWhiteSpace(TemplateName) ? TemplateId : TemplateName,
            ResName = ResName,
            ImagePath = ImagePath,
            AnimName = string.IsNullOrWhiteSpace(AnimName) ? MapToolDefaults.DefaultAnimName : AnimName,
            Loop = Loop,
            Speed = Speed,
            SortOrder = SortOrder,
            DisplayLayer = DisplayLayer ?? string.Empty,
            DialogBubble = DialogBubble ?? string.Empty,
            BubbleId = BubbleId,
            ShadowEnabled = ShadowEnabled,
            IsCollectible = IsCollectible,
            RespawnTime = RespawnTime,
            TrafficCategory = TemplateTrafficCategories.ToCode(TrafficCategory),
            Interactable = Interactable,
            InteractType = TemplateInteractTypes.ToCode(InteractType),
            CollisionEnabled = CollisionEnabled,
            FacingDirection = FacingNames.ToCode(FacingDirection),
            YSortEnabled = YSortEnabled,
            YSortOffset = YSortOffset,
            IsMovable = IsMovable,
            MoveAnimName = string.IsNullOrWhiteSpace(MoveAnimName) ? MapToolDefaults.DefaultMoveAnimName : MoveAnimName,
            LoopRestartDelay = LoopRestartDelay,
            HideWhenOffMap = HideWhenOffMap,
            IsTeleport = IsTeleport,
            TeleportTargetScene = TeleportTargetScene ?? string.Empty,
            TeleportTargetX = TeleportTargetX,
            TeleportTargetY = TeleportTargetY,
            LayerTypeKey = LayerTypeKey ?? string.Empty,
            DefaultWidth = Math.Max(MinGridExtent, GridWidth),
            DefaultHeight = Math.Max(MinGridExtent, GridHeight),
            NineSlice = new NineSliceSettings
            {
                Enabled = NineSliceEnabled,
                Left = Math.Max(MinNineSliceInset, NineSliceLeft),
                Right = Math.Max(MinNineSliceInset, NineSliceRight),
                Top = Math.Max(MinNineSliceInset, NineSliceTop),
                Bottom = Math.Max(MinNineSliceInset, NineSliceBottom),
                CenterMode = NineSliceModes.ToCode(NineSliceCenterMode),
            },
            Type = ObjectTypeNames.ToType(ObjectType),
            IsBuiltIn = true,
        };
    }

    #endregion
}
