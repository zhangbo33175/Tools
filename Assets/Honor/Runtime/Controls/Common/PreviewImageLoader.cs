/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PreviewImageLoader.cs
 * author:    云毅
 * created:   2026
 * descrip:   列表与预览共用的安全图片加载入口
 ***************************************************************/

using System.IO;
using Avalonia.Media.Imaging;

namespace MapEditorTool.Controls;

/// <summary>
/// 编辑器缩略图/大图的安全加载入口。文件缺失或解码失败时返回 <see langword="null"/>，
/// 由调用方显示占位，不让单张坏图打断整个列表构建。
/// </summary>
internal static class PreviewImageLoader
{
    /// <summary>安全加载位图。</summary>
    /// <param name="path">图片绝对路径。</param>
    /// <returns>解码后的位图；文件不存在或解码失败时为 <see langword="null"/>。</returns>
    internal static Bitmap? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
