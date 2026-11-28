/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图发布核心：目标/选项、预检、发布入口与 Unity JSON 出口
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

    /// <summary>Unity Lua 运行时拆分关卡表（Tables.LevelData_*）及资源/角色桥接表。</summary>
    UnityLua,
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

    /// <summary>Unity Lua 发布时是否在覆盖前备份同名表与章节索引。</summary>
    public bool BackupBeforeOverwrite { get; set; } = true;

    /// <summary>Unity Lua 发布时是否把 Unity 缺失的贴图从编辑器 MapData 同步过去。</summary>
    public bool SyncMissingAssets { get; set; } = true;

    /// <summary>Unity Lua 发布时是否生成 Sprite 注册表与编辑器角色模板桥接表。</summary>
    public bool GenerateUnityBridgeFiles { get; set; } = true;
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


#region 发布服务

/// <summary>
/// 地图发布服务。Unity 出口复用 <see cref="MapJsonService"/> 的 MapConfigData JSON；
/// Cocos 出口生成 Cocos Lua 运行时正在消费的拆分关卡表，不引入第二套编辑器模型。
/// </summary>
public static partial class MapPublishService
{
    #region 常量

    private const string CocosLevelsRelative = @"assets\LuaScripts\Game\Tables\Levels";
    private const string UnityMapDatasRelative = @"Assets\GameData\MapDatas";
    private const string UnityLevelsRelative = @"Assets\LuaScripts\Game\Tables\Levels";
    private const string UnityProjectRoot = @"E:\Game\CatGame\CatGameCodeOptimizationBuild";
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

        if (target == MapPublishTarget.UnityLua)
        {
            var unityLevels = Path.Combine(UnityProjectRoot, UnityLevelsRelative);
            if (Directory.Exists(unityLevels))
            {
                return unityLevels;
            }

            return Path.Combine(editorRoot, "docs", "Publish", "UnityLua", "Levels");
        }

        var unity = Path.Combine(UnityProjectRoot, UnityMapDatasRelative);
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
            Files = target switch
            {
                MapPublishTarget.UnityJson => 3,
                MapPublishTarget.UnityLua => 9,
                _ => 7,
            },
            TargetHint = target switch
            {
                MapPublishTarget.UnityJson => "写入 Unity MapConfigData JSON，供 MapConfigRepository 读取",
                MapPublishTarget.UnityLua => "写入 Unity Assets/LuaScripts/Game/Tables/Levels，并生成 Sprite/角色桥接表，供现有 Lua 运行时直接加载",
                _ => "写入 Tables.LevelData_* 拆分 Lua 表，供 Cocos Lua 运行时读取",
            },
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
        FileSystemIo.EnsureDirectory(root);

        var chapter = NumericOr(config.ChapterId, "1");
        var level = NumericOr(config.SceneId, "1");
        var result = options.Target switch
        {
            MapPublishTarget.CocosLua => PublishCocosLua(config, options, root, chapter, level),
            MapPublishTarget.UnityLua => PublishUnityLua(config, options, root, chapter, level),
            _ => PublishUnityJson(config, options, root, chapter, level, fog),
        };

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
        var fileName = MapFileNames.Level(chapter, level);
        var mapPath = Path.Combine(root, fileName);
        var warnings = new List<string>();
        if (!LooksLikeUnityMapDirectory(root))
        {
            warnings.Add($"输出目录下没有 {MapFileNames.LevelPattern}，可能不是 Unity 工程的 Assets/GameData/MapDatas。");
        }

        var json = MapJsonService.Serialize(config);
        WriteAtomic(mapPath, json);

        var files = new List<string> { mapPath };

        if (options.IncludeFog && fog is not null)
        {
            var fogPath = Path.Combine(root, MapFileNames.Fog(chapter, level));
            var fogCopy = new MapFogDocument
            {
                SceneId = config.SceneId,
                Regions = fog.Regions,
            };
            WriteAtomic(fogPath, JsonSerializer.Serialize(fogCopy, JsonFileOptions.Relaxed));
            files.Add(fogPath);
        }

        return FinishPublish(
            config,
            options,
            root,
            chapter,
            level,
            MapPublishTarget.UnityJson,
            files,
            warnings,
            $"Unity JSON 已发布：{fileName}");
    }

    private static bool LooksLikeUnityMapDirectory(string root)
    {
        return root.Replace('\\', '/').Contains("/GameData/MapDatas", StringComparison.OrdinalIgnoreCase)
            || Directory.EnumerateFiles(root, MapFileNames.LevelPattern).Any();
    }

    #endregion

    #region 拆分表落盘

    /// <summary>
    /// 主表 + 5 张子表的写盘口径。Cocos 出口用实例原始贴图路径与非紧凑格式；
    /// Unity 出口用 Unity 贴图路径、内嵌路径点与 compact-v2 头。
    /// </summary>
    private sealed class SplitTableStyle
    {
        /// <summary>主表是否写 <c>Format = "compact-v2"</c>。</summary>
        public bool Compact { get; init; }

        /// <summary>主表 Meta.BgName 的覆盖值；null 表示用地图配置里的 BgName。</summary>
        public string? BackgroundName { get; init; }

        /// <summary>子表贴图路径解析器；null 表示沿用实例自带的 ImagePath。</summary>
        public Func<PlacedObject, string>? ImageResolver { get; init; }

        /// <summary>子表是否内嵌路径点。</summary>
        public bool IncludePathPoints { get; init; }

        /// <summary>内嵌路径点用的网格尺寸；不内嵌路径点时传 0。</summary>
        public int GridSize { get; init; }
    }

    /// <summary>把一张子表写到章节目录，并登记进发布文件清单。</summary>
    /// <param name="chapterDirectory">章节目录。</param>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="suffix">子表后缀；主表传空串。</param>
    /// <param name="content">生成好的 Lua 文本。</param>
    /// <param name="files">发布文件清单。</param>
    private static void WriteSubTable(
        string chapterDirectory,
        string baseName,
        string suffix,
        string content,
        List<string> files)
    {
        var path = SubTablePath(chapterDirectory, baseName, suffix);
        WriteAtomic(path, content);
        files.Add(path);
    }

    /// <summary>按选项写章节索引表并登记；未勾选更新索引时直接跳过。</summary>
    /// <param name="root">输出根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="config">地图数据。</param>
    /// <param name="enabled">是否更新索引。</param>
    /// <param name="files">发布文件清单。</param>
    private static void WriteLevelIndexIfEnabled(
        string root,
        string chapter,
        string level,
        MapConfigData config,
        bool enabled,
        List<string> files)
    {
        if (!enabled)
        {
            return;
        }

        var indexPath = Path.Combine(root, LevelIndexFileName);
        WriteAtomic(indexPath, BuildCocosIndex(indexPath, chapter, config.ChapterName, level));
        files.Add(indexPath);
    }

    /// <summary>
    /// 主表 + 角色/建筑/物件/路径/交通子表 + 可选索引，按固定顺序一次写完。
    /// Cocos Lua 与 Unity Lua 两个出口共用这份落盘顺序，差异全部收进 <see cref="SplitTableStyle"/>。
    /// </summary>
    /// <param name="chapterDirectory">章节目录。</param>
    /// <param name="root">输出根目录（索引表写在这里）。</param>
    /// <param name="baseName">关卡表基名。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="config">地图数据。</param>
    /// <param name="options">发布选项。</param>
    /// <param name="style">写盘口径。</param>
    /// <param name="characters">角色实例。</param>
    /// <param name="buildings">建筑实例。</param>
    /// <param name="objects">物件实例。</param>
    /// <param name="files">发布文件清单。</param>
    private static void WriteSplitTables(
        string chapterDirectory,
        string root,
        string baseName,
        string chapter,
        string level,
        MapConfigData config,
        MapPublishOptions options,
        SplitTableStyle style,
        List<PlacedObject> characters,
        List<PlacedObject> buildings,
        List<PlacedObject> objects,
        List<string> files)
    {
        WriteSubTable(
            chapterDirectory,
            baseName,
            string.Empty,
            BuildCocosMainFile(baseName, chapter, level, config, options.MapName, style.BackgroundName, style.Compact),
            files);

        WriteSubTable(
            chapterDirectory,
            baseName,
            CharactersSuffix,
            BuildCocosCharactersFile(
                baseName,
                characters,
                style.ImageResolver,
                compact: style.Compact),
            files);

        WriteSubTable(
            chapterDirectory,
            baseName,
            BuildingsSuffix,
            BuildCocosBuildingsFile(
                baseName,
                buildings,
                style.ImageResolver,
                style.IncludePathPoints,
                style.GridSize,
                style.Compact),
            files);

        WriteSubTable(
            chapterDirectory,
            baseName,
            ObjectsSuffix,
            BuildCocosObjectsFile(
                baseName,
                objects,
                style.ImageResolver,
                style.IncludePathPoints,
                style.GridSize,
                style.Compact),
            files);

        WriteSubTable(
            chapterDirectory,
            baseName,
            PathsSuffix,
            BuildCocosPathsFile(baseName, characters, config.GridSize),
            files);

        WriteSubTable(
            chapterDirectory,
            baseName,
            TrafficSuffix,
            BuildCocosTrafficFile(baseName, config),
            files);

        WriteLevelIndexIfEnabled(root, chapter, level, config, options.UpdateCocosIndex, files);
    }

    #endregion

    #region 发布收尾

    /// <summary>
    /// 三个出口共用的收尾：Spine 实例提示、写清单并登记、组装发布结果。
    /// </summary>
    /// <param name="config">地图数据。</param>
    /// <param name="options">发布选项。</param>
    /// <param name="root">输出根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="previewTarget">写进结果摘要口径的发布目标。</param>
    /// <param name="files">本次落盘的文件列表。</param>
    /// <param name="warnings">发布警告集合，本方法会追加。</param>
    /// <param name="headline">结果首句，如「Cocos Lua 已发布：Chapter1_Level1」。</param>
    /// <param name="spineWarning">Spine 实例存在时的警告文案生成器；为 null 时该出口不提示。</param>
    /// <returns>发布结果。</returns>
    private static MapPublishResult FinishPublish(
        MapConfigData config,
        MapPublishOptions options,
        string root,
        string chapter,
        string level,
        MapPublishTarget previewTarget,
        List<string> files,
        List<string> warnings,
        string headline,
        Func<int, string>? spineWarning = null)
    {
        if (spineWarning is not null)
        {
            var spineCount = config.Objects.Count(item =>
                string.Equals(item.AnimationKind, AnimationKinds.Spine, StringComparison.OrdinalIgnoreCase));
            if (spineCount > 0)
            {
                warnings.Add(spineWarning(spineCount));
            }
        }

        var manifestPath = Path.Combine(root, MapFileNames.Manifest(chapter, level));
        WriteManifestAndRegister(
            manifestPath,
            options.Target,
            config,
            chapter,
            level,
            options.MapName,
            files,
            root,
            warnings);

        return CreatePublishResult(
            options,
            root,
            files,
            manifestPath,
            warnings,
            $"{headline}（{Preview(config, previewTarget).Summary}）");
    }

    #endregion

}

#endregion
