/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  JsonFileOptions.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器 JSON 落盘选项：缩进、中文直出与空值忽略档位
 ***************************************************************/

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapEditorTool.Services;

/// <summary>
/// 编辑器 JSON 文件选项。所有落盘配置共用同一组档位，
/// 避免每处自行组合 <c>WriteIndented</c> / 中文转义 / 空值策略。
/// </summary>
public static class JsonFileOptions
{
    #region 档位

    /// <summary>缩进输出，保留默认 JSON 转义；适合纯 ASCII 配置。</summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
    };

    /// <summary>缩进输出，中文等非 ASCII 字符直出；适合需要人工阅读的配置。</summary>
    public static readonly JsonSerializerOptions Relaxed = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>缩进 + 中文直出 + 跳过 null 字段；用于运行时地图 JSON。</summary>
    public static readonly JsonSerializerOptions RelaxedIgnoreNull = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #endregion
}
