/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableTextFormat.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置表文本的统一格式口径：紧凑数值与 CSV 单元格转义
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MapEditorTool.Services;

/// <summary>
/// 配置表读写共用的文本格式化与解析入口。所有 CSV / xlsm / UI 回填都走同一套布尔、整数、数值口径，
/// 避免各表实现各自维护小数位、默认值与真假写法。
/// </summary>
internal static class TableTextFormat
{
    #region 常量

    /// <summary>紧凑数值格式：最多保留 3 位小数，去掉尾随 0。</summary>
    private const string CompactNumberFormat = "0.###";

    /// <summary>CSV 需要加引号的字符集合。</summary>
    private static readonly char[] CsvEscapeChars = { ',', '"', '\r', '\n' };

    #endregion

    #region 格式化

    /// <summary>按配置表口径格式化浮点数。</summary>
    /// <param name="value">数值。</param>
    /// <returns>不含千分位、最多 3 位小数的文本。</returns>
    internal static string Number(double value) =>
        value.ToString(CompactNumberFormat, CultureInfo.InvariantCulture);

    /// <summary>按配置表口径格式化整数。</summary>
    /// <param name="value">整数。</param>
    /// <returns>不含千分位的文本。</returns>
    internal static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>按配置表口径格式化布尔值：<c>1</c> 真、<c>0</c> 假。</summary>
    /// <param name="value">布尔值。</param>
    /// <returns><c>1</c> 或 <c>0</c>。</returns>
    internal static string Flag(bool value) => value ? "1" : "0";

    /// <summary>按 Excel 配置表口径格式化布尔值：<c>True</c> / <c>False</c>。</summary>
    /// <param name="value">布尔值。</param>
    /// <returns><c>True</c> 或 <c>False</c>。</returns>
    internal static string ExcelBool(bool value) => value ? "True" : "False";

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

    #region 解析

    /// <summary>
    /// 拆分 CSV 数据行：支持双引号包裹、引号内逗号与 <c>""</c> 转义。
    /// 配置表读取与模板仓库共用，避免各自维护一套切分规则。
    /// </summary>
    /// <param name="line">CSV 行文本。</param>
    /// <returns>单元格集合。</returns>
    internal static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                cells.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        cells.Add(builder.ToString());
        return cells;
    }

    /// <summary>解析布尔单元格：兼容 <c>true/false</c> 与 <c>1/0</c>，空值或非法值回退默认。</summary>
    /// <param name="raw">原始单元格文本。</param>
    /// <param name="fallback">无法识别时的回退值。</param>
    /// <returns>解析结果。</returns>
    internal static bool ParseBool(string? raw, bool fallback)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (bool.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return text switch
        {
            "1" => true,
            "0" => false,
            _ => fallback,
        };
    }

    /// <summary>解析整数单元格，空值或非法值回退默认。</summary>    /// <param name="raw">原始单元格文本。</param>
    /// <param name="fallback">无法识别时的回退值。</param>
    /// <returns>解析结果。</returns>
    internal static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>解析浮点单元格，空值或非法值回退默认。</summary>
    /// <param name="raw">原始单元格文本。</param>
    /// <param name="fallback">无法识别时的回退值。</param>
    /// <returns>解析结果。</returns>
    internal static double ParseNumber(string? raw, double fallback) =>
        double.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    #endregion
}
