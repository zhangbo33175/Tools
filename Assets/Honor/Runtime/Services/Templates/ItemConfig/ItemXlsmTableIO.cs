/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemXlsmTableIO.cs
 * author:    云毅
 * created:   2026
 * descrip:   xlsm 配置表实现：OOXML 读取、列映射、单元格写回与数值判定
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
    public static bool Write(ItemTableFile file, out string error) =>
        ServiceOutcome.Run(() =>
        {
            XDocument document;
            using (var archive = ZipFile.OpenRead(file.Path))
            {
                var entry = archive.GetEntry(SheetEntry);
                if (entry is null)
                {
                    return "xlsm 内找不到 sheet1.xml";
                }

                using var stream = entry.Open();
                document = XDocument.Load(stream);
            }

            var sheetData = document.Descendants(Ns + "sheetData").FirstOrDefault();
            if (sheetData is null)
            {
                return "工作表缺少 sheetData";
            }

            var fieldByIndex = new Dictionary<int, string>();
            foreach (var pair in file.ColumnIndex)
            {
                fieldByIndex.TryAdd(pair.Value, pair.Key);
            }

            // 本次新追加的列（如「显示名称」）在原表里没有表头单元格，先补上列名
            foreach (var column in file.AddedColumns)
            {
                if (column >= 0 && column < file.ColumnNames.Count)
                {
                    SetCell(sheetData, file.HeaderRowNumber, column + 1, file.ColumnNames[column], false);
                }
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
            return null;
        }, out error);

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
            cell.Add(new XElement(Ns + "v", TableTextFormat.Number(number)));
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
