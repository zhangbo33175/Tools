/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图发布双出口：Unity MapConfigData JSON / Cocos Levels Lua 表
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

#region 发布目标与选项

/// <summary>发布目标。</summary>
public enum MapPublishTarget
{
    /// <summary>Unity 运行时地图 JSON（MapConfigData 字段口径）。</summary>
    UnityJson,

    /// <summary>Cocos Lua 运行时拆分关卡表（Tables.LevelData_*）。</summary>
    CocosLua,
}

/// <summary>
/// 发布选项。输出目录是目标工程的接入目录，不是编辑器的源存档目录。
/// </summary>
public sealed class MapPublishOptions
{
    /// <summary>发布目标。</summary>
    public MapPublishTarget Target { get; set; } = MapPublishTarget.UnityJson;

    /// <summary>目标输出根目录。</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Unity 发布时是否带上迷雾 sidecar。</summary>
    public bool IncludeFog { get; set; } = true;

    /// <summary>Cocos Lua 主表的 MapName（默认 GameMainMap，对应地图预制体名）。</summary>
    public string MapName { get; set; } = "GameMainMap";

    /// <summary>Cocos 发布时是否同步更新 TableLevelData.lua.txt 章节索引。</summary>
    public bool UpdateCocosIndex { get; set; } = true;
}

/// <summary>发布预检摘要。</summary>
public sealed class MapPublishPreview
{
    /// <summary>建筑数量。</summary>
    public int Buildings { get; set; }

    /// <summary>物件数量。</summary>
    public int Objects { get; set; }

    /// <summary>角色数量。</summary>
    public int Characters { get; set; }

    /// <summary>启用动画的实例数量（序列帧或 Spine）。</summary>
    public int Animated { get; set; }

    /// <summary>Spine 动画实例数量。</summary>
    public int Spine { get; set; }

    /// <summary>带路径的物件数量（角色 / 建筑 / 物件 / 载具都可带路径）。</summary>
    public int Paths { get; set; }

    /// <summary>红绿灯数量。</summary>
    public int Lights { get; set; }

    /// <summary>斑马线数量。</summary>
    public int Crosswalks { get; set; }

    /// <summary>预计写入的文件数量。</summary>
    public int Files { get; set; }

    /// <summary>输出目录的接入提示。</summary>
    public string TargetHint { get; set; } = string.Empty;

    /// <summary>面向用户的一行摘要。</summary>
    public string Summary =>
        $"建筑 {Buildings} · 物件 {Objects} · 角色 {Characters} · 动画 {Animated}（Spine {Spine}）" +
        $" · 路径 {Paths} · 灯 {Lights} · 横道 {Crosswalks} · 预计 {Files} 个文件";
}

/// <summary>发布结果。</summary>
public sealed class MapPublishResult
{
    /// <summary>目标类型。</summary>
    public MapPublishTarget Target { get; set; }

    /// <summary>实际输出根目录。</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>生成的文件绝对路径。</summary>
    public List<string> Files { get; set; } = new();

    /// <summary>发布清单路径。</summary>
    public string ManifestPath { get; set; } = string.Empty;

    /// <summary>非阻断性提示。</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>发布摘要。</summary>
    public string Summary { get; set; } = string.Empty;
}

#endregion

#region 发布设置持久化

/// <summary>
/// 发布设置。输出目录来自用户上次选择，未选择时按本机工程探测默认值。
/// </summary>
public sealed class MapPublishSettings
{
    /// <summary>Unity 目标输出目录。</summary>
    public string UnityOutputDirectory { get; set; } = string.Empty;

    /// <summary>Cocos 目标输出目录。</summary>
    public string CocosOutputDirectory { get; set; } = string.Empty;

    /// <summary>Cocos MapName。</summary>
    public string MapName { get; set; } = "GameMainMap";

    /// <summary>Unity 是否带迷雾。</summary>
    public bool IncludeFog { get; set; } = true;

    /// <summary>Cocos 是否更新章节索引。</summary>
    public bool UpdateCocosIndex { get; set; } = true;
}

/// <summary>发布设置读写（output\Config\PublishSettings.json）。</summary>
public static class MapPublishSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>设置文件路径。</summary>
    public static string JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "output", "Config", "PublishSettings.json");

    /// <summary>读取设置；缺失或损坏时返回默认值。</summary>
    /// <returns>发布设置。</returns>
    public static MapPublishSettings Load()
    {
        try
        {
            if (File.Exists(JsonPath))
            {
                var loaded = JsonSerializer.Deserialize<MapPublishSettings>(File.ReadAllText(JsonPath));
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.UnityOutputDirectory))
                    {
                        loaded.UnityOutputDirectory = MapPublishService.DefaultOutputRoot(MapPublishTarget.UnityJson);
                    }

                    if (string.IsNullOrWhiteSpace(loaded.CocosOutputDirectory))
                    {
                        loaded.CocosOutputDirectory = MapPublishService.DefaultOutputRoot(MapPublishTarget.CocosLua);
                    }

                    return loaded;
                }
            }
        }
        catch
        {
            // 设置文件损坏时按默认值继续，避免阻断发布。
        }

        return new MapPublishSettings
        {
            UnityOutputDirectory = MapPublishService.DefaultOutputRoot(MapPublishTarget.UnityJson),
            CocosOutputDirectory = MapPublishService.DefaultOutputRoot(MapPublishTarget.CocosLua),
        };
    }

    /// <summary>保存设置。</summary>
    /// <param name="settings">发布设置。</param>
    public static void Save(MapPublishSettings settings)
    {
        var directory = Path.GetDirectoryName(JsonPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(JsonPath, JsonSerializer.Serialize(settings, Options), new UTF8Encoding(false));
    }
}

#endregion

#region 发布服务

/// <summary>
/// 地图发布服务。Unity 出口复用 <see cref="MapJsonService"/> 的 MapConfigData JSON；
/// Cocos 出口生成 Cocos Lua 运行时正在消费的拆分关卡表，不引入第二套编辑器模型。
/// </summary>
public static class MapPublishService
{
    #region 常量

    private const string CocosLevelsRelative = @"assets\LuaScripts\Game\Tables\Levels";
    private const string UnityMapDatasRelative = @"Assets\GameData\MapDatas";
    private const string LuaNumberFormat = "0.##";
    private const string LuaBanner = "--=====================================================================================================";
    private const string LuaNotice = "由地图编辑器自动生成，禁止手动修改";

    #endregion

    #region 默认目录

    /// <summary>按目标返回本机默认输出目录；工程不存在时回退到 docs\Publish。</summary>
    /// <param name="target">发布目标。</param>
    /// <returns>绝对目录。</returns>
    public static string DefaultOutputRoot(MapPublishTarget target)
    {
        var editorRoot = FindEditorRoot();
        if (target == MapPublishTarget.CocosLua)
        {
            var cocos = Path.Combine(@"E:\CocosProjects\CatGameCodeOptimization", CocosLevelsRelative);
            if (Directory.Exists(cocos))
            {
                return cocos;
            }

            return Path.Combine(editorRoot, "docs", "Publish", "Cocos", "Levels");
        }

        var unity = Path.Combine(@"E:\Game\CatGame\CatGameCodeOptimizationBuild", UnityMapDatasRelative);
        if (Directory.Exists(unity))
        {
            return unity;
        }

        return Path.Combine(editorRoot, "docs", "Publish", "Unity");
    }

    /// <summary>从 exe 向上找 MapEditorTool.csproj，得到编辑器工程根。</summary>
    private static string FindEditorRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "MapEditorTool.csproj")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        return AppContext.BaseDirectory;
    }

    #endregion

    #region 预检

    /// <summary>统计当前地图的发布规模。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="target">发布目标。</param>
    /// <returns>预检摘要。</returns>
    public static MapPublishPreview Preview(MapConfigData config, MapPublishTarget target)
    {
        var buildings = config.Objects.Count(item => item.Type == MapObjectType.Building);
        var objects = config.Objects.Count(item => item.Type == MapObjectType.Prop);
        var characters = config.Objects.Count(item => item.Type == MapObjectType.Character);
        var animated = config.Objects.Count(item =>
        {
            if (!item.AnimEnabled)
            {
                return false;
            }

            if (string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(item.SpineAsset);
            }

            // 默认 Idle 不等于真的有关卡动画片段；只把显式改过的序列帧动画算进预检。
            return !string.IsNullOrWhiteSpace(item.AnimName)
                   && !string.Equals(item.AnimName, MapToolDefaults.DefaultAnimName, StringComparison.OrdinalIgnoreCase);
        });
        var spine = config.Objects.Count(item =>
            string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase));
        var paths = config.Objects.Count(item => item.PathPoints.Count > 0);

        return new MapPublishPreview
        {
            Buildings = buildings,
            Objects = objects,
            Characters = characters,
            Animated = animated,
            Spine = spine,
            Paths = paths,
            Lights = config.TrafficLights.Count,
            Crosswalks = config.Crosswalks.Count,
            Files = target == MapPublishTarget.UnityJson
                ? 3
                : 7,
            TargetHint = target == MapPublishTarget.UnityJson
                ? "写入 Unity MapConfigData JSON，供 MapConfigRepository 读取"
                : "写入 Tables.LevelData_* 拆分 Lua 表，供 Cocos Lua 运行时读取",
        };
    }

    #endregion

    #region 发布入口

    /// <summary>
    /// 发布当前地图到目标工程目录。源存档 docs\MapJson 不会被修改。
    /// </summary>
    /// <param name="config">地图数据。</param>
    /// <param name="options">发布选项。</param>
    /// <param name="fog">迷雾文档，仅 Unity 出口使用。</param>
    /// <returns>发布结果。</returns>
    public static MapPublishResult Publish(MapConfigData config, MapPublishOptions options, MapFogDocument? fog = null)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new ArgumentException("发布目录不能为空。", nameof(options));
        }

        var root = Path.GetFullPath(options.OutputDirectory.Trim());
        Directory.CreateDirectory(root);

        var chapter = NumericOr(config.ChapterId, "1");
        var level = NumericOr(config.SceneId, "1");
        var result = options.Target == MapPublishTarget.CocosLua
            ? PublishCocosLua(config, options, root, chapter, level)
            : PublishUnityJson(config, options, root, chapter, level, fog);

        return result;
    }

    #endregion

    #region Unity JSON 出口

    private static MapPublishResult PublishUnityJson(
        MapConfigData config,
        MapPublishOptions options,
        string root,
        string chapter,
        string level,
        MapFogDocument? fog)
    {
        var fileName = $"Map_{chapter}m{level}.json";
        var mapPath = Path.Combine(root, fileName);
        var warnings = new List<string>();
        if (!LooksLikeUnityMapDirectory(root))
        {
            warnings.Add("输出目录下没有 Map_*.json，可能不是 Unity 工程的 Assets/GameData/MapDatas。");
        }

        var json = MapJsonService.Serialize(config);
        WriteAtomic(mapPath, json);

        var files = new List<string> { mapPath };

        if (options.IncludeFog && fog is not null)
        {
            var fogPath = Path.Combine(root, $"Map_{chapter}m{level}.fog.json");
            var fogCopy = new MapFogDocument
            {
                SceneId = config.SceneId,
                Regions = fog.Regions,
            };
            WriteAtomic(fogPath, JsonSerializer.Serialize(fogCopy, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            files.Add(fogPath);
        }

        var manifestPath = Path.Combine(root, $"PublishManifest_{chapter}_{level}.json");
        WriteManifest(
            manifestPath,
            options.Target,
            config,
            chapter,
            level,
            options.MapName,
            files,
            root,
            warnings);
        files.Add(manifestPath);

        return new MapPublishResult
        {
            Target = options.Target,
            OutputDirectory = root,
            Files = files,
            ManifestPath = manifestPath,
            Warnings = warnings,
            Summary = $"Unity JSON 已发布：{fileName}（{Preview(config, MapPublishTarget.UnityJson).Summary}）",
        };
    }

    private static bool LooksLikeUnityMapDirectory(string root)
    {
        return root.Replace('\\', '/').Contains("/GameData/MapDatas", StringComparison.OrdinalIgnoreCase)
            || Directory.EnumerateFiles(root, "Map_*.json").Any();
    }

    #endregion

    #region Cocos Lua 出口

    private static MapPublishResult PublishCocosLua(
        MapConfigData config,
        MapPublishOptions options,
        string root,
        string chapter,
        string level)
    {
        var chapterDirectory = Path.Combine(root, chapter);
        Directory.CreateDirectory(chapterDirectory);

        var baseName = $"TableLevelData_{chapter}_{level}";
        var files = new List<string>();
        var warnings = new List<string>();
        if (!Directory.Exists(root)
            || (!File.Exists(Path.Combine(root, "TableLevelData.lua.txt")) && !root.EndsWith("Levels", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("输出目录下没有 TableLevelData.lua.txt，可能不是 Cocos 的 assets/LuaScripts/Game/Tables/Levels。");
        }

        var mainPath = Path.Combine(chapterDirectory, baseName + ".lua.txt");
        WriteAtomic(mainPath, BuildCocosMainFile(baseName, chapter, level, config, options.MapName));
        files.Add(mainPath);

        var characters = config.Objects.Where(item => item.Type == MapObjectType.Character).ToList();
        var buildings = config.Objects.Where(item => item.Type == MapObjectType.Building).ToList();
        var objects = config.Objects.Where(item => item.Type == MapObjectType.Prop).ToList();

        var charactersPath = Path.Combine(chapterDirectory, baseName + "_Characters.lua.txt");
        WriteAtomic(charactersPath, BuildCocosCharactersFile(baseName, characters));
        files.Add(charactersPath);

        var buildingsPath = Path.Combine(chapterDirectory, baseName + "_Buildings.lua.txt");
        WriteAtomic(buildingsPath, BuildCocosBuildingsFile(baseName, buildings));
        files.Add(buildingsPath);

        var objectsPath = Path.Combine(chapterDirectory, baseName + "_Objects.lua.txt");
        WriteAtomic(objectsPath, BuildCocosObjectsFile(baseName, objects));
        files.Add(objectsPath);

        var pathsPath = Path.Combine(chapterDirectory, baseName + "_Paths.lua.txt");
        WriteAtomic(pathsPath, BuildCocosPathsFile(baseName, characters, config.GridSize));
        files.Add(pathsPath);

        var trafficPath = Path.Combine(chapterDirectory, baseName + "_Traffic.lua.txt");
        WriteAtomic(trafficPath, BuildCocosTrafficFile(baseName, config));
        files.Add(trafficPath);

        if (options.UpdateCocosIndex)
        {
            var indexPath = Path.Combine(root, "TableLevelData.lua.txt");
            WriteAtomic(indexPath, BuildCocosIndex(indexPath, chapter, config.ChapterName, level));
            files.Add(indexPath);
        }

        var spineCount = config.Objects.Count(item =>
            string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase));
        if (spineCount > 0)
        {
            warnings.Add($"有 {spineCount} 个 Spine 实例：Lua 表已写出 animationKind/spineAsset/spineAnimation，Cocos 地图生成链仍需补 Spine 分支。");
        }

        var manifestPath = Path.Combine(root, $"PublishManifest_{chapter}_{level}.json");
        WriteManifest(
            manifestPath,
            options.Target,
            config,
            chapter,
            level,
            options.MapName,
            files,
            root,
            warnings);
        files.Add(manifestPath);

        return new MapPublishResult
        {
            Target = options.Target,
            OutputDirectory = root,
            Files = files,
            ManifestPath = manifestPath,
            Warnings = warnings,
            Summary = $"Cocos Lua 已发布：{baseName}（{Preview(config, MapPublishTarget.CocosLua).Summary}）",
        };
    }

    #endregion

    #region Cocos 主表

    private static string BuildCocosMainFile(
        string baseName,
        string chapter,
        string level,
        MapConfigData config,
        string mapName)
    {
        var sb = new StringBuilder();
        AppendLuaHeader(sb, baseName, $"章节{chapter}关卡{level}地图数据（自动生成·拆分优化版）", "关卡地图数据表（主入口）",
            "拆分优化：主文件只存 Meta/Defaults/Counts/SubTables，实体按需加载");

        var tableName = StripTablePrefix(baseName);
        sb.AppendLine("---@type Tables.LevelData_Item");
        sb.AppendLine($"Tables.{tableName} = {{");
        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    -- 1. 关卡元数据");
        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    Meta = {");
        sb.AppendLine($"        ChapterId     = \"{Escape(chapter)}\",");
        sb.AppendLine($"        ChapterName   = \"{Escape(config.ChapterName)}\",");
        sb.AppendLine($"        LevelId       = \"{Escape(level)}\",");
        sb.AppendLine($"        LevelIndex    = {ParseInt(level)},");
        sb.AppendLine($"        MapName       = \"{Escape(mapName)}\",");
        sb.AppendLine($"        BgName        = \"{Escape(config.BgName)}\",");
        sb.AppendLine($"        SavePath      = \"Assets/GameData/MapDatas/Map_{Escape(chapter)}m{Escape(level)}.json\",");
        sb.AppendLine($"        Time          = \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
        sb.AppendLine($"        Width         = {config.Width},");
        sb.AppendLine($"        Height        = {config.Height},");
        sb.AppendLine($"        GridSize      = {config.GridSize},");
        sb.AppendLine($"        GridCols      = {config.GridCols},");
        sb.AppendLine($"        GridRows      = {config.GridRows},");
        sb.AppendLine($"        BaselineStep  = {config.BaselineStep},");
        sb.AppendLine($"        DrawShape     = \"{Escape(config.DrawShape)}\",");
        sb.AppendLine($"        RegionType    = \"{Escape(config.RegionType)}\",");
        sb.AppendLine("    },");
        sb.AppendLine();

        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    -- 2. 全局默认配置（所有实体共享，实体自身字段优先）");
        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    Defaults = {");
        sb.AppendLine("        SortOrder        = 40,");
        sb.AppendLine("        ShadowEnabled    = true,");
        sb.AppendLine("        YSortEnabled     = true,");
        sb.AppendLine("        YSortOffset      = 0,");
        sb.AppendLine("        Loop             = true,");
        sb.AppendLine("        Speed            = 1,");
        sb.AppendLine("        FacingDirection  = \"down\",");
        sb.AppendLine("        HideWhenOffMap   = true,");
        sb.AppendLine("        LoopRestartDelay = 3,");
        sb.AppendLine("    },");
        sb.AppendLine();

        var characterCount = config.Objects.Count(item => item.Type == MapObjectType.Character);
        var buildingCount = config.Objects.Count(item => item.Type == MapObjectType.Building);
        var objectCount = config.Objects.Count(item => item.Type == MapObjectType.Prop);

        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    -- 3. 实体数量统计");
        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    Counts = {");
        sb.AppendLine($"        Characters = {characterCount},");
        sb.AppendLine($"        Buildings  = {buildingCount},");
        sb.AppendLine($"        Objects    = {objectCount},");
        sb.AppendLine($"        Lights     = {config.TrafficLights.Count},");
        sb.AppendLine($"        Crosswalks = {config.Crosswalks.Count},");
        sb.AppendLine("    },");
        sb.AppendLine();

        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    -- 4. 子表索引（运行时按需 require 加载）");
        sb.AppendLine("    -- ==========================================================================");
        sb.AppendLine("    SubTables = {");
        sb.AppendLine($"        Characters = \"{baseName}_Characters\",");
        sb.AppendLine($"        Buildings  = \"{baseName}_Buildings\",");
        sb.AppendLine($"        Objects    = \"{baseName}_Objects\",");
        sb.AppendLine($"        Paths      = \"{baseName}_Paths\",");
        sb.AppendLine($"        Traffic    = \"{baseName}_Traffic\",");
        sb.AppendLine("    },");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Cocos 子表

    private static string BuildCocosCharactersFile(string baseName, IReadOnlyList<PlacedObject> characters)
    {
        var sb = new StringBuilder();
        var tableName = StripTablePrefix(baseName) + "_Characters";
        AppendLuaHeader(sb, tableName, "角色列表（自动生成）", "关卡角色实体列表（只存差异字段）");
        sb.AppendLine($"Tables.{tableName} = {{");

        if (characters.Count == 0)
        {
            sb.AppendLine("    -- 空列表");
        }
        else
        {
            for (var index = 0; index < characters.Count; index++)
            {
                sb.AppendLine($"    [{index + 1}] = {{");
                AppendCocosObjectFields(
                    sb,
                    characters[index],
                    includeCollectId: false,
                    includeCollectibleFields: false,
                    includeCharacterFields: true);
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildCocosBuildingsFile(string baseName, IReadOnlyList<PlacedObject> buildings)
    {
        var sb = new StringBuilder();
        var tableName = StripTablePrefix(baseName) + "_Buildings";
        AppendLuaHeader(sb, tableName, "建筑列表（自动生成）", "关卡建筑实体列表");
        sb.AppendLine($"Tables.{tableName} = {{");

        if (buildings.Count == 0)
        {
            sb.AppendLine("    -- 空列表");
        }
        else
        {
            for (var index = 0; index < buildings.Count; index++)
            {
                var item = buildings[index];
                sb.AppendLine($"    [{index + 1}] = {{");
                AppendCocosObjectFields(sb, item, includeCollectId: false);
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildCocosObjectsFile(string baseName, IReadOnlyList<PlacedObject> objects)
    {
        var sb = new StringBuilder();
        var tableName = StripTablePrefix(baseName) + "_Objects";
        AppendLuaHeader(sb, tableName, "待收集物件列表（自动生成）", "关卡待收集物件列表",
            "字段与 Buildings 对齐：GridX/GridY 兼容旧换算，PosX/PosY/PixelWidth/PixelHeight/Stretch/LayerType 按 PSD 还原");
        sb.AppendLine($"Tables.{tableName} = {{");

        if (objects.Count == 0)
        {
            sb.AppendLine("    -- 空列表");
        }
        else
        {
            for (var index = 0; index < objects.Count; index++)
            {
                var item = objects[index];
                sb.AppendLine($"    [{index + 1}] = {{");
                AppendCocosObjectFields(sb, item, includeCollectId: true);
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendCocosObjectFields(
        StringBuilder sb,
        PlacedObject item,
        bool includeCollectId,
        bool includeCollectibleFields = true,
        bool includeCharacterFields = false)
    {
        sb.AppendLine($"        Uid        = \"{Escape(item.Id)}\",");
        sb.AppendLine($"        TemplateId = \"{Escape(item.TemplateId)}\",");
        sb.AppendLine($"        Name       = \"{Escape(item.Name)}\",");
        sb.AppendLine($"        GridX      = {item.CellX},");
        sb.AppendLine($"        GridY      = {item.CellY},");
        sb.AppendLine($"        PosX       = {Num(item.X)},");
        sb.AppendLine($"        PosY       = {Num(item.Y)},");
        sb.AppendLine($"        Width      = {item.ExportWidth},");
        sb.AppendLine($"        Height     = {item.ExportHeight},");
        sb.AppendLine($"        PixelWidth  = {Num(item.PixelWidth)},");
        sb.AppendLine($"        PixelHeight = {Num(item.PixelHeight)},");
        sb.AppendLine($"        Stretch     = {Bool(item.Stretch)},");
        sb.AppendLine($"        LayerType   = \"{Escape(item.LayerType)}\",");
        sb.AppendLine($"        DisplayLayer = \"{Escape(item.DisplayLayer)}\",");
        if (includeCollectibleFields)
        {
            sb.AppendLine($"        IsCollectible = {Bool(item.IsCollectible)},");
            sb.AppendLine($"        CollectibleOverObjects = {Bool(item.CollectibleOverObjects)},");
        }

        sb.AppendLine($"        Image      = \"{Escape(item.ImagePath)}\",");
        sb.AppendLine($"        Anim        = \"{Escape(item.AnimName)}\",");
        sb.AppendLine($"        AnimEnabled = {Bool(item.AnimEnabled)},");
        sb.AppendLine($"        AnimationKind = \"{Escape(item.AnimationKind)}\",");
        sb.AppendLine($"        SpineAsset  = \"{Escape(item.SpineAsset)}\",");
        sb.AppendLine($"        SpineAnimation = \"{Escape(item.SpineAnimation)}\",");
        sb.AppendLine($"        Loop        = {Bool(item.Loop)},");
        sb.AppendLine($"        Speed       = {Num(item.Speed)},");
        if (includeCollectId)
        {
            sb.AppendLine($"        CollectId  = \"collect_{Escape(item.Id)}\",");
        }

        sb.AppendLine($"        SortOrder    = {item.SortOrder},");
        sb.AppendLine($"        YSortEnabled = {Bool(item.YSortEnabled)},");
        sb.AppendLine($"        YSortOffset  = {Num(item.YSortOffset)},");
        sb.AppendLine($"        Rotation      = {Num(item.Rotation)},");
        sb.AppendLine($"        Scale         = {Num(item.Scale)},");
        sb.AppendLine($"        ScaleX        = {Num(item.Scale)},");
        sb.AppendLine($"        ScaleY        = {Num(item.Scale)},");
        sb.AppendLine($"        FlipX         = {Bool(item.FlipX)},");
        if (item.BandOverride >= 0)
        {
            sb.AppendLine($"        Band          = {item.BandOverride},");
        }

        if (includeCharacterFields)
        {
            AppendCocosCharacterFields(sb, item);
        }
    }

    /// <summary>角色表独有的行为字段；公共位置/外观字段由 <see cref="AppendCocosObjectFields"/> 统一输出。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="item">角色物件。</param>
    private static void AppendCocosCharacterFields(StringBuilder sb, PlacedObject item)
    {
        sb.AppendLine($"        PatrolLoop  = \"{Escape(item.WalkMode)}\",");
        sb.AppendLine($"        FacingDirection = \"{Escape(item.FacingDirection)}\",");
        sb.AppendLine($"        Interactable = {Bool(item.Interactable)},");
        sb.AppendLine($"        InteractType = \"{Escape(item.InteractType)}\",");
        sb.AppendLine($"        DialogBubble = \"{Escape(item.DialogBubble)}\",");
        sb.AppendLine($"        PathId      = \"path_{Escape(item.Id)}\",");
        sb.AppendLine($"        IsVehicle     = {Bool(item.IsVehicle)},");
        sb.AppendLine($"        TrafficRole  = \"{Escape(item.TrafficRole)}\",");
        sb.AppendLine($"        StopLineX    = {Num(item.StopLineX)},");
        sb.AppendLine($"        StopLineY    = {Num(item.StopLineY)},");
        sb.AppendLine($"        BoundLightId = \"{Escape(item.BoundLightIds.FirstOrDefault() ?? string.Empty)}\",");
        sb.AppendLine($"        BoundLightIds = {LuaStringArray(item.BoundLightIds)},");
        sb.AppendLine($"        YellowTreatAsRed = {Bool(item.YellowTreatAsRed)},");
    }

    private static string BuildCocosPathsFile(string baseName, IReadOnlyList<PlacedObject> characters, int gridSize)
    {
        var sb = new StringBuilder();
        var tableName = StripTablePrefix(baseName) + "_Paths";
        AppendLuaHeader(sb, tableName, "路径数据（自动生成）", "所有带路径物件的路径点，按 PathId 索引",
            "路径点格式：{ gridX, gridY } 纯数组");
        sb.AppendLine($"Tables.{tableName} = {{");

        foreach (var item in characters)
        {
            if (item.PathPoints.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"    [\"path_{Escape(item.Id)}\"] = {{");
            foreach (var point in item.PathPoints)
            {
                // 与 Unity Lua 导出器一致：按编辑器格尺寸向下取整，而不是直接使用 PathPoint 自带的 GridX。
                var gridX = (int)Math.Floor(point.X / Math.Max(1, gridSize));
                var gridY = (int)Math.Floor(point.Y / Math.Max(1, gridSize));
                sb.AppendLine($"        {{{gridX}, {gridY}}},");
            }

            sb.AppendLine("    },");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildCocosTrafficFile(string baseName, MapConfigData config)
    {
        var sb = new StringBuilder();
        var tableName = StripTablePrefix(baseName) + "_Traffic";
        AppendLuaHeader(sb, tableName, "交通规则数据（自动生成）", "红绿灯 / 斑马线 / 区域 / 人行道格 / 道路格");
        sb.AppendLine($"Tables.{tableName} = {{");

        sb.AppendLine("    Lights = {");
        for (var index = 0; index < config.TrafficLights.Count; index++)
        {
            var light = config.TrafficLights[index];
            sb.AppendLine($"        [{index + 1}] = {{");
            sb.AppendLine($"            Uid                = \"{Escape(light.Id)}\",");
            sb.AppendLine($"            TemplateId         = \"{Escape(light.TemplateId)}\",");
            sb.AppendLine($"            Name               = \"{Escape(light.Name)}\",");
            sb.AppendLine($"            GridX              = {light.GridX},");
            sb.AppendLine($"            GridY              = {light.GridY},");
            sb.AppendLine($"            PosX               = {Num(light.PosX)},");
            sb.AppendLine($"            PosY               = {Num(light.PosY)},");
            sb.AppendLine($"            Direction          = \"{Escape(light.Direction)}\",");
            sb.AppendLine($"            Side               = \"{Escape(light.Side)}\",");
            sb.AppendLine($"            IntersectionId     = \"{Escape(light.IntersectionId)}\",");
            sb.AppendLine($"            RedDuration        = {Num(light.RedDuration)},");
            sb.AppendLine($"            GreenDuration      = {Num(light.GreenDuration)},");
            sb.AppendLine($"            YellowDuration     = {Num(light.YellowDuration)},");
            sb.AppendLine($"            OffsetPhase        = {Num(light.OffsetPhase)},");
            sb.AppendLine($"            BoundCrosswalkId   = \"{Escape(light.BoundCrosswalkId)}\",");
            sb.AppendLine($"            InitialPhase       = \"{Escape(light.InitialPhase)}\",");
            sb.AppendLine($"            Image              = \"{Escape(light.Image)}\",");
            sb.AppendLine($"            Anim               = \"{Escape(light.Anim)}\",");
            sb.AppendLine("        },");
        }

        sb.AppendLine("    },");

        sb.AppendLine("    Crosswalks = {");
        for (var index = 0; index < config.Crosswalks.Count; index++)
        {
            var crosswalk = config.Crosswalks[index];
            sb.AppendLine($"        [{index + 1}] = {{");
            sb.AppendLine($"            Uid           = \"{Escape(crosswalk.Id)}\",");
            sb.AppendLine($"            Name          = \"{Escape(crosswalk.Name)}\",");
            sb.AppendLine($"            PointA        = {{ X = {Num(crosswalk.PointA.X)}, Y = {Num(crosswalk.PointA.Y)} }},");
            sb.AppendLine($"            PointB        = {{ X = {Num(crosswalk.PointB.X)}, Y = {Num(crosswalk.PointB.Y)} }},");
            sb.AppendLine($"            Width         = {Num(crosswalk.Width)},");
            sb.AppendLine($"            Direction     = \"{Escape(crosswalk.Direction)}\",");
            sb.AppendLine($"            BoundLightId  = \"{Escape(crosswalk.BoundLightId)}\",");
            sb.AppendLine("        },");
        }

        sb.AppendLine("    },");

        sb.AppendLine("    Regions = {");
        for (var index = 0; index < config.Regions.Count; index++)
        {
            var region = config.Regions[index];
            sb.AppendLine($"        [{index + 1}] = {{");
            sb.AppendLine($"            Uid     = \"{Escape(region.Id)}\",");
            sb.AppendLine($"            Type    = \"{Escape(region.Type)}\",");
            sb.AppendLine($"            X       = {Num(region.X)},");
            sb.AppendLine($"            Y       = {Num(region.Y)},");
            sb.AppendLine($"            Width   = {Num(region.Width)},");
            sb.AppendLine($"            Height  = {Num(region.Height)},");
            sb.AppendLine("        },");
        }

        sb.AppendLine("    },");

        sb.AppendLine("    SidewalkCells = {");
        foreach (var cell in config.SidewalkCells)
        {
            sb.AppendLine($"        {{{cell.X}, {cell.Y}}},");
        }

        sb.AppendLine("    },");

        sb.AppendLine("    RoadCells = {");
        foreach (var cell in config.RoadCells)
        {
            sb.AppendLine($"        {{{cell.X}, {cell.Y}}},");
        }

        sb.AppendLine("    },");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Cocos 章节索引

    private static string BuildCocosIndex(string path, string chapter, string chapterName, string level)
    {
        if (!File.Exists(path))
        {
            var sb = new StringBuilder();
            AppendLuaHeader(sb, "TableLevelData", "章节地图索引（自动生成）", "章节地图数据索引");
            sb.AppendLine("---@class Tables.LevelData @章节地图数据索引");
            sb.AppendLine("Tables.LevelData = {");
            sb.AppendLine($"    {BuildIndexLine(chapter, chapterName, new List<string> { level })}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        var original = File.ReadAllText(path);
        var newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var lines = original.Replace("\r\n", "\n").Split('\n');
        var targetPrefix = $"[\"{chapter}\"]";
        var targetIndex = -1;
        string? existingName = null;
        var levels = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains(targetPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            targetIndex = index;
            var nameMatch = Regex.Match(lines[index], "ChapterName\\s*=\\s*\"((?:\\\\.|[^\"])*)\"");
            if (nameMatch.Success)
            {
                existingName = nameMatch.Groups[1].Value;
            }

            var levelsMatch = Regex.Match(lines[index], "Levels\\s*=\\s*\\{([^}]*)\\}");
            if (levelsMatch.Success)
            {
                foreach (Match levelMatch in Regex.Matches(levelsMatch.Groups[1].Value, "\"((?:\\\\.|[^\"])*)\""))
                {
                    levels.Add(levelMatch.Groups[1].Value);
                }
            }

            break;
        }

        levels.Add(level);
        var merged = levels
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => int.TryParse(value, out var number) ? number : int.MaxValue)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToList();
        var line = BuildIndexLine(chapter, existingName ?? chapterName, merged);

        if (targetIndex >= 0)
        {
            lines[targetIndex] = "    " + line;
        }
        else
        {
            var closeIndex = Array.LastIndexOf(lines, "}");
            if (closeIndex < 0)
            {
                lines = lines.Append("Tables.LevelData = {").Append("}").ToArray();
                closeIndex = lines.Length - 1;
            }

            var mergedLines = lines.Take(closeIndex).Concat(new[] { "    " + line }).Concat(lines.Skip(closeIndex)).ToArray();
            lines = mergedLines;
        }

        return string.Join(newline, lines);
    }

    private static string BuildIndexLine(string chapter, string chapterName, IReadOnlyList<string> levels)
    {
        var quotedLevels = string.Join(", ", levels.Select(value => "\"" + Escape(value) + "\""));
        return $"[\"{Escape(chapter)}\"] = {{ ChapterId = \"{Escape(chapter)}\", " +
               $"ChapterName = \"{Escape(chapterName)}\", LevelCount = {levels.Count}, " +
               $"Levels = {{ {quotedLevels} }} }},";
    }

    #endregion

    #region 清单

    private static void WriteManifest(
        string path,
        MapPublishTarget target,
        MapConfigData config,
        string chapter,
        string level,
        string mapName,
        IReadOnlyList<string> files,
        string root,
        IReadOnlyList<string> warnings)
    {
        var preview = Preview(config, target);
        var relativeFiles = files
            .Where(file => !string.Equals(file, path, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .ToList();

        var manifest = new
        {
            format = "honor.map.publish.v1",
            target = target == MapPublishTarget.UnityJson ? "unity-json" : "cocos-lua",
            chapterId = chapter,
            levelId = level,
            mapName,
            mapWidth = config.Width,
            mapHeight = config.Height,
            gridSize = config.GridSize,
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sourceHash = SourceHash(config),
            counts = new
            {
                buildings = preview.Buildings,
                objects = preview.Objects,
                characters = preview.Characters,
                animated = preview.Animated,
                spine = preview.Spine,
                paths = preview.Paths,
                lights = preview.Lights,
                crosswalks = preview.Crosswalks,
            },
            files = relativeFiles,
            warnings,
        };

        WriteAtomic(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
    }

    private static string SourceHash(MapConfigData config)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(MapJsonService.Serialize(config)));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    #endregion

    #region 格式辅助

    private static void AppendLuaHeader(StringBuilder sb, string tableName, string description, string brief, string? detail = null)
    {
        sb.AppendLine(LuaBanner);
        sb.AppendLine("-- (c) copyright 2026 - 2030, Honor.Runtime");
        sb.AppendLine("-- All Rights Reserved.");
        sb.AppendLine("-- ----------------------------------------------------------------------------------------------------");
        sb.AppendLine($"-- filename:  {tableName}");
        sb.AppendLine($"-- descrip:   {description}");
        sb.AppendLine($"-- notices:   {LuaNotice}");
        sb.AppendLine($"-- @brief     {brief}");
        if (!string.IsNullOrEmpty(detail))
        {
            sb.AppendLine($"-- @desc      {detail}");
        }

        sb.AppendLine(LuaBanner);
        sb.AppendLine();
    }

    private static string StripTablePrefix(string baseName)
        => baseName.StartsWith("Table", StringComparison.Ordinal) ? baseName[5..] : baseName;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            switch (character)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;

                case '"':
                    sb.Append("\\\"");
                    break;

                default:
                    sb.Append(character);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Num(double value) => value.ToString(LuaNumberFormat, CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string LuaStringArray(IEnumerable<string> values)
    {
        var parts = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => "\"" + Escape(value) + "\"")
            .ToList();
        return parts.Count == 0 ? "{}" : "{ " + string.Join(", ", parts) + " }";
    }

    private static string NumericOr(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? fallback : digits;
    }

    private static int ParseInt(string value) => int.TryParse(value, out var number) ? number : 1;

    private static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    #endregion
}

#endregion
