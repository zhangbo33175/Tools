/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件面板布局骨架：左侧列表、右侧属性表单与整体分栏
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 布局骨架

    private Control BuildBody()
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{ListPanelWidth},{BodyPanelGap},*"),
            Margin = BodyMargin,
        };

        body.Children.Add(BuildListPanel());
        var property = BuildPropertyPanel();
        Grid.SetColumn(property, 2);
        body.Children.Add(property);
        Grid.SetRow(body, 1);
        return body;
    }

    private Control BuildListPanel()
    {
        var header = UiFactory.SectionHead("元件列表", margin: ListHeaderMargin);

        var tabs = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = ListTabsMargin,
        };

        var types = new[] { MapObjectType.Building, MapObjectType.Character, MapObjectType.Prop };
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            var tab = new Button
            {
                Content = ObjectTypeNames.Of(type),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = TypeTabPadding,
                Margin = new Thickness(
                    index == 0 ? 0 : TypeTabGap,
                    0,
                    index == types.Length - 1 ? 0 : TypeTabGap,
                    0),
            };

            tab.Click += (_, _) => SwitchTab(type);
            _tabButtons[type] = tab;
            Grid.SetColumn(tab, index);
            tabs.Children.Add(tab);
        }

        // 卡片内用 Grid：标题/类型页签固定，列表占满剩余高度并在卡片内滚动（StackPanel 会给无限高度，列表会溢出卡片）
        var stack = new Grid
        {
            Margin = ListCardMargin,
            RowSpacing = ListCardRowSpacing,
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        Grid.SetRow(header, 0);
        stack.Children.Add(header);
        Grid.SetRow(tabs, 1);
        stack.Children.Add(tabs);

        // 列表区与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        _listHost.Margin = ListHostMargin;
        var list = UiFactory.VerticalScroll(_listHost, showScrollBar: false);
        var listFrame = UiFactory.ScrollListFrame(list);
        Grid.SetRow(listFrame, 2);
        stack.Children.Add(listFrame);

        return CreatePanelCard(stack, true);
    }

    private Control BuildPropertyPanel()
    {
        _formHost.Spacing = FormHostSpacing;
        _formHost.Margin = FormHostMargin;
        _groupColumnLeft.Spacing = FormGroupsSpacing;
        _groupColumnRight.Spacing = FormGroupsSpacing;
        if (_groupsHost.Children.Count == 0)
        {
            _groupsHost.ColumnDefinitions = new ColumnDefinitions("*,14,*");
            _groupsHost.Children.Add(_groupColumnLeft);
            Grid.SetColumn(_groupColumnRight, 2);
            _groupsHost.Children.Add(_groupColumnRight);
        }

        _emptyHint.Text = "请在左侧元件列表中选择一个元件，右侧将显示它的动画、交互、层级碰撞、交通、收集、YSort、移动循环与传送门配置。";
        _emptyHint.Foreground = UiTheme.TextSecondary;
        _emptyHint.TextWrapping = TextWrapping.Wrap;
        _emptyHint.MaxWidth = EmptyHintMaxWidth;
        _emptyHint.HorizontalAlignment = HorizontalAlignment.Left;
        _emptyHint.Margin = EmptyHintMargin;
        _formHost.Children.Add(_emptyHint);
        _formHost.Children.Add(_groupsHost);

        var scroll = UiFactory.VerticalScroll(_formHost);

        BuildFormSkeleton();

        return CreatePanelCard(scroll);
    }

    /// <summary>
    /// 创建左右两栏共用的面板卡片外观：交给 <see cref="UiFactory.PanelCard"/>，
    /// 与资源整理、剧情各页保持同一份描边与圆角。
    /// </summary>
    /// <param name="child">卡片内容。</param>
    /// <param name="clipToBounds">是否裁剪超出圆角的内容，列表卡片需要。</param>
    /// <returns>面板卡片边框。</returns>
    private static Border CreatePanelCard(Control child, bool clipToBounds = false) =>
        UiFactory.PanelCard(child, clipToBounds: clipToBounds);

    #endregion
}
