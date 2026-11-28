/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFactory.Forms.cs
 * author:    云毅
 * created:   2026
 * descrip:   表单件：字段标签、字段行与提示文字。
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器控件工厂：按「按钮 / 卡片 / 表单 / 标签 / 标记」分文件维护，
/// 对外仍是同一个静态类，调用方只认 <c>UiFactory.xxx</c>。
/// </summary>
public static partial class UiFactory
{
    #region 表单标签

    /// <summary>
    /// 创建表单字段标签：默认去掉尾部冒号，靠留白与控件分隔。
    /// 属性栏、资源整理、剧情与配置弹窗共用这一份标签样式。
    /// </summary>
    /// <param name="text">标签文案，尾部全角/半角冒号会被裁掉。</param>
    /// <param name="fontSize">字号；默认 11。</param>
    /// <param name="foreground">文字色；null 使用次要文字色。</param>
    /// <param name="horizontalAlignment">水平对齐；右对齐时文字同向右收。</param>
    /// <param name="margin">外边距；null 使用 0,0,8,0。</param>
    /// <returns>字段标签。</returns>
    public static TextBlock FieldLabel(
        string text,
        double fontSize = UiMetrics.FontBody,
        IBrush? foreground = null,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        Thickness? margin = null)
    {
        var alignedRight = horizontalAlignment == HorizontalAlignment.Right;
        return new TextBlock
        {
            Text = text.TrimEnd('：'),
            FontSize = fontSize,
            Foreground = foreground ?? UiTheme.TextSecondary,
            HorizontalAlignment = horizontalAlignment,
            TextAlignment = alignedRight ? TextAlignment.Right : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin ?? new Thickness(0, 0, 8, 0),
        };
    }

    #endregion

    #region 字段行

    /// <summary>字段行的尺寸档：标签列宽、标签字号与对齐方式。</summary>
    /// <param name="LabelWidth">标签列宽。</param>
    /// <param name="LabelFontSize">标签字号。</param>
    /// <param name="LabelAlignment">标签对齐；右对齐时文字贴向控件。</param>
    public readonly record struct FieldRowSkin(
        double LabelWidth,
        double LabelFontSize = UiMetrics.FontTitle,
        HorizontalAlignment LabelAlignment = HorizontalAlignment.Right);

    /// <summary>
    /// 创建「标签 + 控件」的字段行：标签列定宽，控件列撑满剩余宽度。
    /// 发布窗与地图配置窗的路径行、地图名行共用这一份网格定义。
    /// </summary>
    /// <param name="label">标签文案。</param>
    /// <param name="field">字段控件，会设为横向拉伸。</param>
    /// <param name="skin">尺寸档。</param>
    /// <param name="labelForeground">标签文字色；null 使用次要文字色。</param>
    /// <returns>两列网格。</param>
    public static Grid FieldRow(string label, Control field, FieldRowSkin skin, IBrush? labelForeground = null) =>
        FieldRow(skin, label, field, null, labelForeground);

    /// <summary>
    /// 创建带尾部附件的字段行：标签列定宽，控件列撑满，尾部列贴右（浏览按钮、补充说明）。
    /// </summary>
    /// <param name="label">标签文案。</param>
    /// <param name="field">字段控件。</param>
    /// <param name="trailing">尾部控件。</param>
    /// <param name="skin">尺寸档。</param>
    /// <param name="labelForeground">标签文字色；null 使用次要文字色。</param>
    /// <returns>三列网格。</returns>
    public static Grid FieldRowWithTrailing(
        string label,
        Control field,
        Control trailing,
        FieldRowSkin skin,
        IBrush? labelForeground = null) => FieldRow(skin, label, field, trailing, labelForeground);

    /// <summary>字段行装配：尾部为空时只占「标签 + 控件」两列。</summary>
    private static Grid FieldRow(
        FieldRowSkin skin,
        string label,
        Control field,
        Control? trailing,
        IBrush? labelForeground)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                trailing is null ? $"{skin.LabelWidth},*" : $"{skin.LabelWidth},*,Auto"),
        };

        var text = FieldLabel(label, skin.LabelFontSize, labelForeground, skin.LabelAlignment);
        field.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.Children.Add(text);
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);

        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }

        return grid;
    }

    #endregion

    #region 提示文字

    /// <summary>
    /// 创建提示文字：次要文字色、默认自动换行。表单里的参数说明、操作提示共用这一份。
    /// </summary>
    /// <param name="text">提示文案。</param>
    /// <param name="fontSize">字号；默认 10。</param>
    /// <param name="wrap">是否自动换行。</param>
    /// <param name="margin">外边距。</param>
    /// <returns>提示文字块。</returns>
    public static TextBlock HintText(
        string text,
        double fontSize = UiMetrics.FontCaption,
        bool wrap = true,
        Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = fontSize,
        Foreground = UiTheme.TextSecondary,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        Margin = margin ?? default,
    };

    #endregion

    #region 输入提交

    /// <summary>
    /// 给输入控件挂「回车提交」：只在回车键上执行一次提交并吃掉事件，其余按键照常输入。
    /// 属性栏、资源表编辑器、路径点坐标框共用这一份接线，不再各自写一遍 KeyDown 判断。
    /// </summary>
    /// <param name="target">输入控件（TextBox / ComboBox 等）。</param>
    /// <param name="commit">提交动作。</param>
    public static void CommitOnEnter(Control target, Action commit)
    {
        target.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            commit();
            args.Handled = true;
        };
    }

    #endregion

}
