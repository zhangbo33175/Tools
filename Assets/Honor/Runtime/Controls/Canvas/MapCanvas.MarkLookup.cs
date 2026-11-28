/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.MarkLookup.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布标记去重查询：判断横道 / 红绿灯是否已经是地图上的普通物件
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 画布标记查询：横道与灯头在「自身已经是地图物件」时不再叠画额外标记，
/// 由物件贴图负责显示，避免同一处被画两遍。交通渲染与资源标记渲染共用这一份判定。
/// </summary>
public partial class MapCanvas
{
    /// <summary>地图上已有实例的 ID 集合（空白 ID 不参与，避免误判为空串命中）。</summary>
    /// <param name="config">当前地图数据。</param>
    private static HashSet<string> MapObjectIdSet(MapConfigData config) =>
        config.Objects
            .Select(instance => instance.Id)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>该标记 ID 是否已由地图物件承担显示。</summary>
    /// <param name="objectIds">由 <see cref="MapObjectIdSet"/> 得到的集合。</param>
    /// <param name="id">横道 / 灯头的 ID。</param>
    private static bool IsBakedAsObject(HashSet<string> objectIds, string id) =>
        id.Length > 0 && objectIds.Contains(id);
}
