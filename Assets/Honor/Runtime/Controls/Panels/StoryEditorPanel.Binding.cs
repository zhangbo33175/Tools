/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Binding.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页回填：把节点字段灌进表单，并给说话人、立绘、跳转下拉备好选项
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    #region 回填

    /// <summary>把当前节点的字段灌进三栏表单；没有节点时清空。</summary>
    private void RefreshInspector()
    {
        _sync = true;
        var node = _node;

        _idText.Text = node?.Id ?? string.Empty;
        _kindCombo.SelectedItem = node is null ? null : StoryNodeKinds.Display(node.Kind);
        _speakerBox.Text = node?.Speaker ?? string.Empty;
        _textBox.Text = node?.Text ?? string.Empty;
        _noteBox.Text = node?.Note ?? string.Empty;
        _waitBox.Text = node is null ? string.Empty : node.WaitSeconds.ToString("0.##");
        _showPortraitCheck.IsChecked = node?.ShowPortrait ?? true;
        _effectCombo.SelectedItem = StoryCameraEffects.Display(node?.CameraEffect ?? StoryCameraEffects.None);
        _textBox.IsEnabled = node?.HasText ?? false;
        _speakerBox.IsEnabled = node is null || node.Kind == StoryNodeKinds.Line;

        FillSpeakerPick(node);
        FillPortraitPick(node);
        FillNextCombo(node?.NextId);
        _sync = false;

        RefreshChoiceRows();
        RefreshStats();
    }

    /// <summary>说话人下拉：列出地图角色，按物件独有的数字 ID 绑定，空首项表示不绑定。</summary>
    /// <param name="node">当前节点。</param>
    private void FillSpeakerPick(StoryNode? node)
    {
        var previous = _sync;
        _sync = true;
        _speakerPick.Items.Clear();
        _speakerPick.Items.Add("（不绑定地图角色）");

        var speakers = _speakers;
        foreach (var speaker in speakers)
        {
            _speakerPick.Items.Add(speaker.Label);
        }

        var index = 0;
        if (node is not null && !string.IsNullOrWhiteSpace(node.SpeakerObjectId))
        {
            // 先认实例短 ID（同模板摆多个时它才分得清），再认数字 ID；
            // 旧剧情里存的 8 位实例 ID 也落在数字 ID 字段上，一并兜住
            var byInstance = -1;
            var byNumeric = -1;
            for (var i = 0; i < speakers.Count; i++)
            {
                if (byInstance < 0 && !string.IsNullOrWhiteSpace(node.SpeakerInstanceId) && speakers[i].ObjectId == node.SpeakerInstanceId)
                {
                    byInstance = i;
                }

                if (byNumeric < 0 && (speakers[i].NumericId == node.SpeakerObjectId || speakers[i].ObjectId == node.SpeakerObjectId))
                {
                    byNumeric = i;
                }
            }

            var hit = byInstance >= 0 ? byInstance : byNumeric;
            if (hit >= 0)
            {
                index = hit + 1;
            }
        }
        else if (node is not null && !string.IsNullOrWhiteSpace(node.Speaker))
        {
            for (var i = 0; i < speakers.Count; i++)
            {
                if (speakers[i].Name == node.Speaker)
                {
                    index = i + 1;
                    break;
                }
            }
        }

        _speakerPick.SelectedIndex = index;
        _sync = previous;
    }

    /// <summary>立绘下拉：地图角色 + 当前已填的立绘名，保证手填值不丢。</summary>
    /// <param name="node">当前节点。</param>
    private void FillPortraitPick(StoryNode? node)
    {
        var previous = _sync;
        _sync = true;
        _portraitCombo.Items.Clear();
        _portraitCombo.Items.Add("（跟随说话人）");

        foreach (var character in Characters())
        {
            if (!_portraitCombo.Items.Contains(character.Name))
            {
                _portraitCombo.Items.Add(character.Name);
            }
        }

        var portrait = node?.Portrait ?? string.Empty;
        if (portrait.Length > 0 && !_portraitCombo.Items.Contains(portrait))
        {
            _portraitCombo.Items.Add(portrait);
        }

        var index = 0;
        if (portrait.Length > 0)
        {
            index = _portraitCombo.Items.IndexOf(portrait);
            if (index < 0)
            {
                index = 0;
            }
        }

        _portraitCombo.SelectedIndex = index;
        _sync = previous;
    }

    /// <summary>「接下来」下拉：全节点里除自己和结束节点外的所有节点，首项是「（剧情结束）」。</summary>
    /// <param name="selectedId">当前下一节点 ID。</param>
    private void FillNextCombo(string? selectedId)
    {
        var previous = _sync;
        _sync = true;
        _nextCombo.Items.Clear();
        _nextCombo.Items.Add("（剧情到此结束）");

        var targets = JumpTargets();
        foreach (var node in targets)
        {
            _nextCombo.Items.Add($"{node.Id} · {node.Summary()}");
        }

        var index = 0;
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var found = targets.FindIndex(node => node.Id == selectedId);
            if (found >= 0)
            {
                index = found + 1;
            }
        }

        _nextCombo.SelectedIndex = index;
        _sync = previous;
    }

    /// <summary>可作为跳转目标的节点（排除结束节点与自身）。</summary>
    /// <returns>节点列表。</returns>
    private List<StoryNode> JumpTargets() => _doc.Nodes
        .Where(node => node.IsJumpTarget && node.Id != _node?.Id)
        .ToList();

    /// <summary>地图里的角色对象，用作说话人与立绘来源。</summary>
    /// <returns>角色列表。</returns>
    private List<PlacedObject> Characters() => _config?.Objects
        .Where(obj => obj.Type == MapObjectType.Character && !string.IsNullOrWhiteSpace(obj.Name))
        .ToList() ?? new List<PlacedObject>();

    #endregion
}
