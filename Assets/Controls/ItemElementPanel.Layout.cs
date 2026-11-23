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
            ColumnDefinitions = new ColumnDefinitions($"{ListPanelWidth},8,*"),
            Margin = new Thickness(4, 4, 4, 4),
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
        var header = new TextBlock
        {
            Text = "元件列表",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var tabs = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, 0, 0, 6),
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
                Padding = new Thickness(4, 3),
                Margin = new Thickness(index == 0 ? 0 : 2, 0, index == types.Length - 1 ? 0 : 2, 0),
            };

            tab.Click += (_, _) => SwitchTab(type);
            _tabButtons[type] = tab;
            Grid.SetColumn(tab, index);
            tabs.Children.Add(tab);
        }

        // 卡片内用 Grid：标题/类型页签固定，列表占满剩余高度并在卡片内滚动（StackPanel 会给无限高度，列表会溢出卡片）
        var stack = new Grid
        {
            Margin = new Thickness(6),
            RowSpacing = 6,
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        Grid.SetRow(header, 0);
        stack.Children.Add(header);
        Grid.SetRow(tabs, 1);
        stack.Children.Add(tabs);

        // 列表区与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        _listHost.Margin = new Thickness(4);
        var list = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            Content = _listHost,
        };
        var scrollArea = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14") };
        scrollArea.Children.Add(list);
        var scrollBar = new ClassicScrollBar { Target = list };
        Grid.SetColumn(scrollBar, 1);
        scrollArea.Children.Add(scrollBar);

        var listFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#C9C9C9")),
            BorderThickness = new Thickness(1),
            Child = scrollArea,
        };
        Grid.SetRow(listFrame, 2);
        stack.Children.Add(listFrame);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = stack,
            // 圆角 + 裁剪，避免列表内容压到卡片右下角的描边上
            ClipToBounds = true,
            CornerRadius = new CornerRadius(2),
        };
    }

    private Control BuildPropertyPanel()
    {
        _formHost.Spacing = 8;
        _formHost.Margin = new Thickness(12, 10, 12, 12);
        _groupsHost.Spacing = 10;

        _emptyHint.Text = "请在左侧元件列表中选择一个元件，右侧将显示它的动画、交互、层级碰撞、交通、收集、YSort、移动循环与传送门配置。";
        _emptyHint.Foreground = UiTheme.TextSecondary;
        _emptyHint.TextWrapping = TextWrapping.Wrap;
        _emptyHint.MaxWidth = 640;
        _emptyHint.HorizontalAlignment = HorizontalAlignment.Left;
        _emptyHint.Margin = new Thickness(0, 16, 0, 8);
        _formHost.Children.Add(_emptyHint);
        _formHost.Children.Add(_groupsHost);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _formHost,
        };

        BuildFormSkeleton();

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = scroll,
        };
    }

    #endregion
}
