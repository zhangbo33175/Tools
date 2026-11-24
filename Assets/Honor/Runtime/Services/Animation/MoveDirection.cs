/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MoveDirection.cs
 * author:    云毅
 * created:   2026
 * descrip:   八方向移动表现：把路径切向归一成 8 个方向，并按命名约定匹配方向动画
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Services;

#region 八方向

/// <summary>
/// 八方向移动朝向。角度按屏幕坐标顺时针排列：
/// 右、右下、下、左下、左、左上、上、右上。
/// </summary>
public enum MoveDirection8
{
    /// <summary>右。</summary>
    Right = 0,

    /// <summary>右下。</summary>
    DownRight = 1,

    /// <summary>下。</summary>
    Down = 2,

    /// <summary>左下。</summary>
    DownLeft = 3,

    /// <summary>左。</summary>
    Left = 4,

    /// <summary>左上。</summary>
    UpLeft = 5,

    /// <summary>上。</summary>
    Up = 6,

    /// <summary>右上。</summary>
    UpRight = 7,
}

/// <summary>八方向几何换算。</summary>
public static class MoveDirectionResolver
{
    #region 公开方法

    /// <summary>把路径切向归一成 8 个方向；零向量返回 null。</summary>
    /// <param name="dx">水平分量。</param>
    /// <param name="dy">垂直分量（屏幕坐标向下为正）。</param>
    /// <returns>八方向；无有效方向时为 null。</returns>
    public static MoveDirection8? FromVector(double dx, double dy)
    {
        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
        {
            return null;
        }

        var sector = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4), MidpointRounding.AwayFromZero);
        return (MoveDirection8)(((sector % 8) + 8) % 8);
    }

    /// <summary>方向对应的命名后缀，例如 <c>down_right</c>。</summary>
    /// <param name="direction">八方向。</param>
    /// <returns>小写下划线后缀。</returns>
    public static string Code(MoveDirection8 direction) => direction switch
    {
        MoveDirection8.Right => "right",
        MoveDirection8.DownRight => "down_right",
        MoveDirection8.Down => "down",
        MoveDirection8.DownLeft => "down_left",
        MoveDirection8.Left => "left",
        MoveDirection8.UpLeft => "up_left",
        MoveDirection8.Up => "up",
        MoveDirection8.UpRight => "up_right",
        _ => "right",
    };

    /// <summary>是否包含向左分量。</summary>
    /// <param name="direction">八方向。</param>
    /// <returns>左、左下、左上为 true。</returns>
    public static bool IsLeft(MoveDirection8 direction) =>
        direction is MoveDirection8.Left or MoveDirection8.DownLeft or MoveDirection8.UpLeft;

    /// <summary>是否包含向右分量。</summary>
    /// <param name="direction">八方向。</param>
    /// <returns>右、右下、右上为 true。</returns>
    public static bool IsRight(MoveDirection8 direction) =>
        direction is MoveDirection8.Right or MoveDirection8.DownRight or MoveDirection8.UpRight;

    #endregion
}

/// <summary>
/// 方向动画命名解析：优先找 <c>walk_up</c> / <c>walk_down_right</c> 这类方向动作，
/// 找不到时回落到通用的 <c>walk</c>，由绘制层再按左右方向做水平镜像。
/// </summary>
public static class DirectionalAnimationResolver
{
    #region 公开方法

    /// <summary>按方向解析动作名；没有方向动作时返回基础动作。</summary>
    /// <param name="baseAction">基础动作名，例如 walk。</param>
    /// <param name="direction">当前八方向。</param>
    /// <param name="available">资产或序列帧目录中可用的动作名。</param>
    /// <returns>方向动作名或基础动作名。</returns>
    public static string Resolve(
        string? baseAction,
        MoveDirection8 direction,
        IReadOnlyList<string>? available)
    {
        var baseName = (baseAction ?? string.Empty).Trim();
        if (baseName.Length == 0 || available is null || available.Count == 0)
        {
            return baseName;
        }

        foreach (var candidate in Candidates(baseName, direction))
        {
            if (Contains(available, candidate))
            {
                return candidate;
            }
        }

        return baseName;
    }

    /// <summary>判断是否存在该方向的动作。</summary>
    /// <param name="baseAction">基础动作名。</param>
    /// <param name="direction">八方向。</param>
    /// <param name="available">可用动作名。</param>
    /// <returns>命中方向动作时为 true。</returns>
    public static bool HasDirectional(
        string? baseAction,
        MoveDirection8 direction,
        IReadOnlyList<string>? available) =>
        !string.Equals(Resolve(baseAction, direction, available), (baseAction ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    #endregion

    #region 候选名

    /// <summary>按常见命名约定生成候选名：动作_方向、动作-方向、方向_动作。</summary>
    /// <param name="baseAction">基础动作名。</param>
    /// <param name="direction">八方向。</param>
    /// <returns>候选名序列。</returns>
    private static IEnumerable<string> Candidates(string baseAction, MoveDirection8 direction)
    {
        var code = MoveDirectionResolver.Code(direction);
        var compact = code.Replace("_", string.Empty, StringComparison.Ordinal);
        var pascal = ToPascal(code);

        yield return $"{baseAction}_{code}";
        yield return $"{baseAction}-{code}";
        yield return $"{baseAction}_{compact}";
        yield return $"{baseAction}_{pascal}";
        yield return $"{code}_{baseAction}";
        yield return $"{pascal}_{baseAction}";
    }

    /// <summary>下划线命名转帕斯卡命名，例如 down_right → DownRight。</summary>
    /// <param name="code">小写下划线后缀。</param>
    /// <returns>帕斯卡命名。</returns>
    private static string ToPascal(string code)
    {
        var parts = code.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var result = new System.Text.StringBuilder(code.Length);
        foreach (var part in parts)
        {
            result.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                result.Append(part.AsSpan(1));
            }
        }

        return result.ToString();
    }

    /// <summary>忽略大小写与首尾空白匹配动作名。</summary>
    /// <param name="available">可用动作名。</param>
    /// <param name="candidate">候选名。</param>
    /// <returns>命中时为 true。</returns>
    private static bool Contains(IReadOnlyList<string> available, string candidate)
    {
        foreach (var name in available)
        {
            if (string.Equals(name.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}

#endregion
