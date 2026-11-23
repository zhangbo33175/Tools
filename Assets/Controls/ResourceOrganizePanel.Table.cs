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

    /// <summary>已配置资源表的列宽（表头与数据行共用，保证冻结表头与行内容不会错列）。</summary>
    private const string TableColumns = "84,92,80,104,88,44,50,76,*,100,88,92,96";

    /// <summary>表头四行的高度：分组标题 / 字段名 / 字段类型 / 中文注释。</summary>
    private const string TableHeaderRows = "28,26,22,24";

    /// <summary>需要居中对齐的列（序号、Loop、Speed、SortOrder、ShadowEnabled、IsCollectible、RespawnTime、操作）。</summary>
    private static readonly HashSet<int> CenteredColumns = new() { 0, 5, 6, 7, 9, 10, 11, 12 };

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
