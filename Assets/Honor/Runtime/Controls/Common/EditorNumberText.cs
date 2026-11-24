/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorNumberText.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器数值显示：紧凑小数与整数值回退的统一格式
 ***************************************************************/

using System;
using System.Globalization;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器字段的数值显示入口。坐标、碰撞、阴影、速度等输入框共用同一套
/// InvariantCulture 格式，避免不同面板出现小数位或小数点差异。
/// </summary>
public static class EditorNumberText
{
    #region 常量

    /// <summary>紧凑小数格式：最多两位，整数不补零。</summary>
    public const string CompactFormat = "0.##";

    /// <summary>与整数的判定阈值；小于该差值时按整数显示。</summary>
    public const double WholeTolerance = 0.001;

    #endregion

    #region 公开方法

    /// <summary>按紧凑小数格式输出，如 1.5、2、1.25。</summary>
    /// <param name="value">待显示数值。</param>
    /// <returns>InvariantCulture 文本。</returns>
    public static string Compact(double value) =>
        value.ToString(CompactFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 接近整数时去掉小数位，其余按 <see cref="CompactFormat"/> 输出。
    /// 用于就地拖拽输入框，避免出现 12.0000001 之类的噪声。
    /// </summary>
    /// <param name="value">待显示数值。</param>
    /// <returns>整数值或紧凑小数文本。</returns>
    public static string CompactOrWhole(double value) =>
        Math.Abs(value - Math.Round(value)) < WholeTolerance
            ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : Compact(value);

    #endregion
}
