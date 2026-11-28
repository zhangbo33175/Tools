/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.UnityLua.cs
 * author:    云毅
 * created:   2026
 * descrip:   Unity Lua 出口：复用 Levels 拆分表，补资源同步、Sprite 注册与角色模板桥接
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>Unity Lua 运行时直载出口。</summary>
public static partial class MapPublishService
{
    #region Unity 常量

    /// <summary>Unity 地图贴图根目录（相对 Assets）。</summary>
    private const string UnityMapTextureRoot = "Assets/Res/Textures/Map";

    /// <summary>Unity Sprite 注册表相对 Assets 的路径。</summary>
    private const string UnitySpriteDefsRelative = "LuaScripts/Config/LoadResDefs/MapSpriteDefs.lua.txt";

    /// <summary>Unity 编辑器角色模板桥接表相对 Assets 的路径。</summary>
    private const string UnityMapCharactersRelative =
        "LuaScripts/Game/Tables/AlTables/Character/TableMapCharacters.lua.txt";

    /// <summary>Unity 缺失贴图同步来源根，相对编辑器工程根。</summary>
    private const string UnityMapDataSourceRelative = "MapData/Map";

    #endregion

    #region Unity Lua 发布入口

    /// <summary>
    /// Unity Lua 发布入口：写入 Unity Levels 拆分表，并生成 Sprite 注册表 / 角色模板桥接表，
    /// 供现有 GamePlayHelper + CharacterGenerationManager 直接加载。
    /// </summary>
    /// <param name="config">当前地图配置。</param>
    /// <param name="options">发布选项。</param>
    /// <param name="root">Unity Levels 输出根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <returns>发布结果与文件清单。</returns>
    private static MapPublishResult PublishUnityLua(
        MapConfigData config,
        MapPublishOptions options,
        string root,
        string chapter,
        string level)
    {
        var chapterDirectory = Path.Combine(root, chapter);
        FileSystemIo.EnsureDirectory(chapterDirectory);

        var baseName = LevelBaseName(chapter, level);
        var files = new List<string>();
        var warnings = new List<string>();
        var unityRoot = ResolveUnityProjectRoot(root);
        if (unityRoot is null)
        {
            warnings.Add("未能从输出目录反推 Unity 工程根，Sprite/角色桥接表将写回 Levels 根目录，运行时不会自动 require。");
        }

        var missingImages = new List<string>();
        var imageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string ResolveImage(PlacedObject item)
        {
            var relative = NormalizeImageRelative(item.ImagePath);
            if (relative.Length == 0)
            {
                return string.Empty;
            }

            if (imageCache.TryGetValue(relative, out var cached))
            {
                return cached;
            }

            var levelPath = $"{UnityMapTextureRoot}/Chapter{chapter}/Level{level}/{relative}";
            var probeRoot = unityRoot ?? (Directory.Exists(UnityProjectRoot) ? UnityProjectRoot : FindEditorRoot());
            var fullLevelPath = Path.Combine(probeRoot, levelPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullLevelPath))
            {
                imageCache[relative] = levelPath;
                return levelPath;
            }

            var sharedPath = $"{UnityMapTextureRoot}/Chapter{chapter}/Shared/{relative}";
            var fullSharedPath = Path.Combine(probeRoot, sharedPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullSharedPath))
            {
                imageCache[relative] = sharedPath;
                return sharedPath;
            }

            imageCache[relative] = levelPath;
            if (!missingImages.Contains(relative, StringComparer.OrdinalIgnoreCase))
            {
                missingImages.Add(relative);
            }

            return levelPath;
        }

        var backgroundName = NormalizeResourceName(config.BgName);

        if (options.BackupBeforeOverwrite)
        {
            BackupUnityLevelFiles(root, chapter, level, baseName);
        }

        var characters = config.Objects.Where(item => item.Type == MapObjectType.Character).ToList();
        var buildings = config.Objects.Where(item => item.Type == MapObjectType.Building).ToList();
        var objects = config.Objects.Where(item => item.Type == MapObjectType.Prop).ToList();

        WriteSplitTables(
            chapterDirectory,
            root,
            baseName,
            chapter,
            level,
            config,
            options,
            new SplitTableStyle
            {
                Compact = true,
                BackgroundName = backgroundName,
                ImageResolver = ResolveImage,
                IncludePathPoints = true,
                GridSize = config.GridSize,
            },
            characters,
            buildings,
            objects,
            files);

        if (options.GenerateUnityBridgeFiles)
        {
            WriteUnityBridgeFiles(
                config,
                unityRoot,
                chapter,
                level,
                backgroundName,
                characters,
                ResolveImage,
                files,
                warnings);
        }

        if (options.SyncMissingAssets)
        {
            SyncUnityMissingAssets(
                unityRoot,
                chapter,
                level,
                missingImages,
                warnings);
        }
        else if (missingImages.Count > 0)
        {
            warnings.Add($"Unity 缺失 {missingImages.Count} 张地图贴图，已按选项跳过同步。");
        }

        return FinishPublish(
            config,
            options,
            root,
            chapter,
            level,
            MapPublishTarget.UnityLua,
            files,
            warnings,
            $"Unity Lua 已发布：{baseName}",
            count => $"有 {count} 个 Spine 实例：Lua 表已写出 animationKind/spineAsset/spineAnimation，Unity 角色/物件生成链仍需按实际模板确认 Spine 分支。");
    }

    #endregion

    #region Unity 桥接文件

    /// <summary>生成 Sprite 注册表与编辑器角色模板表。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="unityRoot">Unity 工程根；为空时回退 Levels 根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="backgroundName">背景资源纯名。</param>
    /// <param name="characters">角色列表。</param>
    /// <param name="resolveImage">图片路径解析器。</param>
    /// <param name="files">发布文件列表。</param>
    /// <param name="warnings">发布警告。</param>
    private static void WriteUnityBridgeFiles(
        MapConfigData config,
        string? unityRoot,
        string chapter,
        string level,
        string backgroundName,
        IReadOnlyList<PlacedObject> characters,
        Func<PlacedObject, string> resolveImage,
        List<string> files,
        List<string> warnings)
    {
        var projectRoot = unityRoot ?? FindEditorRoot();
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        if (unityRoot is null)
        {
            warnings.Add("Unity 工程根未解析，桥接表写入编辑器工程 Assets 目录，仅用于离线校验。");
        }

        var spriteDefsPath = Path.Combine(assetsRoot, UnitySpriteDefsRelative.Replace('/', Path.DirectorySeparatorChar));
        WriteAtomic(spriteDefsPath, BuildUnitySpriteDefs(config, chapter, level, backgroundName, resolveImage, warnings));
        files.Add(spriteDefsPath);

        var mapCharactersPath = Path.Combine(assetsRoot, UnityMapCharactersRelative.Replace('/', Path.DirectorySeparatorChar));
        WriteAtomic(mapCharactersPath, BuildUnityMapCharacters(characters, resolveImage));
        files.Add(mapCharactersPath);
    }

    /// <summary>构建 Unity Sprite 注册表；键与 LoadSpriteByName 的 Sprite_ 前缀严格对齐。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="backgroundName">背景资源纯名。</param>
    /// <param name="resolveImage">图片路径解析器。</param>
    /// <param name="warnings">发布警告。</param>
    /// <returns>Lua 文本。</returns>
    private static string BuildUnitySpriteDefs(
        MapConfigData config,
        string chapter,
        string level,
        string backgroundName,
        Func<PlacedObject, string> resolveImage,
        List<string> warnings)
    {
        var sb = new StringBuilder();
        AppendLuaHeader(sb, "MapSpriteDefs", "Unity 地图 Sprite 注册表（自动生成）", "把地图贴图路径注册进 LoadRes",
            "由 MapEditorTool 发布生成；GameMain.lua 在 LoadResDefs 之后 require('MapSpriteDefs') 接入");
        sb.AppendLine("LoadRes = LoadRes or {}");
        sb.AppendLine();

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string resourceName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            if (entries.TryGetValue(resourceName, out var existing)
                && !string.Equals(existing, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Sprite_{resourceName} 存在同名不同路径，保留首条：{existing}；忽略：{assetPath}");
                return;
            }

            entries[resourceName] = assetPath;
        }

        Add(backgroundName, $"{UnityMapTextureRoot}/Chapter{chapter}/Level{level}/Background/{backgroundName}.png");
        foreach (var item in config.Objects)
        {
            var image = NormalizeImageRelative(item.ImagePath);
            if (image.Length == 0)
            {
                continue;
            }

            Add(Path.GetFileNameWithoutExtension(image), resolveImage(item));
        }

        foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var assetPath = pair.Value.Replace('\\', '/');
            if (assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = assetPath[..^4];
            }

            sb.AppendLine($"LoadRes[\"Sprite_{Escape(pair.Key)}\"] = {{");
            sb.AppendLine($"    Name = \"Sprite_{Escape(pair.Key)}\",");
            sb.AppendLine("    TypeName = \"Sprite\",");
            sb.AppendLine($"    ABPath = \"{Escape(assetPath)}\",");
            sb.AppendLine($"    AssetName = \"{Escape(pair.Key)}\",");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>构建 Unity 编辑器角色模板桥接表。</summary>
    /// <param name="characters">角色列表。</param>
    /// <param name="resolveImage">图片路径解析器。</param>
    /// <returns>Lua 文本。</returns>
    private static string BuildUnityMapCharacters(
        IReadOnlyList<PlacedObject> characters,
        Func<PlacedObject, string> resolveImage)
    {
        var sb = new StringBuilder();
        AppendLuaHeader(sb, "TableMapCharacters", "Unity 地图编辑器角色模板桥接表（自动生成）",
            "把编辑器角色模板名映射到通用角色预制体", "Tables.MapCharacters 由 Tables 懒加载入口自动 require");
        sb.AppendLine("Tables.MapCharacters = {");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in characters)
        {
            if (string.IsNullOrWhiteSpace(item.TemplateId) || !seen.Add(item.TemplateId))
            {
                continue;
            }

            var image = NormalizeImageRelative(item.ImagePath);
            var icon = image.Length == 0 ? string.Empty : Path.GetFileNameWithoutExtension(image);
            sb.AppendLine($"    [\"{Escape(item.TemplateId)}\"] = {{");
            sb.AppendLine($"        ID = \"{Escape(item.TemplateId)}\",");
            sb.AppendLine($"        Name = \"{Escape(item.Name)}\",");
            sb.AppendLine($"        Icon = \"{Escape(icon)}\",");
            sb.AppendLine("        ResName = \"GameObject_Character\",");
            sb.AppendLine($"        BubbleId = {item.BubbleId},");
            sb.AppendLine("    },");
        }

        if (seen.Count == 0)
        {
            sb.AppendLine("    -- 空列表");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region 资源同步与备份

    /// <summary>把 Unity 缺失的地图贴图从编辑器 MapData 同步到目标工程，已存在的文件不覆盖。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="resolveImage">图片路径解析器。</param>
    /// <param name="missingImages">缺失图片相对路径列表。</param>
    /// <param name="warnings">发布警告。</param>
    private static void SyncUnityMissingAssets(
        string? unityRoot,
        string chapter,
        string level,
        List<string> missingImages,
        List<string> warnings)
    {
        if (missingImages.Count == 0)
        {
            return;
        }

        var projectRoot = unityRoot ?? UnityProjectRoot;
        var editorRoot = FindEditorRoot();
        var sourceRoots = new[]
        {
            Path.Combine(editorRoot, UnityMapDataSourceRelative, $"Chapter{chapter}", $"Level{level}"),
            Path.Combine(editorRoot, "dist", "publish", UnityMapDataSourceRelative, $"Chapter{chapter}", $"Level{level}"),
        };

        var copied = 0;
        foreach (var relative in missingImages)
        {
            var destination = Path.Combine(
                projectRoot,
                $"{UnityMapTextureRoot}/Chapter{chapter}/Level{level}/{relative}".Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(destination))
            {
                continue;
            }

            var source = sourceRoots
                .Select(root => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)))
                .FirstOrDefault(File.Exists);
            if (source is null)
            {
                warnings.Add($"未找到缺失贴图源文件：Chapter{chapter}/Level{level}/{relative}");
                continue;
            }

            FileSystemIo.EnsureParentDirectory(destination);
            File.Copy(source, destination, overwrite: false);
            copied++;
        }

        if (copied > 0)
        {
            warnings.Add($"已同步 {copied} 张 Unity 缺失地图贴图；首次导入请让 Unity 自动生成 .meta 并按 Sprite 导入。");
        }
    }

    /// <summary>发布前备份同名关卡表与章节索引。</summary>
    /// <param name="root">Levels 根目录。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="baseName">关卡表基名。</param>
    private static void BackupUnityLevelFiles(string root, string chapter, string level, string baseName)
    {
        var chapterDirectory = Path.Combine(root, chapter);
        // ---TODO [Unity 直载] 备份必须放在 Assets 外：Unity 编辑器模式会递归扫描 Lua 根目录下所有
        // *.lua.txt，同名的 Backups 副本会触发 LuaComponent 的“重名脚本”错误。
        // Levels 根目录向上 5 级即 Unity 工程根（Levels→Tables→Game→LuaScripts→Assets→工程根）。
        var backupRoot = Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "..", "Backups", "LuaLevels"));
        var backupDirectory = Path.Combine(backupRoot, $"{chapter}_{level}_{DateTime.Now:yyyyMMdd_HHmmss}");
        var files = new[]
        {
            SubTablePath(chapterDirectory, baseName, string.Empty),
            SubTablePath(chapterDirectory, baseName, CharactersSuffix),
            SubTablePath(chapterDirectory, baseName, BuildingsSuffix),
            SubTablePath(chapterDirectory, baseName, ObjectsSuffix),
            SubTablePath(chapterDirectory, baseName, PathsSuffix),
            SubTablePath(chapterDirectory, baseName, TrafficSuffix),
            Path.Combine(root, LevelIndexFileName),
        };

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            FileSystemIo.EnsureDirectory(backupDirectory);
            File.Copy(file, Path.Combine(backupDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }

    #endregion

    #region 路径辅助

    /// <summary>把图片路径归一为分类/文件名形式，兼容绝对路径与反斜杠。</summary>
    /// <param name="imagePath">原始图片路径。</param>
    /// <returns>归一化相对路径。</returns>
    private static string NormalizeImageRelative(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        var normalized = imagePath.Replace('\\', '/').Trim();
        var mapMarker = "/Textures/Map/";
        var markerIndex = normalized.IndexOf(mapMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            normalized = normalized[(markerIndex + mapMarker.Length)..];
        }

        var chapterMarker = normalized.IndexOf("/Chapter", StringComparison.OrdinalIgnoreCase);
        if (chapterMarker >= 0)
        {
            var levelMarker = normalized.IndexOf("/Level", chapterMarker, StringComparison.OrdinalIgnoreCase);
            if (levelMarker >= 0)
            {
                var nextSlash = normalized.IndexOf('/', levelMarker + 6);
                if (nextSlash >= 0)
                {
                    normalized = normalized[(nextSlash + 1)..];
                }
            }
        }

        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Assets/".Length..];
        }

        return normalized.TrimStart('/');
    }

    /// <summary>从完整资源路径取纯资源名；无扩展名时原样返回。</summary>
    /// <param name="path">资源路径或资源名。</param>
    /// <returns>纯资源名。</returns>
    private static string NormalizeResourceName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(path.Replace('\\', '/'));
        return Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>从 Levels 输出目录向上找 Unity 工程根；找不到时返回 null。</summary>
    /// <param name="root">Levels 目录。</param>
    /// <returns>Unity 工程根绝对路径。</returns>
    private static string? ResolveUnityProjectRoot(string root)
    {
        var cursor = new DirectoryInfo(root);
        while (cursor is not null)
        {
            if (string.Equals(cursor.Name, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return cursor.Parent?.FullName;
            }

            cursor = cursor.Parent;
        }

        return Directory.Exists(UnityProjectRoot) ? UnityProjectRoot : null;
    }

    #endregion
}
