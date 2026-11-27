/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Actions.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页的主要编辑动作：类型、跳转与说话人的写回
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    #region 类型与跳转

    /// <summary>剧情标题改动。</summary>
    private void OnTitleChanged()
    {
        if (_sync || _titleBox.Text is null)
        {
            return;
        }

        _doc.Title = _titleBox.Text;
        MarkDirty();
    }

    /// <summary>节点类型改动：切类型后刷新列表色点与正文可用性。</summary>
    private void OnKindChanged()
    {
        if (_sync || _node is null || _kindCombo.SelectedItem is not string display)
        {
            return;
        }

        var kind = StoryNodeKinds.FromDisplay(display);
        if (kind == _node.Kind)
        {
            return;
        }

        _node.Kind = kind;
        MarkDirty();
        RebuildNodeList(_node.Id);
        RefreshInspector();
    }

    /// <summary>「接下来」改动。</summary>
    private void OnNextChanged()
    {
        if (_sync || _node is null)
        {
            return;
        }

        _node.NextId = ReadNextId();
        MarkDirty();
    }

    /// <summary>从「接下来」下拉读回节点 ID。</summary>
    /// <returns>节点 ID；首项返回空串。</returns>
    private string ReadNextId()
    {
        var index = _nextCombo.SelectedIndex - 1;
        var targets = JumpTargets();
        return index >= 0 && index < targets.Count ? targets[index].Id : string.Empty;
    }

    #endregion

    #region 说话人与立绘

    /// <summary>从地图角色里挑说话人：一并带出显示名与立绘。</summary>
    private void OnSpeakerPicked()
    {
        if (_sync || _node is null)
        {
            return;
        }

        var index = _speakerPick.SelectedIndex - 1;
        var speakers = _speakers;
        if (index < 0 || index >= speakers.Count)
        {
            _node.SpeakerObjectId = string.Empty;
            _node.SpeakerInstanceId = string.Empty;
            MarkDirty();
            return;
        }

        var speaker = speakers[index];
        if (string.IsNullOrWhiteSpace(speaker.NumericId))
        {
            _sync = true;
            FillSpeakerPick(_node);
            _sync = false;
            SetMessage($"{speaker.Name} 还没有数字 ID：先在资源整理页给它补上配置行，再回来绑定");
            return;
        }

        _node.SpeakerObjectId = speaker.NumericId;
        _node.SpeakerInstanceId = speaker.ObjectId;
        _node.Speaker = speaker.Name;

        // 立绘默认跟随说话人：换人就回到跟随，要另指定立绘就再去右侧立绘下拉挑
        _node.Portrait = string.Empty;

        _sync = true;
        _speakerBox.Text = _node.Speaker;
        FillPortraitPick(_node);
        _sync = false;
        MarkDirty();
        RefreshStats();
    }

    /// <summary>说话人显示名改动。</summary>
    private void OnSpeakerTextChanged()
    {
        if (_sync || _node is null)
        {
            return;
        }

        _node.Speaker = _speakerBox.Text ?? string.Empty;
        MarkDirty();
    }

    /// <summary>立绘下拉改动。</summary>
    private void OnPortraitPicked()
    {
        if (_sync || _node is null || _portraitCombo.SelectedIndex < 0)
        {
            return;
        }

        var name = _portraitCombo.SelectedIndex == 0
            ? string.Empty
            : _portraitCombo.Items[_portraitCombo.SelectedIndex]?.ToString() ?? string.Empty;
        if (_node.Portrait == name)
        {
            return;
        }

        _node.Portrait = name;
        MarkDirty();
    }

    #endregion
}
