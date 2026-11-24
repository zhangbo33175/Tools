/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorLogs.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器诊断日志统一入口：固定目录、UTF-8 编码与失败静默
 ***************************************************************/

using System.IO;

namespace MapEditorTool.Services;

/// <summary>
/// 编辑器诊断日志入口。日志只用于定位问题，写入失败必须静默，
/// 不能反过来打断导入、交互或渲染。
/// </summary>
internal static class EditorLogs
{
    #region 写入

    /// <summary>追加一段诊断文本；目录不存在时自动创建。</summary>
    /// <param name="fileName">日志文件名，只允许落在 <see cref="EditorPaths.Logs"/> 下。</param>
    /// <param name="content">完整文本，调用方负责换行格式。</param>
    internal static void Append(string fileName, string content) =>
        WriteCore(fileName, content, append: true);

    /// <summary>覆盖写入一段诊断文本；目录不存在时自动创建。</summary>
    /// <param name="fileName">日志文件名，只允许落在 <see cref="EditorPaths.Logs"/> 下。</param>
    /// <param name="content">完整文本。</param>
    internal static void Write(string fileName, string content) =>
        WriteCore(fileName, content, append: false);

    #endregion

    #region 内部实现

    private static void WriteCore(string fileName, string content, bool append)
    {
        try
        {
            FileSystemIo.EnsureDirectory(EditorPaths.Logs);
            var path = Path.Combine(EditorPaths.Logs, Path.GetFileName(fileName));
            if (append)
            {
                FileSystemIo.AppendText(path, content);
            }
            else
            {
                FileSystemIo.WriteText(path, content);
            }
        }
        catch
        {
            // 日志失败不影响业务，调用方不需要额外兜底。
        }
    }

    #endregion
}
