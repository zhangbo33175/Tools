/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ServiceOutcome.cs
 * author:    云毅
 * created:   2026
 * descrip:   服务层「成功 / 错误串」两态的唯一收口：把可能抛异常的一次性操作压成布尔结果，
 *            各存储服务不再各自手写一遍 try-catch。
 ***************************************************************/

using System;

namespace MapEditorTool.Services;

/// <summary>
/// 服务方法统一的结果包装。约定：返回 true 时 <c>error</c> 为空串，
/// 返回 false 时 <c>error</c> 为可直接展示的失败原因。
/// </summary>
internal static class ServiceOutcome
{
    /// <summary>
    /// 执行一段无返回值的工作；异常统一转成错误串。
    /// </summary>
    /// <param name="work">实际操作。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否成功。</param>
    internal static bool Run(Action work, out string error) =>
        Run(() =>
        {
            work();
            return null;
        }, out error);

    /// <summary>
    /// 执行一段可能带「提前失败」的工作：返回 <c>null</c> 表示成功，
    /// 返回非空文本表示失败原因（用于校验失败这类不需要异常的分支）。
    /// </summary>
    /// <param name="work">实际工作，返回失败原因或 null。</param>
    /// <param name="error">失败原因，成功为空串。</param>
    /// <returns>是否成功。</returns>
    internal static bool Run(Func<string?> work, out string error)
    {
        try
        {
            var failure = work();
            error = failure ?? string.Empty;
            return failure is null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
