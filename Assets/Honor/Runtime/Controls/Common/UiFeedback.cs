/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UiFeedback.cs
 * author:    云毅
 * created:   2026
 * descrip:   UI 层反馈文案与兜底：失败提示统一为「动作失败：原因」
 ***************************************************************/
using System;

namespace MapEditorTool.Controls;

/// <summary>
/// UI 层反馈文案的唯一出处。状态栏、面板提示、预览窗状态行都用同一句
/// 「<paramref name="action"/>失败：<paramref name="error"/>」，改措辞只改这里。
/// </summary>
public static class UiFeedback
{
    /// <summary>用异常对象拼失败提示。</summary>
    /// <param name="action">动作名，例如「保存」「导出切片」「PSD 解析」。</param>
    /// <param name="error">捕获到的异常。</param>
    /// <returns>可直接显示的中文提示。</returns>
    public static string Failed(string action, Exception error) =>
        $"{action}失败：{error.Message}";

    /// <summary>用已有原因串拼失败提示（服务层把 reason 写在 error 出参里时用这条）。</summary>
    /// <param name="action">动作名。</param>
    /// <param name="reason">失败原因，通常是 service 返回的 error 串。</param>
    /// <returns>可直接显示的中文提示。</returns>
    public static string Failed(string action, string reason) =>
        $"{action}失败：{reason}";
}
