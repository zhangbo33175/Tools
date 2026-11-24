/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IdGenerator.cs
 * author:    云毅
 * created:   2026
 * descrip:   短 ID 生成：统一地图实例、交通对象与临时文件命名规则
 ***************************************************************/

using System;

namespace MapEditorTool.Models;

/// <summary>
/// 短 ID 规则。地图物件、交通灯、横道、迷雾区域与临时文件都使用
/// N 格式 GUID 的前 8 位；集中在这里，避免各处硬编码长度或格式。
/// </summary>
public static class IdGenerator
{
    /// <summary>短 ID 长度。</summary>
    public const int ShortLength = 8;

    /// <summary>生成一个新的 8 位十六进制短 ID。</summary>
    /// <returns>小写短 ID。</returns>
    public static string NewShortId() => Guid.NewGuid().ToString("N")[..ShortLength];
}
