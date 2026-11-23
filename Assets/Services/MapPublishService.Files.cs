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
using System.Text;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

public static partial class MapPublishService
{

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

    #region 文件写入

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
