/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryPreviewWindow.Playback.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情预览的推进逻辑：节点跳转、立绘解析、逐句渲染与自动播放
 ***************************************************************/

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class StoryPreviewWindow
{
    #region 推进

    /// <summary>从头播放。</summary>
    private void Restart()
    {
        _step = 0;
        _waitingChoice = false;
        _autoTimer.Stop();
        GoTo(_doc.Start);
    }

    /// <summary>推进一句：选项未选时不动，结束节点重播。</summary>
    private void Advance()
    {
        if (_waitingChoice || _current is null)
        {
            return;
        }

        if (_current.Kind == StoryNodeKinds.End)
        {
            Restart();
            return;
        }

        var next = _doc.Find(_current.NextId);
        if (next is null)
        {
            ShowEnding();
            return;
        }

        GoTo(next);
    }

    /// <summary>跳到指定节点；章节标题不停留，直接顺着主线往后走。</summary>
    /// <param name="node">目标节点。</param>
    private void GoTo(StoryNode? node)
    {
        var guard = 0;
        while (node is not null && node.Kind == StoryNodeKinds.Chapter && guard++ < 32)
        {
            node = _doc.Find(node.NextId);
        }

        if (node is null)
        {
            ShowEnding();
            return;
        }

        _current = node;
        _step++;
        Render(node);
    }

    /// <summary>没有任何后续时的收尾提示。</summary>
    private void ShowEnding()
    {
        _current = null;
        _waitingChoice = false;
        _choicesHost.IsVisible = false;
        _choicesHost.Children.Clear();
        _autoTimer.Stop();
        _speakerText.Text = string.Empty;
        _dialogText.Text = "剧情到此结束，点『重新播放』或按 Esc 关闭";
        _hintText.Text = "End";
        _portrait.IsVisible = false;
    }

    /// <summary>把一个节点渲染到舞台上。</summary>
    /// <param name="node">节点。</param>
    private void Render(StoryNode node)
    {
        _speakerText.Text = node.Kind == StoryNodeKinds.End
            ? "剧终"
            : node.Kind == StoryNodeKinds.Narration
                ? "旁白"
                : node.Kind == StoryNodeKinds.Choice && string.IsNullOrWhiteSpace(node.Speaker)
                    ? "作出选择"
                    : string.IsNullOrWhiteSpace(node.Speaker) ? "（未命名说话人）" : node.Speaker;
        _dialogText.Text = node.Kind == StoryNodeKinds.End
            ? "这一段到此结束，点一下或按空格从头再播。"
            : string.IsNullOrWhiteSpace(node.Text)
                ? "（这一段还没写正文）"
                : node.Text;

        _progressText.Text = $"第 {_step} / {Math.Max(1, _doc.Nodes.Count)} 句";
        _hintText.Text = node.Kind == StoryNodeKinds.End ? "点一下重新播放" : "点击对话框 / 空格 继续";

        ApplyPortrait(node);
        ApplyCamera(node);

        BuildChoices(node);
        StartAutoTimer(node);
    }

    /// <summary>按节点里的立绘字段把角色图放上舞台；本句没写立绘时沿用上一句，避免画面突然空掉。</summary>
    /// <param name="node">节点。</param>
    private void ApplyPortrait(StoryNode node)
    {
        if (!node.ShowPortrait)
        {
            _portrait.IsVisible = false;
            return;
        }

        var bitmap = ResolvePortrait(node);
        if (bitmap is null)
        {
            _portrait.IsVisible = _portrait.Source is not null;
            return;
        }

        _portrait.Source = bitmap;
        _portrait.IsVisible = true;
    }

    /// <summary>解析立绘位图：先按说话人的数字 ID 找角色与图片，再按立绘资源名兜底。</summary>
    /// <param name="node">节点。</param>
    /// <returns>位图；找不到返回 null。</returns>
    private Bitmap? ResolvePortrait(StoryNode node)
    {
        if (!node.ShowPortrait)
        {
            return null;
        }

        var owner = ResolveSpeaker(node);
        var name = string.IsNullOrWhiteSpace(node.Portrait) ? owner?.Name ?? string.Empty : node.Portrait;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var path = owner is not null && owner.Name == name ? owner.ImagePath : string.Empty;
        var fromPath = string.IsNullOrWhiteSpace(path) ? null : _assets?.Load(path);
        return fromPath ?? _assets?.Load(name);
    }

    /// <summary>按实例短 ID / 数字 ID 找说话人；更老的文件里数字 ID 字段存的是 8 位实例 ID，也一并兜住。</summary>
    /// <param name="node">节点。</param>
    /// <returns>角色；没有绑定或找不到返回 null。</returns>
    private StorySpeaker? ResolveSpeaker(StoryNode node)
    {
        var id = node.SpeakerObjectId;
        var instance = node.SpeakerInstanceId;

        if (!string.IsNullOrWhiteSpace(instance))
        {
            foreach (var speaker in _speakers)
            {
                if (speaker.ObjectId == instance)
                {
                    return speaker;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var speaker in _speakers)
        {
            if (speaker.NumericId == id || speaker.ObjectId == id)
            {
                return speaker;
            }
        }

        return null;
    }

    /// <summary>按镜头指令推近或震动；没有指令时恢复常态。</summary>
    /// <param name="node">节点。</param>
    private void ApplyCamera(StoryNode node)
    {
        _portraitScale.ScaleX = node.CameraEffect == StoryCameraEffects.Zoom ? 1.14 : 1;
        _portraitScale.ScaleY = node.CameraEffect == StoryCameraEffects.Zoom ? 1.14 : 1;

        if (node.CameraEffect == StoryCameraEffects.Shake)
        {
            PlayShake();
        }
    }

    /// <summary>按节点里的等待秒数安排自动推进；手动节点或选项节点不自动走。</summary>
    /// <param name="node">节点。</param>
    private void StartAutoTimer(StoryNode node)
    {
        _autoTimer.Stop();
        if (_waitingChoice || node.Kind == StoryNodeKinds.End || node.WaitSeconds <= 0)
        {
            return;
        }

        _autoTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(node.WaitSeconds, 0.2, 20));
        _autoTimer.Start();
    }

    #endregion
}
