/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemConfigSet.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图物件配置表「文件夹」：夹内多张 CSV / xlsm 一起读、按图片名绑定、可编辑写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

#region 表集合

/// <summary>
/// 「地图物件配置表文件夹」：读取夹内所有 CSV / xlsm（顶层，不进备份子目录），
/// 合并成一套按图片名查找的行集合；保存时按各自文件格式写回。
/// </summary>
public sealed partial class ItemConfigSet
{
    #region 常量

    /// <summary>新建行时优先写的汇总 CSV 文件名。</summary>
    private const string PreferredCsvName = "ItemElementConfig.csv";

    /// <summary>新建 CSV 时的列口径（与 ItemElementConfig.csv 一致）。</summary>
    private static readonly string[] CanonicalColumns =
    {
        "#", "ID", "Icon", "ResName", "模板名称", "默认动画", "是否循环", "播放速度", "是否可交互", "交互类型",
        "渲染层级", "是否有碰撞", "对话气泡", "初始朝向", "是否显示阴影", "是否收集物", "重生时间", "交通类别",
        "启用YSort", "YSort偏移", "可移动", "循环重启延迟", "离开地图隐藏", "是否传送门", "目标场景", "目标X", "目标Y",
        "九宫格", "九宫格左边距", "九宫格右边距", "九宫格上边距", "九宫格下边距", "九宫格中心", "显示名称",
    };

    /// <summary>新建 CSV 时的类型行。</summary>
    private static readonly string[] CanonicalTypes =
    {
        "string", "string", "string", "string", "string", "string", "bool", "float", "bool", "string", "int", "bool",
        "string", "string", "bool", "bool", "float", "string", "bool", "float", "bool", "float", "bool", "bool",
        "string", "float", "float", "bool", "int", "int", "int", "int", "string", "string",
    };

    #endregion

    #region 字段

    private readonly Dictionary<string, ItemElementRow> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ItemElementRow> _idIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<ItemElementRow, ItemTableFile> _owner = new();

    #endregion

    #region 构造

    private ItemConfigSet(string directory)
    {
        Directory = directory;
    }

    #endregion

    #region 属性

    /// <summary>文件夹绝对路径。</summary>
    public string Directory { get; }

    /// <summary>夹内的表（按文件名排序，汇总 CSV 靠前）。</summary>
    public List<ItemTableFile> Files { get; } = new();

    /// <summary>主表（打开配置表 / 新建行落到这里）。</summary>
    public ItemTableFile? Primary => Files.FirstOrDefault();

    /// <summary>主表路径（可能为空）。</summary>
    public string PrimaryPath => Primary?.Path ?? string.Empty;

    /// <summary>所有数据行（原始，含历史镜像行）。</summary>
    public IEnumerable<ItemElementRow> Rows => Files.SelectMany(file => file.Rows);

    /// <summary>
    /// 去重后的数据行：同一张图在汇总表与分类表各登记一份（历史「汇总 + 分类」双写留下的副本）时，
    /// 只保留分类表那份；表内同 ID 同图重复也只留第一行。表文件本身不被改写，保存与导出仍走原始行。
    /// </summary>
    public IEnumerable<ItemElementRow> DistinctRows
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 分类表优先遍历：汇总表的镜像行在 key 已占用时被跳过，汇总表独有的行（如 bg_ 背景）照常输出
            foreach (var file in EnumerateClassifiedFirst())
            {
                foreach (var row in file.Rows)
                {
                    var key = RowKey(row);
                    if (seen.Add(key))
                    {
                        yield return row;
                    }
                }
            }
        }
    }

    /// <summary>行所属的表；不属于本集合时返回 null。</summary>
    /// <param name="row">数据行。</param>
    /// <returns>所属表。</returns>
    public ItemTableFile? OwnerOf(ItemElementRow row) =>
        _owner.TryGetValue(row, out var file) ? file : null;

    /// <summary>总行数。</summary>
    public int RowCount => Files.Sum(file => file.Rows.Count);

    /// <summary>去重后的行数。</summary>
    public int DistinctRowCount => DistinctRows.Count();

    /// <summary>是否为汇总表（<c>ItemElementConfig.csv</c>）：它承担「无分类归属」行与兜底，不重复登记有分类的图。</summary>
    /// <param name="file">待判断的表。</param>
    /// <returns>是汇总表返回 true。</returns>
    public static bool IsSummaryFile(ItemTableFile? file) =>
        file is not null
        && string.Equals(System.IO.Path.GetFileName(file.Path), PreferredCsvName, StringComparison.OrdinalIgnoreCase);

    /// <summary>夹内表名摘要。</summary>
    public string Describe() => string.Join("、", Files.Select(file => $"{file.Name}({file.Rows.Count})"));

    /// <summary>按「分类表优先、汇总表最后」的顺序遍历表。</summary>
    /// <returns>表序列。</returns>
    private IEnumerable<ItemTableFile> EnumerateClassifiedFirst() =>
        Files.Where(file => !IsSummaryFile(file)).Concat(Files.Where(IsSummaryFile));

    /// <summary>去重键：模板 ID + 图片名（同图同 ID 视为同一条登记）。</summary>
    /// <param name="row">数据行。</param>
    /// <returns>去重键。</returns>
    private static string RowKey(ItemElementRow row) =>
        row.Id.Trim() + "\u0001" + row.Icon.Trim();

    #endregion
}

#endregion
