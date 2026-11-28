/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.Chips.cs
 * author:    云毅
 * created:   2026
 * descrip:   标签胶囊：小徽标的统一构建。
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器控件工厂：按「按钮 / 卡片 / 表单 / 标签 / 标记」分文件维护，
/// 对外仍是同一个静态类，调用方只认 <c>UiFactory.xxx</c>。
/// </summary>
public static partial class UiFactory
{
    #region 标签胶囊

    /// <summary>标签胶囊的尺寸档：字号、左右与上下内边距、圆角。</summary>
    /// <param name="FontSize">字号。</param>
    /// <param name="PaddingX">左右内边距。</param>
    /// <param name="PaddingY">上下内边距。</param>
    /// <param name="CornerRadius">圆角。</param>
    public readonly record struct ChipSkin(
        double FontSize = 9,
        double PaddingX = 5,
        double PaddingY = 1,
        double CornerRadius = UiMetrics.SmallCornerRadius)
    {
        /// <summary>
        /// 默认档。record struct 的 <c>new ChipSkin()</c> 会走编译器生成的零初始化构造，
        /// 拿到 FontSize=0 而不是参数默认值，因此这里显式给出默认档，调用方不要用无参构造。
        /// </summary>
        public static ChipSkin Default => new(9, 5, 1, UiMetrics.SmallCornerRadius);
    }

    /// <summary>
    /// 创建标签胶囊：底色 + 小字，垂直居中。
    /// 动画列表的能力标签、「起点」徽标与交通规则行尾标记共用这一份结构，
    /// 只在字号与圆角上按各自的密度档取值。
    /// </summary>
    /// <param name="text">标签文案。</param>
    /// <param name="background">底色。</param>
    /// <param name="foreground">文字色；null 使用白字（深色底上的常规用法）。</param>
    /// <param name="skin">尺寸档。</param>
    /// <returns>标签胶囊。</returns>
    public static Border TagChip(string text, IBrush background, IBrush? foreground = null, ChipSkin? skin = null)
    {
        var size = skin ?? ChipSkin.Default;
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(size.CornerRadius),
            Padding = new Thickness(size.PaddingX, size.PaddingY),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = size.FontSize,
                Foreground = foreground ?? UiTheme.White,
            },
        };
    }

    #endregion

}
