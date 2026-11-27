/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectMoveEventArgs.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布事件载荷：一次拖动结束后的移动前 / 移动后坐标
 ***************************************************************/

using System;
using Avalonia;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>
/// 一次拖动结束（松手）后的结果。画布只汇报事实，不关心调用方是记撤销栈还是做别的统计。
/// </summary>
public sealed class ObjectMoveEventArgs : EventArgs
{
    /// <summary>构造一次拖动结果。</summary>
    /// <param name="target">被移动的物件。</param>
    /// <param name="from">按下时物件左上角的世界坐标。</param>
    /// <param name="to">松手时物件左上角的世界坐标。</param>
    public ObjectMoveEventArgs(PlacedObject target, Point from, Point to)
    {
        Target = target;
        From = from;
        To = to;
    }

    /// <summary>被移动的物件。</summary>
    public PlacedObject Target { get; }

    /// <summary>拖动前坐标。</summary>
    public Point From { get; }

    /// <summary>拖动后坐标。</summary>
    public Point To { get; }
}
