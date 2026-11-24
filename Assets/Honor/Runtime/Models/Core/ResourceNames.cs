/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceNames.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源文件名归一：去路径、去扩展名、去尾下划线并统一小写
 ***************************************************************/

using System.IO;

namespace MapEditorTool.Models;

/// <summary>
/// 资源文件名归一规则。配置表索引、面板匹配与分类识别共用同一入口，
/// 统一处理路径、扩展名、首尾空格与结尾下划线，避免各处比较口径不一致。
/// </summary>
public static class ResourceNames
{
    /// <summary>取文件名主体：去目录、去扩展名、去首尾空格并转小写。</summary>
    /// <param name="name">文件名或含目录的路径。</param>
    /// <returns>小写文件名主体；空输入返回空串。</returns>
    public static string Stem(string? name) =>
        Path.GetFileNameWithoutExtension(name?.Trim() ?? string.Empty).ToLowerInvariant();

    /// <summary>
    /// 取用于匹配的归一键：在 <see cref="Stem"/> 基础上去掉结尾下划线。
    /// 资源导出时偶尔会多出尾下划线，匹配时应视为同一资源。
    /// </summary>
    /// <param name="name">文件名或含目录的路径。</param>
    /// <returns>小写、无扩展名、无尾下划线的键。</returns>
    public static string NormalizeKey(string? name) => Stem(name).TrimEnd('_');

    /// <summary>
    /// 比较两个资源名是否指向同一资源。
    /// 任一方归一后为空都返回 false，避免两个空值被误判为匹配。
    /// </summary>
    /// <param name="left">左侧文件名或路径。</param>
    /// <param name="right">右侧文件名或路径。</param>
    /// <returns>指向同一资源返回 true。</returns>
    public static bool Matches(string? left, string? right)
    {
        var key = NormalizeKey(left);
        return key.Length > 0 && key == NormalizeKey(right);
    }

    /// <summary>
    /// 比较一个资源名是否命中任意候选名。
    /// 用于模板的 <c>ImagePath / ResName / TemplateName</c> 多字段匹配，统一空值与归一规则。
    /// </summary>
    /// <param name="value">待比较的资源名或路径。</param>
    /// <param name="candidates">候选资源名集合。</param>
    /// <returns>命中任意一个返回 true。</returns>
    public static bool MatchesAny(string? value, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (Matches(value, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
