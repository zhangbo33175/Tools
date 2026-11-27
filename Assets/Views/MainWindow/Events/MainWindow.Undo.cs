/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.Undo.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口撤销 / 重做：拖动结算入栈、Ctrl+Z 回滚、Ctrl+Y 重做
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MapEditorTool.Controls;
using MapEditorTool.Services;

namespace MapEditorTool;

public partial class MainWindow : Window
{
    /// <summary>场景编辑撤销栈。画布不持有它，只把「拖动结束」这个事实抛回来。</summary>
    private readonly UndoService _undo = new();

    /// <summary>接上画布的拖动结算事件；地图切换时清栈由 LoadMapFile 负责。</summary>
    private void WireUndo()
    {
        Canvas.ObjectMoveCompleted += (_, e) =>
        {
            _undo.Push(new ObjectMoveCommand(new[] { new ObjectMoveEntry(e.Target, e.From, e.To) }));
            SetMessage($"已移动 {e.Target.Name}：({e.From.X:0},{e.From.Y:0}) → ({e.To.X:0},{e.To.Y:0})，Ctrl+Z 可撤销");
        };
    }

    /// <summary>
    /// 撤销一步场景编辑。
    /// <para>回滚命令只改数据，重绘与面板刷新在这里统一做一次：
    /// 走 <see cref="MapCanvas.NotifyDocumentChanged"/> 而不是挨个面板刷，保证属性卡 / 路径列表 / 收集物统计口径一致。</para>
    /// </summary>
    /// <returns>true = 确实撤销了什么；false = 栈空或当前不适合撤销。</returns>
    private bool UndoSceneEdit()
    {
        if (_mode != 0)
        {
            return false;
        }

        var description = _undo.Undo();
        if (description is null)
        {
            SetMessage("没有可撤销的操作");
            return false;
        }

        Canvas.Refresh();
        Canvas.NotifyDocumentChanged();
        SetMessage($"已撤销：{description}");
        return true;
    }

    /// <summary>重做一步（Ctrl+Y / Ctrl+Shift+Z），刷新口径与撤销完全一致。</summary>
    /// <returns>true = 确实重做了什么。</returns>
    private bool RedoSceneEdit()
    {
        if (_mode != 0)
        {
            return false;
        }

        var description = _undo.Redo();
        if (description is null)
        {
            SetMessage("没有可重做的操作");
            return false;
        }

        Canvas.Refresh();
        Canvas.NotifyDocumentChanged();
        SetMessage($"已重做：{description}");
        return true;
    }

    /// <summary>
    /// 场景编辑的 Ctrl+Z / Ctrl+Y 分流。焦点在文本框时不抢键：那里 Ctrl+Z 是文本自身的撤销，
    /// 抢过来会让「改了个数值再 Ctrl+Z」把物件弹回原位而输入框文字没动，语义更乱。
    /// </summary>
    /// <param name="e">窗口键事件。</param>
    /// <returns>true = 已被撤销 / 重做消费。</returns>
    private bool HandleUndoShortcut(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Source is TextBox)
        {
            return false;
        }

        // e.Source 只覆盖「焦点控件自己冒泡」的情况；可编辑下拉框里的输入框、
        // 模板内部再包一层的输入框都要靠焦点链上溯，否则会误抢文本框的撤销键。
        if (IsTextEditingFocus(FocusManager?.GetFocusedElement()))
        {
            return false;
        }

        // Shift 优先判定：Ctrl+Shift+Z 是重做，不能被下面的 Ctrl+Z 分支抢走
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return RedoSceneEdit();
        }

        if (e.Key == Key.Z)
        {
            return UndoSceneEdit();
        }

        if (e.Key == Key.Y)
        {
            return RedoSceneEdit();
        }

        return false;
    }

    /// <summary>焦点是否落在（任意层级嵌套的）文本框上。</summary>
    /// <param name="focused">当前焦点元素，可能为空。</param>
    /// <returns>true = 正在编辑文本，撤销键归文本自己。</returns>
    private static bool IsTextEditingFocus(IInputElement? focused)
    {
        var visual = focused as Visual;
        while (visual is not null)
        {
            if (visual is TextBox)
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
    }
}
