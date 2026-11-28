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

        var title = UiFactory.SectionHead("资源列表");
        Grid.SetRow(title, 0);
        stack.Children.Add(title);

        // 扫描是这一栏的常规动作，不再做整块实心青：常态白底细边，悬停才浮出浅青，
        // 让整页只有右下角「保存修改」一处实心主色。
        var scan = new Button
        {
            Content = "扫描",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 4),
            Height = 28,
            FontSize = 11.5,
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
        _countText.FontSize = UiMetrics.FontBody;
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
        // 与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        var list = UiFactory.VerticalScroll(_unconfiguredHost, showScrollBar: false);
        _resourceListScroll = list;
        var listFrame = UiFactory.ScrollListFrame(list);
        Grid.SetRow(listFrame, 6);
        stack.Children.Add(listFrame);

        // 左栏与右侧卡片走同一个工厂：白底 + 面板描边 + 8 圆角
        return UiFactory.PanelCard(stack, clipToBounds: true);
    }

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

        grid.Children.Add(UiFactory.InCell(Label("Collectible"), 4, 0));
        grid.Children.Add(UiFactory.InCell(_fieldCollectible, 4, 1));

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
        grid.Children.Add(UiFactory.InCell(addButton, 4, 2, columnSpan: 2));

        foreach (var box in new[] { _fieldId, _fieldImagePath, _fieldName, _fieldAnim, _fieldLoop, _fieldSpeed, _fieldOrder, _fieldShadow, _fieldCollectible })
        {
            box.Margin = DetailFieldMargin;
            box.Height = DetailFieldHeight;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        var content = new StackPanel
        {
            Spacing = 0,
            Children =
            {
                UiFactory.SectionHead("选中项", margin: new Thickness(0, 0, 0, 6)),
                new TextBlock
                {
                    Text = $"左侧点选图片后可编辑字段；Ctrl 点击多选、Shift 连选，再点「{AddToTableButtonText}」批量入表。",
                    Foreground = UiTheme.TextSecondary,
                    FontSize = UiMetrics.FontBody,
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
        };

        return UiFactory.PanelCard(content, padding: DetailCardPadding, clipToBounds: true);
    }

    private void AddFieldRow(Grid grid, int row, string leftLabel, Control leftBox, string rightLabel, Control rightBox)
    {
        grid.Children.Add(UiFactory.InCell(Label(leftLabel), row, 0));
        grid.Children.Add(UiFactory.InCell(leftBox, row, 1));
        grid.Children.Add(UiFactory.InCell(Label(rightLabel), row, 2));
        grid.Children.Add(UiFactory.InCell(rightBox, row, 3));
    }

    /// <summary>字段标签：右对齐、去尾冒号，样式统一走 <see cref="UiFactory.FieldLabel"/>。</summary>
    /// <param name="text">标签文案。</param>
    /// <returns>字段标签。</returns>
    private static TextBlock Label(string text) =>
        UiFactory.FieldLabel(text, 11, horizontalAlignment: HorizontalAlignment.Right);

    private Control BuildConfiguredCard()
    {
        // 表头（分组标题/列名/类型/注释四行）固定在顶部，只有数据行滚动：
        // 外层 Grid 的第二列留给 14px 箭头条，因此表头与数据区拿到的可用宽度完全一致，
        // 冻结表头的列不会和数据行错位，右侧也不会出现一条空白的缝。
        // 箭头条所在的这一列再包一层浅灰「轨道槽」并跨表头 + 数据两行，
        // 让滚动条从表格顶部一直贯通到底部，右上角不会空出一小块白块。
        // 表头铺一层极浅青，和下面的数据行拉开层次；底部补一条分隔线收口
        var headerBorder = UiFactory.HeaderBand(
            _configuredHeaderHost,
            brush: UiTheme.Divider,
            background: UiTheme.Form.GroupHeader);

        var bodyScroll = UiFactory.VerticalScroll(_configuredHost, showScrollBar: false);

        var bodyBar = new ClassicScrollBar { Target = bodyScroll };

        var barLane = new Border
        {
            Background = UiTheme.ResourcePanel.ScrollLane,
            Child = bodyBar,
        };

        var inner = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,14"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                UiFactory.InCell(headerBorder, 0, 0),
                UiFactory.InCell(bodyScroll, 1, 0),
                // 轨道槽跨「表头 + 数据」两行，滚动条从顶部贯通到底部
                UiFactory.InCell(barLane, 0, 1, rowSpan: 2),
            },
        };

        var tableFrame = UiFactory.PanelCard(inner, clipToBounds: true);
        tableFrame.Margin = new Thickness(0);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        // 五个按钮只有「文案 / 提示 / 点击」三处差异，套同一个助手，避免再复制样式
        _createTableButton = AddBottomButton(buttons, "新建收集物表", "在配置表文件夹创建 TableCollectibles.csv；已有同名表时不会覆盖", CreateCurrentCategoryTable);
        _createTableButton.IsVisible = false;
        // 命名体检：按图片名生成中文显示名，确认后写进「显示名称」列
        AddBottomButton(buttons, "命名体检", "按图片名推导中文显示名（如 prop_lamp_2 → 路灯），确认后写入各表的「显示名称」列；只写高可信条目", OpenNamingAudit);
        // 地图上已摆放但配置表里没有的资源，一键补齐成配置行并分配数字 ID（实例模板 ID 同步改写）
        AddBottomButton(buttons, "补齐地图资源 ID", "把地图上用到的资源登记进配置表：缺行的按分类新建并分配数字 ID，实例的模板 ID 同步改成数字 ID；写表前自动备份", BindMapResourcesToTable);
        AddBottomButton(buttons, "打开配置表", "在系统默认程序里打开当前分类的配置表", OpenConfigTable);
        AddBottomButton(buttons, "打开文件位置", "打开配置表所在的文件夹", OpenFolder);
        AddBottomButton(buttons, "保存修改", "把改动写回配置表与工程 JSON", SaveChanges, primary: true);

        var bottom = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 4, 0, 0),
            Children = { UiFactory.InCell(buttons, 0, 1) },
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(8),
            Children =
            {
                UiFactory.InCell(tableFrame, 0, 0),
                UiFactory.InCell(bottom, 1, 0),
            },
        };

        return UiFactory.PanelCard(layout, clipToBounds: true);
    }

    /// <summary>底部操作条的统一按钮：紧凑样式 + 悬停提示 + 点击回调，主操作走高亮实心。</summary>
    /// <param name="host">按钮容器。</param>
    /// <param name="text">按钮文案。</param>
    /// <param name="tip">悬停提示。</param>
    /// <param name="click">点击回调。</param>
    /// <param name="primary">是否主操作（青绿实心）。</param>
    /// <returns>创建好的按钮，便于调用方继续调整可见性等状态。</returns>
    private Button AddBottomButton(StackPanel host, string text, string tip, Action click, bool primary = false)
    {
        var button = primary
            ? UiFactory.CompactButton(text, UiTheme.Accent, UiTheme.White)
            : UiFactory.CompactButton(text);
        if (primary)
        {
            button.BorderBrush = UiTheme.Accent;
            button.FontWeight = FontWeight.SemiBold;
            button.CornerRadius = new CornerRadius(FilterButtonCornerRadius);
        }

        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => click();
        host.Children.Add(button);
        return button;
    }

    #endregion
}
