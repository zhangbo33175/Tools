/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUndoableCommand.cs
 * author:    云毅
 * created:   2026
 * descrip:   撤销命令契约：一次编辑只描述「怎么还原」与「怎么再做一遍」
 ***************************************************************/

namespace MapEditorTool.Services;

/// <summary>
/// 一次可撤销编辑。
/// <para>
/// 约定：命令在编辑**已经发生**之后入栈，因此 <see cref="Undo"/> / <see cref="Redo"/> 只负责改数据，
/// 不负责触发重绘和面板刷新——那一步统一由调用方在撤销 / 重做后做一次，
/// 避免每条命令各自决定刷新口径而漏刷某个面板。
/// </para>
/// </summary>
public interface IUndoableCommand
{
    /// <summary>面向用户的操作描述（「移动 prop_fence_1」），撤销后直接拿去当提示文案。</summary>
    string Description { get; }

    /// <summary>回滚到操作前的状态。必须可重入：同一命令可能被撤销多次（撤销→重做→撤销）。</summary>
    void Undo();

    /// <summary>重新应用一次操作，语义与当初用户做的那一次完全一致。</summary>
    void Redo();
}

/// <summary>
/// 可选的命令合并能力：连续微调（同一个数值框来回拖）合成一步，
/// 避免一次拖动在撤销栈里塞上百条记录。不需要合并的命令不实现该接口。
/// </summary>
public interface IMergeableUndoCommand : IUndoableCommand
{
    /// <summary>尝试把紧随其后的同源操作并入当前命令。</summary>
    /// <param name="next">下一条同源命令。</param>
    /// <returns>true = 已并入，调用方不再单独入栈。</returns>
    bool TryMerge(IUndoableCommand next);
}
