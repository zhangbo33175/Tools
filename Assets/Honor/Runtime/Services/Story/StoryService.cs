/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryService.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情文档存盘：与地图同名同目录的 .story.json 读写、备份与节点编序
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 剧情文档的落盘规则：剧情跟着地图走，文件和地图同目录、同主名，
/// 只把扩展名换成 <c>.story.json</c>，所以「Map_1m1.json」对应「Map_1m1.story.json」。
/// </summary>
public static class StoryService
{
    #region 常量

    /// <summary>剧情文件扩展名。</summary>
    public const string Extension = ".story.json";

    /// <summary>覆盖保存前留一份同名单份备份。</summary>
    public const string BackupSuffix = ".bak";

    /// <endregion>

    #endregion

    #region 路径

    /// <summary>取某张地图对应的剧情文件路径。</summary>
    /// <param name="mapPath">地图 JSON 路径。</param>
    /// <returns>剧情文件路径；地图路径为空时返回空串。</returns>
    public static string PathFor(string mapPath) =>
        string.IsNullOrWhiteSpace(mapPath)
            ? string.Empty
            : Path.ChangeExtension(mapPath, Extension);

    /// <summary>取剧情文件是否存在。</summary>
    /// <param name="mapPath">地图 JSON 路径。</param>
    /// <returns>存在返回 true。</returns>
    public static bool Exists(string mapPath)
    {
        var path = PathFor(mapPath);
        return path.Length > 0 && File.Exists(path);
    }

    #endregion

    #region 读写

    /// <summary>读取地图对应的剧情；文件不存在时给一份空白文档，不报错。</summary>
    /// <param name="mapPath">地图 JSON 路径。</param>
    /// <returns>剧情文档；地图路径为空时返回空文档样式的默认文档。</returns>
    public static StoryDocument Load(string mapPath)
    {
        var name = string.IsNullOrWhiteSpace(mapPath)
            ? "未打开地图"
            : Path.GetFileNameWithoutExtension(mapPath);

        var path = PathFor(mapPath);
        if (path.Length == 0 || !File.Exists(path))
        {
            return Create(name);
        }

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<StoryDocument>(json, JsonOptions) ?? Create(name);
            doc.MapFile = string.IsNullOrWhiteSpace(doc.MapFile) ? name : doc.MapFile;

            // 手改过的 JSON 可能缺节点 ID 或起始节点，载入时补齐，避免面板出现空行
            Normalize(doc);
            return doc;
        }
        catch (Exception)
        {
            // 文件损坏时不让编辑器打不开：回退到空白文档，状态栏会提示
            return Create(name);
        }
    }

    /// <summary>把剧情写回地图同目录；覆盖前留一份 <c>.bak</c>。</summary>
    /// <param name="doc">剧情文档。</param>
    /// <param name="mapPath">地图 JSON 路径。</param>
    /// <returns>实际写入的文件路径。</returns>
    public static string Save(StoryDocument doc, string mapPath)
    {
        var path = PathFor(mapPath);
        if (path.Length == 0)
        {
            throw new InvalidOperationException("没有打开地图，剧情无处可存");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            File.Copy(path, path + BackupSuffix, overwrite: true);
        }

        var json = Serialize(doc);
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>把剧情文档序列化成落盘用的 JSON 文本；面板拿它比对是否有改动。</summary>
    /// <param name="doc">剧情文档。</param>
    /// <returns>JSON 文本。</returns>
    public static string Serialize(StoryDocument doc) => JsonSerializer.Serialize(doc, JsonOptions);

    #endregion

    #region 文档维护

    /// <summary>新建一份带「开场」章节标题与收尾对白的空白剧情，保证打开就能直接写。</summary>
    /// <param name="mapName">地图主名。</param>
    /// <returns>可直接编辑的剧情文档。</returns>
    public static StoryDocument Create(string mapName)
    {
        var doc = new StoryDocument
        {
            MapFile = mapName,
            Title = $"{mapName} 剧情",
        };

        var opening = new StoryNode
        {
            Id = "n1",
            Kind = StoryNodeKinds.Chapter,
            Text = "第一章",
            NextId = "n2",
        };
        var firstLine = new StoryNode
        {
            Id = "n2",
            Kind = StoryNodeKinds.Line,
            Speaker = string.Empty,
            Text = string.Empty,
            NextId = "n3",
        };
        var ending = new StoryNode
        {
            Id = "n3",
            Kind = StoryNodeKinds.End,
        };

        doc.Nodes.Add(opening);
        doc.Nodes.Add(firstLine);
        doc.Nodes.Add(ending);
        doc.StartNodeId = opening.Id;
        return doc;
    }

    /// <summary>补齐缺失的节点 ID、起始节点与空集合，确保面板渲染不空。</summary>
    /// <param name="doc">待整理的文档。</param>
    public static void Normalize(StoryDocument doc)
    {
        doc.Nodes ??= new System.Collections.Generic.List<StoryNode>();
        doc.Title = string.IsNullOrWhiteSpace(doc.Title) ? "未命名剧情" : doc.Title;

        var used = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var counter = 0;
        foreach (var node in doc.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || used.Contains(node.Id))
            {
                node.Id = AllocateId(used, ref counter);
            }

            used.Add(node.Id);
            node.Choices ??= new System.Collections.Generic.List<StoryChoice>();
        }

        if (doc.Find(doc.StartNodeId) is null)
        {
            doc.StartNodeId = doc.Nodes.Count > 0 ? doc.Nodes[0].Id : string.Empty;
        }
    }

    /// <summary>分配一个文档内未被占用的节点 ID（n1、n2…）。</summary>
    /// <param name="doc">剧情文档。</param>
    /// <returns>新 ID。</returns>
    public static string NextId(StoryDocument doc)
    {
        var used = new System.Collections.Generic.HashSet<string>(
            doc.Nodes.Select(node => node.Id), StringComparer.Ordinal);
        var counter = 0;
        return AllocateId(used, ref counter);
    }

    /// <summary>从给定计数往上找第一个没被占用的编号。</summary>
    /// <param name="used">已占用的 ID 集合。</param>
    /// <param name="counter">当前编号游标。</param>
    /// <returns>形如 n7 的新 ID。</returns>
    private static string AllocateId(System.Collections.Generic.HashSet<string> used, ref int counter)
    {
        while (true)
        {
            counter++;
            var candidate = "n" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    #endregion

    #region 序列化

    /// <summary>剧情 JSON 的统一口径：camelCase、缩进、保留中文原样、忽略未知字段。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    #endregion
}