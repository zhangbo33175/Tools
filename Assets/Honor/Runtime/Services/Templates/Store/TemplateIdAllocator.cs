/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TemplateIdAllocator.cs
 * author:    云毅
 * created:   2026
 * descrip:   模板与配置表行共用的 ID 分段规则、解析与分配
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 模板 / 配置表行的 ID 分段口径。
/// 人物 1xxx、建筑 2xxx、物件 3xxx；同一千位段内取最大值加一。
/// 所有 ID 的解析、分段判断与分配都从这里收口，避免各调用方各写一套起始值。
/// </summary>
internal static class TemplateIdAllocator
{
    #region 分段定义

    /// <summary>ID 分段步长：每个类型占用一千个编号。</summary>
    public const int RangeSize = 1000;

    /// <summary>一个类型对应的 ID 段。</summary>
    /// <param name="Type">物件类型。</param>
    /// <param name="Start">段起点。</param>
    private readonly record struct Segment(MapObjectType Type, int Start);

    /// <summary>各类型的分段定义；新增类型时只改这一处。</summary>
    private static readonly Segment[] Segments =
    {
        new(MapObjectType.Character, 1000),
        new(MapObjectType.Building, 2000),
        new(MapObjectType.Prop, 3000),
    };

    /// <summary>支持分段的类型集合。</summary>
    public static IReadOnlyList<MapObjectType> Types { get; } = new[]
    {
        MapObjectType.Character,
        MapObjectType.Building,
        MapObjectType.Prop,
    };

    #endregion

    #region 解析与分段

    /// <summary>按类型取 ID 段起点。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>段起点，如人物 1000。</returns>
    public static int StartOf(MapObjectType type) => SegmentOf(type).Start;

    /// <summary>按类型取 ID 段终点（含）。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>段终点，如人物 1999。</returns>
    public static int EndOf(MapObjectType type) => StartOf(type) + RangeSize - 1;

    /// <summary>把官方字段文本解析成整数 ID。</summary>
    /// <param name="raw">ID 文本。</param>
    /// <param name="id">解析结果。</param>
    /// <returns>解析成功返回 true。</returns>
    public static bool TryParse(string? raw, out int id) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

    /// <summary>判断 ID 是否落在指定类型的分段内。</summary>
    /// <param name="raw">ID 文本。</param>
    /// <param name="type">物件类型。</param>
    /// <returns>是则 true。</returns>
    public static bool IsInRange(string? raw, MapObjectType type) =>
        TryParse(raw, out var id)
        && id >= StartOf(type)
        && id <= EndOf(type);

    /// <summary>按 ID 分段反推物件类型；非数字或越界返回 null。</summary>
    /// <param name="raw">ID 文本。</param>
    /// <returns>物件类型。</returns>
    public static MapObjectType? TypeOf(string? raw)
    {
        if (!TryParse(raw, out var id))
        {
            return null;
        }

        foreach (var segment in Segments)
        {
            if (id >= segment.Start && id < segment.Start + RangeSize)
            {
                return segment.Type;
            }
        }

        return null;
    }

    #endregion

    #region 分配

    /// <summary>
    /// 在指定类型的 ID 段内分配下一个可用编号。
    /// 所有来源都会被扫描，跨段脏数据不会把序号推高。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="sources">参与去重的 ID 来源，例如配置表行、模板集合。</param>
    /// <returns>下一个可用 ID 文本。</returns>
    public static string Next(MapObjectType type, params IEnumerable<string>?[] sources)
    {
        var start = StartOf(type);
        var end = EndOf(type);
        var max = start;

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var raw in source)
            {
                if (TryParse(raw, out var id) && id >= start && id <= end && id > max)
                {
                    max = id;
                }
            }
        }

        return TableTextFormat.Integer(max + 1);
    }

    #endregion

    #region 内部实现

    /// <summary>取类型对应的分段；未知类型按物件段处理。</summary>
    /// <param name="type">物件类型。</param>
    /// <returns>分段定义。</returns>
    private static Segment SegmentOf(MapObjectType type)
    {
        foreach (var segment in Segments)
        {
            if (segment.Type == type)
            {
                return segment;
            }
        }

        return Segments[^1];
    }

    #endregion
}
