/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigTableIO.cs
 * author:    云毅
 * created:   2026
 * descrip:   物件配置表 IO 统一入口：按格式分派 CSV / xlsm 实现
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Services;

/// <summary>
/// 物件配置表读写的唯一入口。调用方只描述「哪张表、什么格式」，
/// 不直接依赖 <see cref="CsvTableReader"/> / <see cref="ItemXlsmWriter"/> 等具体实现。
/// </summary>
internal static class ItemTableIO
{
    #region 读取

    /// <summary>按表格式读取一张配置表。</summary>
    /// <param name="path">文件绝对路径。</param>
    /// <param name="kind">表格式。</param>
    /// <returns>读不到表头时返回 null。</returns>
    public static ItemTableFile? Read(string path, ItemTableKind kind) =>
        kind == ItemTableKind.Csv
            ? CsvTableReader.Read(path)
            : ItemXlsmReader.Read(path);

    /// <summary>创建一张新的 CSV 配置表骨架。</summary>
    /// <param name="path">目标路径。</param>
    /// <param name="columns">列名。</param>
    /// <param name="types">类型行。</param>
    /// <returns>空表模型。</returns>
    public static ItemTableFile CreateEmpty(
        string path,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> types) =>
        CsvTableReader.CreateEmpty(path, columns, types);

    #endregion

    #region 写回

    /// <summary>按表格式写回磁盘。</summary>
    /// <param name="file">待写回的配置表。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否写成功。</returns>
    public static bool Write(ItemTableFile file, out string error) =>
        file.Kind == ItemTableKind.Csv
            ? CsvTableWriter.Write(file, out error)
            : ItemXlsmWriter.Write(file, out error);

    #endregion
}