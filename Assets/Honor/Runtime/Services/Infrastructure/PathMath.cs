/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PathMath.cs
 * author:    云毅
 * created:   2026
 * descrip:   路径几何统一入口：长度、按里程取点与行走模式归一到同一套口径，
 *            供场景画布预览和动画预览窗共用，避免各自维护一份插值逻辑。
 ***************************************************************/

using System;
using System.Collections.Generic;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>路径播放模式。</summary>
public enum PathPlaybackMode
{
    /// <summary>到终点后回到起点循环。</summary>
    Loop,

    /// <summary>到终点后沿原路往返。</summary>
    PingPong,

    /// <summary>到终点后停在终点。</summary>
    Once,
}

/// <summary>
/// 路径几何计算。所有方法只读路径点，不修改地图数据。
/// </summary>
public static class PathMath
{
    #region 长度与距离

    /// <summary>两点间欧氏距离。</summary>
    /// <param name="from">起点。</param>
    /// <param name="to">终点。</param>
    /// <returns>距离，世界像素。</returns>
    public static double Distance(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>路径是否可播放：至少两个点才构成一段可移动路径。</summary>
    /// <param name="points">路径点集合。</param>
    /// <returns>可播放返回 <see langword="true"/>。</returns>
    public static bool HasPath(IReadOnlyList<MapPathPointData>? points) =>
        points is { Count: >= 2 };

    /// <summary>计算路径总长度。</summary>
    /// <param name="points">路径点集合；少于两个点时返回 0。</param>
    /// <returns>总长度，世界像素。</returns>
    public static double Length(IReadOnlyList<MapPathPointData>? points)
    {
        if (points is null || points.Count < 2)
        {
            return 0;
        }

        var total = 0.0;
        for (var index = 0; index < points.Count - 1; index++)
        {
            total += Distance(ToPoint(points[index]), ToPoint(points[index + 1]));
        }

        return total;
    }

    #endregion

    #region 里程取点

    /// <summary>
    /// 按累计里程取路径前进方向（单位向量）；用于预览时让角色朝向运动方向。
    /// <para>里程夹在 0..总长，起点取第一段、终点取最后一段，避免在端点得到零向量。</para>
    /// </summary>
    /// <param name="points">路径点集合。</param>
    /// <param name="distance">累计里程，世界像素。</param>
    /// <returns>单位方向；路径无效返回 null。</returns>
    public static Point? DirectionAtDistance(IReadOnlyList<MapPathPointData>? points, double distance)
    {
        if (points is null || points.Count < 2)
        {
            return null;
        }

        var remaining = Math.Max(0, distance);
        Point? lastDirection = null;
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = ToPoint(points[index]);
            var to = ToPoint(points[index + 1]);
            var segment = Distance(from, to);
            if (segment <= 0)
            {
                continue;
            }

            var direction = new Point((to.X - from.X) / segment, (to.Y - from.Y) / segment);
            lastDirection = direction;
            if (remaining <= segment)
            {
                return direction;
            }

            remaining -= segment;
        }

        return lastDirection;
    }

    /// <summary>按累计里程取路径上的点；超出范围时夹到起点/终点。</summary>
    /// <param name="points">路径点集合。</param>
    /// <param name="distance">累计里程，世界像素。</param>
    /// <returns>路径上的点；空路径返回 null。</returns>
    public static Point? PointAtDistance(IReadOnlyList<MapPathPointData>? points, double distance)
    {
        if (points is null || points.Count == 0)
        {
            return null;
        }

        if (points.Count == 1)
        {
            return ToPoint(points[0]);
        }

        var remaining = Math.Max(0, distance);
        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = ToPoint(points[index]);
            var to = ToPoint(points[index + 1]);
            var segment = Distance(from, to);
            if (segment <= 0)
            {
                continue;
            }

            if (remaining <= segment)
            {
                var ratio = remaining / segment;
                return new Point(
                    from.X + (to.X - from.X) * ratio,
                    from.Y + (to.Y - from.Y) * ratio);
            }

            remaining -= segment;
        }

        return ToPoint(points[^1]);
    }

    #endregion

    #region 行走模式

    /// <summary>把地图中的 walkMode 文本解析成播放模式。</summary>
    /// <param name="walkMode">walkMode 原始文本。</param>
    /// <returns>播放模式；未知值按循环处理。</returns>
    public static PathPlaybackMode ParseWalkMode(string? walkMode)
    {
        var mode = (walkMode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode.Contains("once") || mode.Contains("stop"))
        {
            return PathPlaybackMode.Once;
        }

        if (mode.Contains("ping") || mode.Contains("back") || mode.Contains("round"))
        {
            return PathPlaybackMode.PingPong;
        }

        return PathPlaybackMode.Loop;
    }

    /// <summary>
    /// 把已走路程按播放模式归一到一个循环内；调用方只需再按里程取点。
    /// </summary>
    /// <param name="walked">已走路程，世界像素。</param>
    /// <param name="total">路径总长度。</param>
    /// <param name="mode">播放模式。</param>
    /// <returns>归一后的里程，范围 0..total。</returns>
    public static double NormalizeDistance(double walked, double total, PathPlaybackMode mode)
    {
        if (total <= 0)
        {
            return 0;
        }

        var distance = Math.Max(0, walked);
        switch (mode)
        {
            case PathPlaybackMode.Once:
                return Math.Min(distance, total);

            case PathPlaybackMode.PingPong:
                var cycle = total * 2;
                distance %= cycle;
                return distance > total ? cycle - distance : distance;

            default:
                return distance % total;
        }
    }

    #endregion

    #region 辅助

    /// <summary>把路径点数据转成 Avalonia 点。</summary>
    /// <param name="point">路径点。</param>
    /// <returns>Avalonia 点。</returns>
    private static Point ToPoint(MapPathPointData point) => new(point.X, point.Y);

    #endregion
}
