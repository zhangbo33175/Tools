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

    private void RebuildIndex()
    {
        _index.Clear();
        _owner.Clear();
        // Unity 侧真正吃的是分类 xlsm（TableProps / TableBuildings / TableCharacters），
        // 汇总 CSV 只是导出副本：同名的先认 xlsm，CSV 兜底。
        foreach (var file in Files.Where(file => file.Kind == ItemTableKind.Xlsm)
                     .Concat(Files.Where(file => file.Kind == ItemTableKind.Csv)))
        {
            foreach (var row in file.Rows)
            {
                _owner[row] = file;
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
