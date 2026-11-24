/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Files.cs
 * author:    云毅
 * created:   2026
 * descrip:   发布文件写入：清单、源哈希与原子写文件
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

public static partial class MapPublishService
{

    #region 清单

    /// <summary>写入发布清单，并把清单路径登记进本次发布文件列表。</summary>
    /// <param name="path">清单输出路径。</param>
    /// <param name="target">发布目标。</param>
    /// <param name="config">地图配置。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="level">关卡 ID。</param>
    /// <param name="mapName">地图名。</param>
    /// <param name="files">本次发布文件列表。</param>
    /// <param name="root">发布根目录。</param>
    /// <param name="warnings">发布警告。</param>
    // ---TODO 重构：清单写入后自动登记到发布文件列表，调用方不再重复 Add。
    private static void WriteManifestAndRegister(
        string path,
        MapPublishTarget target,
        MapConfigData config,
        string chapter,
        string level,
        string mapName,
        List<string> files,
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

        WriteAtomic(path, JsonSerializer.Serialize(manifest, JsonFileOptions.Relaxed));
        files.Add(path);
    }

    /// <summary>构造统一发布结果；各发布目标只负责提供摘要文案。</summary>
    /// <param name="options">发布选项。</param>
    /// <param name="root">输出根目录。</param>
    /// <param name="files">本次发布文件列表。</param>
    /// <param name="manifestPath">清单路径。</param>
    /// <param name="warnings">发布警告。</param>
    /// <param name="summary">目标相关摘要。</summary>
    /// <returns>发布结果。</returns>
    // ---TODO 重构：Unity / Cocos 结果对象由统一入口构造，只保留摘要差异。
    private static MapPublishResult CreatePublishResult(
        MapPublishOptions options,
        string root,
        List<string> files,
        string manifestPath,
        List<string> warnings,
        string summary) => new()
    {
        Target = options.Target,
        OutputDirectory = root,
        Files = files,
        ManifestPath = manifestPath,
        Warnings = warnings,
        Summary = summary,
    };

    private static string SourceHash(MapConfigData config)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(FileSystemIo.Utf8NoBom.GetBytes(MapJsonService.Serialize(config)));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    #endregion

    #region 文件写入

    private static void WriteAtomic(string path, string content)
    {
        FileSystemIo.EnsureParentDirectory(path);

        var temp = path + ".tmp-" + IdGenerator.NewShortId();
        FileSystemIo.WriteText(temp, content);
        File.Move(temp, path, true);
    }


    #endregion
}
