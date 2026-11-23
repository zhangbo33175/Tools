/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Cocos.Entities.cs
 * author:    云毅
 * created:   2026
 * descrip:   Cocos 实体子表：角色 / 建筑 / 物件统一表骨架与公共字段输出
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
/// <summary>Cocos 实体子表的表骨架、角色/建筑/物件字段输出。</summary>
public static partial class MapPublishService
{
    #region Cocos 子表

    /// <summary>
    /// 构建实体子表：统一处理表头、空列表提示、序号包装与字段回调，
    /// 角色 / 建筑 / 物件子表只保留各自的字段输出差异。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="suffix">子表后缀。</param>
    /// <param name="items">实体列表。</param>
    /// <param name="description">Lua 表描述。</param>
    /// <param name="brief">表用途简述。</param>
    /// <param name="appendFields">实体字段输出回调。</param>
    /// <param name="detail">可选补充说明。</param>
    /// <returns>子表 Lua 文本。</returns>
    private static string BuildCocosEntityTable(
        string baseName,
        string suffix,
        IReadOnlyList<PlacedObject> items,
        string description,
        string brief,
        Action<StringBuilder, PlacedObject> appendFields,
        string? detail = null)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, suffix);
        AppendLuaHeader(sb, tableName, description, brief, detail);
        sb.AppendLine($"Tables.{tableName} = {{");

        if (items.Count == 0)
        {
            sb.AppendLine("    -- 空列表");
        }
        else
        {
            for (var index = 0; index < items.Count; index++)
            {
                sb.AppendLine($"    [{index + 1}] = {{");
                appendFields(sb, items[index]);
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 构建角色子表：公共字段复用 <see cref="AppendCocosObjectFields"/>，
    /// 再补角色独有的巡逻、交互、路径与交通绑定字段。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="characters">角色实例列表。</param>
    /// <returns>角色表 Lua 文本。</returns>
    private static string BuildCocosCharactersFile(string baseName, IReadOnlyList<PlacedObject> characters) =>
        BuildCocosEntityTable(
            baseName,
            CharactersSuffix,
            characters,
            "角色列表（自动生成）",
            "关卡角色实体列表（只存差异字段）",
            (sb, item) => AppendCocosObjectFields(
                sb,
                item,
                includeCollectId: false,
                includeCollectibleFields: false,
                includeCharacterFields: true));

    /// <summary>构建建筑子表；字段口径与物件表共用，但不输出收集物字段。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="buildings">建筑实例列表。</param>
    /// <returns>建筑表 Lua 文本。</returns>
    private static string BuildCocosBuildingsFile(string baseName, IReadOnlyList<PlacedObject> buildings) =>
        BuildCocosEntityTable(
            baseName,
            BuildingsSuffix,
            buildings,
            "建筑列表（自动生成）",
            "关卡建筑实体列表",
            (sb, item) => AppendCocosObjectFields(sb, item, includeCollectId: false));

    /// <summary>构建待收集物件子表；比建筑表多输出收集物标记与 collectId。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="objects">物件实例列表。</param>
    /// <returns>物件表 Lua 文本。</returns>
    private static string BuildCocosObjectsFile(string baseName, IReadOnlyList<PlacedObject> objects) =>
        BuildCocosEntityTable(
            baseName,
            ObjectsSuffix,
            objects,
            "待收集物件列表（自动生成）",
            "关卡待收集物件列表",
            (sb, item) => AppendCocosObjectFields(sb, item, includeCollectId: true),
            "字段与 Buildings 对齐：GridX/GridY 兼容旧换算，PosX/PosY/PixelWidth/PixelHeight/Stretch/LayerType 按 PSD 还原");

    /// <summary>
    /// 输出建筑 / 物件共用的公共字段：位置、尺寸、图层、动画、Spine、排序与翻转。
    /// </summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="item">实例数据。</param>
    /// <param name="includeCollectId">是否输出 collectId（物件表为 true）。</param>
    /// <param name="includeCollectibleFields">是否输出收集物标记字段（建筑表 / 角色表为 false）。</param>
    /// <param name="includeCharacterFields">是否追加角色独有字段。</param>
    private static void AppendCocosObjectFields(
        StringBuilder sb,
        PlacedObject item,
        bool includeCollectId,
        bool includeCollectibleFields = true,
        bool includeCharacterFields = false)
    {
        sb.AppendLine($"        Uid        = \"{Escape(item.Id)}\",");
        sb.AppendLine($"        TemplateId = \"{Escape(item.TemplateId)}\",");
        sb.AppendLine($"        Name       = \"{Escape(item.Name)}\",");
        sb.AppendLine($"        GridX      = {item.CellX},");
        sb.AppendLine($"        GridY      = {item.CellY},");
        sb.AppendLine($"        PosX       = {Num(item.X)},");
        sb.AppendLine($"        PosY       = {Num(item.Y)},");
        sb.AppendLine($"        Width      = {item.ExportWidth},");
        sb.AppendLine($"        Height     = {item.ExportHeight},");
        sb.AppendLine($"        PixelWidth  = {Num(item.PixelWidth)},");
        sb.AppendLine($"        PixelHeight = {Num(item.PixelHeight)},");
        sb.AppendLine($"        Stretch     = {Bool(item.Stretch)},");
        sb.AppendLine($"        LayerType   = \"{Escape(item.LayerType)}\",");
        sb.AppendLine($"        DisplayLayer = \"{Escape(item.DisplayLayer)}\",");
        if (includeCollectibleFields)
        {
            sb.AppendLine($"        IsCollectible = {Bool(item.IsCollectible)},");
            sb.AppendLine($"        CollectibleOverObjects = {Bool(item.CollectibleOverObjects)},");
        }

        sb.AppendLine($"        Image      = \"{Escape(item.ImagePath)}\",");
        sb.AppendLine($"        Anim        = \"{Escape(item.AnimName)}\",");
        sb.AppendLine($"        AnimEnabled = {Bool(item.AnimEnabled)},");
        sb.AppendLine($"        AnimationKind = \"{Escape(item.AnimationKind)}\",");
        sb.AppendLine($"        SpineAsset  = \"{Escape(item.SpineAsset)}\",");
        sb.AppendLine($"        SpineAnimation = \"{Escape(item.SpineAnimation)}\",");
        sb.AppendLine($"        Loop        = {Bool(item.Loop)},");
        sb.AppendLine($"        Speed       = {Num(item.Speed)},");
        if (includeCollectId)
        {
            sb.AppendLine($"        CollectId  = \"collect_{Escape(item.Id)}\",");
        }

        sb.AppendLine($"        SortOrder    = {item.SortOrder},");
        sb.AppendLine($"        YSortEnabled = {Bool(item.YSortEnabled)},");
        sb.AppendLine($"        YSortOffset  = {Num(item.YSortOffset)},");
        sb.AppendLine($"        Rotation      = {Num(item.Rotation)},");
        sb.AppendLine($"        Scale         = {Num(item.Scale)},");
        sb.AppendLine($"        ScaleX        = {Num(item.Scale)},");
        sb.AppendLine($"        ScaleY        = {Num(item.Scale)},");
        sb.AppendLine($"        FlipX         = {Bool(item.FlipX)},");
        if (item.BandOverride >= 0)
        {
            sb.AppendLine($"        Band          = {item.BandOverride},");
        }

        if (includeCharacterFields)
        {
            AppendCocosCharacterFields(sb, item);
        }
    }

    /// <summary>角色表独有的行为字段；公共位置/外观字段由 <see cref="AppendCocosObjectFields"/> 统一输出。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="item">角色物件。</param>
    private static void AppendCocosCharacterFields(StringBuilder sb, PlacedObject item)
    {
        sb.AppendLine($"        PatrolLoop  = \"{Escape(item.WalkMode)}\",");
        sb.AppendLine($"        FacingDirection = \"{Escape(item.FacingDirection)}\",");
        sb.AppendLine($"        Interactable = {Bool(item.Interactable)},");
        sb.AppendLine($"        InteractType = \"{Escape(item.InteractType)}\",");
        sb.AppendLine($"        DialogBubble = \"{Escape(item.DialogBubble)}\",");
        sb.AppendLine($"        PathId      = \"path_{Escape(item.Id)}\",");
        sb.AppendLine($"        IsVehicle     = {Bool(item.IsVehicle)},");
        sb.AppendLine($"        TrafficRole  = \"{Escape(item.TrafficRole)}\",");
        sb.AppendLine($"        StopLineX    = {Num(item.StopLineX)},");
        sb.AppendLine($"        StopLineY    = {Num(item.StopLineY)},");
        sb.AppendLine($"        BoundLightId = \"{Escape(item.BoundLightIds.FirstOrDefault() ?? string.Empty)}\",");
        sb.AppendLine($"        BoundLightIds = {LuaStringArray(item.BoundLightIds)},");
        sb.AppendLine($"        YellowTreatAsRed = {Bool(item.YellowTreatAsRed)},");
    }
    #endregion
}

