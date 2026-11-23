/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.Mutations.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表集合增删改：按图片名匹配、分类落表与模板字段映射
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public sealed partial class ItemConfigSet
{
    #region 增删改

    /// <summary>新增或更新一行（按图片名匹配，跨表）；新行优先落到 <paramref name="preferredFile"/>（当前分类那张表），没有就落主表。</summary>
    /// <param name="template">模板数据。</param>
    /// <param name="preferredFile">新行要落到的表（分类表）；null 表示主表。</param>
    /// <returns>写入的行。</returns>
    public ItemElementRow Upsert(ObjectTemplate template, ItemTableFile? preferredFile = null)
    {
        var name = string.IsNullOrWhiteSpace(template.ResName) ? template.TemplateName : template.ResName;
        var row = Find(name);
        ItemTableFile file;
        if (row is null)
        {
            file = preferredFile ?? Primary ?? CreatePrimary();
            row = file.CreateRow();
            file.Rows.Add(row);
            file.Dirty = true;
            _owner[row] = file;
        }
        else
        {
            file = _owner.TryGetValue(row, out var owner) ? owner : (Primary ?? Files[0]);
        }

        ApplyTemplate(row, template, name);
        file.Dirty = true;
        RebuildIndex();
        return row;
    }

    /// <summary>按图片名删行（只删第一张命中的表）。</summary>
    /// <param name="imageName">图片名。</param>
    /// <returns>是否删掉了。</param>
    public bool Remove(string? imageName)
    {
        var row = Find(imageName);
        if (row is null)
        {
            return false;
        }

        var file = _owner.TryGetValue(row, out var owner) ? owner : null;
        if (file is null)
        {
            return false;
        }

        file.Rows.Remove(row);
        file.Dirty = true;
        _owner.Remove(row);
        RebuildIndex();
        return true;
    }

    /// <summary>按分类生成下一个 ID：建筑 2xxx / 人物 1xxx / 物件 3xxx。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>新的 ID 文本。</returns>
    public string NextId(MapObjectType type)
    {
        var start = type switch
        {
            MapObjectType.Character => 1000,
            MapObjectType.Building => 2000,
            _ => 3000,
        };

        var max = start;
        foreach (var row in Rows)
        {
            if (int.TryParse(row.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && id > max && id / 1000 == start / 1000)
            {
                max = id;
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region 内部实现：新建与模板映射

    private ItemTableFile CreatePrimary()
    {
        var path = System.IO.Path.Combine(Directory, PreferredCsvName);
        var file = ItemTableIO.CreateEmpty(path, CanonicalColumns, CanonicalTypes);
        Files.Insert(0, file);
        return file;
    }

    private static void ApplyTemplate(ItemElementRow row, ObjectTemplate template, string name)
    {
        row.Id = string.IsNullOrWhiteSpace(template.TemplateId) ? row.Id : template.TemplateId;
        row.Icon = string.IsNullOrWhiteSpace(template.ResName) ? (string.IsNullOrWhiteSpace(row.Icon) ? name : row.Icon) : template.ResName;
        row.TemplateName = string.IsNullOrWhiteSpace(template.TemplateName) ? name : template.TemplateName;
        row.AnimName = string.IsNullOrWhiteSpace(template.AnimName) ? MapToolDefaults.DefaultAnimName : template.AnimName;
        row.Loop = template.Loop;
        row.Speed = template.Speed;
        row.SortOrder = template.SortOrder;
        row.ShadowEnabled = template.ShadowEnabled;
        row.IsCollectible = template.IsCollectible;
        row.RespawnTime = template.RespawnTime;
        row.Interactable = template.Interactable;
        row.InteractType = template.InteractType;
        row.CollisionEnabled = template.CollisionEnabled;
        row.DialogBubble = template.DialogBubble;
        row.FacingDirection = template.FacingDirection;
        row.TrafficCategory = template.TrafficCategory;
        row.YSortEnabled = template.YSortEnabled;
        row.YSortOffset = template.YSortOffset;
        row.IsMovable = template.IsMovable;
        row.LoopRestartDelay = template.LoopRestartDelay;
        row.HideWhenOffMap = template.HideWhenOffMap;
        row.IsTeleport = template.IsTeleport;
        row.TeleportTargetScene = template.TeleportTargetScene;
        row.TeleportTargetX = template.TeleportTargetX;
        row.TeleportTargetY = template.TeleportTargetY;
        var nineSlice = template.NineSlice ?? new NineSliceSettings();
        row.NineSliceEnabled = nineSlice.Enabled;
        row.NineSliceLeft = Math.Max(0, nineSlice.Left);
        row.NineSliceRight = Math.Max(0, nineSlice.Right);
        row.NineSliceTop = Math.Max(0, nineSlice.Top);
        row.NineSliceBottom = Math.Max(0, nineSlice.Bottom);
        row.NineSliceCenterMode = NineSliceModes.ToCode(nineSlice.CenterMode);
        if (string.IsNullOrWhiteSpace(row.ResName))
        {
            row.ResName = template.ResName;
        }
    }

    #endregion
}
