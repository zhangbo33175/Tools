/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Table.Rows.cs
 * author:    云毅
 * created:   2026
 * descrip:   已配置资源表：行渲染、行内编辑、删除与撤销
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
            Background = UiTheme.ResourceTable.EditSurface,
            BorderBrush = UiTheme.ResourceTable.EditBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Foreground = UiTheme.ResourceTable.EditText,
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
            Background = UiTheme.ResourceTable.DeleteSurface,
            BorderBrush = UiTheme.ResourceTable.DeleteBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Foreground = UiTheme.ResourceTable.DeleteText,
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
}
