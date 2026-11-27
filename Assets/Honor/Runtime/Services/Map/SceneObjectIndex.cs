/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SceneObjectIndex.cs
 * author:    云毅
 * created:   2026
 * descrip:   场景内同模板物件的位次：数字 ID 只到「哪一种」，这里补到「第几个」
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>同模板物件的位次。</summary>
/// <param name="Index">组内位次，从 1 开始。</param>
/// <param name="Total">同模板实例总数。</param>
public readonly record struct SceneSlot(int Index, int Total)
{
    /// <summary>只有一个实例的模板不需要位次，避免给界面添噪音。</summary>
    public bool HasPeers => Total > 1 && Index > 0;

    /// <summary>徽标文案：<c>#07</c>；没有同伴实例时为空串。</summary>
    public string Badge => HasPeers ? $"#{Index:00}" : string.Empty;
}

/// <summary>
/// 场景位次服务：数字 ID 标识「哪一种物件」，同一个模板摆多个时用它区分「第几个」。
/// 位次来自实例短 ID 排序，只要实例本身还在，位次就不会跟着增删乱跳。
/// </summary>
public static class SceneObjectIndex
{
    /// <summary>取某个实例在同组里的位次。</summary>
    /// <param name="objects">当前地图全部物件。</param>
    /// <param name="item">目标实例。</param>
    /// <param name="keyOf">分组键；不传时按类型 + 模板 ID。数字 ID 口径更准，调用方有数字 ID 就传数字 ID。</param>
    /// <returns>位次与同组总数；找不到时返回 <c>(0, 0)</c>。</returns>
    public static SceneSlot Locate(
        IReadOnlyList<PlacedObject>? objects,
        PlacedObject? item,
        Func<PlacedObject, string>? keyOf = null)
    {
        if (objects is null || item is null || objects.Count == 0)
        {
            return new SceneSlot(0, 0);
        }

        var key = keyOf ?? KeyOf;
        var target = key(item);
        var peers = objects
            .Where(other => string.Equals(key(other), target, StringComparison.OrdinalIgnoreCase))
            .OrderBy(other => other.Id, StringComparer.Ordinal)
            .ToList();

        var index = peers.FindIndex(other => ReferenceEquals(other, item) || other.Id == item.Id);
        return index < 0 ? new SceneSlot(0, 0) : new SceneSlot(index + 1, peers.Count);
    }

    /// <summary>同模板判定键：类型 + 模板 ID；模板 ID 为空时退回图片文件名。</summary>
    /// <param name="item">物件实例。</param>
    /// <returns>分组键。</returns>
    public static string KeyOf(PlacedObject item)
    {
        var template = item.TemplateId ?? string.Empty;
        if (template.Length == 0)
        {
            var image = (item.ImagePath ?? string.Empty).Replace('\\', '/');
            var slash = image.LastIndexOf('/');
            template = slash >= 0 ? image[(slash + 1)..] : image;
        }

        return $"{item.Type}|{template}";
    }
}
