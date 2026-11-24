/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorSettings.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图配置窗口的编辑级设置（字段口径同 Unity MapConfigWindow）
 ***************************************************************/

using System;
using System.IO;

namespace MapEditorTool.Models;

#region 默认值

/// <summary>
/// 地图配置默认值。字符串口径与 Unity <c>MapToolConstants</c> 的默认项一一对应：
/// 工程内路径写 <c>Assets/…</c>，Assets 之外的写 <c>../…</c>（相对 Assets）。
/// </summary>
public static class EditorSettingsDefaults
{
    /// <summary>章节配置表（场景名 / 场景ID / 分区配置来源）。</summary>
    public const string TableChaptersPath = "../Docs/Designs/Excels/Tables/AlTables/LevelConfigs/TableChapters.xlsm";

    /// <summary>主关卡配置表（分区背景图来源）。</summary>
    public const string TableMainLevelsPath = "../Docs/Designs/Excels/Tables/AlTables/LevelConfigs/TableMainLevels.xlsm";

    /// <summary>
    /// 地图物件配置表文件夹：夹内多张表（TableProps / TableBuildings / TableCharacters … xlsm，
    /// 以及汇总 <c>ItemElementConfig.csv</c>）一起读，资源整理按图片名跨表绑定。
    /// </summary>
    public const string ItemElementTablesDirectory = "../Docs/Designs/Excels/Tables/AlTables/Item";

    /// <summary>
    /// 旧口径：单张汇总 CSV（仅用于迁移，目录为空时从它的父目录推导）。
    /// </summary>
    public const string ItemElementCsvPath = "../Docs/Designs/Excels/Tables/AlTables/Item/ItemElementConfig.csv";

    /// <summary>物体预览图目录一（Prefabs 下）。</summary>
    public const string ObjectPreviewPrefabsDirectory = "Assets/Res/Prefabs/Map/Objects";

    /// <summary>物体预览图目录二（Textures 下）。</summary>
    public const string ObjectPreviewTexturesDirectory = "Assets/Res/Textures/Map/Objects";

    /// <summary>分区背景图目录（拼接 <c>{bgName}.png</c>）。</summary>
    public const string LevelBackgroundDirectory = "Assets/Res/Textures/Map/LevelBackground";

    /// <summary>章节地图保存目录（拼接 <c>Map_{chapterId}.json</c>）。</summary>
    public const string MapSaveDirectory = "Assets/GameData/Maps";

    /// <summary>基线步长默认值：八层基线按 0 / 10 / 20 / … 等差拉开。</summary>
    public const int BaselineStep = 10;

    /// <summary>基线步长下限：必须大于层内槽位容量，层内排序才不会跨进相邻层基线。</summary>
    public const int BaselineStepMin = 10;

    /// <summary>基线步长上限，防止误填超大值把 SortOrder 撑爆。</summary>
    public const int BaselineStepMax = 1000;
}

#endregion

#region 设置模型

/// <summary>地图配置窗口的一屏设置：六条路径 + 基线步长。</summary>
public sealed class EditorSettingsData
{
    /// <summary>章节配置表路径。</summary>
    public string TableChaptersPath { get; set; } = EditorSettingsDefaults.TableChaptersPath;

    /// <summary>主关卡配置表路径。</summary>
    public string TableMainLevelsPath { get; set; } = EditorSettingsDefaults.TableMainLevelsPath;

    /// <summary>地图物件配置表文件夹（夹内 CSV + xlsm 全部参与绑定）。</summary>
    public string ItemElementTablesDirectory { get; set; } = EditorSettingsDefaults.ItemElementTablesDirectory;

    /// <summary>旧口径的单张汇总 CSV；仅用于迁移旧存档，新配置不再写它。</summary>
    public string ItemElementCsvPath { get; set; } = EditorSettingsDefaults.ItemElementCsvPath;

    /// <summary>物体预览图目录一。</summary>
    public string ObjectPreviewPrefabsDirectory { get; set; } = EditorSettingsDefaults.ObjectPreviewPrefabsDirectory;

    /// <summary>物体预览图目录二。</summary>
    public string ObjectPreviewTexturesDirectory { get; set; } = EditorSettingsDefaults.ObjectPreviewTexturesDirectory;

    /// <summary>分区背景图目录。</summary>
    public string LevelBackgroundDirectory { get; set; } = EditorSettingsDefaults.LevelBackgroundDirectory;

    /// <summary>章节地图保存目录。</summary>
    public string MapSaveDirectory { get; set; } = EditorSettingsDefaults.MapSaveDirectory;

    /// <summary>相邻显示层基线步长。</summary>
    public int BaselineStep { get; set; } = EditorSettingsDefaults.BaselineStep;

    // ---TODO 重构：设置字段均为不可变值，浅拷贝即可，不再逐字段抄一遍。
    /// <summary>复制一份，避免窗口里的编辑直接改到全局设置。</summary>
    /// <returns>新实例。</returns>
    public EditorSettingsData Clone() => (EditorSettingsData)MemberwiseClone();

    // ---TODO 重构：默认值与旧存档迁移由模型维护，仓库不再维护重复 DTO。
    /// <summary>
    /// 把缺失字段回落到默认值，并把基线步长钳到合法区间。
    /// 设置语义由模型自己维护，持久化层只负责反序列化与调用本方法。
    /// </summary>
    public void Normalize()
    {
        TableChaptersPath = Fallback(TableChaptersPath, EditorSettingsDefaults.TableChaptersPath);
        TableMainLevelsPath = Fallback(TableMainLevelsPath, EditorSettingsDefaults.TableMainLevelsPath);
        ItemElementTablesDirectory = Fallback(ItemElementTablesDirectory, LegacyTablesDirectory());
        ItemElementCsvPath = Fallback(ItemElementCsvPath, EditorSettingsDefaults.ItemElementCsvPath);
        ObjectPreviewPrefabsDirectory = Fallback(
            ObjectPreviewPrefabsDirectory,
            EditorSettingsDefaults.ObjectPreviewPrefabsDirectory);
        ObjectPreviewTexturesDirectory = Fallback(
            ObjectPreviewTexturesDirectory,
            EditorSettingsDefaults.ObjectPreviewTexturesDirectory);
        LevelBackgroundDirectory = Fallback(LevelBackgroundDirectory, EditorSettingsDefaults.LevelBackgroundDirectory);
        MapSaveDirectory = Fallback(MapSaveDirectory, EditorSettingsDefaults.MapSaveDirectory);
        BaselineStep = EditorSettings.ClampBaselineStep(BaselineStep);
    }

    /// <summary>旧存档只写了汇总 CSV 路径时，从它的目录推导配置表文件夹。</summary>
    /// <returns>配置表目录。</returns>
    private string LegacyTablesDirectory() =>
        string.IsNullOrWhiteSpace(ItemElementCsvPath)
            ? EditorSettingsDefaults.ItemElementTablesDirectory
            : Path.GetDirectoryName(ItemElementCsvPath.Replace('\\', '/'))
              ?? EditorSettingsDefaults.ItemElementTablesDirectory;

    /// <summary>空字段回落到默认值。</summary>
    /// <param name="value">原始值。</param>
    /// <param name="fallback">默认值。</param>
    /// <returns>非空值。</returns>
    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}

#endregion

#region 当前设置

/// <summary>全局生效的编辑级设置，启动时从 <c>output\Config\EditorSettings.json</c> 装载。</summary>
public static class EditorSettings
{
    /// <summary>当前设置（始终非空，缺省为 Unity 默认口径）。</summary>
    public static EditorSettingsData Current { get; private set; } = new();

    /// <summary>写入当前设置（内部做一次拷贝，调用方后续改动不会影响全局）。</summary>
    /// <param name="settings">新设置。</param>
    public static void Apply(EditorSettingsData settings) => Current = settings.Clone();

    /// <summary>把基线步长钳到合法区间。</summary>
    /// <param name="value">原始值。</param>
    /// <returns>钳制后的步长。</param>
    public static int ClampBaselineStep(int value) =>
        Math.Clamp(value, EditorSettingsDefaults.BaselineStepMin, EditorSettingsDefaults.BaselineStepMax);

    /// <summary>把 Unity 口径的相对路径解析成绝对路径。</summary>
    /// <param name="path">路径，<c>Assets/…</c> 相对工程根，其余相对 Assets 目录；空值返回 null。</param>
    /// <param name="projectRoot">Unity 工程根（可为 null，此时只能解析绝对路径）。</param>
    /// <returns>绝对路径；无法解析时返回 null。</returns>
    public static string? ResolveProjectPath(string? path, string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return null;
        }

        // 与 Unity 一致：带 Assets/ 前缀的路径以工程根为基准，其余（含 ../）以 Assets 目录为基准
        var baseDirectory = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            ? projectRoot
            : Path.Combine(projectRoot, "Assets");

        return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
    }

    /// <summary>把绝对路径写回 Unity 口径的相对路径；不在工程内时保留绝对路径。</summary>
    /// <param name="absolutePath">绝对路径。</param>
    /// <param name="projectRoot">Unity 工程根（可为 null）。</param>
    /// <returns>可直接展示与落盘的路径文本。</returns>
    public static string MakeProjectRelative(string absolutePath, string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        var full = Path.GetFullPath(absolutePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return full.Replace('\\', '/');
        }

        var root = Path.GetFullPath(projectRoot);
        var assets = Path.Combine(root, "Assets");

        if (full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets/" + full[(assets.Length + 1)..].Replace('\\', '/');
        }

        if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return "../" + full[(root.Length + 1)..].Replace('\\', '/');
        }

        return full.Replace('\\', '/');
    }
}

#endregion
