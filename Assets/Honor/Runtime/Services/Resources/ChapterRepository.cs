/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ChapterRepository.cs
 * author:    云毅
 * created:   2026
 * descrip:   章节表 / 主关卡表读取与「章节 → 关卡」目录模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>章节配置行（TableChapters 的 Chapters 工作表）。</summary>
public sealed class ChapterRow
{
    /// <summary>章节 ID（表里 ID 列，也是关卡文件名 <c>Map_{章节}m{关卡}</c> 的第一段）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>章节名（ChapterTitle，Unity 侧写入场景名的前缀）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>章节信息 ID 原始文本（ChapterInfoID，形如 <c>{"1","2"}</c>）。</summary>
    public string InfoIds { get; init; } = string.Empty;

    /// <summary>可解锁下一章关卡号（UnlockNextLevel，仅用于展示）。</summary>
    public int UnlockNextLevel { get; init; }

    /// <summary>地图资源名（MapName）。</summary>
    public string MapName { get; init; } = string.Empty;

    /// <summary>音乐名（MapMusic）。</summary>
    public string Music { get; init; } = string.Empty;

    /// <summary>本章关卡 ID 列表（由 ChapterInfoID 解析）。</summary>
    public IReadOnlyList<string> LevelIds { get; init; } = Array.Empty<string>();

    /// <summary>树里显示用的「第 N 章」序号（章节 ID 无法转数字时按顺序补）。</summary>
    public int Ordinal { get; init; }
}

/// <summary>主关卡配置行（TableMainLevels 的 MainLevels 工作表）。</summary>
public sealed class LevelRow
{
    /// <summary>关卡 ID（ID 列，与 ChapterInfoID 里的数字对应）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>关卡资源名（LevelID，形如 Main_1）。</summary>
    public string LevelId { get; init; } = string.Empty;

    /// <summary>生命预制体（ItemName）。</summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>地图名称（MapName）。</summary>
    public string MapName { get; init; } = string.Empty;

    /// <summary>关卡背景图资源名（Level_bg）。</summary>
    public string Background { get; init; } = string.Empty;

    /// <summary>寻物类型（FindType：1 填色 / 2 消除）。</summary>
    public string FindType { get; init; } = string.Empty;

    /// <summary>关卡体力（LevelLife）。</summary>
    public string Life { get; init; } = string.Empty;
}

/// <summary>
/// 章节 / 关卡目录：章节表决定「有哪些章、每章几个关」，主关卡表补关卡名与背景图。
/// Excel 缺失时按 Unity 的口径回退运行时 Lua 章节表（<c>TableChapters.lua.txt</c>）。
/// </summary>
public sealed class ChapterCatalog
{
    /// <summary>章节列表（按表内行序）。</summary>
    public List<ChapterRow> Chapters { get; } = new();

    /// <summary>主关卡列表（按表内行序）。</summary>
    public List<LevelRow> Levels { get; } = new();

    /// <summary>章节表实际读取的绝对路径。</summary>
    public string ChaptersPath { get; set; } = string.Empty;

    /// <summary>主关卡表实际读取的绝对路径。</summary>
    public string LevelsPath { get; set; } = string.Empty;

    /// <summary>数据来源说明（Excel / Lua 回退）。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>读取提示（表缺失、某列为空等；成功时为空）。</summary>
    public string? Error { get; set; }

    /// <summary>按关卡 ID 找主关卡行（先比对 ID，再比对 LevelID 的 <c>Main_N</c> 形式）。</summary>
    public LevelRow? FindLevel(string levelId) =>
        Levels.FirstOrDefault(level => level.Id == levelId)
        ?? Levels.FirstOrDefault(level => string.Equals(level.LevelId, $"Main_{levelId}", StringComparison.OrdinalIgnoreCase));
}

/// <summary>章节表 / 主关卡表的读取入口。</summary>
public static class ChapterRepository
{
    private const string LuaChaptersRelativePath = "Assets/LuaScripts/Game/Tables/AlTables/LevelConfigs/TableChapters.lua.txt";

    /// <summary>
    /// 按地图配置里的两条表路径读章节与关卡；表读不到时回退 Lua 章节表。
    /// </summary>
    /// <param name="projectRoot">Unity 工程根（用于解析相对路径与 Lua 回退）。</param>
    /// <returns>章节目录（失败时 Chapters 为空且 Error 有值）。</returns>
    public static ChapterCatalog Load(string? projectRoot)
    {
        var catalog = new ChapterCatalog();
        var settings = EditorSettings.Current;

        catalog.ChaptersPath = EditorSettings.ResolveProjectPath(settings.TableChaptersPath, projectRoot) ?? string.Empty;
        catalog.LevelsPath = EditorSettings.ResolveProjectPath(settings.TableMainLevelsPath, projectRoot) ?? string.Empty;

        var messages = new List<string>();

        var chapterSheet = XlsmTableReader.Read(catalog.ChaptersPath, "Chapters", out var chapterError);
        if (chapterSheet is null)
        {
            messages.Add($"章节表：{chapterError}");
        }
        else
        {
            ReadChapters(chapterSheet, catalog);
            catalog.Source = "Excel";
        }

        var levelSheet = XlsmTableReader.Read(catalog.LevelsPath, "MainLevels", out var levelError);
        if (levelSheet is null)
        {
            messages.Add($"主关卡表：{levelError}");
        }
        else
        {
            ReadLevels(levelSheet, catalog);
        }

        if (catalog.Chapters.Count == 0)
        {
            var luaChapters = LoadChaptersFromLua(projectRoot);
            if (luaChapters.Count > 0)
            {
                catalog.Chapters.AddRange(luaChapters);
                catalog.Source = "Lua 回退";
                messages.Add($"章节表 Excel 不可用，已回退运行时 Lua 章节表（Excel：{catalog.ChaptersPath}）");
            }
        }

        catalog.Error = messages.Count == 0 ? null : string.Join("；", messages);
        return catalog;
    }

    #region 工作表解析

    /// <summary>章节表：表头行找 <c>ChapterTitle</c>，数据行要求 ID 是数字。</summary>
    private static void ReadChapters(TableSheet sheet, ChapterCatalog catalog)
    {
        var (headerRow, columns) = LocateHeader(sheet, "ChapterTitle");
        if (headerRow < 0)
        {
            catalog.Error = "章节表缺少 ChapterTitle 表头";
            return;
        }

        var ordinal = 0;

        for (var rowIndex = headerRow + 1; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var cells = sheet.Rows[rowIndex];
            var id = Value(cells, columns, "ID");
            if (string.IsNullOrWhiteSpace(id) || !IsNumericId(id))
            {
                continue;
            }

            ordinal++;
            var infoIds = Value(cells, columns, "ChapterInfoID");
            var levelIds = ParseIdList(infoIds);
            if (levelIds.Count == 0)
            {
                levelIds.Add(id);
            }

            catalog.Chapters.Add(new ChapterRow
            {
                Id = id,
                Title = Value(cells, columns, "ChapterTitle"),
                InfoIds = infoIds,
                UnlockNextLevel = ParseInt(Value(cells, columns, "UnlockNextLevel")),
                MapName = Value(cells, columns, "MapName"),
                Music = Value(cells, columns, "MapMusic"),
                LevelIds = levelIds,
                Ordinal = ordinal,
            });
        }
    }

    /// <summary>主关卡表：表头行找 <c>LevelID</c>，数据行要求 ID 是数字且 LevelID 非空。</summary>
    private static void ReadLevels(TableSheet sheet, ChapterCatalog catalog)
    {
        var (headerRow, columns) = LocateHeader(sheet, "LevelID");
        if (headerRow < 0)
        {
            return;
        }

        for (var rowIndex = headerRow + 1; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var cells = sheet.Rows[rowIndex];
            var id = Value(cells, columns, "ID");
            var levelId = Value(cells, columns, "LevelID");
            if (string.IsNullOrWhiteSpace(id) || !IsNumericId(id) || string.IsNullOrWhiteSpace(levelId))
            {
                continue;
            }

            catalog.Levels.Add(new LevelRow
            {
                Id = id,
                LevelId = levelId,
                ItemName = Value(cells, columns, "ItemName"),
                MapName = Value(cells, columns, "MapName"),
                Background = Value(cells, columns, "Level_bg"),
                FindType = Value(cells, columns, "FindType"),
                Life = Value(cells, columns, "LevelLife"),
            });
        }
    }

    /// <summary>在前 8 行里找含指定表头的行，并把表头名映射到列号（表头名去空白后大小写不敏感）。</summary>
    /// <returns>表头行号（找不到为 -1）与列号字典。</returns>
    private static (int HeaderRow, Dictionary<string, int> Columns) LocateHeader(TableSheet sheet, string requiredColumn)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(8, sheet.Rows.Count); rowIndex++)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cells = sheet.Rows[rowIndex];
            for (var column = 0; column < cells.Count; column++)
            {
                var header = cells[column].Trim();
                if (header.Length > 0 && !columns.ContainsKey(header))
                {
                    columns[header] = column;
                }
            }

            if (columns.ContainsKey(requiredColumn))
            {
                return (rowIndex, columns);
            }
        }

        return (-1, new Dictionary<string, int>());
    }

    /// <summary>按表头名取单元格文本（列不存在时返回空串）。</summary>
    private static string Value(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index].Trim();
    }

    /// <summary>纯数字 ID（表里的类型行 / 中文标签行会被跳过）。</summary>
    private static bool IsNumericId(string text) => Regex.IsMatch(text, @"^\d+(\.0+)?$");

    /// <summary>解析整数（空 / 脏值按 0）。</summary>
    private static int ParseInt(string text) => int.TryParse(text, out var value) ? value : 0;

    /// <summary>从 <c>{"1","2"}</c> 或 <c>1,2</c> 里取全部数字 ID。</summary>
    private static List<string> ParseIdList(string text)
    {
        var ids = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return ids;
        }

        foreach (Match match in Regex.Matches(text, @"\d+"))
        {
            if (!ids.Contains(match.Value))
            {
                ids.Add(match.Value);
            }
        }

        return ids;
    }

    #endregion

    #region Lua 回退

    /// <summary>
    /// 解析运行时章节表：<c>["1"] = {ID = "1", ChapterTitle = "ChapterName1", ChapterInfoID = {"1"}, …}</c>。
    /// 关卡列表可能为空（旧表），此时按「一章一关」补 ID。
    /// </summary>
    private static List<ChapterRow> LoadChaptersFromLua(string? projectRoot)
    {
        var rows = new List<ChapterRow>();
        var path = projectRoot is null ? null : Path.Combine(projectRoot, LuaChaptersRelativePath);
        if (path is null || !File.Exists(path))
        {
            return rows;
        }

        var titlePattern = new Regex(@"ChapterTitle\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
        var infoPattern = new Regex(@"ChapterInfoID\s*=\s*\{(?<v>[^}]*)\}", RegexOptions.Compiled);
        var ordinal = 0;

        foreach (var line in FileSystemIo.ReadLines(path))
        {
            if (!Regex.IsMatch(line, @"\[""\d+""\]\s*="))
            {
                continue;
            }

            var idMatch = Regex.Match(line, @"ID\s*=\s*""(?<id>[^""]*)""");
            if (!idMatch.Success)
            {
                continue;
            }

            ordinal++;
            var title = titlePattern.Match(line) is { Success: true } titleMatch ? titleMatch.Groups["v"].Value : string.Empty;
            var info = infoPattern.Match(line) is { Success: true } infoMatch ? infoMatch.Groups["v"].Value : string.Empty;
            var levelIds = ParseIdList(info);
            if (levelIds.Count == 0)
            {
                levelIds.Add(idMatch.Groups["id"].Value);
            }

            rows.Add(new ChapterRow
            {
                Id = idMatch.Groups["id"].Value,
                Title = title,
                InfoIds = info,
                LevelIds = levelIds,
                Ordinal = ordinal,
            });
        }

        return rows;
    }

    #endregion
}
