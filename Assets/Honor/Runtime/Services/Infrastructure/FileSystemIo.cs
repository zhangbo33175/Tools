/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileSystemIo.cs
 * author:    云毅
 * created:   2026
 * descrip:   配置资源目录的共用文件系统操作：路径解析、失败清理与 UTF-8 文本读写
 ***************************************************************/

using System.IO;
using System.Text;

namespace MapEditorTool.Services;

/// <summary>
/// 资源目录读写的共用文件系统入口。路径一律相对“配置目录”解析，
/// 清理动作只用于回滚导入失败；文本统一为无 BOM 的 UTF-8。
/// </summary>
internal static class FileSystemIo
{
    #region 文本编码

    /// <summary>编辑器文本落盘编码：UTF-8 且不写 BOM。</summary>
    internal static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>按统一 UTF-8 编码写入文本。</summary>
    internal static void WriteText(string path, string content) =>
        File.WriteAllText(path, content, Utf8NoBom);

    /// <summary>按统一 UTF-8 编码读取文本。</summary>
    internal static string ReadText(string path) =>
        File.ReadAllText(path, Utf8NoBom);

    /// <summary>按统一 UTF-8 编码追加文本。</summary>
    internal static void AppendText(string path, string content) =>
        File.AppendAllText(path, content, Utf8NoBom);

    /// <summary>按统一 UTF-8 编码读取全部行。</summary>
    internal static string[] ReadLines(string path) =>
        File.ReadAllLines(path, Utf8NoBom);

    #endregion

    #region 目录保证

    /// <summary>保证目录存在；已存在时保持幂等。</summary>
    /// <param name="directory">目标目录。</param>
    internal static void EnsureDirectory(string directory) =>
        Directory.CreateDirectory(directory);

    /// <summary>保证文件所在父目录存在；无父目录时直接返回。</summary>
    /// <param name="path">文件路径。</param>
    internal static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    #endregion

    #region 路径与清理
    /// <summary>解析配置目录下的路径；已是绝对路径时原样返回。</summary>
    /// <param name="baseDirectory">配置目录。</param>
    /// <param name="path">绝对或相对路径。</param>
    /// <returns>规范化绝对路径；空输入返回空串。</returns>
    internal static string ResolvePath(string baseDirectory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    /// <summary>尝试删除导入失败留下的目录；删除失败不抛出。</summary>
    /// <param name="directory">待清理目录。</param>
    internal static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
            // 清理失败不覆盖原始导入错误。
        }
    }

    #endregion
}
