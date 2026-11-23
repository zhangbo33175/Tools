/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Cocos.cs
 * author:    云毅
 * created:   2026
 * descrip:   Cocos Levels Lua 出口：主表、角色/建筑/物件/路径/交通子表与章节索引
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
/// <summary>Cocos Lua 主表与发布入口：主表分段输出、共享辅助与文件落盘。</summary>
public static partial class MapPublishService
{
    #region Cocos 常量

    /// <summary>关卡表基名前缀：TableLevelData_{chapter}_{level}。</summary>
    private const string LevelTablePrefix = "TableLevelData_";

    /// <summary>Cocos Lua 文本文件扩展名。</summary>
    private const string LuaFileExtension = ".lua.txt";

    /// <summary>Cocos 章节索引文件基名。</summary>
    private const string LevelIndexFileStem = "TableLevelData";

    /// <summary>Cocos 章节索引文件名。</summary>
    private const string LevelIndexFileName = LevelIndexFileStem + LuaFileExtension;

    /// <summary>Cocos 章节索引 Lua 表名（文件名去 Table 前缀）。</summary>
    private const string LevelIndexTableName = "LevelData";

    /// <summary>Levels 目录名，用于判断输出目录是否已接入 Cocos 表目录。</summary>
    private const string LevelsDirectoryName = "Levels";

    private const string CharactersSuffix = "_Characters";
    private const string BuildingsSuffix = "_Buildings";
    private const string ObjectsSuffix = "_Objects";
    private const string PathsSuffix = "_Paths";
    private const string TrafficSuffix = "_Traffic";

    /// <summary>主表段落分隔注释，保持 Lua 文件分节视觉一致。</summary>
    private const string LuaSectionBanner = "    -- ==========================================================================";

    /// <summary>主表 Defaults 中的默认排序值。</summary>
    private const int DefaultCocosSortOrder = 40;

    /// <summary>主表 Defaults 中的默认 Y 排序偏移。</summary>
    private const double DefaultCocosYSortOffset = 0;

    /// <summary>主表 Defaults 中的默认播放速度。</summary>
    private const double DefaultCocosSpeed = 1;

    /// <summary>主表 Defaults 中的默认朝向。</summary>
    private const string DefaultCocosFacingDirection = "down";

    /// <summary>输出主表分节注释：分隔线 + 标题 + 分隔线。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="title">分节标题。</param>
    private static void AppendLuaSectionBanner(StringBuilder sb, string title)
    {
        sb.AppendLine(LuaSectionBanner);
        sb.AppendLine($"    -- {title}");
        sb.AppendLine(LuaSectionBanner);
    }

    /// <summary>关卡表基名。</summary>
    private static string LevelBaseName(string chapter, string level) =>
        $"{LevelTablePrefix}{chapter}_{level}";

    /// <summary>子表文件名（不含目录，不含扩展名）。</summary>
    private static string SubTableStem(string baseName, string suffix) => baseName + suffix;

    /// <summary>子表文件名（含 .lua.txt）。</summary>
    private static string SubTableFileName(string baseName, string suffix) =>
        SubTableStem(baseName, suffix) + LuaFileExtension;

    /// <summary>拼接章节目录下的子表路径。</summary>
    private static string SubTablePath(string directory, string baseName, string suffix) =>
        Path.Combine(directory, SubTableFileName(baseName, suffix));

    /// <summary>Lua 表名：去掉 TableLevelData_ 前缀后加子表后缀。</summary>
    private static string SubTableName(string baseName, string suffix) =>
        StripTablePrefix(baseName) + suffix;

    #endregion

    #region Cocos Lua 出口

    /// <summary>
    /// Cocos Lua 发布入口：按「章节目录 + 主表 + 5 张子表 + 可选章节索引」落盘。
    /// </summary>
    /// <param name="config">当前地图配置。</param>
    /// <param name="options">发布选项（MapName、是否更新索引等）。</param>
    /// <param name="root">Cocos Levels 输出根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <returns>发布结果与文件清单。</returns>
    private static MapPublishResult PublishCocosLua(
        MapConfigData config,
        MapPublishOptions options,
        string root,
        string chapter,
        string level)
    {
        var chapterDirectory = Path.Combine(root, chapter);
        Directory.CreateDirectory(chapterDirectory);

        var baseName = LevelBaseName(chapter, level);
        var files = new List<string>();
        var warnings = new List<string>();
        if (!Directory.Exists(root)
            || (!File.Exists(Path.Combine(root, LevelIndexFileName))
                && !root.EndsWith(LevelsDirectoryName, StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add($"输出目录下没有 {LevelIndexFileName}，可能不是 Cocos 的 assets/LuaScripts/Game/Tables/Levels。");
        }

        var mainPath = SubTablePath(chapterDirectory, baseName, string.Empty);
        WriteAtomic(mainPath, BuildCocosMainFile(baseName, chapter, level, config, options.MapName));
        files.Add(mainPath);

        var characters = config.Objects.Where(item => item.Type == MapObjectType.Character).ToList();
        var buildings = config.Objects.Where(item => item.Type == MapObjectType.Building).ToList();
        var objects = config.Objects.Where(item => item.Type == MapObjectType.Prop).ToList();

        var charactersPath = SubTablePath(chapterDirectory, baseName, CharactersSuffix);
        WriteAtomic(charactersPath, BuildCocosCharactersFile(baseName, characters));
        files.Add(charactersPath);

        var buildingsPath = SubTablePath(chapterDirectory, baseName, BuildingsSuffix);
        WriteAtomic(buildingsPath, BuildCocosBuildingsFile(baseName, buildings));
        files.Add(buildingsPath);

        var objectsPath = SubTablePath(chapterDirectory, baseName, ObjectsSuffix);
        WriteAtomic(objectsPath, BuildCocosObjectsFile(baseName, objects));
        files.Add(objectsPath);

        var pathsPath = SubTablePath(chapterDirectory, baseName, PathsSuffix);
        WriteAtomic(pathsPath, BuildCocosPathsFile(baseName, characters, config.GridSize));
        files.Add(pathsPath);

        var trafficPath = SubTablePath(chapterDirectory, baseName, TrafficSuffix);
        WriteAtomic(trafficPath, BuildCocosTrafficFile(baseName, config));
        files.Add(trafficPath);

        if (options.UpdateCocosIndex)
        {
            var indexPath = Path.Combine(root, LevelIndexFileName);
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

    /// <summary>
    /// 构建关卡主表：只写 Meta / Defaults / Counts / SubTables，实体数据放到子表按需加载。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="config">当前地图配置。</param>
    /// <param name="mapName">Cocos 地图预制体名。</param>
    /// <returns>主表 Lua 文本。</returns>
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
        AppendCocosMetaSection(sb, chapter, level, config, mapName);
        sb.AppendLine();
        AppendCocosDefaultsSection(sb);
        sb.AppendLine();
        AppendCocosCountsSection(sb, config);
        sb.AppendLine();
        AppendCocosSubTablesSection(sb, baseName);
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>输出主表 Meta 段：章节、关卡、地图尺寸与网格口径。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="config">当前地图配置。</param>
    /// <param name="mapName">Cocos 地图预制体名。</param>
    private static void AppendCocosMetaSection(
        StringBuilder sb,
        string chapter,
        string level,
        MapConfigData config,
        string mapName)
    {
        AppendLuaSectionBanner(sb, "1. 关卡元数据");
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
    }

    /// <summary>输出主表 Defaults 段：所有实体共享的默认排序、阴影、朝向与循环参数。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    private static void AppendCocosDefaultsSection(StringBuilder sb)
    {
        AppendLuaSectionBanner(sb, "2. 全局默认配置（所有实体共享，实体自身字段优先）");
        sb.AppendLine("    Defaults = {");
        sb.AppendLine($"        SortOrder        = {DefaultCocosSortOrder},");
        sb.AppendLine("        ShadowEnabled    = true,");
        sb.AppendLine("        YSortEnabled     = true,");
        sb.AppendLine($"        YSortOffset      = {Num(DefaultCocosYSortOffset)},");
        sb.AppendLine("        Loop             = true,");
        sb.AppendLine($"        Speed            = {Num(DefaultCocosSpeed)},");
        sb.AppendLine($"        FacingDirection  = \"{DefaultCocosFacingDirection}\",");
        sb.AppendLine($"        HideWhenOffMap   = {Bool(MapToolDefaults.DefaultHideWhenOffMap)},");
        sb.AppendLine($"        LoopRestartDelay = {Num(MapToolDefaults.DefaultLoopRestartDelay)},");
        sb.AppendLine("    },");
    }

    /// <summary>输出主表 Counts 段：各实体类型数量，供运行时快速判断子表是否为空。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="config">当前地图配置。</param>
    private static void AppendCocosCountsSection(StringBuilder sb, MapConfigData config)
    {
        var characterCount = config.Objects.Count(item => item.Type == MapObjectType.Character);
        var buildingCount = config.Objects.Count(item => item.Type == MapObjectType.Building);
        var objectCount = config.Objects.Count(item => item.Type == MapObjectType.Prop);

        AppendLuaSectionBanner(sb, "3. 实体数量统计");
        sb.AppendLine("    Counts = {");
        sb.AppendLine($"        Characters = {characterCount},");
        sb.AppendLine($"        Buildings  = {buildingCount},");
        sb.AppendLine($"        Objects    = {objectCount},");
        sb.AppendLine($"        Lights     = {config.TrafficLights.Count},");
        sb.AppendLine($"        Crosswalks = {config.Crosswalks.Count},");
        sb.AppendLine("    },");
    }

    /// <summary>输出主表 SubTables 段：运行时按需 require 的子表文件名。</summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="baseName">关卡表基名。</param>
    private static void AppendCocosSubTablesSection(StringBuilder sb, string baseName)
    {
        AppendLuaSectionBanner(sb, "4. 子表索引（运行时按需 require 加载）");
        sb.AppendLine("    SubTables = {");
        sb.AppendLine($"        Characters = \"{SubTableStem(baseName, CharactersSuffix)}\",");
        sb.AppendLine($"        Buildings  = \"{SubTableStem(baseName, BuildingsSuffix)}\",");
        sb.AppendLine($"        Objects    = \"{SubTableStem(baseName, ObjectsSuffix)}\",");
        sb.AppendLine($"        Paths      = \"{SubTableStem(baseName, PathsSuffix)}\",");
        sb.AppendLine($"        Traffic    = \"{SubTableStem(baseName, TrafficSuffix)}\",");
        sb.AppendLine("    },");
    }

    #endregion
}

