/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiMetrics.cs
 * author:    云毅
 * created:   2026
 * descrip:   统一视觉度量：圆角阶梯、控件高度与常用间距的唯一来源，
 *            各面板只引用这里的语义常量，不再各自复制数字。
 ***************************************************************/

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器界面的统一度量表。圆角沿用「输入框 6 / 按钮 5 / 卡片 8」三档，
/// 高度与间距按用途归档，避免同一数字散落在多个面板里各写一遍。
/// </summary>
public static class UiMetrics
{
    /// <summary>输入框、下拉框圆角。</summary>
    public const double InputCornerRadius = 6.0;

    /// <summary>按钮、胶囊、标签圆角。</summary>
    public const double ButtonCornerRadius = 5.0;

    /// <summary>卡片、面板、分组块圆角。</summary>
    public const double CardCornerRadius = 8.0;

    /// <summary>标准表单行高（输入框 / 下拉框 / 单选行）。</summary>
    public const double FieldHeight = 30.0;

    /// <summary>紧凑行高（表格单元格、列表内按钮）。</summary>
    public const double CompactRowHeight = 26.0;

    /// <summary>字段之间的纵向间距。</summary>
    public const double FieldGap = 8.0;

    /// <summary>卡片、分组块之间的间距。</summary>
    public const double BlockGap = 10.0;

    /// <summary>标题竖条宽度（3px 青条）。</summary>
    public const double AccentBarWidth = 3.0;

    /// <summary>标题竖条圆角：3px 宽的细条只做轻微倒角。</summary>
    public const double AccentBarCornerRadius = 2.0;

    /// <summary>小构件圆角：标签、缩略图、内嵌舞台面这类小面积容器。</summary>
    public const double SmallCornerRadius = 3.0;

    /// <summary>次要说明字号（单位、计数、辅助提示）。</summary>
    public const double FontCaption = 10.0;

    /// <summary>正文字号（字段值、按钮文字、列表正文）。</summary>
    public const double FontBody = 11.0;

    /// <summary>小标题字号（卡片标题、分组标题、行首强调）。</summary>
    public const double FontTitle = 12.0;
}
