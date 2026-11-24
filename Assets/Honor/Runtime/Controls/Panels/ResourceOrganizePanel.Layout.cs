/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源面板布局：左栏扫描预览、右侧字段表和底部配置表骨架。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool.Controls;

public sealed partial class ResourceOrganizePanel : UserControl
{
    #region 布局

    private Control BuildLayout()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LeftWidth},8,*"),
            Background = UiTheme.Form.SubtleSurface,
            Margin = new Thickness(4),
        };

        root.Children.Add(BuildLeftPanel());

        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        right.Children.Add(BuildDetailCard());

        var table = BuildConfiguredCard();
        Grid.SetRow(table, 1);
        table.Margin = new Thickness(0, 4, 0, 0);
        right.Children.Add(table);

        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private Control BuildLeftPanel()
    {
        // 纵向排布改成 Grid：上方几块固定，列表占满剩余高度 —— 滚动条轨道才能拉满整张卡片
        var stack = new Grid
        {
            Margin = new Thickness(8),
            RowSpacing = 6,
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,*"),
        };

        var title = new TextBlock
        {
            Text = "未配置资源",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
        };
        Grid.SetRow(title, 0);
        stack.Children.Add(title);

        var scan = new Button
        {
            Content = "扫描",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 5),
            Background = UiTheme.Accent,
            Foreground = UiTheme.White,
        };
        ToolTip.SetTip(scan, "扫描「地图配置 → 地图保存目录」里的 PNG（烘焙切片 / 分区底图）");
        scan.Click += (_, _) => ScanUnconfigured(silent: false);
        Grid.SetRow(scan, 1);
        stack.Children.Add(scan);

        var filterRows = (FilterNames.Length + 2) / 3;
        var filters = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", filterRows))),
        };

        for (int index = 0; index < FilterNames.Length; index++)
        {
            var name = FilterNames[index];
            var button = new Button
            {
                Content = name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(4, 3),
                Margin = new Thickness(0, 0, 0, 3),
            };
            button.Click += (_, _) => ApplyFilter(name);
            _filterButtons[name] = button;
            Grid.SetColumn(button, index % 3);
            Grid.SetRow(button, index / 3);
            filters.Children.Add(button);
        }

        Grid.SetRow(filters, 2);
        stack.Children.Add(filters);

        _search.PlaceholderText = "按资源名 / 目录过滤";
        _search.Padding = new Thickness(6, 3);
        _search.TextChanged += (_, _) =>
        {
            ResetSelection();
            RefreshUnconfiguredList();
        };
        Grid.SetRow(_search, 3);
        stack.Children.Add(_search);

        _countText.Foreground = UiTheme.TextSecondary;
        _countText.FontSize = 11;
        _countText.Text = "共 0 个未配置资源";
        Grid.SetRow(_countText, 4);
        stack.Children.Add(_countText);

        var previewBorder = new Border
        {
            Height = 170,
            Background = UiTheme.ResourcePanel.PreviewSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = _previewHost,
            // 预览图为原始尺寸时 DesiredSize 远超 170px，不裁剪会溢出盖住下方列表
            ClipToBounds = true,
        };
        _previewHost.Margin = new Thickness(4);
        Grid.SetRow(previewBorder, 5);
        stack.Children.Add(previewBorder);

        _unconfiguredHost.Spacing = 1;
        _unconfiguredHost.Margin = new Thickness(4);
        var list = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _unconfiguredHost,
        };
        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        list.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        var listFrame = UiFactory.ScrollListFrame(list);
        Grid.SetRow(listFrame, 6);
        stack.Children.Add(listFrame);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    private Control BuildDetailCard()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("76,*,76,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto") };

        AddFieldRow(grid, 0, "ID", _fieldId, "ImagePath", _fieldImagePath);
        AddFieldRow(grid, 1, "Name", _fieldName, "Anim", _fieldAnim);
        AddFieldRow(grid, 2, "Loop", _fieldLoop, "Speed", _fieldSpeed);
        AddFieldRow(grid, 3, "Order", _fieldOrder, "Shadow", _fieldShadow);

        var collectibleLabel = Label("Collectible");
        Grid.SetRow(collectibleLabel, 4);
        Grid.SetColumn(collectibleLabel, 0);
        grid.Children.Add(collectibleLabel);

        Grid.SetRow(_fieldCollectible, 4);
        Grid.SetColumn(_fieldCollectible, 1);
        grid.Children.Add(_fieldCollectible);

        var addButton = UiFactory.CompactButton(AddToTableButtonText);
        addButton.HorizontalAlignment = HorizontalAlignment.Right;
        addButton.VerticalAlignment = VerticalAlignment.Center;
        addButton.Click += (_, _) => AddToConfigTable();
        _addButton = addButton;
        Grid.SetRow(addButton, 4);
        Grid.SetColumn(addButton, 2);
        Grid.SetColumnSpan(addButton, 2);
        grid.Children.Add(addButton);

        foreach (var box in new[] { _fieldId, _fieldImagePath, _fieldName, _fieldAnim, _fieldLoop, _fieldSpeed, _fieldOrder, _fieldShadow, _fieldCollectible })
        {
            box.Margin = new Thickness(0, 2, 6, 2);
        }

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = "选中项",
                        Foreground = UiTheme.AccentDark,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 2),
                    },
                    new TextBlock
                    {
                        Text = $"左侧点选图片后可编辑字段；Ctrl 点击多选、Shift 连选，再点「{AddToTableButtonText}」批量入表。",
                        Foreground = UiTheme.TextSecondary,
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4),
                    },
                    grid,
                    _csvInfoText,
                },
            },
        };
    }

    private void AddFieldRow(Grid grid, int row, string leftLabel, Control leftBox, string rightLabel, Control rightBox)
    {
        var left = Label(leftLabel);
        Grid.SetRow(left, row);
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        Grid.SetRow(leftBox, row);
        Grid.SetColumn(leftBox, 1);
        grid.Children.Add(leftBox);

        var right = Label(rightLabel);
        Grid.SetRow(right, row);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        Grid.SetRow(rightBox, row);
        Grid.SetColumn(rightBox, 3);
        grid.Children.Add(rightBox);
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text + ":",
        Foreground = UiTheme.ResourcePanel.FieldLabel,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Right,
        Margin = new Thickness(0, 0, 4, 0),
    };

    private Control BuildConfiguredCard()
    {
        var header = new TextBlock
        {
            Text = "已配置资源",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
        };

        // 表头（分组标题/列名/类型/注释四行）固定在顶部，只有数据行滚动：
        // 外层 Grid 的第二列留给 14px 箭头条，因此表头与数据区拿到的可用宽度完全一致，
        // 冻结表头的列不会和数据行错位，右侧也不会出现一条空白的缝。
        // 箭头条所在的这一列再包一层浅灰「轨道槽」并跨表头 + 数据两行，
        // 让滚动条从表格顶部一直贯通到底部，右上角不会空出一小块白块。
        var headerBorder = new Border
        {
            Background = UiTheme.White,
            Child = _configuredHeaderHost,
        };
        Grid.SetColumn(headerBorder, 0);
        Grid.SetRow(headerBorder, 0);

        var bodyScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            Content = _configuredHost,
        };
        Grid.SetColumn(bodyScroll, 0);
        Grid.SetRow(bodyScroll, 1);

        var bodyBar = new ClassicScrollBar { Target = bodyScroll };

        var barLane = new Border
        {
            Background = UiTheme.ResourcePanel.ScrollLane,
            Child = bodyBar,
        };
        Grid.SetColumn(barLane, 1);
        Grid.SetRow(barLane, 0);
        Grid.SetRowSpan(barLane, 2);

        var inner = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,14"),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        inner.Children.Add(headerBorder);
        inner.Children.Add(bodyScroll);
        inner.Children.Add(barLane);

        var tableFrame = new Border
        {
            BorderBrush = UiTheme.ResourceTable.Line,
            BorderThickness = new Thickness(1),
            Background = UiTheme.Card,
            ClipToBounds = true,
            Child = inner,
        };
        tableFrame.Margin = new Thickness(0, 4, 0, 0);
        Grid.SetRow(tableFrame, 1);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var createTable = UiFactory.CompactButton("新建收集物表");
        createTable.IsVisible = false;
        ToolTip.SetTip(createTable, "在配置表文件夹创建 TableCollectibles.csv；已有同名表时不会覆盖");
        createTable.Click += (_, _) => CreateCurrentCategoryTable();
        _createTableButton = createTable;
        buttons.Children.Add(createTable);

        var openTable = UiFactory.CompactButton("打开配置表");
        openTable.Click += (_, _) => OpenConfigTable();
        buttons.Children.Add(openTable);

        var openFolder = UiFactory.CompactButton("打开文件位置");
        openFolder.Click += (_, _) => OpenFolder();
        buttons.Children.Add(openFolder);

        var save = UiFactory.CompactButton("保存修改", UiTheme.Accent, UiTheme.White);
        save.Click += (_, _) => SaveChanges();
        buttons.Children.Add(save);

        var bottom = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 4, 0, 0),
            Children = { buttons },
        };
        Grid.SetColumn(buttons, 1);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(8),
        };
        layout.Children.Add(header);
        Grid.SetRow(tableFrame, 1);
        layout.Children.Add(tableFrame);
        Grid.SetRow(bottom, 2);
        layout.Children.Add(bottom);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Child = layout,
        };
    }

    #endregion
}
