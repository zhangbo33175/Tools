/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TilePaletteStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   调色板纹理清单持久化（本机偏好：上次用过的瓦片贴图）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 调色板纹理清单的落盘服务。
/// 纹理清单属于本机编辑偏好而不是地图数据，因此单独存 <c>output\Config\TilePalette.json</c>，
/// 地图 JSON 里只存「哪一格用了哪张图」。
/// </summary>
public static class TilePaletteStore
{
    private static readonly JsonSerializerOptions Options = new(JsonFileOptions.Relaxed)
    {
        WriteIndented = true,
    };

    /// <summary>清单文件路径。</summary>
    public static string FilePath => Path.Combine(EditorPaths.Config, "TilePalette.json");

    /// <summary>读取纹理清单；文件缺失或损坏时返回空列表。</summary>
    /// <returns>贴图路径列表。</returns>
    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new List<string>();
            }

            var json = FileSystemIo.ReadText(FilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json, Options);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            EditorLogs.Append("TilePalette.log", $"[读取失败] {ex.Message}`n");
            return new List<string>();
        }
    }

    /// <summary>保存纹理清单；写盘失败只记日志，不影响编辑。</summary>
    /// <param name="images">贴图路径列表。</param>
    public static void Save(IReadOnlyList<string> images)
    {
        try
        {
            Directory.CreateDirectory(EditorPaths.Config);
            FileSystemIo.WriteText(FilePath, JsonSerializer.Serialize(images, Options));
        }
        catch (Exception ex)
        {
            EditorLogs.Append("TilePalette.log", $"[保存失败] {ex.Message}`n");
        }
    }
}

