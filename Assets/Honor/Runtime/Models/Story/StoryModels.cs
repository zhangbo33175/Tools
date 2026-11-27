/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryModels.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情文档模型：节点、选项、演出字段与自检结果
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;

namespace MapEditorTool.Models;

#region 枚举常量

/// <summary>剧情节点类型。全部落成字符串，JSON 里可读、可手改。</summary>
public static class StoryNodeKinds
{
    /// <summary>角色对白。</summary>
    public const string Line = "line";

    /// <summary>旁白 / 叙述，没有说话人。</summary>
    public const string Narration = "narration";

    /// <summary>选项节点：停顿并给出若干分支。</summary>
    public const string Choice = "choice";

    /// <summary>章节 / 小节标题，只起分隔作用，预览时不停留。</summary>
    public const string Chapter = "chapter";

    /// <summary>剧情结束。</summary>
    public const string End = "end";

    /// <summary>全部可选类型，用于下拉框顺序。</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Line, Narration, Choice, Chapter, End,
    };

    /// <summary>类型的中文显示名。</summary>
    /// <param name="kind">节点类型。</param>
    /// <returns>显示名；未知类型原样返回。</returns>
    public static string Display(string kind) => kind switch
    {
        Line => "对白",
        Narration => "旁白",
        Choice => "选项",
        Chapter => "章节标题",
        End => "结束",
        _ => kind,
    };

    /// <summary>按显示名反查类型，供下拉框回填。</summary>
    /// <param name="display">显示名。</param>
    /// <returns>节点类型。</returns>
    public static string FromDisplay(string display) =>
        All.FirstOrDefault(kind => Display(kind) == display) ?? Line;
}

/// <summary>节点的镜头 / 屏幕演出指令。预览窗按这里的值改变舞台表现。</summary>
public static class StoryCameraEffects
{
    /// <summary>不演出。</summary>
    public const string None = "none";

    /// <summary>镜头推近立绘。</summary>
    public const string Zoom = "zoom";

    /// <summary>屏幕轻微震动。</summary>
    public const string Shake = "shake";

    /// <summary>全部可选指令。</summary>
    public static readonly IReadOnlyList<string> All = new[] { None, Zoom, Shake };

    /// <summary>指令的中文显示名。</summary>
    /// <param name="effect">指令键。</param>
    /// <returns>显示名。</returns>
    public static string Display(string effect) => effect switch
    {
        Zoom => "镜头推近",
        Shake => "屏幕震动",
        _ => "无",
    };

    /// <summary>按显示名反查指令。</summary>
    /// <param name="display">显示名。</param>
    /// <returns>指令键。</returns>
    public static string FromDisplay(string display) =>
        All.FirstOrDefault(effect => Display(effect) == display) ?? None;
}

#endregion

#region 数据结构

/// <summary>选项节点的一个分支：一句选项文本 + 要跳去的节点 ID。</summary>
public sealed class StoryChoice
{
    /// <summary>选项文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>目标节点 ID；空串表示走 <see cref="StoryNode.NextId"/> 的默认后续。</summary>
    public string TargetId { get; set; } = string.Empty;
}

/// <summary>
/// 一个剧情节点：一段对白、一条旁白、一组选项或一个章节标题。
/// <para>节点之间用 <see cref="NextId"/> 串成主线，选项节点用 <see cref="Choices"/> 分叉；
/// 演出字段（立绘、等待、镜头指令）都挂在节点上，一个节点就是一步演出。</para>
/// </summary>
public sealed class StoryNode
{
    /// <summary>节点 ID，文档内唯一，由编辑器按 n1/n2… 分配。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>节点类型，见 <see cref="StoryNodeKinds"/>。</summary>
    public string Kind { get; set; } = StoryNodeKinds.Line;

    /// <summary>说话人显示名。</summary>
    public string Speaker { get; set; } = string.Empty;

    /// <summary>
    /// 说话人绑定的物件数字 ID：物件在配置表里的行 ID，没有配置行时回退关卡切片序号。
    /// <para>图片只是配置里挂的资源，可能被多个物件共用，所以绑定只认数字 ID，不认资源名。</para>
    /// </summary>
    public string SpeakerObjectId { get; set; } = string.Empty;

    /// <summary>立绘资源名：留空时按 <see cref="SpeakerObjectId"/> 找到物件、再用它的图片。</summary>
    public string Portrait { get; set; } = string.Empty;

    /// <summary>
    /// 说话人所在实例的短 ID：数字 ID 只到「哪一种」，同一个模板摆多个时必须靠它落到「哪一个」。
    /// <para>留空表示该数字 ID 在地图上只有一个实例。</para>
    /// </summary>
    public string SpeakerInstanceId { get; set; } = string.Empty;

    /// <summary>对白 / 旁白正文。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>主线下一节点 ID；空串表示到此收束。</summary>
    public string NextId { get; set; } = string.Empty;

    /// <summary>选项分支，仅选项节点使用。</summary>
    public List<StoryChoice> Choices { get; set; } = new();

    /// <summary>演出：是否显示左侧立绘。</summary>
    public bool ShowPortrait { get; set; } = true;

    /// <summary>演出：自动推进等待秒数，0 表示手动点击推进。</summary>
    public double WaitSeconds { get; set; }

    /// <summary>演出：镜头指令，见 <see cref="StoryCameraEffects"/>。</summary>
    public string CameraEffect { get; set; } = StoryCameraEffects.None;

    /// <summary>策划备注，只在编辑器里显示，不参与预览。</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>节点是否承载可读文本（章节标题与结束节点不需要正文）。</summary>
    public bool HasText => Kind is StoryNodeKinds.Line or StoryNodeKinds.Narration or StoryNodeKinds.Choice;

    /// <summary>节点是否可选为跳转目标。</summary>
    public bool IsJumpTarget => Kind != StoryNodeKinds.End;

    /// <summary>列表行标题：说话人 + 正文摘要，章节节点直接显示标题。</summary>
    /// <returns>一行摘要。</returns>
    public string Summary()
    {
        if (Kind == StoryNodeKinds.Chapter)
        {
            return string.IsNullOrWhiteSpace(Text) ? "（未命名章节）" : Text;
        }

        if (Kind == StoryNodeKinds.End)
        {
            return "结束";
        }

        var body = Text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (body.Length > 22)
        {
            body = body[..22] + "…";
        }

        var head = string.IsNullOrWhiteSpace(Speaker) ? StoryNodeKinds.Display(Kind) : Speaker;
        return body.Length == 0 ? head : $"{head}：{body}";
    }
}

/// <summary>一份剧情文档，落盘为与地图同名的 <c>.story.json</c>。</summary>
public sealed class StoryDocument
{
    /// <summary>绑定的地图文件名（不含路径），用于提示与校验。</summary>
    public string MapFile { get; set; } = string.Empty;

    /// <summary>剧情标题。</summary>
    public string Title { get; set; } = "未命名剧情";

    /// <summary>起始节点 ID。</summary>
    public string StartNodeId { get; set; } = string.Empty;

    /// <summary>全部节点，顺序即列表展示顺序。</summary>
    public List<StoryNode> Nodes { get; set; } = new();

    /// <summary>按 ID 找节点。</summary>
    /// <param name="id">节点 ID。</param>
    /// <returns>节点；找不到返回 null。</returns>
    public StoryNode? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Nodes.FirstOrDefault(node => node.Id == id);

    /// <summary>起始节点；未设置或指向已删节点时回退到第一个节点。</summary>
    /// <returns>起始节点；空文档返回 null。</returns>
    public StoryNode? Start => Find(StartNodeId) ?? Nodes.FirstOrDefault();
}

#endregion
