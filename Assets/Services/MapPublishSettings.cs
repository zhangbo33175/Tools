/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishSettings.cs
 * author:    云毅
 * created:   2026
 * descrip:   发布设置持久化：设置模型、默认输出目录与 PublishSettings.json 读写
 ***************************************************************/

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MapEditorTool.Services;

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
