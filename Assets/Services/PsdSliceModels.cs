/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdSliceModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   PSD 切片数据模型：切片记录、切片集合与清单缓存
 ***************************************************************/

using System.Collections.Generic;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 一条自裁切片记录（与 PSD 图层一一对应）。
/// <para>
/// <see cref="Left"/>/<see cref="Top"/>/<see cref="Width"/>/<see cref="Height"/> 是「裁掉多余透明边之后」的
/// 最终画布矩形（已经叠进 <see cref="SourceLeft"/>/<see cref="SourceTop"/> 的偏移），导入时直接用这组值定位实例；
/// 原始图层框留在 <see cref="SourceLeft"/>…<see cref="SourceHeight"/> 里做核对。
/// </para>
/// <para>
/// <see cref="Rotation"/> 是「本层像素 = 复用文件顺时针转多少度」：0 表示同向，90 / 180 / 270 表示
/// 该层是已有切片旋转后的同一张图，落盘只留一份，实例用旋转字段还原。
/// </para>
/// </summary>
public sealed record PsdSliceRecord(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("layer")] string Layer = MapLayerKeys.Prop,
    [property: JsonPropertyName("band")] int Band = LayerBands.Standing,
    [property: JsonPropertyName("sourceLeft")] int SourceLeft = 0,
    [property: JsonPropertyName("sourceTop")] int SourceTop = 0,
    [property: JsonPropertyName("sourceWidth")] int SourceWidth = 0,
    [property: JsonPropertyName("sourceHeight")] int SourceHeight = 0,
    [property: JsonPropertyName("trimmed")] bool Trimmed = false,
    [property: JsonPropertyName("rotation")] int Rotation = 0);

/// <summary>切片集合：既用于「导出切片」落盘结果，也用于导入时的内存预览。</summary>
public sealed class PsdSliceSet
{
    /// <summary>图层序号 → 切片路径（预览时是「将来会落盘的绝对路径」，磁盘上并不存在）。</summary>
    public Dictionary<int, string> ByIndex { get; } = new();

    /// <summary>图层序号 → 切片记录（含分层、裁剪后的画布矩形、复用旋转与源框）。</summary>
    public Dictionary<int, PsdSliceRecord> Records { get; } = new();

    /// <summary>图层序号 → PNG 字节（只有导入预览会填，导出落盘时为空以省内存）。</summary>
    public Dictionary<int, byte[]> PngByIndex { get; } = new();

    /// <summary>整幅底图（导出成功或预览合成时非空）。</summary>
    public string? CompositePath { get; set; }

    /// <summary>整幅底图的 PNG 字节（导入预览用）。</summary>
    public byte[]? CompositePng { get; set; }

    /// <summary>底图像素宽（预览用）。</summary>
    public int CompositeWidth { get; set; }

    /// <summary>底图像素高（预览用）。</summary>
    public int CompositeHeight { get; set; }

    /// <summary>本次新烘焙的切片数。</summary>
    public int BakedCount { get; set; }

    /// <summary>命中缓存的切片数。</summary>
    public int CachedCount { get; set; }

    /// <summary>像素解码失败（压缩方式不支持等）的图层数。</summary>
    public int FailedCount { get; set; }

    /// <summary>整层全透明（裁完无像素）而跳过的图层数。</summary>
    public int SkippedCount { get; set; }

    /// <summary>与已出图切片像素完全一致（含旋转后一致）而复用的图层数。</summary>
    public int DuplicateCount { get; set; }

    /// <summary>其中靠旋转 90 / 180 / 270 判定为同一张图的图层数。</summary>
    public int RotationDuplicateCount { get; set; }

    /// <summary>本轮清掉的旧烘焙残留 PNG 数（旧清单里有、本轮不再保留的文件）。</summary>
    public int PrunedCount { get; set; }

    /// <summary>是否直接复用了上次的缓存清单（只有导出才可能命中）。</summary>
    public bool FromCache { get; set; }

    /// <summary>是否为「只进内存、不落盘」的导入预览。</summary>
    public bool PreviewOnly { get; set; }

    /// <summary>切片输出目录（预览时是目标目录，未创建）。</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>按图层序号取切片路径；没有自裁切片返回 null。</summary>
    /// <param name="index">图层序号。</param>
    /// <returns>切片绝对路径或 null。</param>
    public string? PathFor(int index) => ByIndex.TryGetValue(index, out var path) ? path : null;

    /// <summary>按图层序号取切片记录（分层 / 裁剪矩形）；没有返回 null。</summary>
    /// <param name="index">图层序号。</param>
    /// <returns>切片记录或 null。</returns>
    public PsdSliceRecord? RecordFor(int index) => Records.TryGetValue(index, out var record) ? record : null;
}

/// <summary>切片清单缓存文件（导出结果 + 判断「旧文件是不是本工具生成的」）。</summary>
internal sealed record PsdSliceManifest(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("length")] long Length,
    [property: JsonPropertyName("ticks")] long Ticks,
    [property: JsonPropertyName("slices")] List<PsdSliceRecord> Slices,
    [property: JsonPropertyName("version")] int Version = 0);
