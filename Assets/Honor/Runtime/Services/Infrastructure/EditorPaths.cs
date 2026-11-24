/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorPaths.cs
 * author:    云毅
 * created:   2026
 * descrip:   编辑器输出目录：配置、日志与工程根缺失时的回退目录
 ***************************************************************/

using System;
using System.IO;

namespace MapEditorTool.Services;

/// <summary>
/// 编辑器运行期输出目录。统一 <c>output</c> 根与常见子目录，
/// 各服务不再各自拼 <c>AppContext.BaseDirectory\output\...</c>。
/// </summary>
public static class EditorPaths
{
    #region 根目录

    /// <summary>程序目录下的输出根。</summary>
    public static string OutputRoot => Path.Combine(AppContext.BaseDirectory, "output");

    /// <summary>配置目录：模板、动画、设置与资源根清单。</summary>
    public static string Config => Path.Combine(OutputRoot, "Config");

    /// <summary>异常日志目录。</summary>
    public static string Logs => Path.Combine(OutputRoot, "Logs");

    #endregion

    #region 工程根缺失时的回退目录

    /// <summary>地图 JSON 回退目录。</summary>
    public static string FallbackMapDataDirectory => Path.Combine(OutputRoot, "MapDatas");

    /// <summary>切片资源回退目录。</summary>
    public static string FallbackMapDirectory => Path.Combine(OutputRoot, "Map");

    /// <summary>资源库首选回退目录 <c>output\Res</c>。</summary>
    public static string FallbackResDirectory => Path.Combine(OutputRoot, "Res");

    /// <summary>资源库次选回退目录 <c>output\Resources</c>。</summary>
    public static string FallbackResourcesDirectory => Path.Combine(OutputRoot, "Resources");

    #endregion
}
