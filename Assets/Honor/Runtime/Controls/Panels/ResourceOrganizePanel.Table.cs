/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Table.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表：列定义、当前表解析与模板移除。
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

    /// <summary>已配置资源表的一列：表头、类型、中文注释、列宽与是否居中。</summary>
    /// <param name="Header">字段名。</param>
    /// <param name="TypeName">字段类型。</param>
    /// <param name="Comment">中文注释。</param>
    /// <param name="Width">列宽，支持 <c>*</c>。</param>
    /// <param name="Centered">内容是否居中。</param>
    private readonly record struct ResourceColumn(
        string Header,
        string TypeName,
        string Comment,
        string Width,
        bool Centered);

    /// <summary>
    /// 已配置资源表的唯一列定义：表头、类型行、注释行、列宽与对齐都从这里派生，
    /// 避免列宽字符串、居中索引与三个表头数组各写一份而错列。
    /// </summary>
    private static readonly ResourceColumn[] ResourceColumns =
    {
        new("TemplateId", "int", "序号", "84", true),
        new("ImagePath", "string", "图片名称", "92", false),
        new("ResName", "string", "资源ResDef名", "80", false),
        new("TemplateName", "string", "建筑名称", "104", false),
        new("AnimName", "string", "默认动画名", "88", false),
        new("Loop", "bool", "循环", "44", true),
        new("Speed", "float", "播放速度", "50", true),
        new("SortOrder", "int", "渲染层级排序", "76", true),
        new("DialogBubble", "string", "对话气泡内容", "*", false),
        new("ShadowEnabled", "bool", "是否显示阴影", "100", true),
        new("IsCollectible", "bool", "是否收集物", "88", true),
        new("RespawnTime", "float", "重生时间(秒)", "92", true),
        new("操作", "—", "编辑 / 删除", "96", true),
    };

    /// <summary>已配置资源表的列宽（表头与数据行共用，保证冻结表头与行内容不会错列）。</summary>
    private static string TableColumns => string.Join(",", ResourceColumns.Select(column => column.Width));

    /// <summary>表头四行的高度：分组标题 / 字段名 / 字段类型 / 中文注释。</summary>
    private const string TableHeaderRows = "28,26,22,24";

    /// <summary>数据行高度，表行与模板行共用。</summary>
    private const double TableRowHeight = 26.0;

    /// <summary>表格单元格的统一内边距。</summary>
    private static readonly Thickness TableCellPadding = new(3, 0);

    /// <summary>数据格右侧与底部 1px 格线。</summary>
    private static readonly Thickness TableCellBorderThickness = new(0, 0, 1, 1);

    /// <summary>行内编辑框统一高度。</summary>
    private const double TableEditorHeight = 22.0;

    /// <summary>行内编辑框统一字号。</summary>
    private const double TableEditorFontSize = 11.0;

    /// <summary>行内编辑框内边距。</summary>
    private static readonly Thickness TableEditorPadding = new(2, 0);

    /// <summary>表格空状态提示字号。</summary>
    private const double TableEmptyHintFontSize = 11.0;

    /// <summary>表格空状态提示外边距。</summary>
    private static readonly Thickness TableEmptyHintMargin = new(8, 10, 8, 10);

    /// <summary>表头分组标题字号。</summary>
    private const double TableTitleFontSize = 12.0;

    /// <summary>表头分组标题行高。</summary>
    private const double TableTitleLineHeight = 18.0;

    /// <summary>表头分组标题内边距。</summary>
    private static readonly Thickness TableTitlePadding = new(6, 0);

    /// <summary>表头字段名字号。</summary>
    private const double TableHeaderNameFontSize = 11.0;

    /// <summary>表头字段名行高。</summary>
    private const double TableHeaderNameLineHeight = 16.0;

    /// <summary>表头类型行字号。</summary>
    private const double TableHeaderTypeFontSize = 10.0;

    /// <summary>表头类型行行高。</summary>
    private const double TableHeaderTypeLineHeight = 14.0;

    /// <summary>表头注释行字号。</summary>
    private const double TableHeaderCommentFontSize = 10.5;

    /// <summary>表头注释行行高。</summary>
    private const double TableHeaderCommentLineHeight = 15.0;

    /// <summary>操作列索引，始终为最后一列。</summary>
    private static int ActionColumnIndex => ResourceColumns.Length - 1;

    /// <summary>判断某列是否居中；越界列按左对齐处理。</summary>
    /// <param name="column">列索引。</param>
    /// <returns>true 表示居中。</returns>
    private static bool IsCenteredColumn(int column) =>
        column >= 0 && column < ResourceColumns.Length && ResourceColumns[column].Centered;

    /// <summary>生成数据行定义，统一表行与模板行的高度。</summary>
    /// <param name="count">行数。</param>
    /// <returns>行定义。</returns>
    private static RowDefinitions BuildBodyRowDefinitions(int count) =>
        new(string.Join(",", Enumerable.Repeat(TableRowHeight.ToString(CultureInfo.InvariantCulture), count)));

    /// <summary>行内编辑按钮文字。</summary>
    private const string EditActionText = "编辑";

    /// <summary>行内删除按钮文字。</summary>
    private const string DeleteActionText = "删除";

    /// <summary>操作列的单元格内边距。</summary>
    private static readonly Thickness ActionCellPadding = new(3);

    /// <summary>行内按钮内边距。</summary>
    private static readonly Thickness ActionButtonPadding = new(3, 0);

    /// <summary>行内按钮统一高度。</summary>
    private const double ActionButtonHeight = 20.0;

    /// <summary>行内按钮描边线宽。</summary>
    private const double ActionButtonBorderThickness = 1.0;

    /// <summary>行内按钮圆角。</summary>
    private const double ActionButtonCornerRadius = 2.0;

    /// <summary>行内按钮字号。</summary>
    private const double ActionButtonFontSize = 11.0;

    /// <summary>编辑与删除按钮之间的间距。</summary>
    private const double ActionButtonSpacing = 3.0;

    /// <summary>
    /// 构建行尾「编辑 / 删除」按钮组，表行与模板行共用同一外观与间距。
    /// </summary>
    /// <param name="edit">编辑动作。</param>
    /// <param name="remove">删除动作。</param>
    /// <returns>横向按钮组。</returns>
    private static Control BuildRowActions(Action edit, Action remove)
    {
        var editButton = CreateActionButton(
            EditActionText,
            UiTheme.ResourceTable.EditSurface,
            UiTheme.ResourceTable.EditBorder,
            UiTheme.ResourceTable.EditText,
            HorizontalAlignment.Stretch,
            edit);

        var removeButton = CreateActionButton(
            DeleteActionText,
            UiTheme.ResourceTable.DeleteSurface,
            UiTheme.ResourceTable.DeleteBorder,
            UiTheme.ResourceTable.DeleteText,
            HorizontalAlignment.Center,
            remove);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ActionButtonSpacing,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { editButton, removeButton },
        };
    }

    /// <summary>创建行内操作按钮，颜色全部来自资源配置表主题令牌。</summary>
    /// <param name="text">按钮文字。</param>
    /// <param name="surface">底色。</param>
    /// <param name="border">描边色。</param>
    /// <param name="foreground">文字色。</param>
    /// <param name="alignment">水平对齐方式。</param>
    /// <param name="action">点击动作。</param>
    /// <returns>按钮控件。</returns>
    private static Button CreateActionButton(
        string text,
        IBrush surface,
        IBrush border,
        IBrush foreground,
        HorizontalAlignment alignment,
        Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = ActionButtonPadding,
            Height = ActionButtonHeight,
            MinHeight = 0,
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(ActionButtonBorderThickness),
            CornerRadius = new CornerRadius(ActionButtonCornerRadius),
            Foreground = foreground,
            FontSize = ActionButtonFontSize,
            HorizontalAlignment = alignment,
        };
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>把操作按钮组放入最后一列，并统一操作列内边距。</summary>
    /// <param name="grid">数据行网格。</param>
    /// <param name="actions">按钮组。</param>
    /// <param name="row">行索引。</param>
    private void AddActionCell(Grid grid, Control actions, int row)
    {
        var cell = CreateCell(actions, ActionColumnIndex, row);
        cell.Padding = ActionCellPadding;
        grid.Children.Add(cell);
    }

    /// <summary>单元格格线（浅）。</summary>
    private static readonly IBrush CellBorderBrush = UiTheme.ResourceTable.CellBorder;

    /// <summary>表头行之间的分隔线（深），对齐 Excel 配置表的黑框。</summary>
    private static readonly IBrush TableLineBrush = UiTheme.ResourceTable.Line;

    /// <summary>分组标题行底色，取自配置表截图。</summary>
    private static readonly IBrush TitleRowBrush = UiTheme.ResourceTable.TitleRow;

    /// <summary>注释行底色，取自配置表截图。</summary>
    private static readonly IBrush CommentRowBrush = UiTheme.ResourceTable.CommentRow;

    private static readonly IBrush NameTextBrush = UiTheme.ResourceTable.NameText;
    private static readonly IBrush TypeTextBrush = UiTheme.ResourceTable.TypeText;

    /// <summary>表头单元格的视觉描述：底色、分隔线、文字色、字号与字重。</summary>
    private readonly record struct TableHeaderAppearance(
        IBrush Background,
        IBrush Line,
        IBrush Text,
        double FontSize,
        double LineHeight,
        FontWeight Weight);

    /// <summary>表头三行的样式：字段名 / 字段类型 / 中文注释。</summary>
    private enum TableHeaderStyle
    {
        Name,
        Type,
        Comment,
    }

    /// <summary>左侧分类对应的配置表（「全部」返回 null，表示看夹内全部表）。</summary>
    private ItemTableFile? ActiveTableFile() => _itemTable?.FileForCategory(_filter);

    /// <summary>
    /// 只有「必须独立成表」的分类缺表时，才显示「新建收集物表」。
    /// 目前只有收集物；其他分类保持原有的手动表文件约定。
    /// </summary>
    /// <param name="activeFile">当前分类已命中的表；null 表示缺表。</param>
    private void RefreshCreateTableButton(ItemTableFile? activeFile)
    {
        if (_createTableButton is null)
        {
            return;
        }

        var definition = ResourceCategories.FindByDisplay(_filter);
        _createTableButton.IsVisible = _itemTable is not null
            && definition is { RequiresOwnTable: true }
            && activeFile is null;
    }

    /// <summary>确认模板没有被任何摆放实例引用；被引用时提示并拒绝删除。</summary>
    /// <param name="displayId">模板显示 ID。</param>
    /// <returns>没有实例引用返回 true。</returns>
    // ---TODO 重构：删除模板 / 删除表格行的占用检查统一走此入口。
    private bool EnsureNotReferenced(string displayId)
    {
        var used = _config?.Objects.Count(item => item.TemplateId == displayId) ?? 0;
        if (used <= 0)
        {
            return true;
        }

        _notify($"元件 {displayId} 仍被 {used} 个摆放实例引用，请先删除这些实例");
        return false;
    }

    /// <summary>把配置表的行画成「已配置资源列表」（单元格直接改行模型并标记该表待保存）。</summary>

    private void RemoveTemplate(ObjectTemplate template)
    {
        var row = _itemTable?.FindByTemplate(template);
        var displayId = template.TemplateId.Trim();
        if (row is not null)
        {
            if (!RemoveTableRowCore(row))
            {
                return;
            }
        }
        else if (!EnsureNotReferenced(displayId))
        {
            // 没有表行时仍要拦截被实例引用的模板，避免只删模板留下悬空引用。
            return;
        }

        _templates.Remove(template);
        if (_editing == template)
        {
            ExitEditMode();
        }

        RefreshAfterTableChange();
        _notify($"已从配置表移除 {displayId}，后续 ID 已顺延");
    }

    #endregion
}
