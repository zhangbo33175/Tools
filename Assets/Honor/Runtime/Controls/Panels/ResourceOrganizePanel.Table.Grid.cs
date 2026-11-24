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

        // 左侧分类按钮 ↔ 配置表：建筑 / 人物 / 物件 / 收集物 / 猫猫 各自只看自己那张表（Unity 同款口径）
        var activeFile = ActiveTableFile();
        RefreshCreateTableButton(activeFile);
        var titleText = activeFile is not null
            ? $"已配置资源列表（{activeFile.Name} · {activeFile.Rows.Count} 行）"
            : _itemTable is null
                ? "资源模板配置表"
                : ResourceCategories.IsAll(_filter)
                    ? $"已配置资源列表（全部 {_itemTable.Files.Count} 张表 · {_itemTable.RowCount} 行）"
                    : $"已配置资源列表（「{_filter}」没有对应的表）";

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
            Padding = TableTitlePadding,
            Child = new TextBlock
            {
                Text = titleText,
                Foreground = NameTextBrush,
                FontWeight = FontWeight.Bold,
                FontSize = TableTitleFontSize,
                LineHeight = TableTitleLineHeight,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(titleCell, 0);
        Grid.SetColumnSpan(titleCell, ResourceColumns.Length);
        Grid.SetRow(titleCell, 0);
        headerGrid.Children.Add(titleCell);

        for (int column = 0; column < ResourceColumns.Length; column++)
        {
            var definition = ResourceColumns[column];
            AddHeaderCell(headerGrid, definition.Header, column, 1, TableHeaderStyle.Name);
            AddHeaderCell(headerGrid, definition.TypeName, column, 2, TableHeaderStyle.Type);
            AddHeaderCell(headerGrid, definition.Comment, column, 3, TableHeaderStyle.Comment);
        }

        _configuredHeaderHost.Children.Add(headerGrid);

        // 有配置表时，列表直接显示「分类对应那张表」的行（Unity 同款：按钮 ↔ 表）
        var tableRows = _itemTable is null
            ? null
            : activeFile?.Rows ?? (ResourceCategories.IsAll(_filter) ? _itemTable.Rows : null);
        if (tableRows is not null)
        {
            BuildTableRows(tableRows);
            return;
        }

        if (_itemTable is not null)
        {
            var definition = ResourceCategories.FindByDisplay(_filter);
            var hint = definition is { RequiresOwnTable: true }
                ? $"「{_filter}」还没有独立配置表；选中左侧图片后点「{AddToTableButtonText}」会自动创建 {definition.TableHints[0]}.csv，也可点底部「新建收集物表」。"
                : $"「{_filter}」在配置表文件夹里没有对应的表（例如猫猫要有 TableCats.xlsm）；左侧列表仍可扫描这类图片。";
            AddEmptyMessage(hint, wrap: true);
            return;
        }

        if (_templates.Count == 0)
        {
            AddEmptyMessage($"还没有已配置资源，选中左侧未配置资源后点「{AddToTableButtonText}」。");
            return;
        }

        BuildResourceBodyGrid(
            _templates,
            template => BuildRowActions(
                () => SelectTemplateForEdit(template),
                () => RemoveTemplate(template)),
            afterWrite: null);
    }

    /// <summary>加一格表头，按行样式配底色、字色与分隔线。</summary>
    private void AddHeaderCell(Grid grid, string text, int column, int row, TableHeaderStyle style)
    {
        var appearance = ResolveHeaderAppearance(style);
        var centered = IsCenteredColumn(column);

        var cell = new Border
        {
            Background = appearance.Background,
            BorderBrush = appearance.Line,
            BorderThickness = TableCellBorderThickness,
            Padding = TableCellPadding,
            Child = new TextBlock
            {
                Text = text,
                Foreground = appearance.Text,
                FontWeight = appearance.Weight,
                FontSize = appearance.FontSize,
                LineHeight = appearance.LineHeight,
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

    /// <summary>解析表头样式，集中管理字号、行高、底色与分隔线。</summary>
    /// <param name="style">表头类型。</param>
    private static TableHeaderAppearance ResolveHeaderAppearance(TableHeaderStyle style) =>
        style switch
        {
            TableHeaderStyle.Name => new TableHeaderAppearance(
                UiTheme.White,
                CellBorderBrush,
                NameTextBrush,
                TableHeaderNameFontSize,
                TableHeaderNameLineHeight,
                FontWeight.SemiBold),
            TableHeaderStyle.Type => new TableHeaderAppearance(
                UiTheme.White,
                CellBorderBrush,
                TypeTextBrush,
                TableHeaderTypeFontSize,
                TableHeaderTypeLineHeight,
                FontWeight.Normal),
            _ => new TableHeaderAppearance(
                CommentRowBrush,
                TableLineBrush,
                NameTextBrush,
                TableHeaderCommentFontSize,
                TableHeaderCommentLineHeight,
                FontWeight.Normal),
        };

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
            box.TextAlignment = IsCenteredColumn(column) ? TextAlignment.Center : TextAlignment.Left;
        }

        var cell = new Border
        {
            Background = UiTheme.White,
            BorderBrush = CellBorderBrush,
            BorderThickness = TableCellBorderThickness,
            Padding = TableCellPadding,
            Child = control,
        };
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        return cell;
    }

    /// <summary>在已配置资源区放入统一样式的空状态提示。</summary>
    /// <param name="text">提示文案。</param>
    /// <param name="wrap">是否允许换行。</param>
    private void AddEmptyMessage(string text, bool wrap = false)
    {
        _configuredHost.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = UiTheme.TextSecondary,
            FontSize = TableEmptyHintFontSize,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Margin = TableEmptyHintMargin,
        });
    }

    /// <summary>表内文本格的基础样式：透明底、透明边框、固定行高。</summary>
    /// <param name="text">初始文本。</param>
    /// <returns>尚未绑定编辑行为的文本框。</returns>
    // ---TODO 重构：只读格与可编辑格共用同一份表格文本样式。
    private static TextBox TableEditorBox(string text) => new()
    {
        Text = text,
        FontSize = TableEditorFontSize,
        Background = UiTheme.Transparent,
        BorderThickness = new Thickness(0),
        MinHeight = 0,
        Height = TableEditorHeight,
        Padding = TableEditorPadding,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>只读文本格：灰字、透明底，不显示可编辑暗示。</summary>
    private static TextBox ReadOnly(string text)
    {
        var box = TableEditorBox(text);
        box.IsReadOnly = true;
        box.Foreground = UiTheme.ResourceTable.ReadOnlyText;
        return box;
    }

    /// <summary>表内文本编辑器：失焦或回车提交，边框透明以贴合表格观感。</summary>
    /// <param name="text">初始文本。</param>
    /// <param name="write">提交回写动作。</param>
    private TextBox Bind(string text, Action<string> write)
    {
        var box = TableEditorBox(text);

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
