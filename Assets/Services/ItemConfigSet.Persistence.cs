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
        var key = Normalize(imageName);
        if (key.Length == 0)
        {
            return null;
        }

        if (_index.TryGetValue(key, out var row))
        {
            return row;
        }

        var trimmed = key.TrimEnd('_');
        return _index.TryGetValue(trimmed, out row) ? row : null;
    }

    /// <summary>行来自哪张表。</summary>
    /// <param name="row">数据行。</param>
    /// <returns>所在表；未知为 null。</returns>
    public ItemTableFile? FileOf(ItemElementRow row) => _owner.TryGetValue(row, out var file) ? file : null;

    /// <summary>
    /// 按左侧分类（全部 / 背景 / 建筑 / 人物 / 物件 / 猫猫）取它对应的那张表 ——
    /// Unity 侧就是「分类按钮 ↔ 配置表」：建筑 → TableBuildings、人物 → TableCharacters、
    /// 物件 → TableProps、猫猫 → TableCats（没有就按 <c>cat_</c> 行回退）。
    /// </summary>
    /// <param name="category">分类显示名。</param>
    /// <returns>分类对应的表；「全部」或找不到为 null（null 表示看全部表）。</returns>
    public ItemTableFile? FileForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || category == "全部")
        {
            return null;
        }

        var (names, prefix) = category switch
        {
            "建筑" => (new[] { "TableBuildings", "TableBuilding", "Buildings" }, "building_"),
            "人物" => (new[] { "TableCharacters", "TableCharacter", "Characters" }, "char_"),
            "物件" => (new[] { "TableProps", "TableProp", "Props" }, "prop_"),
            "猫猫" => (new[] { "TableCats", "TableCat", "Cats" }, "cat_"),
            "背景" => (new[] { "TableBackgrounds", "TableBackground", "Backgrounds" }, "bg_"),
            _ => (Array.Empty<string>(), string.Empty),
        };

        foreach (var hint in names)
        {
            var byName = Files.FirstOrDefault(file =>
                System.IO.Path.GetFileNameWithoutExtension(file.Name)
                    .Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        // 没有同名表：退化为「表里有没有这类行」（如猫猫行可能混在别的表里）
        return prefix.Length == 0
            ? null
            : Files.FirstOrDefault(file => file.Rows.Any(row => row.Icon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
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
        return new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            TemplateId = row.Id,
            TemplateName = row.TemplateName,
            ResName = row.ResName,
            ImagePath = row.Icon,
            AnimName = string.IsNullOrWhiteSpace(row.AnimName) ? MapToolDefaults.DefaultAnimName : row.AnimName,
            Loop = row.Loop,
            Speed = row.Speed,
            SortOrder = row.SortOrder,
            ShadowEnabled = row.ShadowEnabled,
            IsCollectible = row.IsCollectible,
            RespawnTime = row.RespawnTime,
            Interactable = row.Interactable,
            InteractType = string.IsNullOrWhiteSpace(row.InteractType)
                ? (type == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk)
                : row.InteractType,
            CollisionEnabled = row.CollisionEnabled,
            DialogBubble = row.DialogBubble,
            FacingDirection = row.FacingDirection,
            DisplayLayer = row.DisplayLayer,
            TrafficCategory = row.TrafficCategory,
            YSortEnabled = row.YSortEnabled,
            YSortOffset = row.YSortOffset,
            IsMovable = row.IsMovable,
            LoopRestartDelay = row.LoopRestartDelay,
            HideWhenOffMap = row.HideWhenOffMap,
            IsTeleport = row.IsTeleport,
            TeleportTargetScene = row.TeleportTargetScene,
            TeleportTargetX = row.TeleportTargetX,
            TeleportTargetY = row.TeleportTargetY,
            DefaultWidth = type == MapObjectType.Building ? 4 : 1,
            DefaultHeight = type == MapObjectType.Building ? 4 : type == MapObjectType.Character ? 2 : 1,
            NineSlice = new NineSliceSettings
            {
                Enabled = row.NineSliceEnabled,
                Left = Math.Max(0, row.NineSliceLeft),
                Right = Math.Max(0, row.NineSliceRight),
                Top = Math.Max(0, row.NineSliceTop),
                Bottom = Math.Max(0, row.NineSliceBottom),
                CenterMode = NineSliceModes.ToCode(row.NineSliceCenterMode),
            },
            LayerTypeKey = type switch
            {
                MapObjectType.Building => MapLayerKeys.Building,
                MapObjectType.Character => MapLayerKeys.Character,
                _ => MapLayerKeys.Prop,
            },
        };
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
