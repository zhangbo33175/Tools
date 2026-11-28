/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFonts.cs
 * author:    云毅
 * created:   2026
 * descrip:   界面字体族的唯一来源：等宽数字、正文默认字体都从这里取，
 *            避免各面板各自 new FontFamily 再各自写一遍字体名。
 ***************************************************************/

using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>编辑器界面字体族表。</summary>
public static class UiFonts
{
    /// <summary>等宽字体：坐标、数值、计时这类要竖排对齐的文本。</summary>
    public static readonly FontFamily Mono = new("Consolas");
}
