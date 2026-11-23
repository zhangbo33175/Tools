/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableTextFormat.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表文本的统一格式口径：紧凑数值与 CSV 单元格转义
 ***************************************************************/

using System.Globalization;

namespace MapEditorTool.Services;

/// <summary>
/// 配置表读写共用的文本格式化。所有 CSV / xlsm / UI 回填都走同一套数值与转义口径，
/// 避免各表实现各自维护小数位与引号规则。
/// </summary>
internal static class TableTextFormat
{
    #region 常量

    /// <summary>紧凑数值格式：最多保留 3 位小数，去掉尾随 0。</summary>
    private const string CompactNumberFormat = "0.###";

    /// <summary>CSV 需要加引号的字符集合。</summary>
    private static readonly char[] CsvEscapeChars = { ',', '"', '\r', '\n' };

    #endregion

    #region 公开方法

    /// <summary>按配置表口径格式化浮点数。</summary>
    /// <param name="value">数值。</param>
    /// <returns>不含千分位、最多 3 位小数的文本。</returns>
    internal static string Number(double value) =>
        value.ToString(CompactNumberFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 按 CSV 规则转义单元格：包含逗号、双引号或换行时加双引号，内部双引号翻倍。
    /// </summary>
    /// <param name="value">原始单元格文本。</param>
    /// <returns>可直接拼入 CSV 行的文本。</returns>
    internal static string CsvEscape(string? value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny(CsvEscapeChars) < 0
            ? text
            : "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    #endregion
}
