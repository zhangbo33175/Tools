/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Inspector.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页中栏：剧情标题与当前节点的台词、类型、跳转
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
    #region 中栏

    /// <summary>中间栏：上面写剧情标题，下面写当前节点说什么、接着去哪。</summary>
    /// <returns>中栏内容。</returns>
    private Control BuildInspector()
    {
        _titleBox = Field();
        _titleBox.TextChanged += (_, _) => OnTitleChanged();

        _statText = new TextBlock
        {
            FontSize = UiMetrics.FontCaption,
            Foreground = UiTheme.PreviewMuted,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var headRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(12, 10, 12, 6),
        };
        headRow.Children.Add(UiFactory.SectionHead("节点详情"));
        Grid.SetColumn(_statText, 1);
        headRow.Children.Add(_statText);

        _kindCombo = FieldCombo(StoryNodeKinds.All.Select(StoryNodeKinds.Display).ToArray());
        _kindCombo.SelectionChanged += (_, _) => OnKindChanged();

        _nextCombo = FieldCombo();
        _nextCombo.SelectionChanged += (_, _) => OnNextChanged();

        _speakerPick = FieldCombo();
        _speakerPick.SelectionChanged += (_, _) => OnSpeakerPicked();

        _speakerBox = Field();
        _speakerBox.TextChanged += (_, _) => OnSpeakerTextChanged();

        _textBox = Field(multiline: true, height: 132);
        _textBox.TextChanged += (_, _) => OnTextChanged();

        _noteBox = Field(multiline: true, height: 62);
        _noteBox.TextChanged += (_, _) => OnNoteChanged();

        var stack = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(12, 0, 12, 12),
            Children =
            {
                Labeled("剧情标题", _titleBox, "保存后写在剧情文件里，也是预览窗标题"),
                Card("节点", new StackPanel
                {
                    Spacing = 9,
                    Children =
                    {
                        Pair("节点类型", _kindCombo, "接下来", _nextCombo),
                        Labeled("节点 ID（自动分配，跳转时按它指路）", ReadOnlyIdText()),
                    },
                }),
                Card("台词", new StackPanel
                {
                    Spacing = 9,
                    Children =
                    {
                        Labeled("说话人（先从地图角色里挑，再改显示名）", _speakerPick),
                        Labeled("显示名", _speakerBox),
                        Labeled("正文（旁白可留空标题，对白按句分行）", _textBox),
                    },
                }),
                Card("策划备注", _noteBox, "只在编辑器里看，不参与预览"),
            },
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                headRow,
                Row(UiFactory.VerticalScroll(stack), 1),
            },
        };
        return root;
    }

    /// <summary>只读的节点 ID 显示框。</summary>
    /// <returns>只读框。</returns>
    private TextBox ReadOnlyIdText()
    {
        _idText = new TextBox
        {
            IsReadOnly = true,
            FontSize = 11.5,
            MinHeight = 28,
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(6),
            Background = UiTheme.Form.FieldReadOnly,
            BorderBrush = UiTheme.Form.FieldBorder,
            BorderThickness = new Thickness(1),
        };
        return _idText;
    }

    /// <summary>带标签的字段。</summary>
    /// <param name="label">标签。</param>
    /// <param name="field">控件。</param>
    /// <param name="hint">可选说明。</param>
    /// <returns>竖排字段。</returns>
    private static Control Labeled(string label, Control field, string? hint = null)
    {
        var stack = new StackPanel { Spacing = 3, Children = { FieldLabel(label), field } };
        if (!string.IsNullOrEmpty(hint))
        {
            stack.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 9.5,
                Foreground = UiTheme.PreviewMuted,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return stack;
    }

    /// <summary>并排两个字段。</summary>
    /// <param name="leftLabel">左标签。</param>
    /// <param name="left">左控件。</param>
    /// <param name="rightLabel">右标签。</param>
    /// <param name="right">右控件。</param>
    /// <returns>两列一行。</returns>
    private static Control Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Children =
            {
                Labeled(leftLabel, left),
                Labeled(rightLabel, right),
            },
        };
        Grid.SetColumn((Control)grid.Children[1], 1);
        return grid;
    }

    /// <summary>白底卡片：标题 + 内容，8 圆角。</summary>
    /// <param name="title">卡片标题。</param>
    /// <param name="content">内容。</param>
    /// <param name="hint">可选说明。</param>
    /// <returns>卡片。</returns>
    private static Control Card(string title, Control content, string? hint = null)
    {
        var inner = new StackPanel
        {
            Spacing = 8,
            // 卡片标题统一成「3px 竖条 + 加粗文字」，与左右两栏的栏标题同款
            Children = { UiFactory.SectionHead(title), content },
        };

        if (!string.IsNullOrEmpty(hint))
        {
            inner.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 9.5,
                Foreground = UiTheme.PreviewMuted,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return UiFactory.GroupBlock(inner);
    }

    #endregion
}
