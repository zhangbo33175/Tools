/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemCsvTableIO.cs
 * author:    云毅
 * created:   2026
 * descrip:   CSV 配置表实现：读取、空表创建、单元格拆分与写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MapEditorTool.Services;
#region CSV

/// <summary>CSV 配置表的读取（表头 1–2 行 + 数据行）。</summary>
internal static class CsvTableReader
{
    public static ItemTableFile? Read(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0)
            {
                return null;
            }

            var headerIndex = FindHeaderRow(lines);
            if (headerIndex < 0)
            {
                return null;
            }

            var file = new ItemTableFile(path, ItemTableKind.Csv);
            var headerCells = SplitCells(lines[headerIndex]);
            file.ColumnNames.AddRange(headerCells.Select(cell => cell.Trim()));
            ItemRowMapper.BuildColumnIndex(file.ColumnNames, file.ColumnIndex);
            if (file.ColumnIndex.Count == 0)
            {
                return null;
            }

            var dataStart = headerIndex + 1;
            // 紧跟表头的类型行（string,int,bool…）不算数据
            while (dataStart < lines.Length
                   && ItemRowMapper.LooksLikeTypeRow(SplitCells(lines[dataStart])))
            {
                dataStart++;
            }

            for (var index = headerIndex; index < dataStart; index++)
            {
                file.HeaderLines.Add(lines[index]);
            }

            for (var index = dataStart; index < lines.Length; index++)
            {
                var cells = SplitCells(lines[index]);
                if (cells.Count == 0 || cells.All(cell => string.IsNullOrWhiteSpace(cell)))
                {
                    continue;
                }

                while (cells.Count < file.ColumnNames.Count)
                {
                    cells.Add(string.Empty);
                }

                file.Rows.Add(ItemRowMapper.Bind(cells, file.ColumnIndex, index + 1));
            }

            return file;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>新建一张空 CSV（列名 / 类型两行表头）。</summary>
    public static ItemTableFile CreateEmpty(string path, IReadOnlyList<string> columns, IReadOnlyList<string> types)
    {
        var file = new ItemTableFile(path, ItemTableKind.Csv);
        file.ColumnNames.AddRange(columns);
        file.HeaderLines.Add(string.Join(",", columns.Select(Escape)));
        file.HeaderLines.Add(string.Join(",", types.Select(Escape)));
        ItemRowMapper.BuildColumnIndex(file.ColumnNames, file.ColumnIndex);
        file.Dirty = true;
        return file;
    }

    private static int FindHeaderRow(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < Math.Min(lines.Count, 12); index++)
        {
            var cells = SplitCells(lines[index]);
            if (cells.Any(cell =>
                    cell.Trim().Equals("Icon", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("ImagePath", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("图片名称", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("模板名称", StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    internal static List<string> SplitCells(string line)
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

    /// <summary>按统一 CSV 口径转义单元格。</summary>
    /// <param name="value">原始文本。</param>
    /// <returns>可拼入 CSV 行的文本。</returns>
    internal static string Escape(string value) => TableTextFormat.CsvEscape(value);
}

/// <summary>CSV 配置表的写回（表头原样 + 数据行按列回写）。</summary>
internal static class CsvTableWriter
{
    public static bool Write(ItemTableFile file, out string error)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var header in file.HeaderLines)
            {
                builder.Append(header).Append("\r\n");
            }

            foreach (var row in file.Rows)
            {
                file.SyncCells(row);
                builder.Append(string.Join(",", row.Cells.Select(CsvTableReader.Escape))).Append("\r\n");
            }

            var directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            File.WriteAllText(file.Path, builder.ToString(), new UTF8Encoding(false));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}

#endregion
