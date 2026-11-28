/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GridSettingsOptions.cs
 * author:    云毅
 * created:   2026
 * descrip:   网格设置的可选值表：形状 / Unity Cell Layout / Cell Swizzle
 *            取值口径与 Unity Grid 组件一一对应，界面下拉与落盘校验共用这一份，
 *            避免窗口里写一遍中文、模型里再写一遍合法值。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace MapEditorTool.Models;

#region 下拉选项

/// <summary>下拉项：落盘值 + 界面显示名。</summary>
/// <param name="Value">写入设置的英文值（与 Unity 枚举名对齐）。</param>
/// <param name="Label">界面显示名。</param>
public readonly record struct GridOption(string Value, string Label);

#endregion

#region 网格形状

/// <summary>
/// 画布网格形状：与 Unity <c>Grid.cellLayout</c> 一一对应的四种
/// （矩形 / 六边形 / 等距 / 等距 Z 作 Y），取值与 <c>DrawShapes</c> 同源。
/// </summary>
public static class GridShapes
{
    /// <summary>正交矩形格。</summary>
    public const string Rect = "rect";

    /// <summary>六边形格（尖顶蜂窝）。</summary>
    public const string Hex = "hex";

    /// <summary>菱形格（2:1 等距）。</summary>
    public const string Diamond = "diamond";

    /// <summary>菱形格，导出按 Isometric Z as Y 认格（画布几何与非 Z 版完全相同）。</summary>
    public const string DiamondZ = "diamondz";

    /// <summary>全部可选值：顺序、数量与 <see cref="GridLayouts"/> 严格一一对应。</summary>
    public static readonly IReadOnlyList<GridOption> All = new[]
    {
        new GridOption(Rect, "Rectangle（矩形）"),
        new GridOption(Hex, "Hexagon（六边形）"),
        new GridOption(Diamond, "Isometric（等距）"),
        new GridOption(DiamondZ, "Isometric Z as Y（等距 Z 作 Y）"),
    };

    /// <summary>把任意值收敛到合法形状，未知值回落到矩形。</summary>
    /// <param name="value">原始值。</param>
    /// <returns>合法形状值。</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Rect;
        }

        var match = All.FirstOrDefault(option =>
            string.Equals(option.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match.Value ?? Rect;
    }

    /// <summary>按值取界面显示名。</summary>
    /// <param name="value">形状值。</param>
    /// <returns>显示名。</returns>
    public static string LabelOf(string? value) =>
        All.FirstOrDefault(option => option.Value == Normalize(value)).Label;

    /// <summary>是否菱形几何（Isometric 与 Isometric Z as Y 画布上完全一样）。</summary>
    /// <param name="value">形状值。</param>
    /// <returns>菱形几何返回 true。</returns>
    public static bool IsDiamond(string? value)
    {
        var shape = Normalize(value);
        return shape == Diamond || shape == DiamondZ;
    }

    /// <summary>形状 → Unity Cell Layout 值。</summary>
    /// <param name="value">形状值。</param>
    /// <returns>Cell Layout 值。</returns>
    public static string LayoutOf(string? value) => Normalize(value) switch
    {
        Hex => GridLayouts.Hexagon,
        Diamond => GridLayouts.Isometric,
        DiamondZ => GridLayouts.IsometricZ,
        _ => GridLayouts.Rectangle,
    };

    /// <summary>Unity Cell Layout → 形状值（与 <see cref="LayoutOf"/> 互逆）。</summary>
    /// <param name="layout">Cell Layout 值。</param>
    /// <returns>形状值。</returns>
    public static string FromLayout(string? layout) =>
        GridLayouts.Normalize(layout) switch
        {
            GridLayouts.Hexagon => Hex,
            GridLayouts.Isometric => Diamond,
            GridLayouts.IsometricZ => DiamondZ,
            _ => Rect,
        };
}

#endregion

#region Unity Cell Layout

/// <summary>Unity <c>Grid.cellLayout</c> 的四个取值。</summary>
public static class GridLayouts
{
    /// <summary>矩形（标准 Tilemap）。</summary>
    public const string Rectangle = "Rectangle";

    /// <summary>六边形。</summary>
    public const string Hexagon = "Hexagon";

    /// <summary>等距（菱形）。</summary>
    public const string Isometric = "Isometric";

    /// <summary>等距、Z 轴当 Y 轴（用于多层高度）。</summary>
    public const string IsometricZ = "IsometricZ";

    /// <summary>全部可选值（界面下拉顺序，与 Unity 枚举顺序一致）。</summary>
    public static readonly IReadOnlyList<GridOption> All = new[]
    {
        new GridOption(Rectangle, "Rectangle（矩形）"),
        new GridOption(Hexagon, "Hexagon（六边形）"),
        new GridOption(Isometric, "Isometric（等距）"),
        new GridOption(IsometricZ, "Isometric Z as Y（等距 Z 作 Y）"),
    };

    /// <summary>把任意值收敛到合法布局，未知值回落到矩形。</summary>
    /// <param name="value">原始值。</param>
    /// <returns>合法布局值。</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Rectangle;
        }

        var match = All.FirstOrDefault(option =>
            string.Equals(option.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match.Value ?? Rectangle;
    }

    /// <summary>按值取界面显示名。</summary>
    /// <param name="value">布局值。</param>
    /// <returns>显示名。</returns>
    public static string LabelOf(string? value) =>
        All.FirstOrDefault(option => option.Value == Normalize(value)).Label;
}

#endregion

#region Unity Cell Swizzle

/// <summary>Unity <c>Grid.cellSwizzle</c> 的六个轴向排列。</summary>
public static class GridSwizzles
{
    /// <summary>全部可选值（X/Y/Z 六种排列）。</summary>
    public static readonly IReadOnlyList<GridOption> All = new[]
    {
        new GridOption("XYZ", "XYZ"),
        new GridOption("XZY", "XZY"),
        new GridOption("YXZ", "YXZ"),
        new GridOption("YZX", "YZX"),
        new GridOption("ZXY", "ZXY"),
        new GridOption("ZYX", "ZYX"),
    };

    /// <summary>把任意值收敛到合法轴序，未知值回落到 XYZ。</summary>
    /// <param name="value">原始值。</param>
    /// <returns>合法轴序值。</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "XYZ";
        }

        var match = All.FirstOrDefault(option =>
            string.Equals(option.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match.Value ?? "XYZ";
    }

    /// <summary>按值取界面显示名。</summary>
    /// <param name="value">轴序值。</param>
    /// <returns>显示名。</returns>
    public static string LabelOf(string? value) =>
        All.FirstOrDefault(option => option.Value == Normalize(value)).Label;
}

#endregion
