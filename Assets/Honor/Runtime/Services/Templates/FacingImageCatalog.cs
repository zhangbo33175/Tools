/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacingImageCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   四方向出图索引：按模板 ID 查朝向 → 图片路径，供画布换图而不转图
 ***************************************************************/

using System;
using System.Collections.Generic;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 朝向出图索引。画布只认「模板 ID + 朝向键」，不持有模板集合，
/// 模板改动后由窗口侧重建一次即可，避免每个物件每帧去遍历模板列表。
/// </summary>
public sealed class FacingImageCatalog
{
    #region 常量

    /// <summary>空表实例：没有任何模板配置变体时复用，避免画布写空判断。</summary>
    public static FacingImageCatalog Empty { get; } = new();

    #endregion

    #region 字段

    /// <summary>模板 ID → （朝向键 → 图片路径）；键比较忽略大小写。</summary>
    private readonly Dictionary<string, Dictionary<string, string>> _byTemplate =
        new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 公开属性

    /// <summary>是否一个模板都没配置变体。</summary>
    public bool IsEmpty => _byTemplate.Count == 0;

    #endregion

    #region 公开方法

    /// <summary>用模板集合重建索引；没有变体的模板不入表。</summary>
    /// <param name="templates">元件模板集合。</param>
    public void Rebuild(IEnumerable<ObjectTemplate> templates)
    {
        _byTemplate.Clear();
        if (templates is null)
        {
            return;
        }

        foreach (var template in templates)
        {
            if (template is null
                || string.IsNullOrWhiteSpace(template.TemplateId)
                || !template.HasFacingVariants)
            {
                continue;
            }

            var variants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in template.FacingImages)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                variants[pair.Key.Trim().ToLowerInvariant()] = pair.Value;
            }

            if (variants.Count > 0)
            {
                _byTemplate[template.TemplateId] = variants;
            }
        }
    }

    /// <summary>该模板是否配置了朝向变体（决定「换图」还是「转图」）。</summary>
    /// <param name="templateId">模板 ID。</param>
    /// <returns>配置了任意方向图片时返回 true。</returns>
    public bool HasVariants(string templateId) =>
        !string.IsNullOrWhiteSpace(templateId) && _byTemplate.ContainsKey(templateId);

    /// <summary>按模板与朝向取图片路径；没有该方向时返回 null，由调用方回落到主图。</summary>
    /// <param name="templateId">模板 ID。</param>
    /// <param name="facing">朝向键，见 <see cref="Facings"/>。</param>
    /// <returns>变体图片路径；未配置时返回 null。</returns>
    public string? ImageFor(string templateId, string facing)
    {
        if (string.IsNullOrWhiteSpace(templateId)
            || string.IsNullOrWhiteSpace(facing)
            || !_byTemplate.TryGetValue(templateId, out var variants))
        {
            return null;
        }

        return variants.TryGetValue(facing.Trim().ToLowerInvariant(), out var path) ? path : null;
    }

    #endregion
}
