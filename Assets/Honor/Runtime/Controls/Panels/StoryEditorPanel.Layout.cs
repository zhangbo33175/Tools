/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StoryEditorPanel.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   剧情编辑页的骨架：头部工具条、节点列、底部状态条与控件工厂
 ***************************************************************/

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class StoryEditorPanel
{
    #region 骨架

    /// <summary>装配整页：头部一条工具条，下面「节点列 / 节点详情 / 演出与自检」三栏。</summary>
    /// <returns>整页内容。</returns>
    private Control BuildLayout()
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,*,372"),
            ColumnSpacing = 8,
            Margin = new Thickness(8, 8, 8, 0),
        };

        body.Children.Add(BuildNodeColumn());

        var inspector = Pane(BuildInspector(), padding: 0);
        Grid.SetColumn(inspector, 1);
        body.Children.Add(inspector);

        var side = Pane(BuildSide(), padding: 0);
        Grid.SetColumn(side, 2);
        body.Children.Add(side);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                BuildHeader(),
                Row(body, 1),
                Row(BuildFooter(), 2),
            },
        };
    }

    /// <summary>把控件放到指定行（单列网格）——直接委托 <see cref="UiFactory.InCell"/>，不再自己写附加属性。</summary>
    /// <param name="child">控件。</param>
    /// <param name="row">行号。</param>
    /// <returns>同一个控件。</returns>
    private static Control Row(Control child, int row) => UiFactory.InCell(child, row, 0);

    /// <summary>带白底、1px 描边与卡片圆角的面板容器，交给 <see cref="UiFactory.PanelCard"/>。</summary>
    /// <param name="child">内容。</param>
    /// <param name="padding">内边距。</param>
    /// <returns>面板容器。</returns>
    private static Border Pane(Control child, double padding)
    {
        var pane = UiFactory.PanelCard(child, clipToBounds: true, padding: new Thickness(padding));
        pane.BorderBrush = UiTheme.Form.GroupBorder;
        return pane;
    }

    #endregion

    #region 头部

    /// <summary>头部：左侧标题与当前剧情文件，右侧新建 / 保存 / 预览。</summary>
    /// <returns>头部工具条。</returns>
    private Control BuildHeader()
    {
        _headText = new TextBlock
        {
            Text = "剧情编辑",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _mapText = new TextBlock
        {
            Text = "未打开地图",
            FontSize = 10.5,
            Foreground = UiTheme.PreviewMuted,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var newButton = ToolButton("新建剧情", null, UiTheme.TextSecondary, NewStory);
        newButton.Margin = new Thickness(0, 0, 6, 0);
        var saveButton = ToolButton("保存剧情", UiTheme.PreviewAccentSoft, UiTheme.InfoText, SaveStoryFromButton);
        saveButton.Margin = new Thickness(0, 0, 6, 0);
        var previewButton = ToolButton("▶ 预览播放", UiTheme.Accent, UiTheme.White, OpenPreview);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { newButton, saveButton, previewButton },
        };

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _headText, _mapText },
        };

        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
        };
        head.Children.Add(title);
        Grid.SetColumn(buttons, 1);
        head.Children.Add(buttons);

        return UiFactory.HeaderBand(head, new Thickness(10, 8, 10, 8), UiTheme.PanelBorder);
    }

    #endregion

    #region 节点列

    /// <summary>左栏：节点列表 + 增删与排序按钮。</summary>
    /// <returns>左栏面板。</summary>
    private Control BuildNodeColumn()
    {
        _list = new ListBox
        {
            Background = UiTheme.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        _list.ItemTemplate = new FuncDataTemplate<StoryNode>((node, _) => BuildNodeRow(node));
        _list.SelectionChanged += (_, _) => OnNodeSelected();
        // 节点行是 ListBox 模板行，选中底色由主题的 ContentPresenter 决定，这里用剧情页专用色覆盖
        _list.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected").Template().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, UiTheme.Story.NodeSelectedSurface),
                new Setter(TemplatedControl.BorderBrushProperty, UiTheme.Story.NodeSelectedBorder),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            },
        });

        var scroll = UiFactory.VerticalScroll(_list);

        _nodeCountText = new TextBlock
        {
            FontSize = 10.5,
            Foreground = UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 6, 0, 6),
        };

        var addButton = SmallButton("+ 节点", UiTheme.PreviewAccentSoft, UiTheme.InfoText, AddNode);
        var deleteButton = SmallButton("删除", UiTheme.ResourceTable.DeleteSurface,
            UiTheme.ResourceTable.DeleteText, DeleteNode);
        var upButton = SmallButton("↑", null, UiTheme.TextSecondary, () => MoveNode(-1));
        var downButton = SmallButton("↓", null, UiTheme.TextSecondary, () => MoveNode(1));
        var startButton = SmallButton("设为起点", null, UiTheme.WizardTitle, MarkAsStart);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(9, 0, 9, 9),
            Children = { addButton, deleteButton, upButton, downButton, startButton },
        };

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { scroll, Row(actions, 1) },
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 9, 10, 5),
            Children = { UiFactory.SectionHead("节点"), _nodeCountText },
        };

        var host = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { header, Row(body, 1) },
        };

        var column = Pane(host, padding: 0);
        Grid.SetColumn(column, 0);
        return column;
    }

    /// <summary>一条节点行：类型色点 + 摘要 + 右侧节点 ID，起点额外带徽标。</summary>
    /// <param name="node">节点。</param>
    /// <returns>列表行。</returns>
    private Control BuildNodeRow(StoryNode node)
    {
        // 重建 ItemsSource 时虚拟化栈会回收集旧容器，模板可能以空数据再构建一次，这里必须挡住
        if (node is null)
        {
            return new Border { Height = 0 };
        }

        var summary = new TextBlock
        {
            Text = node.Summary(),
            FontSize = 11.5,
            Foreground = UiTheme.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var id = new TextBlock
        {
            Text = node.Id,
            FontSize = 9.5,
            Foreground = UiTheme.PreviewMuted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4,
        };

        if (node.Id == _doc.StartNodeId)
        {
            right.Children.Add(StartBadge());
        }

        right.Children.Add(id);

        var line = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 7,
        };
        line.Children.Add(UiFactory.MarkerDot(StoryKindBrush(node.Kind), 7));
        Grid.SetColumn(summary, 1);
        line.Children.Add(summary);
        Grid.SetColumn(right, 2);
        line.Children.Add(right);

        return UiFactory.ListDividerRow(line, new Thickness(8, 7, 8, 7));
    }

    /// <summary>「起点」小徽标。</summary>
    /// <returns>徽标。</returns>
    private static Control StartBadge() => UiFactory.TagChip(
        "起点",
        UiTheme.Story.StartSurface,
        UiTheme.Story.StartText,
        new UiFactory.ChipSkin(9, 5, 1, 5));

    /// <summary>按节点类型取色点画笔。</summary>
    /// <param name="kind">节点类型。</param>
    /// <returns>色点画刷。</returns>
    private static IBrush StoryKindBrush(string kind) => kind switch
    {
        StoryNodeKinds.Narration => UiTheme.Story.KindNarration,
        StoryNodeKinds.Choice => UiTheme.Story.KindChoice,
        StoryNodeKinds.Chapter => UiTheme.Story.KindChapter,
        StoryNodeKinds.End => UiTheme.Story.KindEnd,
        _ => UiTheme.Story.KindLine,
    };

    #endregion

    #region 底部

    /// <summary>底部状态条：保存目标、改动状态与提示。</summary>
    private Control BuildFooter()
    {
        _footerText = new TextBlock
        {
            FontSize = 10.5,
            Foreground = UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var footer = UiFactory.FooterBand(_footerText, new Thickness(10, 6, 10, 6));
        footer.Margin = new Thickness(0, 8, 0, 0);
        return footer;
    }

    #endregion

    #region 控件工厂

    /// <summary>头部工具按钮：白描边，主按钮直接给实心底。</summary>
    /// <param name="text">文案。</param>
    /// <param name="background">底色；null 为白底描边。</param>
    /// <param name="foreground">文字色。</param>
    /// <param name="onClick">点击回调。</param>
    /// <returns>按钮。</returns>
    private static Button ToolButton(string text, IBrush? background, IBrush foreground, Action onClick) =>
        ActionButton(text, background, foreground, onClick, compact: false);

    /// <summary>列表底部的小按钮，与工具按钮同源，只缩一号尺寸。</summary>
    /// <param name="text">文案。</param>
    /// <param name="background">底色；null 为白底描边。</param>
    /// <param name="foreground">文字色。</param>
    /// <param name="onClick">点击回调。</param>
    /// <returns>按钮。</returns>
    private static Button SmallButton(string text, IBrush? background, IBrush foreground, Action onClick) =>
        ActionButton(text, background, foreground, onClick, compact: true);

    /// <summary>剧情页唯一的按钮工厂：尺寸两档，配色与圆角共用一套规则。</summary>
    /// <param name="text">文案。</param>
    /// <param name="background">底色；null 为白底描边。</param>
    /// <param name="foreground">文字色。</param>
    /// <param name="onClick">点击回调。</param>
    /// <param name="compact">是否紧凑档。</param>
    /// <returns>按钮。</returns>
    private static Button ActionButton(
        string text,
        IBrush? background,
        IBrush foreground,
        Action onClick,
        bool compact)
    {
        var button = new Button
        {
            Content = text,
            FontSize = compact ? 10.5 : 11.5,
            Padding = compact ? new Thickness(8, 3) : new Thickness(11, 5),
            MinHeight = compact ? 25 : 28,
            CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius),
            Foreground = foreground,
            Background = background ?? UiTheme.Card,
            BorderBrush = background ?? UiTheme.Form.FieldBorder,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>表单字段标签。</summary>
    /// <param name="text">标签文字。</param>
    /// <returns>标签。</returns>
    private static Control FieldLabel(string text) =>
        UiFactory.FieldLabel(text, 10.5, UiTheme.ResourcePanel.FieldLabel, margin: new Thickness(1, 0, 0, 3));

    /// <summary>输入框：6 圆角，与其它面板同一口径。</summary>
    /// <param name="multiline">是否多行。</param>
    /// <param name="height">最小高度。</param>
    /// <returns>输入框。</param>
    private static TextBox Field(bool multiline = false, double height = double.NaN) => new()
    {
        FontSize = UiMetrics.FontTitle,
        MinHeight = multiline ? height : UiMetrics.FieldHeight,
        Height = height,
        Padding = new Thickness(8, 5),
        CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
        BorderBrush = UiTheme.Form.FieldBorder,
        BorderThickness = new Thickness(1),
        AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
    };

    /// <summary>下拉框：6 圆角。</summary>
    /// <param name="items">选项。</param>
    /// <returns>下拉框。</returns>
    private static ComboBox FieldCombo(params string[] items)
    {
        var box = new ComboBox
        {
            FontSize = UiMetrics.FontTitle,
            MinHeight = UiMetrics.FieldHeight,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            BorderBrush = UiTheme.Form.FieldBorder,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var item in items)
        {
            box.Items.Add(item);
        }

        return box;
    }

    /// <endregion>

    #endregion
}
