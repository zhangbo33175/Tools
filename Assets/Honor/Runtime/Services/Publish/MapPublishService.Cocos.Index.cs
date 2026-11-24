/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapPublishService.Cocos.Index.cs
 * author:    云毅
 * created:   2026
 * descrip:   Cocos 章节索引：创建、合并与关卡行排序
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
/// <summary>Cocos 章节索引的创建、合并与关卡行输出。</summary>
public static partial class MapPublishService
{
    #region Cocos 章节索引

    /// <summary>
    /// 构建章节索引：文件不存在时创建，存在时在原文件中合并当前关卡。
    /// </summary>
    /// <param name="path">索引文件绝对路径。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="level">当前关卡 ID。</param>
    /// <returns>完整索引文件内容。</returns>
    private static string BuildCocosIndex(string path, string chapter, string chapterName, string level)
    {
        if (!File.Exists(path))
        {
            return CreateCocosIndex(chapter, chapterName, level);
        }

        return MergeCocosIndex(FileSystemIo.ReadText(path), chapter, chapterName, level);
    }

    /// <summary>创建全新的章节索引内容。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="level">首个关卡 ID。</param>
    /// <returns>索引文件内容。</returns>
    private static string CreateCocosIndex(string chapter, string chapterName, string level)
    {
        var sb = new StringBuilder();
        AppendLuaHeader(sb, LevelIndexFileStem, "章节地图索引（自动生成）", "章节地图数据索引");
        sb.AppendLine($"---@class Tables.{LevelIndexTableName} @章节地图数据索引");
        sb.AppendLine($"Tables.{LevelIndexTableName} = {{");
        sb.AppendLine($"    {BuildIndexLine(chapter, chapterName, new List<string> { level })}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 在已有索引中合并章节：保留原 ChapterName 与 Levels，追加当前关卡后去重，
    /// 关卡 ID 能转数字时按数字排序，否则按字符串排序。
    /// </summary>
    /// <param name="original">已有索引全文。</param>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名（仅新建条目时使用）。</param>
    /// <param name="level">当前关卡 ID。</param>
    /// <returns>合并后的索引全文。</returns>
    private static string MergeCocosIndex(string original, string chapter, string chapterName, string level)
    {
        var newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var lines = original.Replace("\r\n", "\n").Split('\n');
        var targetPrefix = $"[\"{chapter}\"]";
        var targetIndex = -1;
        string? existingName = null;
        var levels = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains(targetPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            targetIndex = index;
            var nameMatch = Regex.Match(lines[index], "ChapterName\\s*=\\s*\"((?:\\\\.|[^\"])*)\"");
            if (nameMatch.Success)
            {
                existingName = nameMatch.Groups[1].Value;
            }

            var levelsMatch = Regex.Match(lines[index], "Levels\\s*=\\s*\\{([^}]*)\\}");
            if (levelsMatch.Success)
            {
                foreach (Match levelMatch in Regex.Matches(levelsMatch.Groups[1].Value, "\"((?:\\\\.|[^\"])*)\""))
                {
                    levels.Add(levelMatch.Groups[1].Value);
                }
            }

            break;
        }

        levels.Add(level);
        var merged = levels
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => int.TryParse(value, out var number) ? number : int.MaxValue)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToList();
        var line = BuildIndexLine(chapter, existingName ?? chapterName, merged);

        if (targetIndex >= 0)
        {
            lines[targetIndex] = "    " + line;
        }
        else
        {
            var closeIndex = Array.LastIndexOf(lines, "}");
            if (closeIndex < 0)
            {
                lines = lines.Append($"Tables.{LevelIndexTableName} = {{").Append("}").ToArray();
                closeIndex = lines.Length - 1;
            }

            var mergedLines = lines.Take(closeIndex).Concat(new[] { "    " + line }).Concat(lines.Skip(closeIndex)).ToArray();
            lines = mergedLines;
        }

        return string.Join(newline, lines);
    }

    /// <summary>构建单章节索引行，供新建与合并共用。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <param name="chapterName">章节显示名。</param>
    /// <param name="levels">已排序去重的关卡列表。</param>
    /// <returns>一行 Lua 表项。</returns>
    private static string BuildIndexLine(string chapter, string chapterName, IReadOnlyList<string> levels)
    {
        var quotedLevels = string.Join(", ", levels.Select(value => "\"" + Escape(value) + "\""));
        return $"[\"{Escape(chapter)}\"] = {{ ChapterId = \"{Escape(chapter)}\", " +
               $"ChapterName = \"{Escape(chapterName)}\", LevelCount = {levels.Count}, " +
               $"Levels = {{ {quotedLevels} }} }},";
    }

    #endregion
}

