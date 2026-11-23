/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorSettingsStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图配置的本地持久化（替代 Unity 的 EditorPrefs）
 ***************************************************************/

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 类定义

/// <summary>
/// 地图配置仓库。Unity 版把六条路径 / 基线步长 / 预制体写进 EditorPrefs，
/// 独立版没有 EditorPrefs，改为落盘到 <c>output\Config\EditorSettings.json</c>。
/// </summary>
public sealed class EditorSettingsStore
{
    #region 常量与字段

    private const string FileName = "EditorSettings.json";

    #endregion

    #region 构造

    /// <summary>初始化仓库。</summary>
    /// <param name="configDirectory">配置目录（<c>output\Config</c>）。</param>
    public EditorSettingsStore(string configDirectory)
    {
        JsonPath = Path.Combine(configDirectory, FileName);
    }

    #endregion

    #region 公开属性

    /// <summary>设置文件路径。</summary>
    public string JsonPath { get; }

    #endregion

    #region 公开方法

    /// <summary>读取设置；文件缺失或损坏时返回默认设置并立即落盘，保证下次启动有文件可改。</summary>
    /// <returns>设置实例。</returns>
    public EditorSettingsData LoadOrCreate()
    {
        var settings = TryLoad(JsonPath);
        if (settings is not null)
        {
            return settings;
        }

        settings = new EditorSettingsData();
        TrySave(settings, out _);
        return settings;
    }

    /// <summary>写入设置（基线步长按上下限钳制）。</summary>
    /// <param name="settings">设置实例。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否写入成功。</returns>
    public bool TrySave(EditorSettingsData settings, out string error)
    {
        try
        {
            var directory = Path.GetDirectoryName(JsonPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = SettingsPayload.FromModel(settings);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(JsonPath, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    #endregion

    #region 内部实现

    /// <summary>读取设置文件，缺失 / 损坏时返回 null。</summary>
    private static EditorSettingsData? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<SettingsPayload>(File.ReadAllText(path, Encoding.UTF8));
            return payload?.ToModel();
        }
        catch (Exception)
        {
            // 设置损坏时回落到默认口径，不阻塞编辑器启动
            return null;
        }
    }

    /// <summary>JSON 传输对象：属性名与 UI 标签口径一致。</summary>
    private sealed class SettingsPayload
    {
        public string TableChaptersPath { get; set; } = EditorSettingsDefaults.TableChaptersPath;

        public string TableMainLevelsPath { get; set; } = EditorSettingsDefaults.TableMainLevelsPath;

        public string ItemElementTablesDirectory { get; set; } = EditorSettingsDefaults.ItemElementTablesDirectory;

        public string ItemElementCsvPath { get; set; } = EditorSettingsDefaults.ItemElementCsvPath;

        public string ObjectPreviewPrefabsDirectory { get; set; } = EditorSettingsDefaults.ObjectPreviewPrefabsDirectory;

        public string ObjectPreviewTexturesDirectory { get; set; } = EditorSettingsDefaults.ObjectPreviewTexturesDirectory;

        public string LevelBackgroundDirectory { get; set; } = EditorSettingsDefaults.LevelBackgroundDirectory;

        public string MapSaveDirectory { get; set; } = EditorSettingsDefaults.MapSaveDirectory;

        public int BaselineStep { get; set; } = EditorSettingsDefaults.BaselineStep;

        /// <summary>设置 → 传输对象。</summary>
        public static SettingsPayload FromModel(EditorSettingsData settings) => new()
        {
            TableChaptersPath = settings.TableChaptersPath,
            TableMainLevelsPath = settings.TableMainLevelsPath,
            ItemElementTablesDirectory = settings.ItemElementTablesDirectory,
            ItemElementCsvPath = settings.ItemElementCsvPath,
            ObjectPreviewPrefabsDirectory = settings.ObjectPreviewPrefabsDirectory,
            ObjectPreviewTexturesDirectory = settings.ObjectPreviewTexturesDirectory,
            LevelBackgroundDirectory = settings.LevelBackgroundDirectory,
            MapSaveDirectory = settings.MapSaveDirectory,
            BaselineStep = EditorSettings.ClampBaselineStep(settings.BaselineStep),
        };

        /// <summary>传输对象 → 设置（空字段回落到默认值，避免手改坏 JSON 后界面一片空白）。</summary>
        public EditorSettingsData ToModel() => new()
        {
            TableChaptersPath = Fallback(TableChaptersPath, EditorSettingsDefaults.TableChaptersPath),
            TableMainLevelsPath = Fallback(TableMainLevelsPath, EditorSettingsDefaults.TableMainLevelsPath),
            ItemElementTablesDirectory = Fallback(
                ItemElementTablesDirectory,
                string.IsNullOrWhiteSpace(ItemElementCsvPath)
                    ? EditorSettingsDefaults.ItemElementTablesDirectory
                    : Path.GetDirectoryName(ItemElementCsvPath.Replace('\\', '/')) ?? EditorSettingsDefaults.ItemElementTablesDirectory),
            ItemElementCsvPath = Fallback(ItemElementCsvPath, EditorSettingsDefaults.ItemElementCsvPath),
            ObjectPreviewPrefabsDirectory = Fallback(ObjectPreviewPrefabsDirectory, EditorSettingsDefaults.ObjectPreviewPrefabsDirectory),
            ObjectPreviewTexturesDirectory = Fallback(ObjectPreviewTexturesDirectory, EditorSettingsDefaults.ObjectPreviewTexturesDirectory),
            LevelBackgroundDirectory = Fallback(LevelBackgroundDirectory, EditorSettingsDefaults.LevelBackgroundDirectory),
            MapSaveDirectory = Fallback(MapSaveDirectory, EditorSettingsDefaults.MapSaveDirectory),
            BaselineStep = EditorSettings.ClampBaselineStep(BaselineStep),
        };

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    #endregion
}

#endregion
