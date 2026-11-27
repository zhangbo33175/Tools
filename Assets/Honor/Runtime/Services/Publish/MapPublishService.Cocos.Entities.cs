/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Cocos.Entities.cs
 * author:    云毅
 * created:   2026
 * descrip:   Cocos/Unity 实体子表：统一字段模型、详细输出与紧凑输出
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

/// <summary>实体表字段：名称、Lua 字面量、是否表值、是否允许进入共享默认值。</summary>
public static partial class MapPublishService
{
    #region 实体字段模型

    /// <summary>
    /// 实体字段描述。IsTable=true 的字段（PathPoints/BoundLightIds 等）不参与默认值与模板复用，
    /// 避免多个实体共享同一张可变表；AllowDefault=false 的字段是“缺失即无”的可选字段。
    /// </summary>
    private readonly record struct LuaEntityField(
        string Name,
        string Literal,
        bool IsTable = false,
        bool AllowDefault = true);

    #endregion

    #region Cocos 子表

    /// <summary>
    /// 构建实体子表：统一处理表头、空列表提示与字段输出。
    /// compact=false 保持旧版逐字段输出；compact=true 输出行差异 + 模板复用 + Base 兜底。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="suffix">子表后缀。</param>
    /// <param name="items">实体列表。</param>
    /// <param name="description">Lua 表描述。</param>
    /// <param name="brief">表用途简述。</param>
    /// <param name="buildFields">实体字段构建回调。</param>
    /// <param name="detail">可选补充说明。</param>
    /// <param name="compact">是否使用紧凑格式。</param>
    /// <returns>子表 Lua 文本。</returns>
    private static string BuildCocosEntityTable(
        string baseName,
        string suffix,
        IReadOnlyList<PlacedObject> items,
        string description,
        string brief,
        Func<PlacedObject, IReadOnlyList<LuaEntityField>> buildFields,
        string? detail = null,
        bool compact = false)
    {
        if (compact)
        {
            return BuildCompactEntityTable(baseName, suffix, items, description, brief, buildFields, detail);
        }

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
                AppendLuaFields(sb, buildFields(items[index]), "        ");
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 紧凑实体子表：按 TemplateId 汇总不变字段到 Templates，把全量一致字段汇总到 Base，
    /// 每行只保留真正有差异的字段。运行时通过 __index 链透明读取，不改变字段访问语义。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="suffix">子表后缀。</param>
    /// <param name="items">实体列表。</param>
    /// <param name="description">Lua 表描述。</param>
    /// <param name="brief">表用途简述。</param>
    /// <param name="buildFields">实体字段构建回调。</param>
    /// <param name="detail">可选补充说明。</param>
    /// <returns>紧凑格式 Lua 文本。</returns>
    private static string BuildCompactEntityTable(
        string baseName,
        string suffix,
        IReadOnlyList<PlacedObject> items,
        string description,
        string brief,
        Func<PlacedObject, IReadOnlyList<LuaEntityField>> buildFields,
        string? detail = null)
    {
        var tableName = SubTableName(baseName, suffix);
        var sb = new StringBuilder();
        AppendLuaHeader(sb, tableName, description, brief, detail);
        AppendLuaComment(sb, "紧凑格式 v2：行字段 → 模板字段 → Base 默认值；字段语义与旧表完全一致");

        if (items.Count == 0)
        {
            sb.AppendLine($"Tables.{tableName} = {{}}");
            return sb.ToString();
        }

        var rows = items.Select(buildFields).Select(fields => fields.ToList()).ToList();
        var fieldOrder = CollectFieldOrder(rows);
        var rowMaps = rows.Select(fields => fields.ToDictionary(field => field.Name, StringComparer.Ordinal)).ToList();

        // 先按 TemplateId 收集同模板实例，组内完全一致的字段进入模板表。
        var templateGroups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var templateOrder = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var key = items[index].TemplateId ?? string.Empty;
            if (!templateGroups.TryGetValue(key, out var group))
            {
                group = new List<int>();
                templateGroups[key] = group;
                templateOrder.Add(key);
            }

            group.Add(index);
        }

        var consumed = new HashSet<(int Row, string Field)>();
        var templateFields = new Dictionary<string, List<LuaEntityField>>(StringComparer.Ordinal);
        foreach (var key in templateOrder)
        {
            var group = templateGroups[key];
            if (group.Count < 2)
            {
                continue;
            }

            var reusable = new List<LuaEntityField>();
            foreach (var fieldName in fieldOrder)
            {
                if (fieldName == "TemplateId")
                {
                    continue;
                }

                var values = group
                    .Select(rowIndex => rowMaps[rowIndex].TryGetValue(fieldName, out var field)
                        ? (LuaEntityField?)field
                        : null)
                    .ToList();
                var first = values[0];
                if (!first.HasValue || first.Value.IsTable || !first.Value.AllowDefault)
                {
                    continue;
                }

                var literal = first.Value.Literal;
                if (values.All(field => field.HasValue && field.Value.Literal == literal))
                {
                    reusable.Add(first.Value);
                    foreach (var rowIndex in group)
                    {
                        consumed.Add((rowIndex, fieldName));
                    }
                }
            }

            if (reusable.Count > 0)
            {
                templateFields[key] = reusable;
            }
        }

        // 再收集 Base 默认值：仅当该字段在所有未进入模板的实例上取值完全一致时才提升，
        // 保证 Base 是“真正的字段默认值”，而非出现次数最多的实例值（如 PosX/Name/Image 不得进入 Base），
        // 从而避免新增实体缺字段时被错误地套用某条实例的坐标或资源路径。
        var baseFields = new List<LuaEntityField>();
        var baseValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fieldName in fieldOrder)
        {
            var candidates = new Dictionary<string, (LuaEntityField Field, int Count)>(StringComparer.Ordinal);
            var eligibleRows = 0;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (consumed.Contains((rowIndex, fieldName)))
                {
                    continue;
                }

                var field = rowMaps[rowIndex].TryGetValue(fieldName, out var current)
                    ? (LuaEntityField?)current
                    : null;
                if (!field.HasValue || field.Value.IsTable || !field.Value.AllowDefault)
                {
                    continue;
                }

                eligibleRows++;
                var existing = candidates.TryGetValue(field.Value.Literal, out var entry)
                    ? entry
                    : (Field: field.Value, Count: 0);
                candidates[field.Value.Literal] = (existing.Field, existing.Count + 1);
            }

            if (candidates.Count == 0)
            {
                continue;
            }

            var best = candidates.Values.OrderByDescending(entry => entry.Count).First();
            if (candidates.Count != 1 || eligibleRows < 3 || best.Count != eligibleRows)
            {
                continue;
            }

            baseFields.Add(new LuaEntityField(fieldName, best.Field.Literal));
            baseValues[fieldName] = best.Field.Literal;
        }

        sb.AppendLine("local Base = {");
        AppendLuaFields(sb, baseFields, "    ");
        sb.AppendLine("}");
        sb.AppendLine();

        // 模板字段若与 Base 取值相同则无需重复写出，模板通过 __index Base 继承即可；
        // 仅保留模板内真正区别于 Base 的字段，避免模板与 Base 双重冗余。
        foreach (var key in templateOrder)
        {
            var fields = templateFields.TryGetValue(key, out var reusable) ? reusable : null;
            if (fields is null)
            {
                continue;
            }

            var trimmed = fields
                .Where(field => !baseValues.TryGetValue(field.Name, out var baseLiteral) || field.Literal != baseLiteral)
                .ToList();
            if (trimmed.Count == 0)
            {
                templateFields.Remove(key);
            }
            else
            {
                templateFields[key] = trimmed;
            }
        }

        sb.AppendLine("local Templates = {");
        foreach (var key in templateOrder)
        {
            var fields = templateFields.TryGetValue(key, out var reusable) ? reusable : null;
            if (fields is null || fields.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"    [\"{Escape(key)}\"] = {{");
            AppendLuaFields(sb, fields, "        ");
            sb.AppendLine("    },");
        }

        sb.AppendLine("}");
        sb.AppendLine("for _, template in pairs(Templates) do");
        sb.AppendLine("    setmetatable(template, { __index = Base })");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine("local function Row(data)");
        sb.AppendLine("    return setmetatable(data, { __index = Templates[data.TemplateId] or Base })");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine($"Tables.{tableName} = {{");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            sb.AppendLine("    Row{");
            foreach (var field in rows[rowIndex])
            {
                if (consumed.Contains((rowIndex, field.Name)))
                {
                    continue;
                }

                if (baseValues.TryGetValue(field.Name, out var baseLiteral) && field.Literal == baseLiteral)
                {
                    continue;
                }

                sb.AppendLine($"        {field.Name} = {field.Literal},");
            }

            sb.AppendLine("    },");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>收集所有行字段的首次出现顺序，保持生成结果稳定可读。</summary>
    /// <param name="rows">实体字段行。</param>
    /// <returns>字段名有序列表。</returns>
    private static List<string> CollectFieldOrder(IEnumerable<IReadOnlyList<LuaEntityField>> rows)
    {
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var field in row)
            {
                if (seen.Add(field.Name))
                {
                    order.Add(field.Name);
                }
            }
        }

        return order;
    }

    /// <summary>按固定缩进输出字段行；表值字面量由字段构建器负责生成单行字面量。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="fields">字段列表。</param>
    /// <param name="indent">缩进字符串。</param>
    private static void AppendLuaFields(StringBuilder sb, IReadOnlyList<LuaEntityField> fields, string indent)
    {
        foreach (var field in fields)
        {
            sb.Append(indent)
                .Append(field.Name.PadRight(20))
                .Append("= ")
                .Append(field.Literal)
                .AppendLine(",");
        }
    }

    /// <summary>追加单行 Lua 注释。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="comment">注释文本。</param>
    private static void AppendLuaComment(StringBuilder sb, string comment)
    {
        sb.AppendLine($"-- @desc      {comment}");
        sb.AppendLine();
    }

    /// <summary>
    /// 构建角色子表：公共字段复用 <see cref="BuildCocosObjectFields"/>，
    /// 再补角色独有的巡逻、交互、路径与交通绑定字段。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="characters">角色实例列表。</param>
    /// <param name="imageResolver">贴图路径解析器。</param>
    /// <param name="includePathPoints">是否内嵌路径点。</param>
    /// <param name="gridSize">网格尺寸。</param>
    /// <param name="compact">是否使用紧凑格式。</param>
    /// <returns>角色表 Lua 文本。</returns>
    private static string BuildCocosCharactersFile(
        string baseName,
        IReadOnlyList<PlacedObject> characters,
        Func<PlacedObject, string>? imageResolver = null,
        bool includePathPoints = false,
        int gridSize = 0,
        bool compact = false) =>
        BuildCocosEntityTable(
            baseName,
            CharactersSuffix,
            characters,
            "角色列表（自动生成）",
            "关卡角色实体列表（只存差异字段）",
            item => BuildCocosObjectFields(
                item,
                includeCollectId: false,
                includeCollectibleFields: false,
                includeCharacterFields: true,
                imageResolver: imageResolver,
                includePathPoints: includePathPoints,
                gridSize: gridSize),
            compact: compact);

    /// <summary>构建建筑子表；字段口径与物件表共用，但不输出收集物字段。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="buildings">建筑实例列表。</param>
    /// <param name="imageResolver">贴图路径解析器。</param>
    /// <param name="includePathPoints">是否内嵌路径点。</param>
    /// <param name="gridSize">网格尺寸。</param>
    /// <param name="compact">是否使用紧凑格式。</param>
    /// <returns>建筑表 Lua 文本。</returns>
    private static string BuildCocosBuildingsFile(
        string baseName,
        IReadOnlyList<PlacedObject> buildings,
        Func<PlacedObject, string>? imageResolver = null,
        bool includePathPoints = false,
        int gridSize = 0,
        bool compact = false) =>
        BuildCocosEntityTable(
            baseName,
            BuildingsSuffix,
            buildings,
            "建筑列表（自动生成）",
            "关卡建筑实体列表",
            item => BuildCocosObjectFields(
                item,
                includeCollectId: false,
                imageResolver: imageResolver,
                includePathPoints: includePathPoints,
                gridSize: gridSize),
            compact: compact);

    /// <summary>构建待收集物件子表；比建筑表多输出收集物标记与 collectId。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="objects">物件实例列表。</param>
    /// <param name="imageResolver">贴图路径解析器。</param>
    /// <param name="includePathPoints">是否内嵌路径点。</param>
    /// <param name="gridSize">网格尺寸。</param>
    /// <param name="compact">是否使用紧凑格式。</param>
    /// <returns>物件表 Lua 文本。</returns>
    private static string BuildCocosObjectsFile(
        string baseName,
        IReadOnlyList<PlacedObject> objects,
        Func<PlacedObject, string>? imageResolver = null,
        bool includePathPoints = false,
        int gridSize = 0,
        bool compact = false) =>
        BuildCocosEntityTable(
            baseName,
            ObjectsSuffix,
            objects,
            "待收集物件列表（自动生成）",
            "关卡待收集物件列表",
            item => BuildCocosObjectFields(
                item,
                includeCollectId: true,
                imageResolver: imageResolver,
                includePathPoints: includePathPoints,
                gridSize: gridSize),
            "字段与 Buildings 对齐：GridX/GridY 兼容旧换算，PosX/PosY/PixelWidth/PixelHeight/Stretch/LayerType 按 PSD 还原",
            compact: compact);

    /// <summary>
    /// 构建建筑 / 物件 / 角色共用字段。表值字段（PathPoints/BoundLightIds）保持行内输出；
    /// 可选字段（Band/PathId/物件 PatrolLoop）标记为不允许默认，避免“缺失即无”的语义被 Base 改写。
    /// </summary>
    /// <param name="item">实例数据。</param>
    /// <param name="includeCollectId">是否输出 collectId（物件表为 true）。</param>
    /// <param name="includeCollectibleFields">是否输出收集物标记字段（建筑表 / 角色表为 false）。</param>
    /// <param name="includeCharacterFields">是否追加角色独有字段。</param>
    /// <param name="imageResolver">贴图路径解析器。</param>
    /// <param name="includePathPoints">是否内嵌路径点。</param>
    /// <param name="gridSize">网格尺寸。</param>
    /// <returns>字段列表。</returns>
    private static IReadOnlyList<LuaEntityField> BuildCocosObjectFields(
        PlacedObject item,
        bool includeCollectId,
        bool includeCollectibleFields = true,
        bool includeCharacterFields = false,
        Func<PlacedObject, string>? imageResolver = null,
        bool includePathPoints = false,
        int gridSize = 0)
    {
        var fields = new List<LuaEntityField>
        {
            new("Uid", $"\"{Escape(item.Id)}\"", false, false),
            new("TemplateId", $"\"{Escape(item.TemplateId)}\"", false, false),
            new("Name", $"\"{Escape(item.Name)}\""),
            new("GridX", item.CellX.ToString(CultureInfo.InvariantCulture)),
            new("GridY", item.CellY.ToString(CultureInfo.InvariantCulture)),
            new("PosX", Num(item.X)),
            new("PosY", Num(item.Y)),
            new("Width", item.ExportWidth.ToString(CultureInfo.InvariantCulture)),
            new("Height", item.ExportHeight.ToString(CultureInfo.InvariantCulture)),
            new("PixelWidth", Num(item.PixelWidth)),
            new("PixelHeight", Num(item.PixelHeight)),
            new("Stretch", Bool(item.Stretch)),
            new("LayerType", $"\"{Escape(item.LayerType)}\""),
            new("DisplayLayer", $"\"{Escape(item.DisplayLayer)}\""),
        };

        if (includeCollectibleFields)
        {
            fields.Add(new LuaEntityField("IsCollectible", Bool(item.IsCollectible)));
            fields.Add(new LuaEntityField("CollectibleOverObjects", Bool(item.CollectibleOverObjects)));
        }

        var imagePath = imageResolver?.Invoke(item) ?? item.ImagePath;
        fields.Add(new LuaEntityField("Image", $"\"{Escape(imagePath)}\""));
        fields.Add(new LuaEntityField("Anim", $"\"{Escape(item.AnimName)}\""));
        fields.Add(new LuaEntityField("AnimEnabled", Bool(item.AnimEnabled)));
        fields.Add(new LuaEntityField("AnimationKind", $"\"{Escape(item.AnimationKind)}\""));
        fields.Add(new LuaEntityField("SpineAsset", $"\"{Escape(item.SpineAsset)}\""));
        fields.Add(new LuaEntityField("SpineAnimation", $"\"{Escape(item.SpineAnimation)}\""));
        fields.Add(new LuaEntityField("Loop", Bool(item.Loop)));
        fields.Add(new LuaEntityField("Speed", Num(item.Speed)));
        if (includeCollectId)
        {
            fields.Add(new LuaEntityField("CollectId", $"\"collect_{Escape(item.Id)}\"", false, false));
        }

        fields.Add(new LuaEntityField("SortOrder", item.SortOrder.ToString(CultureInfo.InvariantCulture)));
        fields.Add(new LuaEntityField("YSortEnabled", Bool(item.YSortEnabled)));
        fields.Add(new LuaEntityField("YSortOffset", Num(item.YSortOffset)));
        fields.Add(new LuaEntityField("Rotation", Num(item.Rotation)));
        fields.Add(new LuaEntityField("Scale", Num(item.Scale)));
        fields.Add(new LuaEntityField("ScaleX", Num(item.Scale)));
        fields.Add(new LuaEntityField("ScaleY", Num(item.Scale)));
        fields.Add(new LuaEntityField("FlipX", Bool(item.FlipX)));
        if (item.BandOverride >= 0)
        {
            fields.Add(new LuaEntityField("Band", item.BandOverride.ToString(CultureInfo.InvariantCulture), false, false));
        }

        if (includePathPoints && item.PathPoints.Count > 0)
        {
            // ---TODO [Unity 直载] 建筑/物件路径走条目内嵌 PathPoints：Unity 运行时的
            // _SpawnSingleMapObject 只读 entry.PathPoints，不会按 PathId 回查 _Paths。
            // 角色表不输出此布尔 PatrolLoop，避免与角色字段中的字符串 PatrolLoop 重名覆盖。
            var grid = gridSize > 0 ? gridSize : MapToolDefaults.GridSize;
            var points = string.Join(", ", item.PathPoints.Select(point =>
            {
                var gridX = (int)Math.Floor(point.X / Math.Max(1, grid));
                var gridY = (int)Math.Floor(point.Y / Math.Max(1, grid));
                return $"{{{gridX}, {gridY}}}";
            }));
            fields.Add(new LuaEntityField("PathPoints", $"{{ {points} }}", true, false));
            if (!includeCharacterFields)
            {
                var patrolLoop = !string.Equals(item.WalkMode, "once", StringComparison.OrdinalIgnoreCase);
                fields.Add(new LuaEntityField("PatrolLoop", Bool(patrolLoop), false, false));
            }
        }

        if (includeCharacterFields)
        {
            fields.AddRange(BuildCocosCharacterFields(item));
        }

        return fields;
    }

    /// <summary>角色表独有的行为字段；公共位置/外观字段由 <see cref="BuildCocosObjectFields"/> 统一输出。</summary>
    /// <param name="item">角色物件。</param>
    /// <returns>角色字段列表。</returns>
    private static IReadOnlyList<LuaEntityField> BuildCocosCharacterFields(PlacedObject item)
    {
        var fields = new List<LuaEntityField>
        {
            new("PatrolLoop", $"\"{Escape(item.WalkMode)}\""),
            new("FacingDirection", $"\"{Escape(item.FacingDirection)}\""),
            new("Interactable", Bool(item.Interactable)),
            new("InteractType", $"\"{Escape(item.InteractType)}\""),
            new("DialogBubble", $"\"{Escape(item.DialogBubble)}\""),
        };

        // ---TODO [Unity 直载] 仅真实带路径的角色才输出 PathId；否则运行时按 Paths 子表回查会得到 nil，
        // 且会让“无路径角色”看起来像配了巡逻路径。Cocos 侧读取同样按 PathId 存在性判断，语义一致。
        if (item.PathPoints.Count > 0)
        {
            fields.Add(new LuaEntityField("PathId", $"\"path_{Escape(item.Id)}\"", false, false));
        }

        fields.Add(new LuaEntityField("IsVehicle", Bool(item.IsVehicle)));
        fields.Add(new LuaEntityField("TrafficRole", $"\"{Escape(item.TrafficRole)}\""));
        fields.Add(new LuaEntityField("StopLineX", Num(item.StopLineX)));
        fields.Add(new LuaEntityField("StopLineY", Num(item.StopLineY)));
        fields.Add(new LuaEntityField("BoundLightId", $"\"{Escape(item.BoundLightIds.FirstOrDefault() ?? string.Empty)}\""));
        fields.Add(new LuaEntityField("BoundLightIds", LuaStringArray(item.BoundLightIds), true, false));
        fields.Add(new LuaEntityField("YellowTreatAsRed", Bool(item.YellowTreatAsRed)));
        return fields;
    }

    #endregion
}
