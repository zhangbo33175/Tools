/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ImageFileTypes.cs
 * author:    云毅
 * created:   2026
 * descrip:   动画/贴图导入支持的图片扩展名与判定
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;

namespace MapEditorTool.Services;

/// <summary>
/// 贴图与动画页支持的图片格式。Spine、序列帧与 Atlas 页解析共用同一份扩展名清单，
/// 避免导入端与加载端对 <c>webp</c> 等格式判断不一致。
/// </summary>
internal static class ImageFileTypes
{
    /// <summary>支持的图片扩展名（含点号）。</summary>
    internal static readonly IReadOnlyList<string> All = new[] { ".png", ".jpg", ".jpeg", ".webp" };

    /// <summary>支持的无扩展名探测顺序：先试原名，再逐个补扩展名。</summary>
    internal static readonly IReadOnlyList<string> AllWithEmpty = new[] { string.Empty, ".png", ".jpg", ".jpeg", ".webp" };

    /// <summary>判断文件或路径是否为支持的图片格式。</summary>
    /// <param name="path">文件名或路径。</param>
    /// <returns>扩展名受支持时为 <see langword="true"/>。</returns>
    internal static bool IsSupported(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Length == 0)
        {
            return false;
        }

        foreach (var supported in All)
        {
            if (extension.Equals(supported, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
