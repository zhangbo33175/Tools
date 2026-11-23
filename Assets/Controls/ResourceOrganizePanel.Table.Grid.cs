/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Table.Grid.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表：四行表头、单元格工厂与整体网格刷新
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

}
