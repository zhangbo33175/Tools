/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.Persistence.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表集合持久化：夹内读取、按格式写回与保存编排
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public sealed partial class ItemConfigSet
{
    #region 读取

    /// <summary>读取文件夹里的全部配置表；不存在 / 没有可用表时返回 null。</summary>
    /// <param name="directory">文件夹绝对路径。</param>
    /// <returns>表集合。</returns>
    public static ItemConfigSet? LoadDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
        {
            return null;
        }

        var set = new ItemConfigSet(System.IO.Path.GetFullPath(directory));
        var paths = System.IO.Directory
            .EnumerateFiles(set.Directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => System.IO.Path.GetFileName(path).Equals(PreferredCsvName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => System.IO.Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in paths)
        {
            if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("TableTemplate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kind = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? ItemTableKind.Csv
                : ItemTableKind.Xlsm;
            var file = ItemTableIO.Read(path, kind);

            // 只收「物件配置表」：列里得能找到图片名 / 模板名 / ID 之一
            if (file is not null && file.ColumnIndex.ContainsKey(ItemFieldNames.Icon))
            {
                set.Files.Add(file);
            }
        }

        if (set.Files.Count == 0)
        {
            return null;
        }

        set.RebuildIndex();
        return set;
    }

    /// <summary>按图片名取行（先查主表，再查其余表）。</summary>
    /// <param name="imageName">图片名（可含扩展名）。</param>
    /// <returns>命中的行，未命中为 null。</returns>
    public ItemElementRow? Find(string? imageName)
    {
        var key = ResourceNames.NormalizeKey(imageName);
        return key.Length == 0 ? null : _index.TryGetValue(key, out var row) ? row : null;
    }

    /// <summary>
    /// 按模板查找它绑定的表行：先看图片路径，再看资源名，最后才用调用方给的备用名。
    /// 资源面板的已配置 / 未配置两条链路共用这一顺序，避免各处自行拼两次查找。
    /// </summary>
    /// <param name="template">待查找的模板。</param>
    /// <param name="fallbackName">图片路径与资源名都未命中时的备用资源名。</param>
    /// <returns>命中的行；都没有为 null。</returns>
    public ItemElementRow? FindByTemplate(ObjectTemplate template, string? fallbackName = null)
    {
        // ---TODO 重构：模板绑定的查找顺序统一收敛到这里。
        var row = Find(template.ImagePath) ?? Find(template.ResName);
        return row ?? (string.IsNullOrWhiteSpace(fallbackName) ? null : Find(fallbackName));
    }

    /// <summary>行来自哪张表。</summary>
    /// <param name="row">数据行。</param>
    /// <returns>所在表；未知为 null。</returns>
    public ItemTableFile? FileOf(ItemElementRow row) => _owner.TryGetValue(row, out var file) ? file : null;

    /// <summary>
    /// 按左侧分类（全部 / 背景 / 建筑 / 人物 / 物件 / 收集物 / 猫猫）取它对应的那张表 ——
    /// Unity 侧就是「分类按钮 ↔ 配置表」：建筑 → TableBuildings、人物 → TableCharacters、
    /// 物件 → TableProps、收集物 → TableCollectibles、猫猫 → TableCats（没有就按 <c>cat_</c> 行回退）。
    /// </summary>
    /// <param name="category">分类显示名。</param>
    /// <returns>分类对应的表；「全部」或找不到为 null（null 表示看全部表）。</returns>
    public ItemTableFile? FileForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || ResourceCategories.IsAll(category))
        {
            return null;
        }

        var definition = ResourceCategories.FindByDisplay(category);
        if (definition is null)
        {
            return null;
        }

        var byName = FindNamedCategoryTable(definition);
        if (byName is not null)
        {
            return byName;
        }

        // 收集物必须独立成表，不能因为 TableProps 里偶尔有 collect_ 行就混用
        if (definition.RequiresOwnTable)
        {
            return null;
        }

        // 没有同名表：退化为「表里有没有这类行」（如猫猫行可能混在别的表里）
        return Files.FirstOrDefault(file => file.Rows.Any(row =>
            row.Icon.StartsWith(definition.AssetPrefix, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>按表名候选找分类表；只认文件名，不做行前缀回退。</summary>
    /// <param name="definition">分类定义。</param>
    /// <returns>命中的表；没有为 null。</returns>
    private ItemTableFile? FindNamedCategoryTable(ResourceCategoryDefinition definition)
    {
        foreach (var hint in definition.TableHints)
        {
            var byName = Files.FirstOrDefault(file =>
                System.IO.Path.GetFileNameWithoutExtension(file.Name)
                    .Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        return null;
    }

    /// <summary>
    /// 确保一个「必须独立成表」的分类拥有自己的配置表；没有就按分类首选表名建一张空 CSV。
    /// 只创建内存模型，落盘由 <see cref="CreateCategoryTable"/> 或保存流程负责。
    /// </summary>
    /// <param name="category">分类显示名，例如「收集物」。</param>
    /// <returns>分类表；分类不支持独立表或同名文件无法读取时为 null。</returns>
    public ItemTableFile? EnsureCategoryTable(string? category)
    {
        var definition = ResourceCategories.FindByDisplay(category);
        if (definition is null || !definition.RequiresOwnTable)
        {
            return null;
        }

        var existing = FindNamedCategoryTable(definition);
        if (existing is not null)
        {
            return existing;
        }

        var fileName = definition.TableHints[0] + ".csv";
        var path = System.IO.Path.Combine(Directory, fileName);
        if (System.IO.File.Exists(path))
        {
            var loaded = ItemTableIO.Read(path, ItemTableKind.Csv);
            if (loaded is null)
            {
                return null;
            }

            Files.Add(loaded);
            return loaded;
        }

        var created = ItemTableIO.CreateEmpty(path, CanonicalColumns, CanonicalTypes);
        Files.Add(created);
        return created;
    }

    /// <summary>
    /// 创建分类独立表并立即落盘。用于资源整理页的「新建收集物表」，
    /// 避免用户必须先加一行再点保存才看到文件。
    /// </summary>
    /// <param name="category">分类显示名。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>创建 / 命中的表；失败为 null。</returns>
    public ItemTableFile? CreateCategoryTable(string? category, out string error)
    {
        error = string.Empty;
        var file = EnsureCategoryTable(category);
        if (file is null)
        {
            error = $"无法创建「{category}」配置表：目录未绑定、分类不支持独立表，或同名文件表头无法识别。";
            return null;
        }

        if (file.Dirty)
        {
            if (!ItemTableIO.Write(file, out error))
            {
                Files.Remove(file);
                return null;
            }

            file.Dirty = false;
        }

        return file;
    }

    /// <summary>行内容改过之后重建查找索引（改了图片名要重新挂到新名字下）。</summary>
    public void RefreshIndexes() => RebuildIndex();

    /// <summary>
    /// 把配置表行铺成可直接摆放 / 编辑的模板（物品元件编辑与场景资源列表共用同一套映射）。
    /// </summary>
    /// <param name="row">表行。</param>
    /// <param name="type">该行所属分类。</param>
    /// <returns>由表行转换出的模板。</returns>
    public static ObjectTemplate CreateTemplate(ItemElementRow row, MapObjectType type)
    {
        // ---TODO 重构：表行转模板复用行模型的唯一映射入口，避免服务层再维护字段清单。
        var template = new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            DefaultWidth = TemplateDefaults.PlacementWidth(type),
            DefaultHeight = TemplateDefaults.PlacementHeight(type),
            LayerTypeKey = TemplateDefaults.LayerKey(type),
        };

        // 共有字段只走行模型的唯一映射入口，避免表行转模板再维护一份字段清单。
        row.ApplyToTemplate(template);

        // 身份字段按「表行 → 模板」的完整语义补齐，不受空值跳过策略影响。
        template.TemplateId = row.Id;
        template.TemplateName = row.TemplateName;
        template.ResName = row.ResName;
        template.ImagePath = row.Icon;
        if (string.IsNullOrWhiteSpace(template.InteractType))
        {
            template.InteractType = TemplateDefaults.InteractType(type);
        }

        return template;
    }

    #endregion

    #region 保存

    /// <summary>把所有改过的表写回磁盘（CSV 重写文件，xlsm 就地改单元格）。</summary>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否全部写成功。</returns>
    public bool TrySave(out string error)
    {
        error = string.Empty;
        foreach (var file in Files.Where(file => file.Dirty))
        {
            var ok = ItemTableIO.Write(file, out var fileError);
            if (!ok)
            {
                error = $"{file.Name}：{fileError}";
                return false;
            }

            file.Dirty = false;
        }

        return true;
    }

    #endregion
}
