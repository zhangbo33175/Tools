/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  XlsmTableReader.cs
 * author:    云毅
 * created:   2026
 * descrip:   零依赖 OOXML 表格读取（章节表 / 主关卡表专用，xlsm 与 xlsx 同构）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MapEditorTool.Services;

/// <summary>一张工作表：按原始行序取到的单元格文本（空格补空串）。</summary>
public sealed class TableSheet
{
    /// <summary>工作表名（<c>workbook.xml</c> 里的 sheet name）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>所有行，每行是列序对齐后的字符串列表。</summary>
    public List<List<string>> Rows { get; } = new();
}

/// <summary>
/// 读 xlsm / xlsx 的纯托管实现：<c>ZipArchive</c> 拆包 + <c>XDocument</c> 解
/// <c>workbook.xml</c>、<c>sharedStrings.xml</c> 与工作表 XML，不依赖 Excel / COM / 第三方库。
/// 只取文本与数字字面值（公式取缓存结果），样式、合并单元格、宏包一律忽略。
/// </summary>
public static class XlsmTableReader
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// 读取指定工作表；<paramref name="preferredSheet"/> 为空或找不到时退回第一张表。
    /// </summary>
    /// <param name="filePath">xlsm / xlsx 绝对路径。</param>
    /// <param name="preferredSheet">期望的工作表名（大小写不敏感，可为空）。</param>
    /// <param name="error">失败原因（成功时为 null）。</param>
    /// <returns>工作表内容；失败返回 null。</returns>
    public static TableSheet? Read(string? filePath, string? preferredSheet, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            error = $"表格不存在：{filePath}";
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(filePath);

            var workbook = LoadXml(archive, "xl/workbook.xml");
            if (workbook?.Root is null)
            {
                error = "不是 OOXML 表格（缺少 xl/workbook.xml）";
                return null;
            }

            var sheets = workbook.Root.Element(MainNs + "sheets")?.Elements(MainNs + "sheet").ToList();
            if (sheets is null || sheets.Count == 0)
            {
                error = "工作簿里没有工作表";
                return null;
            }

            var relationships = LoadRelationships(archive);
            var sharedStrings = LoadSharedStrings(archive);

            var target = sheets.FirstOrDefault(sheet =>
                             string.Equals((string?)sheet.Attribute("name"), preferredSheet, StringComparison.OrdinalIgnoreCase))
                         ?? sheets[0];

            var sheetName = (string?)target.Attribute("name") ?? string.Empty;
            var relationId = (string?)target.Attribute(OfficeRelNs + "id");
            var sheetPath = relationId is not null && relationships.TryGetValue(relationId, out var path)
                ? NormalizePartPath(path)
                : "xl/worksheets/sheet1.xml";

            var sheetXml = LoadXml(archive, sheetPath);
            if (sheetXml?.Root is null)
            {
                error = $"工作表 XML 缺失：{sheetPath}";
                return null;
            }

            var sheet = new TableSheet { Name = sheetName };
            FillRows(sheet, sheetXml.Root, sharedStrings);
            return sheet;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return null;
        }
    }

    /// <summary>把工作表节点铺成行列表：按 <c>r</c> 属性定位行列，缺列补空串。</summary>
    private static void FillRows(TableSheet sheet, XElement sheetRoot, IReadOnlyList<string> sharedStrings)
    {
        var data = sheetRoot.Element(MainNs + "sheetData");
        if (data is null)
        {
            return;
        }

        var rows = sheet.Rows;

        foreach (var row in data.Elements(MainNs + "row"))
        {
            var rowIndex = int.TryParse((string?)row.Attribute("r"), out var declared) ? declared : rows.Count + 1;
            while (rows.Count < rowIndex - 1)
            {
                rows.Add(new List<string>());
            }

            var cells = rowIndex - 1 < rows.Count ? rows[rowIndex - 1] : new List<string>();
            var cursor = -1;

            foreach (var cell in row.Elements(MainNs + "c"))
            {
                var column = ColumnIndex((string?)cell.Attribute("r")) ?? cursor + 1;
                while (cells.Count <= column)
                {
                    cells.Add(string.Empty);
                }

                cells[column] = ReadCell(cell, sharedStrings);
                cursor = column;
            }

            if (rowIndex - 1 >= rows.Count)
            {
                rows.Add(cells);
            }
            else
            {
                rows[rowIndex - 1] = cells;
            }
        }
    }

    /// <summary>单元格文本：共享串走 <c>t="s"</c>，富文本 / 内联串拼接全部 <c>t</c> 节点。</summary>
    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");

        if (type == "inlineStr")
        {
            var inline = cell.Element(MainNs + "is");
            return inline is null
                ? string.Empty
                : string.Concat(inline.Descendants(MainNs + "t").Select(node => node.Value));
        }

        var raw = cell.Element(MainNs + "v")?.Value ?? string.Empty;

        return type switch
        {
            "s" => int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty,
            "b" => raw == "1" ? "TRUE" : "FALSE",
            _ => raw,
        };
    }

    /// <summary><c>"AB12"</c> → 27（0 基列号）；无引用返回 null。</summary>
    private static int? ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var index = 0;
        var hasLetter = false;

        foreach (var ch in reference)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                index = index * 26 + (ch - 'A' + 1);
                hasLetter = true;
            }
            else if (ch is >= 'a' and <= 'z')
            {
                index = index * 26 + (ch - 'a' + 1);
                hasLetter = true;
            }
            else
            {
                break;
            }
        }

        return hasLetter ? index - 1 : null;
    }

    /// <summary><c>xl/_rels/workbook.xml.rels</c>：rId → 工作表部件路径。</summary>
    private static Dictionary<string, string> LoadRelationships(ZipArchive archive)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var document = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        if (document?.Root is null)
        {
            return result;
        }

        foreach (var relationship in document.Root.Elements(PackageRelNs + "Relationship"))
        {
            var id = (string?)relationship.Attribute("Id");
            var target = (string?)relationship.Attribute("Target");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
            {
                result[id] = target;
            }
        }

        return result;
    }

    /// <summary><c>xl/sharedStrings.xml</c>：下标 → 文本（富文本把各 run 拼起来）。</summary>
    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var result = new List<string>();
        var document = LoadXml(archive, "xl/sharedStrings.xml");
        if (document?.Root is null)
        {
            return result;
        }

        foreach (var item in document.Root.Elements(MainNs + "si"))
        {
            result.Add(string.Concat(item.Descendants(MainNs + "t").Select(node => node.Value)));
        }

        return result;
    }

    /// <summary>部件路径归一：<c>/xl/...</c> 去前导斜杠，相对路径补 <c>xl/</c> 前缀。</summary>
    private static string NormalizePartPath(string target)
    {
        var path = target.Replace('\\', '/');
        if (path.StartsWith('/'))
        {
            return path.TrimStart('/');
        }

        return path.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? path : "xl/" + path;
    }

    /// <summary>读包内 XML 部件（不存在返回 null）。</summary>
    private static XDocument? LoadXml(ZipArchive archive, string partPath)
    {
        var entry = archive.GetEntry(partPath);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
