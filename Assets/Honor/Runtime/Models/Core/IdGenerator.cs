/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IdGenerator.cs
 * author:    云毅
 * created:   2026
 * descrip:   短 ID 生成：统一地图实例、交通对象与临时文件命名规则
 ***************************************************************/

using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace MapEditorTool.Models;

/// <summary>
/// 短 ID 规则。地图物件、交通灯、横道、迷雾区域与临时文件统一用 8 位十进制数字，
/// 全部落在这里，避免各处硬编码长度或格式。
/// </summary>
public static class IdGenerator
{
    /// <summary>短 ID 长度。</summary>
    public const int ShortLength = 8;

    /// <summary>8 位十进制短 ID 的下界：首位非零，保证长度恒为 8。</summary>
    public const int NumericMin = 10000000;

    /// <summary>8 位十进制短 ID 的取值个数。</summary>
    public const int NumericRange = 90000000;

    /// <summary>生成一个新的 8 位数字短 ID，例如 <c>40319827</c>。</summary>
    /// <returns>8 位十进制字符串；调用方仍需按现有集合查重。</returns>
    public static string NewShortId() => NewNumericId();

    /// <summary>生成 8 位十进制短 ID：随机取 <c>10000000..99999999</c>，不会产生前导零。</summary>
    /// <returns>8 位数字字符串。</returns>
    public static string NewNumericId()
    {
        var value = NumericMin + RandomNumberGenerator.GetInt32(0, NumericRange);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>判断一串短 ID 是否已经是 8 位十进制数字（旧存档是十六进制，需要迁移）。</summary>
    /// <param name="id">待判断的短 ID。</param>
    /// <returns>true = 已是 8 位数字。</returns>
    public static bool IsNumericId(string? id)
    {
        var text = (id ?? string.Empty).Trim();
        return text.Length == ShortLength && text[0] != '0' && text.All(char.IsDigit);
    }
}
