/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Status.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页的统计、保存状态与新建剧情
 ***************************************************************/

using System;
using System.Linq;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    /// <summary>标记有改动并刷新状态条。</summary>
    private void MarkDirty()
    {
        _dirty = true;
        _touched = true;
        RefreshFooter();
    }

    /// <summary>刷新左栏节点数与中栏统计、右栏自检。</summary>
    private void RefreshStats()
    {
        var check = StoryValidation.Check(_doc);
        _nodeCountText.Text = $"{check.NodeCount} 个节点";
        _statText.Text = $"对白 {check.TextNodeCount} · 分支 {check.ChoiceCount} · 说话人 {check.SpeakerCount}";

        if (check.Ready)
        {
            _issuesText.Text = "✓ 检查通过：主线闭合，对白、分支与跳转都齐";
            _issuesText.Foreground = UiTheme.Traffic.ReadyText;
            return;
        }

        _issuesText.Foreground = UiTheme.Traffic.GapText;
        var lines = check.Issues.Take(8).Select(issue =>
            string.IsNullOrEmpty(issue.NodeId) ? $"· {issue.Message}" : $"· {issue.NodeId}：{issue.Message}");
        var text = string.Join(Environment.NewLine, lines);
        if (check.Issues.Count > 8)
        {
            text += $"{Environment.NewLine}· 另有 {check.Issues.Count - 8} 条";
        }

        _issuesText.Text = text;
    }

    /// <summary>刷新底部保存状态与头部地图提示。</summary>
    private void RefreshFooter()
    {
        if (string.IsNullOrWhiteSpace(_mapPath))
        {
            _footerText.Text = "未打开地图：剧情会跟着地图同名保存，先载入一张地图";
            _mapText.Text = "未打开地图";
            return;
        }

        var name = System.IO.Path.GetFileName(_mapPath);
        var target = StoryService.PathFor(_mapPath);
        var exists = StoryService.Exists(_mapPath);
        _dirty = !exists || StoryService.Serialize(_doc) != _savedSnapshot;

        _mapText.Text = $"{name} · {System.IO.Path.GetFileName(target)}";
        if (!exists)
        {
            _footerText.Text = $"保存目标：{target}　·　尚未生成剧情文件";
            _footerText.Foreground = UiTheme.Traffic.GapText;
            return;
        }

        _footerText.Text = _dirty
            ? $"保存目标：{target}　·　有未保存改动"
            : $"保存目标：{target}　·　已与文件同步";
        _footerText.Foreground = _dirty ? UiTheme.Traffic.GapText : UiTheme.TextSecondary;
    }

    /// <summary>新建一份空白剧情，覆盖当前内存文档（不动已落盘文件）。</summary>
    private void NewStory()
    {
        if (_dirty)
        {
            SetMessage("当前剧情有未保存改动：先点『保存剧情』，再新建");
            return;
        }

        var name = string.IsNullOrWhiteSpace(_mapPath)
            ? "未打开地图"
            : System.IO.Path.GetFileNameWithoutExtension(_mapPath);
        _doc = StoryService.Create(name);
        _node = _doc.Start;
        _dirty = true;
        _sync = true;
        _titleBox.Text = _doc.Title;
        _sync = false;
        RebuildNodeList(_node?.Id);
        RefreshInspector();
        RefreshFooter();
        SetMessage("已新建空白剧情：章节标题 + 一句对白 + 结束，保存后写入地图同目录");
    }
}
