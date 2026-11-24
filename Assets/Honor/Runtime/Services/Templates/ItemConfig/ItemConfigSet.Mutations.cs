/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.Mutations.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表集合增删改：按图片名匹配、分类落表与表行/模板双向映射
 ***************************************************************/

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
        return row is not null && Remove(row);
    }

    /// <summary>删掉指定的表行（按行对象删除，避免同名图片误删其它表）。</summary>
    /// <param name="row">要删除的行。</param>
    /// <returns>是否删掉了。</returns>
    public bool Remove(ItemElementRow row)
    {
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
        // 先同步共有字段，再按「新建行」口径补齐身份与默认值
        row.ApplyEditableFields(template);

        row.Id = string.IsNullOrWhiteSpace(template.TemplateId) ? row.Id : template.TemplateId;
        row.Icon = string.IsNullOrWhiteSpace(template.ResName) ? (string.IsNullOrWhiteSpace(row.Icon) ? name : row.Icon) : template.ResName;
        row.TemplateName = string.IsNullOrWhiteSpace(template.TemplateName) ? name : template.TemplateName;
        row.AnimName = TemplateDefaults.AnimNameOrDefault(template.AnimName);
        if (string.IsNullOrWhiteSpace(row.ResName))
        {
            row.ResName = template.ResName;
        }
    }

    #endregion
}
