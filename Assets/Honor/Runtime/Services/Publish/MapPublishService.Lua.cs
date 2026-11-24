/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Lua.cs
 * author:    云毅
 * created:   2026
 * descrip:   Lua 格式辅助：文件头、转义、数值/布尔与数组字面量
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MapEditorTool.Services;

public static partial class MapPublishService
{

    #region 格式辅助

    private static void AppendLuaHeader(StringBuilder sb, string tableName, string description, string brief, string? detail = null)
    {
        sb.AppendLine(LuaBanner);
        sb.AppendLine("-- (c) copyright 2026 - 2030, Honor.Runtime");
        sb.AppendLine("-- All Rights Reserved.");
        sb.AppendLine("-- ----------------------------------------------------------------------------------------------------");
        sb.AppendLine($"-- filename:  {tableName}");
        sb.AppendLine($"-- descrip:   {description}");
        sb.AppendLine($"-- notices:   {LuaNotice}");
        sb.AppendLine($"-- @brief     {brief}");
        if (!string.IsNullOrEmpty(detail))
        {
            sb.AppendLine($"-- @desc      {detail}");
        }

        sb.AppendLine(LuaBanner);
        sb.AppendLine();
    }

    private static string StripTablePrefix(string baseName)
        => baseName.StartsWith("Table", StringComparison.Ordinal) ? baseName[5..] : baseName;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            switch (character)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;

                case '"':
                    sb.Append("\\\"");
                    break;

                default:
                    sb.Append(character);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Num(double value) => value.ToString(LuaNumberFormat, CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string LuaStringArray(IEnumerable<string> values)
    {
        var parts = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => "\"" + Escape(value) + "\"")
            .ToList();
        return parts.Count == 0 ? "{}" : "{ " + string.Join(", ", parts) + " }";
    }

    private static string NumericOr(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? fallback : digits;
    }

    private static int ParseInt(string value) => int.TryParse(value, out var number) ? number : 1;

    #endregion
}
