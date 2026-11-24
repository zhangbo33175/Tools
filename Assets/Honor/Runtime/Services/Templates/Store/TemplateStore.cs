/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateStore.cs
 * author:    云毅
 * created:   2026
 * descrip:   元件模板持久化入口：JSON 工程配置 + CSV 导入导出
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 元件模板仓库。负责读写独立版 JSON 工程配置，以及 Unity 口径 CSV（4 行表头：标题 / 列名 / 类型 / 备注，第 5 行起为数据）。
/// JSON 读取与兼容载荷映射位于 JSON partial，CSV 列定义与解析辅助位于 CSV partial。
/// </summary>
public sealed partial class TemplateStore
{
    #region 常量

    /// <summary>CSV 表头行数（标题 / 列名 / 类型 / 备注）。</summary>
    private const int CsvHeaderRowCount = 4;

    /// <summary>列名行在 CSV 中的下标。</summary>
    private const int CsvColumnNameRowIndex = 1;

    /// <summary>类型提示行的类型关键字，用于导入时识别并跳过。</summary>
    private static readonly HashSet<string> TypeHintTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "bool", "int", "float", "double",
    };

    /// <summary>模板配置列定义：列名 + 类型提示 + 取值 + 赋值。</summary>
    private static readonly IReadOnlyList<TemplateColumn> Columns = BuildColumns();

    #endregion

    #region 构造

    /// <summary>初始化仓库。</summary>
    /// <param name="outputDirectory">输出目录，JSON 与 CSV 默认落在该目录下。</param>
    public TemplateStore(string outputDirectory)
    {
        OutputDirectory = outputDirectory;
        JsonPath = Path.Combine(outputDirectory, "ObjectTemplates.json");
        NineSlicePath = Path.Combine(outputDirectory, "NineSliceConfig.json");
    }

    #endregion

    #region 公开属性

    /// <summary>输出目录。</summary>
    public string OutputDirectory { get; }

    /// <summary>JSON 工程配置文件路径。</summary>
    public string JsonPath { get; }

    /// <summary>九宫格配置旁路文件路径：配置表没有对应列时，九宫格按素材名存在这里。</summary>
    public string NineSlicePath { get; }

    /// <summary>CSV 默认文件名。</summary>
    public string DefaultCsvFileName => "ItemElementConfig";

    #endregion

    #region 公开方法

    /// <summary>读取 JSON 工程配置；文件缺失或损坏时用工厂方法构建默认模板集合并立即落盘。</summary>
    /// <param name="factory">默认模板构建方法。</param>
    /// <returns>模板列表。</returns>
    public List<ObjectTemplate> LoadOrCreate(Func<List<ObjectTemplate>> factory)
    {
        var templates = TryLoadJson(JsonPath);
        if (templates.Count > 0)
        {
            return templates;
        }

        templates = factory();
        TrySaveJson(templates, out _);
        return templates;
    }

    /// <summary>把模板集合写入 JSON 工程配置。</summary>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否保存成功。</returns>
    public bool TrySaveJson(IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            FileSystemIo.EnsureDirectory(OutputDirectory);
            var payload = templates.Select(TemplatePayload.FromModel).ToList();
            FileSystemIo.WriteText(JsonPath, JsonSerializer.Serialize(payload, JsonFileOptions.Indented));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>读取九宫格旁路配置；文件缺失或损坏时返回空目录，不阻塞编辑器启动。</summary>
    /// <returns>九宫格目录。</returns>
    public NineSliceCatalog LoadNineSlices()
    {
        var catalog = new NineSliceCatalog();
        try
        {
            if (!File.Exists(NineSlicePath))
            {
                return catalog;
            }

            var payload = JsonSerializer.Deserialize<NineSliceFilePayload>(FileSystemIo.ReadText(NineSlicePath));
            if (payload?.Entries is null)
            {
                return catalog;
            }

            foreach (var entry in payload.Entries)
            {
                catalog.Register(
                    entry.TemplateId,
                    entry.ResName,
                    entry.ImagePath,
                    entry.TemplateName,
                    NineSliceSettings.FromMargins(
                        entry.Enabled,
                        entry.Left,
                        entry.Right,
                        entry.Top,
                        entry.Bottom,
                        entry.CenterMode));
            }
        }
        catch (Exception)
        {
            // 九宫格文件损坏时按未配置处理，画布退回普通拉伸
        }

        return catalog;
    }

    /// <summary>把模板集合中的九宫格配置写入旁路文件（仅写启用项；表行与 JSON 模板共用同一份）。</summary>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否保存成功。</returns>
    public bool TrySaveNineSlices(IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            FileSystemIo.EnsureDirectory(OutputDirectory);
            var entries = new List<NineSliceEntryPayload>();
            foreach (var template in templates)
            {
                var nineSlice = template.NineSlice;
                if (nineSlice is null || !nineSlice.Enabled)
                {
                    continue;
                }

                var clamped = nineSlice.Clone().ClampMargins();
                entries.Add(new NineSliceEntryPayload
                {
                    TemplateId = template.TemplateId,
                    ResName = template.ResName,
                    ImagePath = template.ImagePath,
                    TemplateName = template.TemplateName,
                    Enabled = true,
                    Left = clamped.Left,
                    Right = clamped.Right,
                    Top = clamped.Top,
                    Bottom = clamped.Bottom,
                    CenterMode = NineSliceModes.ToCode(clamped.CenterMode),
                });
            }

            FileSystemIo.WriteText(
                NineSlicePath,
                JsonSerializer.Serialize(new NineSliceFilePayload { Entries = entries }, JsonFileOptions.Indented));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>导出 CSV（4 行表头 + 数据行，列口径与 Unity 配置表一致）。</summary>
    /// <param name="path">目标路径。</param>
    /// <param name="templates">模板集合。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否导出成功。</returns>
    public bool TryExportCsv(string path, IReadOnlyList<ObjectTemplate> templates, out string error)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("物品元件配置表");
            builder.AppendLine(string.Join(",", Columns.Select(column => column.Name)));
            builder.AppendLine(string.Join(",", Columns.Select(column => column.TypeHint)));
            builder.AppendLine("自动生成：独立版地图编辑器导出");

            foreach (var template in templates)
            {
                builder.AppendLine(string.Join(",", Columns.Select(column => TableTextFormat.CsvEscape(column.Read(template)))));
            }

            FileSystemIo.EnsureParentDirectory(path);

            FileSystemIo.WriteText(path, builder.ToString());
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>导入 CSV，兼容 4 行表头与单行表头两种布局。</summary>
    /// <param name="path">文件路径。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>解析出的模板列表；失败返回空列表。</returns>
    public List<ObjectTemplate> ImportCsv(string path, out string error)
    {
        var templates = new List<ObjectTemplate>();
        try
        {
            var lines = FileSystemIo.ReadLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            if (lines.Count == 0)
            {
                error = "文件为空。";
                return templates;
            }

            var headerIndex = FindHeaderRow(lines);
            if (headerIndex < 0)
            {
                error = "未找到 TemplateId 列，无法识别配置表。";
                return templates;
            }

            var header = TableTextFormat.SplitCsvLine(lines[headerIndex]);
            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < header.Count; index++)
            {
                columnIndex[header[index].Trim()] = index;
            }

            for (var rowIndex = headerIndex + 1; rowIndex < lines.Count; rowIndex++)
            {
                var cells = TableTextFormat.SplitCsvLine(lines[rowIndex]);

                // 备注行 / 分隔行：单元格数量明显少于列数，直接跳过
                if (cells.Count < header.Count)
                {
                    continue;
                }

                var rawId = Cell(cells, columnIndex, "TemplateId");
                if (string.IsNullOrWhiteSpace(rawId) || TypeHintTokens.Contains(rawId.Trim()))
                {
                    continue;
                }

                var template = new ObjectTemplate { IsBuiltIn = false };
                foreach (var column in Columns)
                {
                    column.Assign(template, Cell(cells, columnIndex, column.Name));
                }

                if (string.IsNullOrWhiteSpace(template.TemplateName))
                {
                    template.TemplateName = template.TemplateId;
                }

                templates.Add(template);
            }

            error = string.Empty;
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        return templates;
    }

    #endregion

}
