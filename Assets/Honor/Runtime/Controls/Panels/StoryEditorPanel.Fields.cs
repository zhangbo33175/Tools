/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Fields.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页的文本与演出字段写回：正文、备注、立绘开关、等待与镜头
 ***************************************************************/

using System;
using System.Globalization;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    /// <summary>正文改动。</summary>
    private void OnTextChanged()
    {
        if (_sync || _node is null || _textBox.Text is null)
        {
            return;
        }

        _node.Text = _textBox.Text;
        MarkDirty();
    }

    /// <summary>策划备注改动。</summary>
    private void OnNoteChanged()
    {
        if (_sync || _node is null)
        {
            return;
        }

        _node.Note = _noteBox.Text ?? string.Empty;
        MarkDirty();
    }

    /// <summary>立绘开关改动。</summary>
    private void OnShowPortraitChanged()
    {
        if (_sync || _node is null)
        {
            return;
        }

        _node.ShowPortrait = _showPortraitCheck.IsChecked ?? true;
        MarkDirty();
    }

    /// <summary>自动推进秒数改动：非数字按 0 处理，负数归零。</summary>
    private void OnWaitChanged()
    {
        if (_sync || _node is null)
        {
            return;
        }

        var text = _waitBox.Text ?? string.Empty;
        var ok = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);
        _node.WaitSeconds = ok ? Math.Max(0, seconds) : 0;
        MarkDirty();
    }

    /// <summary>镜头指令改动。</summary>
    private void OnEffectChanged()
    {
        if (_sync || _node is null || _effectCombo.SelectedItem is not string display)
        {
            return;
        }

        _node.CameraEffect = StoryCameraEffects.FromDisplay(display);
        MarkDirty();
    }
}
