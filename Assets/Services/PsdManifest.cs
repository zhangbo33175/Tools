/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdManifest.cs
 * author:    云毅
 * created:   2026
 * descrip:   读取 Unity 侧 PSD 导入清单（psd.json）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MapEditorTool.Services;

/// <summary>
/// 清单里的一层：图层原始名 + 包围盒对应 Unity 侧判定的层级类型与切片图，
/// 外加放置层（智能对象）的旋转、镜像与四边形显示尺寸。
/// </summary>
/// <param name="Name">图层原始名（Photoshop 里的名字）。</param>
/// <param name="LayerType">层级类型：background / greenery / road / building / prop / character / foreground / bubble。</param>
/// <param name="PngPath">Unity 侧导出（或复用）的切片 PNG 绝对路径，未导出为 null。</param>
/// <param name="SortOrder">排序号。</param>
/// <param name="Rotation">放置层旋转角（度，逆时针为正，对齐 Unity Z 轴）。</param>
/// <param name="FlipX">放置层是否被水平的「编辑 > 变换 > 水平翻转」镜像。</param>
/// <param name="QuadWidth">放置四边形宽（= 图层框宽 × scaleX），0 表示非放置层。</param>
/// <param name="QuadHeight">放置四边形高（= 图层框高 × scaleY），0 表示非放置层。</param>
/// <param name="Decision">Unity 侧的处置结论：<c>reuse</c> 复用已有切片 / <c>background</c> 并入底图 / <c>hidden</c> 忽略。</param>
public sealed record PsdManifestLayer(
    string Name,
    string LayerType,
    string? PngPath,
    int SortOrder,
    double Rotation,
    bool FlipX,
    double QuadWidth,
    double QuadHeight,
    string Decision);

/// <summary>
/// Unity 侧 PSD 导入清单。<c>Assets/Res/Textures/Map/psd.json</c> 是 Unity 版导入 PSD 后落盘的权威结果：
/// 每层都已判定好层级类型、并指向导出好的切片 PNG。桌面版按它导入，就不必再用图层名去猜，
/// 也就不会出现「道路层被判成前景/道具、贴图又找不到」这类偏差。
/// </summary>
public sealed class PsdManifest
{
    #region 常量

    /// <summary>工程内清单相对路径（从项目根算起）。</summary>
    private const string ProjectManifestRelative = "Assets/Res/Textures/Map/psd.json";

    /// <summary>与 PSD 同目录时的清单文件名。</summary>
    private const string LocalManifestFileName = "psd.json";

    #endregion

    #region 字段

    /// <summary>按「图层名 + 包围盒左上角」索引，同名多份的拷贝不会被混用。</summary>
    private readonly Dictionary<string, PsdManifestLayer> _layers = new(StringComparer.Ordinal);

    #endregion

    #region 属性

    /// <summary>清单条数。</summary>
    public int Count => _layers.Count;

    /// <summary>清单文件路径。</summary>
    public string SourcePath { get; private init; } = string.Empty;

    #endregion

    #region 读取

    /// <summary>
    /// 找并读取清单：先看 PSD 同目录的 <c>psd.json</code>，再按工程结构向上查找
    /// <c>Assets/Res/Textures/Map/psd.json</c>。都找不到返回 null（导入退回解析原始 PSD）。
    /// </summary>
    /// <param name="psdPath">PSD / PSB 文件路径。</param>
    /// <returns>清单；找不到或解析失败返回 null。</returns>
    public static PsdManifest? TryLoad(string psdPath)
    {
        var path = LocateManifest(psdPath);
        if (path is null)
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
            return Build(json.RootElement, path);
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>定位清单文件：PSD 同目录优先，其次向上找工程内的 Map 清单。</summary>
    /// <param name="psdPath">PSD / PSB 文件路径。</param>
    /// <returns>清单绝对路径；找不到返回 null。</returns>
    private static string? LocateManifest(string psdPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(psdPath));
        if (directory is null)
        {
            return null;
        }

        var local = Path.Combine(directory, LocalManifestFileName);
        if (File.Exists(local))
        {
            return local;
        }

        // 从 PSD 所在目录逐层向上找 <项目根>/Assets/Res/Textures/Map/psd.json
        var cursor = new DirectoryInfo(directory);
        while (cursor is not null)
        {
            var candidate = Path.Combine(cursor.FullName, ProjectManifestRelative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            cursor = cursor.Parent;
        }

        return null;
    }

    /// <summary>把清单 JSON 转成索引。</summary>
    /// <param name="root">清单根节点。</param>
    /// <param name="path">清单文件路径。</param>
    /// <returns>清单实例。</returns>
    private static PsdManifest Build(JsonElement root, string path)
    {
        var manifest = new PsdManifest { SourcePath = path };

        if (!root.TryGetProperty("layers", out var layers) || layers.ValueKind != JsonValueKind.Array)
        {
            return manifest;
        }

        foreach (var layer in layers.EnumerateArray())
        {
            var name = layer.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? string.Empty : string.Empty;
            if (name.Length == 0 || !layer.TryGetProperty("box", out var box))
            {
                continue;
            }

            var left = box.TryGetProperty("left", out var leftNode) ? leftNode.GetInt32() : 0;
            var top = box.TryGetProperty("top", out var topNode) ? topNode.GetInt32() : 0;
            var layerType = layer.TryGetProperty("layerType", out var typeNode) ? typeNode.GetString() ?? string.Empty : string.Empty;
            var sortOrder = layer.TryGetProperty("sortOrder", out var sortNode) && sortNode.TryGetInt32(out var order) ? order : 0;
            var pngPath = layer.TryGetProperty("pngPath", out var pngNode) ? pngNode.GetString() : null;
            var decision = layer.TryGetProperty("decision", out var decisionNode) ? decisionNode.GetString() ?? string.Empty : string.Empty;

            var entry = new PsdManifestLayer(
                name,
                layerType,
                string.IsNullOrWhiteSpace(pngPath) ? null : pngPath,
                sortOrder,
                ReadNumber(layer, "rotation"),
                layer.TryGetProperty("flipX", out var flipNode) && flipNode.ValueKind == JsonValueKind.True,
                ReadNumber(layer, "quadW"),
                ReadNumber(layer, "quadH"),
                decision);

            manifest._layers[Key(name, left, top)] = entry;
        }

        return manifest;
    }

    /// <summary>读一个数值字段；缺失或类型不符时返回 0。</summary>
    /// <param name="layer">清单条目节点。</param>
    /// <param name="property">字段名。</param>
    /// <returns>数值。</returns>
    private static double ReadNumber(JsonElement layer, string property) =>
        layer.TryGetProperty(property, out var node) && node.TryGetDouble(out var value) ? value : 0.0;

    #endregion

    #region 查询

    /// <summary>按图层名与包围盒查清单条目。</summary>
    /// <param name="layer">PSD 图层。</param>
    /// <returns>清单条目；没命中返回 null。</returns>
    public PsdManifestLayer? Find(PsdLayerInfo layer) =>
        _layers.TryGetValue(Key(layer.Name, layer.Left, layer.Top), out var hit) ? hit : null;

    /// <summary>索引键：图层名 + 左上角坐标。</summary>
    /// <param name="name">图层名。</param>
    /// <param name="left">左边界像素。</param>
    /// <param name="top">上边界像素。</param>
    /// <returns>索引键。</returns>
    private static string Key(string name, int left, int top) => $"{name}|{left}|{top}";

    #endregion

}
