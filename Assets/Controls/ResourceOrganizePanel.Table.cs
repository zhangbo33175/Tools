/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Table.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表：四行表头、可编辑单元格与操作列。
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
    #region 已配置资源表

    /// <summary>已配置资源表的列宽（表头与数据行共用，保证冻结表头与行内容不会错列）。</summary>
    private const string TableColumns = "84,92,80,104,88,44,50,76,*,100,88,92,96";

    /// <summary>表头四行的高度：分组标题 / 字段名 / 字段类型 / 中文注释。</summary>
    private const string TableHeaderRows = "28,26,22,24";

    /// <summary>需要居中对齐的列（序号、Loop、Speed、SortOrder、ShadowEnabled、IsCollectible、RespawnTime、操作）。</summary>
    private static readonly HashSet<int> CenteredColumns = new() { 0, 5, 6, 7, 9, 10, 11, 12 };

    /// <summary>单元格格线（浅）。</summary>
    private static readonly IBrush CellBorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xD6));

    /// <summary>表头行之间的分隔线（深），对齐 Excel 配置表的黑框。</summary>
    private static readonly IBrush TableLineBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    /// <summary>分组标题行底色，取自配置表截图。</summary>
    private static readonly IBrush TitleRowBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0xD9, 0xF1));

    /// <summary>注释行底色，取自配置表截图。</summary>
    private static readonly IBrush CommentRowBrush = new SolidColorBrush(Color.FromRgb(0x92, 0xD0, 0x50));

    private static readonly IBrush NameTextBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
    private static readonly IBrush TypeTextBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    /// <summary>表头四行的样式：分组标题 / 字段名 / 字段类型 / 中文注释。</summary>
    private enum TableHeaderStyle
    {
        Title,
        Name,
        Type,
        Comment,
    }

    /// <summary>左侧分类对应的配置表（「全部」返回 null，表示看夹内全部表）。</summary>
    private ItemTableFile? ActiveTableFile() => _itemTable?.FileForCategory(_filter);

    /// <summary>把配置表的行画成「已配置资源列表」（单元格直接改行模型并标记该表待保存）。</summary>
    /// <param name="rows">要显示的行（当前分类那张表的全部行）。</param>
    private void BuildTableRows(IEnumerable<ItemElementRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            var tableName = ActiveTableFile()?.Name ?? "配置表";
            _configuredHost.Children.Add(new TextBlock
            {
                Text = $"「{_filter}」在 {tableName} 里还没有行 —— 左侧选中图片后点「添加到配置表」。",
                Foreground = UiTheme.TextSecondary,
                FontSize = 11,
                Margin = new Thickness(8, 10, 8, 10),
            });
            return;
        }

        var bodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(TableColumns),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("26", list.Count))),
        };

        int row = 0;
        foreach (var item in list)
        {
            AddCell(bodyGrid, ReadOnly(item.Id), 0, row);
            AddCell(bodyGrid, BindRow(item, item.Icon, value => item.Icon = value), 1, row);
            AddCell(bodyGrid, BindRow(item, item.ResName, value => item.ResName = value), 2, row);
            AddCell(bodyGrid, BindRow(item, item.TemplateName, value => item.TemplateName = value), 3, row);
            AddCell(bodyGrid, BindRow(item, item.AnimName, value => item.AnimName = value), 4, row);
            AddCell(bodyGrid, BindRow(item, item.Loop ? "1" : "0", value => item.Loop = ParseBool(value, item.Loop)), 5, row);
            AddCell(bodyGrid, BindRow(item, item.Speed.ToString("0.##", CultureInfo.InvariantCulture), value => item.Speed = ParseNumber(value, item.Speed)), 6, row);
            AddCell(bodyGrid, BindRow(item, item.SortOrder.ToString(CultureInfo.InvariantCulture), value => item.SortOrder = (int)ParseNumber(value, item.SortOrder)), 7, row);
            AddCell(bodyGrid, BindRow(item, item.DialogBubble, value => item.DialogBubble = value), 8, row);
            AddCell(bodyGrid, BindRow(item, item.ShadowEnabled ? "1" : "0", value => item.ShadowEnabled = ParseBool(value, item.ShadowEnabled)), 9, row);
            AddCell(bodyGrid, BindRow(item, item.IsCollectible ? "1" : "0", value => item.IsCollectible = ParseBool(value, item.IsCollectible)), 10, row);
            AddCell(bodyGrid, BindRow(item, item.RespawnTime.ToString("0.##", CultureInfo.InvariantCulture), value => item.RespawnTime = ParseNumber(value, item.RespawnTime)), 11, row);

            var actions = BuildTableRowActions(item);
            var actionsCell = CreateCell(actions, 12, row);
            actionsCell.Padding = new Thickness(3);
            bodyGrid.Children.Add(actionsCell);

            row++;
        }

        _configuredHost.Children.Add(bodyGrid);
    }

    /// <summary>行内「编辑 / 删除」按钮（针对配置表行，不是模板库）。</summary>
    private Control BuildTableRowActions(ItemElementRow item)
    {
        var edit = new Button
        {
            Content = "编辑",
            Padding = new Thickness(3, 0),
            Height = 20,
            MinHeight = 0,
            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF3, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x87, 0xBE)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Foreground = new SolidColorBrush(Color.FromRgb(0x0B, 0x6E, 0x9B)),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        edit.Click += (_, _) => EditTableRow(item);

        var remove = new Button
        {
            Content = "删除",
            Padding = new Thickness(3, 0),
            Height = 20,
            MinHeight = 0,
            Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x50, 0x4D)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x36, 0x2F)),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        remove.Click += (_, _) => RemoveTableRow(item);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { edit, remove },
        };
    }

    /// <summary>表行单元格：写回行模型，并标记所在表待保存。</summary>
    private TextBox BindRow(ItemElementRow item, string text, Action<string> write) =>
        Bind(item, text, value =>
        {
            write(value);
            if (_itemTable?.FileOf(item) is { } file)
            {
                file.Dirty = true;
                _tableDirty = true;
            }

            _itemTable?.RefreshIndexes();
        });

    /// <summary>点表行的「编辑」：把该行套进表单进入修改模式（有同名模板就复用，没有就造一个临时模板）。</summary>
    private void EditTableRow(ItemElementRow item)
    {
        var template = _templates.FirstOrDefault(candidate =>
            NamesEqual(candidate.ImagePath, item.Icon) ||
            NamesEqual(candidate.ResName, item.Icon) ||
            NamesEqual(candidate.TemplateName, item.TemplateName));

        if (template is null)
        {
            template = new ObjectTemplate { Type = TypeOfCategory(CategoryOf(item.Icon)), IsBuiltIn = false };
            item.ApplyToTemplate(template);
            template.ImagePath = item.Icon;
            template.ResName = string.IsNullOrWhiteSpace(item.Icon) ? item.TemplateName : item.Icon;
        }

        SelectTemplateForEdit(template);
    }

    /// <summary>表行「删除」：删掉该表里的这一行（拦被场景实例引用的 ID）。</summary>
    private void RemoveTableRow(ItemElementRow item)
    {
        var used = _config?.Objects.Count(obj => obj.TemplateId == item.Id) ?? 0;
        if (used > 0)
        {
            _notify($"元件 {item.Id} 仍被 {used} 个摆放实例引用，请先删除这些实例");
            return;
        }

        if (_itemTable?.Remove(item.Icon) != true)
        {
            _notify($"没能删除 {item.Icon}：配置表里找不到同名行");
            return;
        }

        _tableDirty = true;
        var template = _templates.FirstOrDefault(candidate =>
            NamesEqual(candidate.ImagePath, item.Icon) || NamesEqual(candidate.ResName, item.Icon));
        if (template is not null)
        {
            _templates.Remove(template);
        }

        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已删除 {item.Id}（{item.Icon}），记得点「保存修改」落盘");
    }

    private static bool NamesEqual(string? left, string? right)
    {
        static string Normalize(string? value) =>
            Path.GetFileNameWithoutExtension(value?.Trim() ?? string.Empty)
                .TrimEnd('_')
                .ToLowerInvariant();

        return Normalize(left).Length > 0 && Normalize(left) == Normalize(right);
    }

    private void RefreshConfiguredTable()
    {
        _configuredHeaderHost.Children.Clear();
        _configuredHost.Children.Clear();

        // 左侧分类按钮 ↔ 配置表：建筑 / 人物 / 物件 / 猫猫 各自只看自己那张表（Unity 同款口径）
        var activeFile = ActiveTableFile();
        var titleText = activeFile is not null
            ? $"已配置资源列表（{activeFile.Name} · {activeFile.Rows.Count} 行）"
            : _itemTable is null
                ? "资源模板配置表"
                : _filter == "全部"
                    ? $"已配置资源列表（全部 {_itemTable.Files.Count} 张表 · {_itemTable.RowCount} 行）"
                    : $"已配置资源列表（「{_filter}」没有对应的表）";

        string[] headers = { "TemplateId", "ImagePath", "ResName", "TemplateName", "AnimName", "Loop", "Speed", "SortOrder", "DialogBubble", "ShadowEnabled", "IsCollectible", "RespawnTime", "操作" };
        string[] types = { "int", "string", "string", "string", "string", "bool", "float", "int", "string", "bool", "bool", "float", "—" };
        string[] comments = { "序号", "图片名称", "资源ResDef名", "建筑名称", "默认动画名", "循环", "播放速度", "渲染层级排序", "对话气泡内容", "是否显示阴影", "是否收集物", "重生时间(秒)", "编辑 / 删除" };

        // 表头四行单独一棵 Grid：挂在不可滚动的容器里，只随横向偏移一起移动
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(TableColumns),
            RowDefinitions = new RowDefinitions(TableHeaderRows),
        };

        // 第一行：分组标题条，横跨全部列
        var titleCell = new Border
        {
            Background = TitleRowBrush,
            BorderBrush = TableLineBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 0),
            Child = new TextBlock
            {
                Text = titleText,
                Foreground = NameTextBrush,
                FontWeight = FontWeight.Bold,
                FontSize = 12,
                LineHeight = 18,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(titleCell, 0);
        Grid.SetColumnSpan(titleCell, headers.Length);
        Grid.SetRow(titleCell, 0);
        headerGrid.Children.Add(titleCell);

        for (int column = 0; column < headers.Length; column++)
        {
            AddHeaderCell(headerGrid, headers[column], column, 1, TableHeaderStyle.Name);
            AddHeaderCell(headerGrid, types[column], column, 2, TableHeaderStyle.Type);
            AddHeaderCell(headerGrid, comments[column], column, 3, TableHeaderStyle.Comment);
        }

        _configuredHeaderHost.Children.Add(headerGrid);

        // 有配置表时，列表直接显示「分类对应那张表」的行（Unity 同款：按钮 ↔ 表）
        var tableRows = _itemTable is null
            ? null
            : activeFile?.Rows ?? (_filter == "全部" ? _itemTable.Rows : null);
        if (tableRows is not null)
        {
            BuildTableRows(tableRows);
            return;
        }

        if (_itemTable is not null)
        {
            _configuredHost.Children.Add(new TextBlock
            {
                Text = $"「{_filter}」在配置表文件夹里没有对应的表（例如猫猫要有 TableCats.xlsm）；左侧列表仍可扫描这类图片。",
                Foreground = UiTheme.TextSecondary,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 10, 8, 10),
            });
            return;
        }

        if (_templates.Count == 0)
        {
            _configuredHost.Children.Add(new TextBlock
            {
                Text = "还没有已配置资源，选中左侧未配置资源后点「添加到配置表」。",
                Foreground = UiTheme.TextSecondary,
                FontSize = 11,
                Margin = new Thickness(8, 10, 8, 10),
            });
            return;
        }

        var bodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(TableColumns),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("26", _templates.Count))),
        };

        int row = 0;
        foreach (var template in _templates)
        {
            AddCell(bodyGrid, ReadOnly(template.TemplateId), 0, row);
            AddCell(bodyGrid, Bind(template, template.ImagePath, value => template.ImagePath = value), 1, row);
            AddCell(bodyGrid, Bind(template, template.ResName, value => template.ResName = value), 2, row);
            AddCell(bodyGrid, Bind(template, template.TemplateName, value => template.TemplateName = value), 3, row);
            AddCell(bodyGrid, Bind(template, template.AnimName, value => template.AnimName = value), 4, row);
            AddCell(bodyGrid, Bind(template, template.Loop ? "1" : "0", value => template.Loop = ParseBool(value, template.Loop)), 5, row);
            AddCell(bodyGrid, Bind(template, template.Speed.ToString("0.##"), value => template.Speed = ParseNumber(value, template.Speed)), 6, row);
            AddCell(bodyGrid, Bind(template, template.SortOrder.ToString(), value => template.SortOrder = (int)ParseNumber(value, template.SortOrder)), 7, row);
            AddCell(bodyGrid, Bind(template, template.DialogBubble, value => template.DialogBubble = value), 8, row);
            AddCell(bodyGrid, Bind(template, template.ShadowEnabled ? "1" : "0", value => template.ShadowEnabled = ParseBool(value, template.ShadowEnabled)), 9, row);
            AddCell(bodyGrid, Bind(template, template.IsCollectible ? "1" : "0", value => template.IsCollectible = ParseBool(value, template.IsCollectible)), 10, row);
            AddCell(bodyGrid, Bind(template, template.RespawnTime.ToString("0.##"), value => template.RespawnTime = ParseNumber(value, template.RespawnTime)), 11, row);

            var edit = new Button
            {
                Content = "编辑",
                Padding = new Thickness(3, 0),
                Height = 20,
                MinHeight = 0,
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF3, 0xFA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x87, 0xBE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0B, 0x6E, 0x9B)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var editTarget = template;
            edit.Click += (_, _) => SelectTemplateForEdit(editTarget);

            var remove = new Button
            {
                Content = "删除",
                Padding = new Thickness(3, 0),
                Height = 20,
                MinHeight = 0,
                Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x50, 0x4D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x36, 0x2F)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var captured = template;
            remove.Click += (_, _) => RemoveTemplate(captured);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { edit, remove },
            };
            var removeCell = CreateCell(actions, 12, row);
            removeCell.Padding = new Thickness(3);
            bodyGrid.Children.Add(removeCell);

            row++;
        }

        _configuredHost.Children.Add(bodyGrid);
    }

    /// <summary>加一格表头，按行样式配底色、字色与分隔线。</summary>
    private void AddHeaderCell(Grid grid, string text, int column, int row, TableHeaderStyle style)
    {
        IBrush background;
        IBrush lineBrush;
        IBrush textBrush;
        double fontSize;
        double lineHeight;
        FontWeight weight;
        switch (style)
        {
            case TableHeaderStyle.Name:
                background = Brushes.White;
                lineBrush = CellBorderBrush;
                textBrush = NameTextBrush;
                fontSize = 11;
                lineHeight = 16;
                weight = FontWeight.SemiBold;
                break;
            case TableHeaderStyle.Type:
                background = Brushes.White;
                lineBrush = CellBorderBrush;
                textBrush = TypeTextBrush;
                fontSize = 10;
                lineHeight = 14;
                weight = FontWeight.Normal;
                break;
            default:
                background = CommentRowBrush;
                lineBrush = TableLineBrush;
                textBrush = NameTextBrush;
                fontSize = 10.5;
                lineHeight = 15;
                weight = FontWeight.Normal;
                break;
        }

        var centered = CenteredColumns.Contains(column);

        var cell = new Border
        {
            Background = background,
            BorderBrush = lineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(3, 0),
            Child = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontWeight = weight,
                FontSize = fontSize,
                LineHeight = lineHeight,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };

        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        grid.Children.Add(cell);
    }

    /// <summary>加一格数据：白底 + 右侧/底部 1px 格线，内部编辑器去掉自身边框以贴合表格观感。</summary>
    private void AddCell(Grid grid, Control control, int column, int row)
    {
        grid.Children.Add(CreateCell(control, column, row));
    }

    private Border CreateCell(Control control, int column, int row)
    {
        // 数字 / 布尔 / 序号 / 操作列的内容在列宽里居中，避免 1、0 贴在左边线上
        if (control is TextBox box)
        {
            box.TextAlignment = CenteredColumns.Contains(column) ? TextAlignment.Center : TextAlignment.Left;
        }

        var cell = new Border
        {
            Background = Brushes.White,
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(3, 0),
            Child = control,
        };
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        return cell;
    }

    /// <summary>只读文本格：灰字、透明底，不显示可编辑暗示。</summary>
    private static TextBox ReadOnly(string text) => new()
    {
        Text = text,
        IsReadOnly = true,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        MinHeight = 0,
        Height = 22,
        Padding = new Thickness(2, 0),
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private TextBox Bind(object owner, string text, Action<string> write)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = 11,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinHeight = 0,
            Height = 22,
            Padding = new Thickness(2, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        void Commit()
        {
            write(box.Text?.Trim() ?? string.Empty);
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };

        return box;
    }

    private void RemoveTemplate(ObjectTemplate template)
    {
        var used = _config?.Objects.Count(item => item.TemplateId == template.TemplateId) ?? 0;
        if (used > 0)
        {
            _notify($"元件 {template.TemplateId} 仍被 {used} 个摆放实例引用，请先删除这些实例");
            return;
        }

        _templates.Remove(template);
        if (_editing == template)
        {
            _editing = null;
            if (_addButton is not null)
            {
                _addButton.Content = "添加到配置表";
            }
        }

        // 同步删除配置表里的同名行
        var removedByPath = _itemTable?.Remove(template.ImagePath) ?? false;
        var removedByName = _itemTable?.Remove(template.ResName) ?? false;
        if (removedByPath || removedByName)
        {
            _tableDirty = true;
        }

        RefreshConfiguredTable();
        ScanUnconfigured(silent: true);
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify($"已从配置表移除 {template.TemplateId}");
    }

    #endregion
}
