/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UndoService.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑撤销栈：后进先出的命令栈 + 重做栈，容量固定，切图即清空
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace MapEditorTool.Services;

/// <summary>
/// 场景编辑的撤销 / 重做入口。
/// <para>
/// 只存命令、不认画布：画布只负责在交互结束时把「移动前 / 移动后」坐标抛出来，
/// 由窗口层组装命令并入栈。这样后续要接删除、摆放、属性修改，只要加一个命令类，
/// 不用再改画布和快捷键。
/// </para>
/// </summary>
public sealed class UndoService
{
    /// <summary>栈深上限。一次拖动一条，100 条足够覆盖一轮手滑；再深只是白占内存。</summary>
    private const int Capacity = 100;

    private readonly List<IUndoableCommand> _undoStack = new();
    private readonly List<IUndoableCommand> _redoStack = new();

    /// <summary>撤销 / 重做状态变化，供未来的工具栏按钮启停使用。</summary>
    public event EventHandler? Changed;

    /// <summary>是否还有可撤销的操作。</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>是否还有可重做的操作。</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>下一次撤销将回滚的操作描述；空栈时为 null。</summary>
    public string? UndoDescription => CanUndo ? _undoStack[^1].Description : null;

    /// <summary>
    /// 把一条**已经生效**的编辑压进撤销栈。新操作会清空重做栈（和所有编辑器的语义一致）。
    /// </summary>
    /// <param name="command">描述本次编辑的命令。</param>
    public void Push(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 同源连续操作（同一个值被来回拖）并进上一条，避免拖动过程把栈塞满
        if (command is IMergeableUndoCommand && _undoStack.Count > 0 &&
            _undoStack[^1] is IMergeableUndoCommand last && last.TryMerge(command))
        {
            _redoStack.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _undoStack.Add(command);
        if (_undoStack.Count > Capacity)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>撤销一步。</summary>
    /// <returns>被撤销的操作描述；栈空时为 null。</returns>
    /// <remarks>命令只改数据、不抛异常；这里不做 catch，免得吞掉真正需要暴露的模型错误。</remarks>
    public string? Undo()
    {
        if (_undoStack.Count == 0)
        {
            return null;
        }

        var command = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        command.Undo();
        _redoStack.Add(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return command.Description;
    }

    /// <summary>重做一步（Ctrl+Y / Ctrl+Shift+Z）。</summary>
    /// <returns>被重做的操作描述；栈空时为 null。</returns>
    public string? Redo()
    {
        if (_redoStack.Count == 0)
        {
            return null;
        }

        var command = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        command.Redo();
        _undoStack.Add(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return command.Description;
    }

    /// <summary>切换地图 / 重载磁盘数据时清空两侧栈：旧命令引用的是上一张图的对象。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
