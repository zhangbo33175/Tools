/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Side.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页右栏：立绘与镜头演出、选项分支、文档自检
 ***************************************************************/

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    #region 右栏

    /// <summary>右栏：这一节点的演出怎么演、有哪些分支、整份文档有没有走不通的地方。</summary>
    /// <returns>右栏内容。</returns>
    private Control BuildSide()
    {
        _showPortraitCheck = new CheckBox
        {
            Content = "显示左侧立绘",
            FontSize = 11,
            IsChecked = true,
        };
        _showPortraitCheck.IsCheckedChanged += (_, _) => OnShowPortraitChanged();

        _portraitCombo = FieldCombo();
        _portraitCombo.SelectionChanged += (_, _) => OnPortraitPicked();

        _waitBox = Field();
        _waitBox.TextChanged += (_, _) => OnWaitChanged();

        _effectCombo = FieldCombo(StoryCameraEffects.All.Select(StoryCameraEffects.Display).ToArray());
        _effectCombo.SelectionChanged += (_, _) => OnEffectChanged();

        _choicesHost = new StackPanel { Spacing = 6 };
        _optionEmpty = new TextBlock
        {
            Text = "这一节点还没有分支：点上面的「+ 分支」，会自动把类型改成「选项」并加一条默认指向原后续的分支",
            FontSize = 10,
            Foreground = UiTheme.PreviewMuted,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        var addChoice = SmallButton("+ 分支", UiTheme.Story.KindChoice, UiTheme.White, AddChoice);
        addChoice.HorizontalAlignment = HorizontalAlignment.Left;

        _issuesText = new TextBlock
        {
            FontSize = 10.5,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        };

        var effectCard = Card("立绘与镜头", new StackPanel
        {
            Spacing = 9,
            Children =
            {
                _showPortraitCheck,
                Labeled("立绘取自哪个角色", _portraitCombo),
                Pair("自动推进（秒，0 = 手动）", _waitBox, "镜头指令", _effectCombo),
            },
        });

        var choiceCard = Card("选项分支", new StackPanel
        {
            Spacing = 7,
            Children = { addChoice, _optionEmpty, _choicesHost },
        });

        var checkCard = Card("文档自检", _issuesText);

        var stack = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(12, 0, 12, 12),
            Children = { effectCard, choiceCard, checkCard },
        };

        var headRow = new StackPanel
        {
            Margin = new Thickness(12, 10, 12, 6),
            Children = { SectionTitle("演出与分支") },
        };

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                headRow,
                Row(new ScrollViewer
                {
                    Content = stack,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                }, 1),
            },
        };
    }

    /// <summary>重建选项分支列表；没有分支时显示空态说明。</summary>
    private void RefreshChoiceRows()
    {
        _choicesHost.Children.Clear();
        var choices = _node?.Choices;
        var hasChoices = choices is { Count: > 0 };
        _optionEmpty.IsVisible = !hasChoices;

        if (!hasChoices)
        {
            return;
        }

        foreach (var choice in choices!)
        {
            _choicesHost.Children.Add(BuildChoiceRow(choice));
        }
    }

    /// <summary>一条分支行：选项文本 + 目标节点 + 删除。</summary>
    /// <param name="choice">分支。</param>
    /// <returns>分支行。</returns>
    private Control BuildChoiceRow(StoryChoice choice)
    {
        var text = Field();
        text.Text = choice.Text;
        text.PlaceholderText = "玩家看到的选项文字";
        text.TextChanged += (_, _) =>
        {
            if (_sync)
            {
                return;
            }

            choice.Text = text.Text ?? string.Empty;
            _dirty = true;
            RefreshFooter();
        };

        var target = FieldCombo();
        FillTargetCombo(target, choice.TargetId, includeEmpty: true);
        target.SelectionChanged += (_, _) =>
        {
            if (_sync || target.SelectedIndex < 0)
            {
                return;
            }

            choice.TargetId = TargetIdOf(target, includeEmpty: true);
            _dirty = true;
            RefreshFooter();
        };

        var remove = SmallButton("×", UiTheme.ResourceTable.DeleteSurface,
            UiTheme.ResourceTable.DeleteText, () =>
            {
                if (_node is null)
                {
                    return;
                }

                _node.Choices.Remove(choice);
                _dirty = true;
                RefreshChoiceRows();
                RefreshStats();
                RefreshFooter();
            });
        remove.Width = 28;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 4,
        };
        grid.Children.Add(text);
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);
        Grid.SetRow(target, 1);
        Grid.SetColumnSpan(target, 2);
        grid.Children.Add(target);

        return new Border
        {
            Background = UiTheme.Story.EffectStrip,
            BorderBrush = UiTheme.Form.GroupBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = grid,
        };
    }

    /// <summary>给目标节点下拉填上全部可跳节点。</summary>
    /// <param name="combo">下拉框。</param>
    /// <param name="selectedId">当前选中 ID。</param>
    /// <param name="includeEmpty">是否加一行「（走默认后续）」。</param>
    private void FillTargetCombo(ComboBox combo, string selectedId, bool includeEmpty)
    {
        var previous = _sync;
        _sync = true;
        combo.Items.Clear();
        if (includeEmpty)
        {
            combo.Items.Add("（走默认后续）");
        }

        var targets = _doc.Nodes.Where(node => node.IsJumpTarget && node.Id != _node?.Id).ToList();
        foreach (var node in targets)
        {
            combo.Items.Add($"{node.Id} · {node.Summary()}");
        }

        var index = includeEmpty ? 0 : -1;
        var found = targets.FindIndex(node => node.Id == selectedId);
        if (found >= 0)
        {
            index = found + (includeEmpty ? 1 : 0);
        }

        combo.SelectedIndex = index;
        _sync = false;
    }

    /// <summary>从目标下拉读回节点 ID。</summary>
    /// <param name="combo">下拉框。</param>
    /// <param name="includeEmpty">是否含空行。</param>
    /// <returns>节点 ID；空行返回空串。</returns>
    private string TargetIdOf(ComboBox combo, bool includeEmpty)
    {
        var index = combo.SelectedIndex - (includeEmpty ? 1 : 0);
        var targets = _doc.Nodes.Where(node => node.IsJumpTarget && node.Id != _node?.Id).ToList();
        return index >= 0 && index < targets.Count ? targets[index].Id : string.Empty;
    }

    /// <summary>新增一条分支，默认指向当前节点的后续。</summary>
    private void AddChoice()
    {
        if (_node is null)
        {
            SetMessage("先选中一个节点再加分支");
            return;
        }

        if (_node.Kind != StoryNodeKinds.Choice)
        {
            _node.Kind = StoryNodeKinds.Choice;
            _sync = true;
            _kindCombo.SelectedItem = StoryNodeKinds.Display(StoryNodeKinds.Choice);
            _sync = false;
            RebuildNodeList(_node.Id);
        }

        _node.Choices.Add(new StoryChoice { Text = "选项文字", TargetId = _node.NextId });
        _dirty = true;
        RefreshChoiceRows();
        RefreshStats();
        RefreshFooter();
    }

    #endregion
}
