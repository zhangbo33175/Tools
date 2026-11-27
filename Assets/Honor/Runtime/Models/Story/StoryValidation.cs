/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryValidation.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情自检：重复节点、悬空跳转、空对白与未绑说话人
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;

namespace MapEditorTool.Models;

/// <summary>一条剧情自检问题。</summary>
/// <param name="NodeId">出问题的节点 ID，可空。</param>
/// <param name="Message">问题描述。</param>
public readonly record struct StoryIssue(string NodeId, string Message);

/// <summary>剧情自检结果：统计数字 + 问题清单，面板头部直接展示。</summary>
public sealed class StoryValidation
{
    /// <summary>节点总数。</summary>
    public int NodeCount { get; init; }

    /// <summary>有正文的节点数（对白 + 旁白 + 选项）。</summary>
    public int TextNodeCount { get; init; }

    /// <summary>选项分支总数。</summary>
    public int ChoiceCount { get; init; }

    /// <summary>不同说话人数量。</summary>
    public int SpeakerCount { get; init; }

    /// <summary>已绑定地图对象的对白节点数量。</summary>
    public int BoundNodeCount { get; init; }

    /// <summary>问题清单；为空表示可以播放。</summary>
    public List<StoryIssue> Issues { get; init; } = new();

    /// <summary>自检是否全部通过。</summary>
    public bool Ready => Issues.Count == 0;

    /// <summary>
    /// 对整份文档做一次只读自检：统计节点与分支，并列出会让剧情走不下去的问题。
    /// </summary>
    /// <param name="doc">剧情文档。</param>
    /// <returns>自检结果。</returns>
    public static StoryValidation Check(StoryDocument doc)
    {
        var issues = new List<StoryIssue>();
        var ids = doc.Nodes.Select(node => node.Id).ToList();

        // 1. 节点 ID 必须唯一，否则跳转会落到第一个同名节点上
        foreach (var group in doc.Nodes.GroupBy(node => node.Id).Where(g => g.Count() > 1))
        {
            issues.Add(new StoryIssue(group.Key, $"节点 ID「{group.Key}」重复 {group.Count()} 次"));
        }

        foreach (var node in doc.Nodes)
        {
            // 2. 主线跳转不能指空
            if (!string.IsNullOrWhiteSpace(node.NextId) && !ids.Contains(node.NextId))
            {
                issues.Add(new StoryIssue(node.Id, $"下一节点「{node.NextId}」不存在"));
            }

            // 3. 对白 / 旁白必须有正文
            if (node.HasText && string.IsNullOrWhiteSpace(node.Text))
            {
                issues.Add(new StoryIssue(node.Id, "正文为空"));
            }

            // 4. 对白要说清楚是谁在说
            if (node.Kind == StoryNodeKinds.Line && string.IsNullOrWhiteSpace(node.Speaker))
            {
                issues.Add(new StoryIssue(node.Id, "对白没有说话人"));
            }

            // 5. 选项节点至少要有一条分支，且分支目标要存在
            if (node.Kind == StoryNodeKinds.Choice)
            {
                if (node.Choices.Count == 0)
                {
                    issues.Add(new StoryIssue(node.Id, "选项节点没有分支"));
                }

                foreach (var choice in node.Choices.Where(choice => !string.IsNullOrWhiteSpace(choice.TargetId) && !ids.Contains(choice.TargetId)))
                {
                    issues.Add(new StoryIssue(node.Id, $"选项「{choice.Text}」指向的「{choice.TargetId}」不存在"));
                }
            }

            // 6. 除结束节点外，主线上没有后续又没分支，等于断线
            if (node.Kind is StoryNodeKinds.Line or StoryNodeKinds.Narration
                && string.IsNullOrWhiteSpace(node.NextId))
            {
                issues.Add(new StoryIssue(node.Id, "没有下一节点，主线在此中断"));
            }
        }

        // 7. 起始节点必须是文档里真实存在的节点
        if (!string.IsNullOrWhiteSpace(doc.StartNodeId) && !ids.Contains(doc.StartNodeId))
        {
            issues.Add(new StoryIssue(doc.StartNodeId, $"起始节点「{doc.StartNodeId}」不存在"));
        }

        return new StoryValidation
        {
            NodeCount = doc.Nodes.Count,
            TextNodeCount = doc.Nodes.Count(node => node.HasText),
            ChoiceCount = doc.Nodes.Sum(node => node.Choices.Count),
            SpeakerCount = doc.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.Speaker))
                .Select(node => node.Speaker)
                .Distinct()
                .Count(),
            BoundNodeCount = doc.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.SpeakerObjectId)),
            Issues = issues,
        };
    }
}
