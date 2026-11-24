/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NineSliceCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   九宫格配置目录：按素材名给画布提供 NineSliceSettings（与配置表解耦）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 九宫格配置目录。表行（xlsm）没有九宫格列时，配置落在 output/Config/NineSliceConfig.json，
/// 运行时按模板 ID / 素材名 / 图片名多键登记，画布取到任意一个别名即可命中。
/// </summary>
public sealed class NineSliceCatalog
{
    #region 字段

    private readonly Dictionary<string, NineSliceSettings> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 公开属性

    /// <summary>登记条目数（按别名去重前的键数量）。</summary>
    public int Count => _entries.Count;

    #endregion

    #region 公开方法

    /// <summary>把模板的九宫格配置登记进目录；未启用且边距全 0 的模板不登记，避免把默认值铺满整张地图。</summary>
    /// <param name="template">模板。</param>
    public void Register(ObjectTemplate template)
    {
        Register(template.TemplateId, template.ResName, template.ImagePath, template.TemplateName, template.NineSlice);
    }

    /// <summary>按显式键登记一份九宫格配置。</summary>
    /// <param name="templateId">模板 ID，可为空。</param>
    /// <param name="resName">素材资源名，可为空。</param>
    /// <param name="imagePath">图片名 / 路径，可为空。</param>
    /// <param name="templateName">模板显示名，可为空。</param>
    /// <param name="settings">九宫格配置；null 表示不登记。</param>
    public void Register(
        string? templateId,
        string? resName,
        string? imagePath,
        string? templateName,
        NineSliceSettings? settings)
    {
        if (settings is null || !settings.Enabled)
        {
            return;
        }

        var copy = settings.Clone();
        foreach (var key in EnumerateKeys(templateId, resName, imagePath, templateName))
        {
            _entries[key] = copy;
        }
    }

    /// <summary>按实例上的任一别名查九宫格配置。</summary>
    /// <param name="templateId">实例引用的模板 ID。</param>
    /// <param name="imagePath">实例图片名 / 路径。</param>
    /// <param name="name">实例显示名。</param>
    /// <returns>命中返回配置，否则 null。</returns>
    public NineSliceSettings? Find(string? templateId, string? imagePath, string? name)
    {
        foreach (var key in EnumerateKeys(templateId, null, imagePath, name))
        {
            if (_entries.TryGetValue(key, out var settings))
            {
                return settings;
            }
        }

        return null;
    }

    /// <summary>枚举同一份配置的所有可用键：模板 ID、素材名、图片名、模板名（均去扩展名并小写）。</summary>
    /// <param name="templateId">模板 ID。</param>
    /// <param name="resName">素材资源名。</param>
    /// <param name="imagePath">图片名 / 路径。</param>
    /// <param name="templateName">模板显示名。</param>
    /// <returns>非空键序列。</returns>
    public static IEnumerable<string> EnumerateKeys(
        string? templateId,
        string? resName,
        string? imagePath,
        string? templateName)
    {
        foreach (var candidate in new[] { templateId, resName, imagePath, templateName })
        {
            var key = KeyOf(candidate);
            if (key.Length > 0)
            {
                yield return key;
            }
        }
    }

    /// <summary>把模板 ID / 素材名 / 图片路径统一成目录键：去目录、去扩展名、去空白、转小写。</summary>
    /// <param name="value">原始值。</param>
    /// <returns>目录键；空值返回空串。</returns>
    public static string KeyOf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Replace('\\', '/');
        var name = Path.GetFileNameWithoutExtension(trimmed);
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().ToLowerInvariant();
    }

    #endregion
}
