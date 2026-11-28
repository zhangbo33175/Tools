/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryPreviewWindow.Choices.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情预览的选项分支与键鼠输入：分支按钮、数字键选中、震屏演出
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryPreviewWindow
{
    #region 选项

    /// <summary>按节点内容铺选项按钮；非选项节点清空。</summary>
    /// <param name="node">节点。</param>
    private void BuildChoices(StoryNode node)
    {
        _choicesHost.Children.Clear();
        _waitingChoice = false;
        _choicesHost.IsVisible = false;

        if (node.Kind != StoryNodeKinds.Choice || node.Choices.Count == 0)
        {
            return;
        }

        for (var index = 0; index < node.Choices.Count; index++)
        {
            var choice = node.Choices[index];
            _choicesHost.Children.Add(ChoiceButton(index + 1, choice));
        }

        _waitingChoice = true;
        _choicesHost.IsVisible = true;
        _hintText.Text = "选择一条分支继续";
        _autoTimer.Stop();
    }

    /// <summary>一个分支按钮。</summary>
    /// <param name="number">序号，键盘提示用。</param>
    /// <param name="choice">分支。</param>
    /// <returns>按钮。</returns>
    private Control ChoiceButton(int number, StoryChoice choice)
    {
        var label = new TextBlock
        {
            Text = $"{number}. {(string.IsNullOrWhiteSpace(choice.Text) ? "（未填选项文字）" : choice.Text)}",
            FontSize = 12.5,
            Foreground = UiTheme.Story.ChoiceText,
            TextWrapping = TextWrapping.Wrap,
        };

        var button = new Button
        {
            Content = label,
            Background = UiTheme.Story.ChoiceSurface,
            BorderBrush = UiTheme.Story.ChoiceBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius),
            Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => Choose(choice);
        RowState.AttachHover(button, hovered =>
            button.Background = hovered ? UiTheme.Story.ChoiceHover : UiTheme.Story.ChoiceSurface);
        return button;
    }

    /// <summary>选中一条分支：有目标就跳，没目标就走默认后续。</summary>
    /// <param name="choice">分支。</param>
    private void Choose(StoryChoice choice)
    {
        var target = _doc.Find(choice.TargetId);
        _waitingChoice = false;
        _choicesHost.IsVisible = false;
        _choicesHost.Children.Clear();

        if (target is not null)
        {
            GoTo(target);
            return;
        }

        if (_current is not null && _doc.Find(_current.NextId) is not null)
        {
            Advance();
            return;
        }

        ShowEnding();
    }

    #endregion

    #region 输入与演出

    /// <summary>键盘：空格/回车推进，数字选分支，Esc 关闭。</summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">按键参数。</param>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                Advance();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            default:
                if (_waitingChoice && _current is not null)
                {
                    var index = e.Key - Key.D1;
                    if (index >= 0 && index < _current.Choices.Count)
                    {
                        Choose(_current.Choices[index]);
                        e.Handled = true;
                    }
                }

                break;
        }
    }

    /// <summary>按节点指令震一下舞台：来回平移几帧后归位。</summary>
    private void PlayShake()
    {
        _shakeTimer?.Stop();
        var frames = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(28) };
        timer.Tick += (_, _) =>
        {
            frames++;
            if (frames >= ShakeFrames)
            {
                timer.Stop();
                _stageShift.X = 0;
                _stageShift.Y = 0;
                return;
            }

            var amplitude = 7.0 * (1 - frames / (double)ShakeFrames);
            _stageShift.X = frames % 2 == 0 ? amplitude : -amplitude;
            _stageShift.Y = frames % 4 < 2 ? amplitude / 2 : -amplitude / 2;
        };
        _shakeTimer = timer;
        timer.Start();
    }

    #endregion
}
