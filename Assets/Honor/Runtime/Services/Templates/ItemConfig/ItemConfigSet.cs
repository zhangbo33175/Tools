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
        "九宫格", "九宫格左边距", "九宫格右边距", "九宫格上边距", "九宫格下边距", "九宫格中心",
    };

    /// <summary>新建 CSV 时的类型行。</summary>
    private static readonly string[] CanonicalTypes =
    {
        "string", "string", "string", "string", "string", "string", "bool", "float", "bool", "string", "int", "bool",
        "string", "string", "bool", "bool", "float", "string", "bool", "float", "bool", "float", "bool", "bool",
        "string", "float", "float", "bool", "int", "int", "int", "int", "string",
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

    /// <summary>所有数据行。</summary>
    public IEnumerable<ItemElementRow> Rows => Files.SelectMany(file => file.Rows);

    /// <summary>总行数。</summary>
    public int RowCount => Files.Sum(file => file.Rows.Count);

    /// <summary>夹内表名摘要。</summary>
    public string Describe() => string.Join("、", Files.Select(file => $"{file.Name}({file.Rows.Count})"));

    #endregion
}

#endregion
