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
        sb.AppendLine($"        Characters = \"{SubTableStem(baseName, CharactersSuffix)}\",");
        sb.AppendLine($"        Buildings  = \"{SubTableStem(baseName, BuildingsSuffix)}\",");
        sb.AppendLine($"        Objects    = \"{SubTableStem(baseName, ObjectsSuffix)}\",");
        sb.AppendLine($"        Paths      = \"{SubTableStem(baseName, PathsSuffix)}\",");
        sb.AppendLine($"        Traffic    = \"{SubTableStem(baseName, TrafficSuffix)}\",");
        sb.AppendLine("    },");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Cocos 子表

    /// <summary>
    /// 构建角色子表：公共字段复用 <see cref="AppendCocosObjectFields"/>，
    /// 再补角色独有的巡逻、交互、路径与交通绑定字段。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="characters">角色实例列表。</param>
    /// <returns>角色表 Lua 文本。</returns>
    private static string BuildCocosCharactersFile(string baseName, IReadOnlyList<PlacedObject> characters)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, CharactersSuffix);
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

    /// <summary>构建建筑子表；字段口径与物件表共用，但不输出收集物字段。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="buildings">建筑实例列表。</param>
    /// <returns>建筑表 Lua 文本。</returns>
    private static string BuildCocosBuildingsFile(string baseName, IReadOnlyList<PlacedObject> buildings)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, BuildingsSuffix);
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

    /// <summary>构建待收集物件子表；比建筑表多输出收集物标记与 collectId。</summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="objects">物件实例列表。</param>
    /// <returns>物件表 Lua 文本。</returns>
    private static string BuildCocosObjectsFile(string baseName, IReadOnlyList<PlacedObject> objects)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, ObjectsSuffix);
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

    /// <summary>
    /// 输出建筑 / 物件共用的公共字段：位置、尺寸、图层、动画、Spine、排序与翻转。
    /// </summary>
    /// <param name="sb">Lua 文本缓冲。</param>
    /// <param name="item">实例数据。</param>
    /// <param name="includeCollectId">是否输出 collectId（物件表为 true）。</param>
    /// <param name="includeCollectibleFields">是否输出收集物标记字段（建筑表 / 角色表为 false）。</param>
    /// <param name="includeCharacterFields">是否追加角色独有字段。</param>
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

    /// <summary>
    /// 构建路径子表：每个带路径实例输出一条 <c>PathId → 路径点数组</c>，
    /// 路径点使用格坐标，运行时无需再按像素换算。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="characters">当前参与路径导出的实例（角色 / 载具等）。</param>
    /// <param name="gridSize">格尺寸（像素）。</param>
    /// <returns>路径表 Lua 文本。</returns>
    private static string BuildCocosPathsFile(string baseName, IReadOnlyList<PlacedObject> characters, int gridSize)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, PathsSuffix);
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

    /// <summary>
    /// 构建交通子表：红绿灯、横道、人行道 / 道路格子与车辆绑定关系统一从地图配置导出。
    /// </summary>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="config">当前地图配置。</param>
    /// <returns>交通表 Lua 文本。</returns>
    private static string BuildCocosTrafficFile(string baseName, MapConfigData config)
    {
        var sb = new StringBuilder();
        var tableName = SubTableName(baseName, TrafficSuffix);
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

    /// <summary>
    /// 构建章节索引：文件不存在时创建，存在时在原文件中合并当前关卡。
    /// </summary>
    /// <param name="path">索引文件绝对路径。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="level">当前关卡 ID。</param>
    /// <returns>完整索引文件内容。</returns>
    private static string BuildCocosIndex(string path, string chapter, string chapterName, string level)
    {
        if (!File.Exists(path))
        {
            return CreateCocosIndex(chapter, chapterName, level);
        }

        return MergeCocosIndex(File.ReadAllText(path), chapter, chapterName, level);
    }

    /// <summary>创建全新的章节索引内容。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="level">首个关卡 ID。</param>
    /// <returns>索引文件内容。</returns>
    private static string CreateCocosIndex(string chapter, string chapterName, string level)
    {
        var sb = new StringBuilder();
        AppendLuaHeader(sb, LevelIndexFileStem, "章节地图索引（自动生成）", "章节地图数据索引");
        sb.AppendLine($"---@class Tables.{LevelIndexTableName} @章节地图数据索引");
        sb.AppendLine($"Tables.{LevelIndexTableName} = {{");
        sb.AppendLine($"    {BuildIndexLine(chapter, chapterName, new List<string> { level })}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 在已有索引中合并章节：保留原 ChapterName 与 Levels，追加当前关卡后去重，
    /// 关卡 ID 能转数字时按数字排序，否则按字符串排序。
    /// </summary>
    /// <param name="original">已有索引全文。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名（仅新建条目时使用）。</param>
    /// <param name="level">当前关卡 ID。</param>
    /// <returns>合并后的索引全文。</returns>
    private static string MergeCocosIndex(string original, string chapter, string chapterName, string level)
    {
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
                lines = lines.Append($"Tables.{LevelIndexTableName} = {{").Append("}").ToArray();
                closeIndex = lines.Length - 1;
            }

            var mergedLines = lines.Take(closeIndex).Concat(new[] { "    " + line }).Concat(lines.Skip(closeIndex)).ToArray();
            lines = mergedLines;
        }

        return string.Join(newline, lines);
    }

    /// <summary>构建单章节索引行，供新建与合并共用。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="levels">已排序去重的关卡列表。</param>
    /// <returns>一行 Lua 表项。</returns>
    private static string BuildIndexLine(string chapter, string chapterName, IReadOnlyList<string> levels)
    {
        var quotedLevels = string.Join(", ", levels.Select(value => "\"" + Escape(value) + "\""));
        return $"[\"{Escape(chapter)}\"] = {{ ChapterId = \"{Escape(chapter)}\", " +
               $"ChapterName = \"{Escape(chapterName)}\", LevelCount = {levels.Count}, " +
               $"Levels = {{ {quotedLevels} }} }},";
    }

    #endregion
}

