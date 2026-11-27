/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.Index.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表集合索引：图片名/模板名归一与跨表归属重建
 ***************************************************************/

using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public sealed partial class ItemConfigSet
{
    #region 索引维护

    /// <summary>按配置表 ID 取行；ID 为空或表里没有为 null。</summary>
    /// <param name="id">配置表数字 ID。</param>
    /// <returns>命中的行；没有为 null。</returns>
    public ItemElementRow? FindId(string? id)
    {
        var key = (id ?? string.Empty).Trim();
        return key.Length == 0 ? null : _idIndex.TryGetValue(key, out var row) ? row : null;
    }

    private void RebuildIndex()
    {
        _index.Clear();
        _idIndex.Clear();
        _owner.Clear();
        // Unity 侧真正吃的是分类 xlsm（TableProps / TableBuildings / TableCharacters），
        // 汇总 CSV 只是导出副本：同名的先认 xlsm，CSV 兜底。
        foreach (var file in Files.Where(file => file.Kind == ItemTableKind.Xlsm)
                     .Concat(Files.Where(file => file.Kind == ItemTableKind.Csv)))
        {
            foreach (var row in file.Rows)
            {
                _owner[row] = file;
                var id = row.Id.Trim();
                if (id.Length > 0)
                {
                    _idIndex.TryAdd(id, row);
                }

                var icon = ResourceNames.NormalizeKey(row.Icon);
                if (icon.Length > 0)
                {
                    _index.TryAdd(icon, row);
                }

                var name = ResourceNames.NormalizeKey(row.TemplateName);
                if (name.Length > 0)
                {
                    _index.TryAdd(name, row);
                }
            }
        }
    }

    #endregion
}
