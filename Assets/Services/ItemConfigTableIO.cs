/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigTableIO.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图物件配置表的 CSV / xlsm 读取与写回
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

#region 行数据映射

/// <summary>把「列名 → 单元格」的表行映射成 <see cref="ItemElementRow"/>。</summary>
internal static class ItemRowMapper
{
    /// <summary>按列名表建立字段 → 列下标映射。</summary>
    /// <param name="columnNames">列名（按列序）。</param>
    /// <param name="target">目标映射表。</summary>
    public static void BuildColumnIndex(IReadOnlyList<string> columnNames, Dictionary<string, int> target)
    {
        target.Clear();
        foreach (var pair in ItemFieldNames.Aliases)
        {
            for (var index = 0; index < columnNames.Count; index++)
            {
                var name = columnNames[index].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (pair.Value.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    target.TryAdd(pair.Key, index);
                    break;
                }
            }
        }
    }

    /// <summary>把一行单元格映射成行模型。</summary>
    /// <param name="cells">单元格（按列序）。</param>
    /// <param name="columns">字段 → 列下标。</param>
    /// <param name="rowNumber">行号。</param>
    /// <returns>行模型。</returns>
    public static ItemElementRow Bind(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, int rowNumber)
    {
        var row = new ItemElementRow { RowNumber = rowNumber };
        foreach (var cell in cells)
        {
            row.Cells.Add(cell);
        }

        row.Id = Get(cells, columns, ItemFieldNames.Id);
        row.Icon = Get(cells, columns, ItemFieldNames.Icon);
        row.ResName = Get(cells, columns, ItemFieldNames.ResName);
        row.TemplateName = Get(cells, columns, ItemFieldNames.TemplateName);
        row.AnimName = Get(cells, columns, ItemFieldNames.AnimName) is { Length: > 0 } anim ? anim : "Idle";
        row.Loop = ParseBool(Get(cells, columns, ItemFieldNames.Loop), true);
        row.Speed = ParseNumber(Get(cells, columns, ItemFieldNames.Speed), 1.0);
        row.Interactable = ParseBool(Get(cells, columns, ItemFieldNames.Interactable), true);
        row.InteractType = Get(cells, columns, ItemFieldNames.InteractType);
        row.SortOrder = (int)ParseNumber(Get(cells, columns, ItemFieldNames.SortOrder), 0);
        row.CollisionEnabled = ParseBool(Get(cells, columns, ItemFieldNames.Collision), false);
        row.DialogBubble = Get(cells, columns, ItemFieldNames.DialogBubble);
        row.FacingDirection = Get(cells, columns, ItemFieldNames.FacingDirection);
        row.DisplayLayer = Get(cells, columns, ItemFieldNames.DisplayLayer);
        row.ShadowEnabled = ParseBool(Get(cells, columns, ItemFieldNames.Shadow), true);
        row.IsCollectible = ParseBool(Get(cells, columns, ItemFieldNames.Collectible), false);
        row.RespawnTime = ParseNumber(Get(cells, columns, ItemFieldNames.RespawnTime), 0);
        row.TrafficCategory = Get(cells, columns, ItemFieldNames.TrafficCategory) is { Length: > 0 } traffic ? traffic : "none";
        row.YSortEnabled = ParseBool(Get(cells, columns, ItemFieldNames.YSortEnabled), true);
        row.YSortOffset = ParseNumber(Get(cells, columns, ItemFieldNames.YSortOffset), 0);
        row.IsMovable = ParseBool(Get(cells, columns, ItemFieldNames.Movable), false);
        row.LoopRestartDelay = ParseNumber(Get(cells, columns, ItemFieldNames.LoopRestartDelay), 0);
        row.HideWhenOffMap = ParseBool(Get(cells, columns, ItemFieldNames.HideWhenOffMap), true);
        row.IsTeleport = ParseBool(Get(cells, columns, ItemFieldNames.Teleport), false);
        row.TeleportTargetScene = Get(cells, columns, ItemFieldNames.TeleportScene);
        row.TeleportTargetX = ParseNumber(Get(cells, columns, ItemFieldNames.TeleportX), 0);
        row.TeleportTargetY = ParseNumber(Get(cells, columns, ItemFieldNames.TeleportY), 0);
        row.NineSliceEnabled = ParseBool(Get(cells, columns, ItemFieldNames.NineSliceEnabled), false);
        row.NineSliceLeft = (int)ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceLeft), 0);
        row.NineSliceRight = (int)ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceRight), 0);
        row.NineSliceTop = (int)ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceTop), 0);
        row.NineSliceBottom = (int)ParseNumber(Get(cells, columns, ItemFieldNames.NineSliceBottom), 0);
        row.NineSliceCenterMode = Get(cells, columns, ItemFieldNames.NineSliceCenterMode);
        return row;
    }

    /// <summary>该行看起来是不是「类型 / 备注」表头行。</summary>
    /// <param name="cells">单元格。</param>
    /// <returns>是表头行返回 true。</param>
    public static bool LooksLikeTypeRow(IReadOnlyList<string> cells)
    {
        var nonEmpty = cells.Where(cell => !string.IsNullOrWhiteSpace(cell)).ToList();
        if (nonEmpty.Count == 0)
        {
            return true;
        }

        return nonEmpty.Take(6).Any(cell => ItemFieldNames.TypeTokens.Contains(cell.Trim()));
    }

    private static string Get(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> columns, string field)
    {
        if (!columns.TryGetValue(field, out var index) || index < 0 || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index].Trim();
    }

    private static bool ParseBool(string raw, bool fallback) => bool.TryParse(raw, out var parsed)
        ? parsed
        : raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => fallback,
        };

    private static double ParseNumber(string raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

#endregion

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

    internal static string Escape(string value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
            ? text
            : '"' + text.Replace("\"", "\"\"") + '"';
    }
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

#region XLSM

/// <summary>xlsm 配置表的读取（复用章节表用的零依赖 OOXML 读取器）。</summary>
internal static class ItemXlsmReader
{
    /// <summary>读一张 xlsm 的前 12 行找列名行，之后的数据行映射成行模型。</summary>
    /// <param name="path">文件绝对路径。</param>
    /// <returns>表；读不到返回 null。</returns>
    public static ItemTableFile? Read(string path)
    {
        var sheet = XlsmTableReader.Read(path, null, out _);
        if (sheet is null || sheet.Rows.Count == 0)
        {
            return null;
        }

        var headerIndex = -1;
        var headerWidth = 0;
        for (var index = 0; index < Math.Min(sheet.Rows.Count, 12); index++)
        {
            var cells = sheet.Rows[index];
            if (cells.Any(cell =>
                    cell.Trim().Equals("Icon", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("ImagePath", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("TemplateId", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("模板名称", StringComparison.OrdinalIgnoreCase)
                    || cell.Trim().Equals("图片名称", StringComparison.OrdinalIgnoreCase)))
            {
                headerIndex = index;
                headerWidth = cells.Count;
                break;
            }
        }

        if (headerIndex < 0)
        {
            return null;
        }

        var file = new ItemTableFile(path, ItemTableKind.Xlsm) { HeaderRowNumber = headerIndex + 1 };
        for (var column = 0; column < headerWidth; column++)
        {
            var cells = sheet.Rows[headerIndex];
            file.ColumnNames.Add(column < cells.Count ? cells[column].Trim() : string.Empty);
        }

        ItemRowMapper.BuildColumnIndex(file.ColumnNames, file.ColumnIndex);
        if (file.ColumnIndex.Count == 0)
        {
            return null;
        }

        for (var index = headerIndex + 1; index < sheet.Rows.Count; index++)
        {
            var cells = new List<string>(sheet.Rows[index]);
            if (cells.Count == 0 || ItemRowMapper.LooksLikeTypeRow(cells))
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
}

/// <summary>xlsm 配置表的写回（只改管理列的单元格，样式与其余内容原样保留）。</summary>
internal static class ItemXlsmWriter
{
    private const string SheetEntry = "xl/worksheets/sheet1.xml";
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>写回一张 xlsm：逐行改管理列，重建 zip 时其余条目原样拷贝。</summary>
    /// <param name="file">表。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>是否成功。</returns>
    public static bool Write(ItemTableFile file, out string error)
    {
        try
        {
            XDocument document;
            using (var archive = ZipFile.OpenRead(file.Path))
            {
                var entry = archive.GetEntry(SheetEntry);
                if (entry is null)
                {
                    error = "xlsm 内找不到 sheet1.xml";
                    return false;
                }

                using var stream = entry.Open();
                document = XDocument.Load(stream);
            }

            var sheetData = document.Descendants(Ns + "sheetData").FirstOrDefault();
            if (sheetData is null)
            {
                error = "工作表缺少 sheetData";
                return false;
            }

            var fieldByIndex = new Dictionary<int, string>();
            foreach (var pair in file.ColumnIndex)
            {
                fieldByIndex.TryAdd(pair.Value, pair.Key);
            }

            foreach (var row in file.Rows)
            {
                file.SyncCells(row);
                foreach (var pair in fieldByIndex)
                {
                    var value = pair.Key < row.Cells.Count ? row.Cells[pair.Key] : string.Empty;
                    SetCell(sheetData, row.RowNumber, pair.Key + 1, value, ItemFieldNames.NumericFields.Contains(pair.Value));
                }
            }

            var temp = file.Path + ".tmp";
            File.Copy(file.Path, temp, true);
            using (var archive = ZipFile.Open(temp, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry(SheetEntry);
                entry?.Delete();
                var created = archive.CreateEntry(SheetEntry, CompressionLevel.Optimal);
                using var stream = created.Open();
                document.Save(stream);
            }

            File.Move(temp, file.Path, true);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void SetCell(XElement sheetData, int rowNumber, int column, string value, bool numeric)
    {
        var row = sheetData.Elements(Ns + "row")
            .FirstOrDefault(element => ((int?)element.Attribute("r") ?? 0) == rowNumber);
        if (row is null)
        {
            row = new XElement(Ns + "row", new XAttribute("r", rowNumber));
            var next = sheetData.Elements(Ns + "row")
                .FirstOrDefault(element => ((int?)element.Attribute("r") ?? 0) > rowNumber);
            if (next is null)
            {
                sheetData.Add(row);
            }
            else
            {
                next.AddBeforeSelf(row);
            }
        }

        var reference = ColumnName(column) + rowNumber;
        var cell = row.Elements(Ns + "c").FirstOrDefault(element => (string?)element.Attribute("r") == reference);

        // Unity 侧的表把 bool 存成 1/0（无 t 属性的数值单元格），文本列才是共享字符串；
        // 回写时沿用这一口径，避免把 True/False 写成文本让 Unity 解析不了。
        var numericCell = numeric || (cell is not null && (string?)cell.Attribute("t") is null);
        if (cell is null)
        {
            cell = new XElement(Ns + "c", new XAttribute("r", reference));
            var style = FindStyle(sheetData, rowNumber - 1, column);
            if (style is not null)
            {
                cell.Add(new XAttribute("s", style));
            }

            var next = row.Elements(Ns + "c")
                .FirstOrDefault(element => ColumnNumber((string?)element.Attribute("r") ?? string.Empty) > column);
            if (next is null)
            {
                row.Add(cell);
            }
            else
            {
                next.AddBeforeSelf(cell);
            }

            if (!numeric && FindPreviousCellNumeric(sheetData, rowNumber - 1, column) == true)
            {
                numericCell = true;
            }
        }

        cell.Elements().Where(element => element.Name == Ns + "v" || element.Name == Ns + "is").Remove();
        if (numericCell && TryParseCellNumber(value, out var number))
        {
            cell.Attribute("t")?.Remove();
            cell.Add(new XElement(Ns + "v", number.ToString("0.###", CultureInfo.InvariantCulture)));
            return;
        }

        cell.SetAttributeValue("t", "inlineStr");
        var text = new XElement(Ns + "t", value ?? string.Empty);
        if ((value ?? string.Empty).Length > 0 && (value![0] == ' ' || value[^1] == ' '))
        {
            text.SetAttributeValue(Xml + "space", "preserve");
        }

        cell.Add(new XElement(Ns + "is", text));
    }

    /// <summary>上一行同列单元格是否数值格式（无 t 属性）：true=数值、false=文本、null=没有可比对的单元格。</summary>
    private static bool? FindPreviousCellNumeric(XElement sheetData, int rowNumber, int column)
    {
        if (rowNumber <= 0)
        {
            return null;
        }

        var row = sheetData.Elements(Ns + "row")
            .FirstOrDefault(element => ((int?)element.Attribute("r") ?? 0) == rowNumber);
        var reference = ColumnName(column) + rowNumber;
        var cell = row?.Elements(Ns + "c").FirstOrDefault(element => (string?)element.Attribute("r") == reference);
        if (cell is null)
        {
            return null;
        }

        return (string?)cell.Attribute("t") is null;
    }

    /// <summary>数值单元格的取值：数字直写；bool 列按 1/0 写，跟 Unity 原表保持一致。</summary>
    private static bool TryParseCellNumber(string? value, out double number)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        if (string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
        {
            number = 1;
            return true;
        }

        if (string.Equals(value, "False", StringComparison.OrdinalIgnoreCase))
        {
            number = 0;
            return true;
        }

        number = 0;
        return false;
    }

    private static string? FindStyle(XElement sheetData, int rowNumber, int column)
    {
        for (var probe = rowNumber; probe > 0; probe--)
        {
            var row = sheetData.Elements(Ns + "row")
                .FirstOrDefault(element => ((int?)element.Attribute("r") ?? 0) == probe);
            if (row is null)
            {
                continue;
            }

            var reference = ColumnName(column) + probe;
            var cell = row.Elements(Ns + "c").FirstOrDefault(element => (string?)element.Attribute("r") == reference);
            var style = (string?)cell?.Attribute("s");
            if (style is not null)
            {
                return style;
            }
        }

        return null;
    }

    private static string ColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            var remainder = (number - 1) % 26;
            name = (char)('A' + remainder) + name;
            number = (number - 1) / 26;
        }

        return name;
    }

    private static int ColumnNumber(string reference)
    {
        var number = 0;
        foreach (var character in reference)
        {
            if (character >= 'A' && character <= 'Z')
            {
                number = number * 26 + (character - 'A' + 1);
            }
            else if (character >= 'a' && character <= 'z')
            {
                number = number * 26 + (character - 'a' + 1);
            }
            else
            {
                break;
            }
        }

        return number;
    }
}

#endregion