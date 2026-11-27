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
            // 与其它页面统一的浅青底，替代原来的中性灰
            Background = UiTheme.PanelSurface,
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

        var title = SectionHead("资源列表");
        Grid.SetRow(title, 0);
        stack.Children.Add(title);

        var scan = new Button
        {
            Content = "扫描",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
            Height = 30,
            Background = UiTheme.Accent,
            BorderBrush = UiTheme.Accent,
            Foreground = UiTheme.White,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(FilterButtonCornerRadius),
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
                Padding = new Thickness(4, 4),
                Margin = new Thickness(0, 0, 3, 4),
                CornerRadius = new CornerRadius(FilterButtonCornerRadius),
                FontSize = 11.5,
            };
            button.Click += (_, _) => ApplyFilter(name);
            _filterButtons[name] = button;
            Grid.SetColumn(button, index % 3);
            Grid.SetRow(button, index / 3);
            filters.Children.Add(button);
        }

        Grid.SetRow(filters, 2);
        stack.Children.Add(filters);
        // 建完立刻套一次选中态：首帧只有当前分类是高亮浅青，其余保持白底细描边
        RefreshFilterButtons();

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
        _countText.Text = "共 0 个资源";
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
        ShowPreviewPlaceholder();
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
        _resourceListScroll = list;
        var listFrame = UiFactory.ScrollListFrame(list);
        Grid.SetRow(listFrame, 6);
        stack.Children.Add(listFrame);

        return new Border
        {
            // 左栏与右侧卡片统一成白底 + 8 圆角，不再是一块直角的浅蓝底板
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PanelCornerRadius),
            ClipToBounds = true,
            Child = stack,
        };
    }

    /// <summary>「选中项」卡片圆角。</summary>
    private const double DetailCardCornerRadius = 8.0;

    /// <summary>「选中项」卡片内边距。</summary>
    private static readonly Thickness DetailCardPadding = new(14, 12, 14, 12);

    /// <summary>「选中项」字段行距。</summary>
    private const double DetailRowSpacing = 8.0;

    /// <summary>「选中项」字段框高度。</summary>
    private const double DetailFieldHeight = 30.0;

    /// <summary>「选中项」字段框外边距。</summary>
    private static readonly Thickness DetailFieldMargin = new(0, 0, 10, 0);

    /// <summary>标题下细分隔线 / 说明文字的下边距。</summary>
    private const double DetailDividerMarginBottom = 10.0;

    private Control BuildDetailCard()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("84,*,84,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = DetailRowSpacing,
        };

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
        // 这张卡上唯一的写操作，给实心强调色，比白底小按钮更容易被找到
        addButton.Background = UiTheme.Accent;
        addButton.BorderBrush = UiTheme.Accent;
        addButton.Foreground = UiTheme.White;
        addButton.FontWeight = FontWeight.SemiBold;
        addButton.Height = DetailFieldHeight;
        addButton.Padding = new Thickness(14, 0);
        addButton.CornerRadius = new CornerRadius(FilterButtonCornerRadius);
        addButton.HorizontalAlignment = HorizontalAlignment.Right;
        addButton.VerticalAlignment = VerticalAlignment.Bottom;
        addButton.Click += (_, _) => AddToConfigTable();
        _addButton = addButton;
        Grid.SetRow(addButton, 4);
        Grid.SetColumn(addButton, 2);
        Grid.SetColumnSpan(addButton, 2);
        grid.Children.Add(addButton);

        foreach (var box in new[] { _fieldId, _fieldImagePath, _fieldName, _fieldAnim, _fieldLoop, _fieldSpeed, _fieldOrder, _fieldShadow, _fieldCollectible })
        {
            box.Margin = DetailFieldMargin;
            box.Height = DetailFieldHeight;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DetailCardCornerRadius),
            Padding = DetailCardPadding,
            Child = new StackPanel
            {
                Spacing = 0,
                Children =
                {
                    SectionHead("选中项", new Thickness(0, 0, 0, 6)),
                    new TextBlock
                    {
                        Text = $"左侧点选图片后可编辑字段；Ctrl 点击多选、Shift 连选，再点「{AddToTableButtonText}」批量入表。",
                        Foreground = UiTheme.TextSecondary,
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, DetailDividerMarginBottom),
                    },
                    new Border
                    {
                        Height = 1,
                        Background = UiTheme.Divider,
                        Margin = new Thickness(0, 0, 0, DetailDividerMarginBottom),
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
        // 与底部属性栏保持同一口径：标签不带尾冒号，靠留白分隔
        Text = text,
        Foreground = UiTheme.TextSecondary,
        FontSize = 11,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Right,
        Margin = new Thickness(0, 0, 8, 0),
    };

    private Control BuildConfiguredCard()
    {
        // 表头（分组标题/列名/类型/注释四行）固定在顶部，只有数据行滚动：
        // 外层 Grid 的第二列留给 14px 箭头条，因此表头与数据区拿到的可用宽度完全一致，
        // 冻结表头的列不会和数据行错位，右侧也不会出现一条空白的缝。
        // 箭头条所在的这一列再包一层浅灰「轨道槽」并跨表头 + 数据两行，
        // 让滚动条从表格顶部一直贯通到底部，右上角不会空出一小块白块。
        var headerBorder = new Border
        {
            // 表头铺一层极浅青，和下面的数据行拉开层次；底部补一条分隔线收口
            Background = UiTheme.Form.GroupHeader,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
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
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Background = UiTheme.Card,
            ClipToBounds = true,
            Child = inner,
        };
        tableFrame.Margin = new Thickness(0);
        Grid.SetRow(tableFrame, 0);

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

        // 地图上已摆放但配置表里没有的资源，一键补齐成配置行并分配数字 ID（实例模板 ID 同步改写）
        var bindMap = UiFactory.CompactButton("补齐地图资源 ID");
        ToolTip.SetTip(bindMap, "把地图上用到的资源登记进配置表：缺行的按分类新建并分配数字 ID，实例的模板 ID 同步改成数字 ID；写表前自动备份");
        bindMap.Click += (_, _) => BindMapResourcesToTable();
        buttons.Children.Add(bindMap);

        var openTable = UiFactory.CompactButton("打开配置表");
        openTable.Click += (_, _) => OpenConfigTable();
        buttons.Children.Add(openTable);

        var openFolder = UiFactory.CompactButton("打开文件位置");
        openFolder.Click += (_, _) => OpenFolder();
        buttons.Children.Add(openFolder);

        var save = UiFactory.CompactButton("保存修改", UiTheme.Accent, UiTheme.White);
        save.BorderBrush = UiTheme.Accent;
        save.FontWeight = FontWeight.SemiBold;
        save.CornerRadius = new CornerRadius(FilterButtonCornerRadius);
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
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(8),
        };
        Grid.SetRow(tableFrame, 0);
        layout.Children.Add(tableFrame);
        Grid.SetRow(bottom, 1);
        layout.Children.Add(bottom);

        return new Border
        {
            // 与「选中项」卡一致：白底 + 8 圆角 + 细描边
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PanelCornerRadius),
            ClipToBounds = true,
            Child = layout,
        };
    }

    /// <summary>栏标题：复用 <see cref="UiFactory.SectionHead"/>，各页标题语汇保持一致。</summary>
    /// <param name="text">标题文案。</param>
    /// <param name="margin">可选外边距。</param>
    /// <returns>可直接塞进面板的标题行。</returns>
    private static Control SectionHead(string text, Thickness? margin = null) =>
        UiFactory.SectionHead(text, margin: margin);

    #endregion
}
