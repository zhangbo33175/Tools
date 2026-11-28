/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RowVisual.cs
 * author:    云毅
 * created:   2026
 * descrip:   列表行/选项条的三态配色：常态、悬停、选中，供各列表统一取用。
 ***************************************************************/

using Avalonia.Media;

namespace MapEditorTool.Controls;

/// <summary>列表行与选项条的「常态 / 悬停 / 选中」三态配色。</summary>
/// <param name="NormalBack">常态底色。</param>
/// <param name="NormalBorder">常态描边。</param>
/// <param name="HoverBack">悬停底色。</param>
/// <param name="SelectedBack">选中底色。</param>
/// <param name="SelectedBorder">选中描边。</param>
/// <param name="SelectedAccentWidth">选中时左侧强调线宽度；0 表示只用整圈描边。</param>
public readonly record struct RowVisual(
    IBrush NormalBack,
    IBrush NormalBorder,
    IBrush HoverBack,
    IBrush SelectedBack,
    IBrush SelectedBorder,
    double SelectedAccentWidth = 0);
