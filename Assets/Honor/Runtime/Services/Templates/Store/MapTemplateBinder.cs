/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapTemplateBinder.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图实例 → 配置表 ID 绑定：按资源名对号、缺失资源建行并分配数字 ID
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>一次地图资源绑定统计。</summary>
/// <param name="Total">参与绑定的实例总数（物件 + 红绿灯）。</param>
/// <param name="AlreadyBound">本来就用配置表数字 ID 的实例数。</param>
/// <param name="Bound">按资源名对到已有配置行、已改写模板 ID 的实例数。</param>
/// <param name="Created">为缺失资源新建的配置行数（一个资源一行）。</param>
/// <param name="Missing">仍未对上配置表 ID 的资源名（去重）。</param>
public sealed record MapBindResult(
    int Total,
    int AlreadyBound,
    int Bound,
    int Created,
    IReadOnlyList<string> Missing)
{
    /// <summary>没有遗留缺失资源。</summary>
    public bool IsComplete => Missing.Count == 0;
}

/// <summary>
/// 地图资源与配置表 ID 的绑定服务。
/// <para>规则：配置表是资源身份的唯一来源。地图实例的 <c>TemplateId</c> 必须是配置表的数字 ID；
/// 历史地图里按资源名存的实例，在这里按「资源名 → 配置行」对号改写；
/// 配置表里还没有的资源，可以按分类表新建一行并分配该分类段的下一个数字 ID。</para>
/// </summary>
public static class MapTemplateBinder
{
    #region 公开方法

    /// <summary>
    /// 把地图里所有实例的模板 ID 对到配置表数字 ID。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="table">已加载的配置表集合；null 时无法绑定。</param>
    /// <param name="createMissing">true = 为配置表里没有的资源新建行并分配 ID；false = 只改已能对上的。</param>
    /// <param name="reservedIds">额外参与去重的 ID（模板库里的 ID），避免新行撞号。</param>
    /// <returns>绑定统计。</returns>
    public static MapBindResult Bind(
        MapConfigData config,
        ItemConfigSet? table,
        bool createMissing,
        IEnumerable<string>? reservedIds = null)
    {
        var total = config.Objects.Count + config.TrafficLights.Count;
        if (table is null)
        {
            var missingOnly = CollectMissingKeys(config);
            return new MapBindResult(total, 0, 0, 0, missingOnly);
        }

        var byId = BuildIdIndex(table);
        var reserved = new HashSet<string>(reservedIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            var id = row.Id.Trim();
            if (id.Length > 0)
            {
                byId[id] = row;
            }

            if (id.Length > 0)
            {
                reserved.Add(id);
            }
        }

        var bound = 0;
        var created = 0;
        var already = 0;
        var missing = new List<string>();

        foreach (var instance in config.Objects)
        {
            var outcome = BindOne(
                instance.Type,
                instance,
                table,
                byId,
                reserved,
                createMissing,
                apply: id => instance.TemplateId = id,
                keySource: instance.TemplateId,
                nameSource: instance.Name,
                imageSource: instance.ImagePath,
                allowFallback: true);
            switch (outcome)
            {
                case BindOutcome.AlreadyBound:
                    already++;
                    break;
                case BindOutcome.Bound:
                    bound++;
                    break;
                case BindOutcome.Created:
                    created++;
                    break;
                default:
                    missing.Add(ArtKey(instance.TemplateId, instance.Name, instance.ImagePath));
                    break;
            }
        }

        foreach (var light in config.TrafficLights)
        {
            var outcome = BindOne(
                MapObjectType.Prop,
                light,
                table,
                byId,
                reserved,
                createMissing,
                apply: id => light.TemplateId = id,
                keySource: light.TemplateId,
                nameSource: light.Name,
                imageSource: light.Image,
                allowFallback: false);
            switch (outcome)
            {
                case BindOutcome.AlreadyBound:
                    already++;
                    break;
                case BindOutcome.Bound:
                    bound++;
                    break;
                case BindOutcome.Created:
                    created++;
                    break;
                default:
                    missing.Add(ArtKey(light.TemplateId, light.Name, light.Image));
                    break;
            }
        }

        table.RefreshIndexes();
        return new MapBindResult(
            total,
            already,
            bound,
            created,
            missing.Where(key => key.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>
    /// 统计当前地图里「模板 ID 不是配置表数字 ID」的实例数，用于保存 / 发布前的提示。
    /// </summary>
    /// <param name="config">地图配置。</param>
    /// <param name="table">配置表集合。</param>
    /// <returns>未对上的实例数。</returns>
    public static int CountUnbound(MapConfigData config, ItemConfigSet? table)
    {
        if (table is null)
        {
            return config.Objects.Count + config.TrafficLights.Count;
        }

        var result = Bind(config, table, createMissing: false);
        return result.Missing.Count;
    }

    /// <summary>
    /// 判断一个实例的资源是否没登记在配置表，返回它的美术名（空串 = 已登记）。
    /// 用于画布把「配置表里查不到的资源」直接框出来；不写表、不改实例。
    /// </summary>
    /// <param name="templateId">实例当前模板 ID（可能已经是配置表数字 ID）。</param>
    /// <param name="name">实例名。</param>
    /// <param name="imagePath">实例图片路径。</param>
    /// <param name="table">配置表集合；null 表示没有表可查，一律视为未登记。</param>
    /// <param name="allowFallback">是否退用实例名 / 图片名再查一次（红绿灯传 false）。</param>
    /// <returns>未登记时返回美术名，已登记返回空串。</returns>
    public static string UnregisteredKey(
        string? templateId,
        string? name,
        string? imagePath,
        ItemConfigSet? table,
        bool allowFallback = true)
    {
        var key = ArtKey(templateId, name, imagePath);
        if (table is null)
        {
            return key;
        }

        var current = templateId?.Trim() ?? string.Empty;
        if (current.Length > 0 && current.All(char.IsDigit) && table.FindId(current) is not null)
        {
            return string.Empty;
        }

        if (key.Length == 0)
        {
            // 连名字都没有的实例无法判定，按已登记处理，避免整张图被标红
            return string.Empty;
        }

        if (table.Find(key) is not null)
        {
            return string.Empty;
        }

        if (allowFallback && (table.Find(name) is not null || table.Find(imagePath) is not null))
        {
            return string.Empty;
        }

        return key;
    }

    #endregion

    #region 绑定实现

    /// <summary>单个实例的绑定结果。</summary>
    private enum BindOutcome
    {
        /// <summary>本来就用配置表 ID。</summary>
        AlreadyBound,

        /// <summary>按资源名对到了已有行。</summary>
        Bound,

        /// <summary>新建了配置行。</summary>
        Created,

        /// <summary>没有对上，也没有新建。</summary>
        Missing,
    }

    /// <summary>绑定一个实例；成功时通过 <paramref name="apply"/> 把数字 ID 写回实例。</summary>
    /// <param name="allowFallback">
    /// 资源名未命中时是否退用实例名 / 图片名再查一次。
    /// 红绿灯必须为 false：灯是独立交通实体，不能借用美术行的 ID（会丢掉交通类别）。
    /// </param>
    private static BindOutcome BindOne(
        MapObjectType type,
        object payload,
        ItemConfigSet table,
        Dictionary<string, ItemElementRow> byId,
        HashSet<string> reserved,
        bool createMissing,
        Action<string> apply,
        string? keySource,
        string? nameSource,
        string? imageSource,
        bool allowFallback)
    {
        // 已经是配置表数字 ID：什么都不做
        var current = keySource?.Trim() ?? string.Empty;
        if (current.Length > 0 && current.All(char.IsDigit) && byId.ContainsKey(current))
        {
            return BindOutcome.AlreadyBound;
        }

        var key = ArtKey(keySource, nameSource, imageSource);
        if (key.Length == 0)
        {
            return BindOutcome.Missing;
        }

        // 资源名对号：模板 ID → 实例名 → 图片名，命中任意一个就改写为配置行 ID
        var row = table.Find(key);
        if (row is null && allowFallback)
        {
            row = table.Find(nameSource) ?? table.Find(imageSource);
        }

        if (row is not null && row.Id.Trim().Length > 0)
        {
            apply(row.Id.Trim());
            return BindOutcome.Bound;
        }

        if (!createMissing)
        {
            return BindOutcome.Missing;
        }

        var id = table.NextId(type, reserved);
        reserved.Add(id);
        var file = ResolveTableFile(table, key, type);
        var template = BuildTemplate(type, payload, key);
        var createdRow = AddRow(table, file, id, key, template);
        byId[id] = createdRow;
        apply(id);
        return BindOutcome.Created;
    }

    /// <summary>取资源的「美术名」：模板 ID 去目录去扩展名，空则退实例名、图片名。</summary>
    /// <remarks>
    /// PSD 自裁切片的名字带 <c>_左上X_左上Y</c> 坐标后缀（如 <c>bg_stop_1_2759_1445</c>），
    /// 同一张美术会被切成多个实例名；配置表按美术建行，所以先剥掉坐标后缀再对号。
    /// 已经是数字 ID 的候选直接跳过，避免把旧数字当成资源名建出空行。
    /// </remarks>
    private static string ArtKey(string? keySource, string? nameSource, string? imageSource)
    {
        foreach (var candidate in new[] { keySource, nameSource, imageSource })
        {
            var key = ResourceNames.NormalizeKey(candidate);
            if (key.Length == 0 || key.All(char.IsDigit))
            {
                continue;
            }

            key = SliceSuffix.Replace(key, string.Empty).TrimEnd('_');
            if (key.Length > 0)
            {
                return key;
            }
        }

        return string.Empty;
    }

    /// <summary>切片名尾部的坐标后缀：<c>_&lt;x&gt;_&lt;y&gt;</c>。</summary>
    private static readonly System.Text.RegularExpressions.Regex SliceSuffix =
        new(@"_(\d+)_(\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>按资源前缀决定落到哪张分类表：建筑 / 人物 / 猫猫 → 各自表，其余进物件表。</summary>
    private static ItemTableFile ResolveTableFile(ItemConfigSet table, string key, MapObjectType type)
    {
        var category = ResourceCategories.Classify(key);
        var display = ResourceCategories.DisplayNameOfKey(category);
        var file = table.FileForCategory(display);
        if (file is not null)
        {
            return file;
        }

        display = type switch
        {
            MapObjectType.Building => ResourceCategories.BuildingCategory.DisplayName,
            MapObjectType.Character => ResourceCategories.CharacterCategory.DisplayName,
            _ => ResourceCategories.PropCategory.DisplayName,
        };
        return table.FileForCategory(display) ?? table.Primary ?? table.Files[0];
    }

    /// <summary>用实例上的业务字段拼一份模板，作为新建配置行的取值来源。</summary>
    private static ObjectTemplate BuildTemplate(MapObjectType type, object payload, string key)
    {
        var template = new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            TemplateId = string.Empty,
            TemplateName = key,
            ResName = key,
            ImagePath = string.Empty,
            DefaultWidth = TemplateDefaults.PlacementWidth(type),
            DefaultHeight = TemplateDefaults.PlacementHeight(type),
            LayerTypeKey = TemplateDefaults.LayerKey(type),
            InteractType = TemplateDefaults.InteractType(type),
            CollisionEnabled = TemplateDefaults.CollisionEnabled(type),
            AnimName = MapToolDefaults.DefaultAnimName,
        };

        if (payload is PlacedObject instance)
        {
            template.ImagePath = string.IsNullOrWhiteSpace(instance.ImagePath) ? key : instance.ImagePath;
            template.LayerTypeKey = string.IsNullOrWhiteSpace(instance.LayerType) ? template.LayerTypeKey : instance.LayerType;
            template.DisplayLayer = instance.DisplayLayer;
            template.DefaultWidth = Math.Max(1, instance.Width);
            template.DefaultHeight = Math.Max(1, instance.Height);
            template.AnimName = TemplateDefaults.AnimNameOrDefault(instance.AnimName);
            template.Loop = instance.Loop;
            template.Speed = instance.Speed;
            template.SortOrder = instance.SortOrder;
            template.Interactable = instance.Interactable;
            template.InteractType = TemplateInteractTypes.ToTemplateCode(instance.InteractType);
            template.DialogBubble = instance.DialogBubble;
            template.BubbleId = instance.BubbleId;
            template.CollisionEnabled = instance.CollisionEnabled;
            template.FacingDirection = instance.FacingDirection;
            template.ShadowEnabled = instance.ShadowEnabled;
            template.IsCollectible = instance.IsCollectible;
            template.RespawnTime = instance.RespawnTime;
            template.IsMovable = instance.IsMovable;
            template.MoveAnimName = instance.MoveAnimName;
            template.YSortEnabled = instance.YSortEnabled;
            template.YSortOffset = instance.YSortOffset;
            template.IsTeleport = instance.IsTeleport;
            template.TeleportTargetScene = instance.TeleportTargetScene;
            template.TeleportTargetX = instance.TeleportTargetX;
            template.TeleportTargetY = instance.TeleportTargetY;
            return template;
        }

        if (payload is MapTrafficLightData light)
        {
            template.ImagePath = string.IsNullOrWhiteSpace(light.Image) ? key : light.Image;
            template.AnimName = TemplateDefaults.AnimNameOrDefault(light.Anim);
            template.InteractType = TemplateInteractTypes.None;
            template.CollisionEnabled = false;
            template.TrafficCategory = TemplateTrafficCategories.Streetlight;
        }

        return template;
    }

    /// <summary>在目标表里追加一行并写入字段；ID / 图片名 / 模板名 / 预制体名一次性写全。</summary>
    private static ItemElementRow AddRow(
        ItemConfigSet table,
        ItemTableFile file,
        string id,
        string key,
        ObjectTemplate template)
    {
        var row = file.CreateRow();
        file.Rows.Add(row);
        file.Dirty = true;

        row.Id = id;
        row.Icon = key;
        row.TemplateName = key;
        row.ResName = PrefabName(template.Type);
        row.ApplyEditableFields(template);
        if (string.IsNullOrWhiteSpace(row.AnimName))
        {
            row.AnimName = MapToolDefaults.DefaultAnimName;
        }

        return row;
    }

    /// <summary>Unity 侧预制体名：建筑 / 人物 / 物件各一个固定前缀。</summary>
    private static string PrefabName(MapObjectType type) => type switch
    {
        MapObjectType.Building => "GameObject_Building",
        MapObjectType.Character => "GameObject_Character",
        _ => "GameObject_Prop",
    };

    /// <summary>配置表「ID → 行」索引。</summary>
    private static Dictionary<string, ItemElementRow> BuildIdIndex(ItemConfigSet table)
    {
        var byId = new Dictionary<string, ItemElementRow>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            var id = row.Id.Trim();
            if (id.Length > 0)
            {
                byId.TryAdd(id, row);
            }
        }

        return byId;
    }

    /// <summary>只收集缺失资源名（表未加载时用）。</summary>
    private static IReadOnlyList<string> CollectMissingKeys(MapConfigData config)
    {
        var keys = new List<string>();
        foreach (var instance in config.Objects)
        {
            var key = ArtKey(instance.TemplateId, instance.Name, instance.ImagePath);
            if (key.Length > 0)
            {
                keys.Add(key);
            }
        }

        foreach (var light in config.TrafficLights)
        {
            var key = ArtKey(light.TemplateId, light.Name, light.Image);
            if (key.Length > 0)
            {
                keys.Add(key);
            }
        }

        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    #endregion
}

/// <summary>配置表目录备份：写表前先整目录拷一份，出问题可直接覆盖回去。</summary>
public static class ItemTableBackup
{
    /// <summary>把配置表目录拷到 <c>backup_yyyyMMdd_HHmmss</c> 子目录。</summary>
    /// <param name="directory">配置表目录。</param>
    /// <param name="backupDirectory">备份目录；失败时为空串。</param>
    /// <returns>成功返回 true。</returns>
    public static bool TryCreate(string? directory, out string backupDirectory)
    {
        backupDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var target = Path.Combine(directory, "backup_" + stamp);
            Directory.CreateDirectory(target);
            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(path, Path.Combine(target, Path.GetFileName(path)), overwrite: true);
            }

            backupDirectory = target;
            return true;
        }
        catch (Exception)
        {
            backupDirectory = string.Empty;
            return false;
        }
    }
}
