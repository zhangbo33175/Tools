/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RowState.cs
 * author:    云毅
 * created:   2026
 * descrip:   列表行三态的唯一落笔点：按 RowVisual 刷新底色与描边、挂接悬停回调，
 *            编辑器列表卡与预览窗列表项不再各自手写一套赋值。
 ***************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace MapEditorTool.Controls;

/// <summary>把 <see cref="RowVisual"/> 落到具体控件上，并统一处理悬停订阅。</summary>
internal static class RowState
{
    /// <summary>刷新卡片行（Border）的三态外观；选中时若配色带强调线则改为左侧边。</summary>
    /// <param name="target">目标行。</param>
    /// <param name="selected">是否选中。</param>
    /// <param name="hovered">是否悬停。</param>
    /// <param name="visual">三态配色。</param>
    public static void Apply(Border target, bool selected, bool hovered, RowVisual visual)
    {
        if (selected)
        {
            target.Background = visual.SelectedBack;
            target.BorderBrush = visual.SelectedBorder;
            if (visual.SelectedAccentWidth > 0)
            {
                target.BorderThickness = new Thickness(visual.SelectedAccentWidth, 0, 0, 0);
            }

            return;
        }

        target.Background = hovered ? visual.HoverBack : visual.NormalBack;
        target.BorderBrush = visual.NormalBorder;
    }

    /// <summary>刷新模板化控件（列表项、按钮）的选中外观；未选中时清空高亮。</summary>
    /// <param name="target">目标控件。</param>
    /// <param name="selected">是否选中。</param>
    /// <param name="visual">三态配色。</param>
    public static void Apply(TemplatedControl target, bool selected, RowVisual visual)
    {
        if (!selected)
        {
            Clear(target);
            return;
        }

        target.Background = visual.SelectedBack;
        target.BorderBrush = visual.SelectedBorder;
        target.BorderThickness = visual.SelectedAccentWidth > 0
            ? new Thickness(visual.SelectedAccentWidth, 0, 0, 0)
            : new Thickness(1);
    }

    /// <summary>清掉高亮，回到透明无描边（列表项默认外观由主题负责）。</summary>
    /// <param name="target">目标控件。</param>
    public static void Clear(TemplatedControl target)
    {
        target.Background = UiTheme.Transparent;
        target.BorderThickness = new Thickness(0);
    }

    /// <summary>订阅指针进出，回调只报「是否进入」，由调用方决定怎么刷新。</summary>
    /// <param name="target">目标控件。</param>
    /// <param name="onHover">悬停状态回调。</param>
    public static void AttachHover(Control target, System.Action<bool> onHover)
    {
        target.PointerEntered += (_, _) => onHover(true);
        target.PointerExited += (_, _) => onHover(false);
    }
}
