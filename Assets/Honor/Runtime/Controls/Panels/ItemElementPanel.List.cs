/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.List.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件面板列表：分页切换、行构建、缩略图加载与选中同步
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 列表

    /// <summary>切换元件分类页签并立即重建列表。</summary>
    /// <param name="type">目标分类。</param>
    private void SwitchTab(MapObjectType type)
    {
        _tabType = type;
        RefreshList();
    }

    /// <summary>
    /// 重建元件列表：同步页签状态、取当前分类数据源、生成行、兜底选中首行。
    /// 有配置表时按表行渲染，没有表时退回 JSON 模板。
    /// </summary>
    private void RefreshList()
    {
        _listHost.Children.Clear();
        UpdateTabAppearance();

        var table = ActiveTableFile();
        var items = table is null
            ? _templates.Where(template => template.Type == _tabType).ToList()
            : table.Rows.Select(TemplateOf).ToList();

        if (items.Count == 0)
        {
            _listHost.Children.Add(CreateEmptyListHint(table));
        }

        foreach (var template in items)
        {
            _listHost.Children.Add(BuildListRow(template, ReferenceEquals(template, _selected)));
        }

        EnsureSelection(items);
    }

    /// <summary>按当前页签刷新四个分类按钮的底色与文字色。</summary>
    private void UpdateTabAppearance()
    {
        foreach (var (type, button) in _tabButtons)
        {
            var active = type == _tabType;
            button.Background = active ? UiTheme.Accent : IdleTabBrush;
            button.Foreground = active ? UiTheme.White : UiTheme.TextPrimary;
        }
    }

    /// <summary>构造空列表提示；有无配置表给出不同的引导文案。</summary>
    /// <param name="table">当前分类对应的配置表，null 表示 JSON 模板模式。</param>
    /// <returns>空状态提示控件。</returns>
    private static Control CreateEmptyListHint(ItemTableFile? table)
    {
        return new TextBlock
        {
            Text = table is null
                ? "（该分类下暂无元件，点击「添加元件」新建）"
                : $"（{table.Name} 暂无数据行）",
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = EmptyListHintMargin,
        };
    }

    /// <summary>保证右侧表单始终有编辑对象：当前选中仍存在则保留，否则选首行或清空。</summary>
    /// <param name="items">当前分类可见的全部模板。</param>
    private void EnsureSelection(IReadOnlyList<ObjectTemplate> items)
    {
        if (_selected is not null && items.Any(template => ReferenceEquals(template, _selected)))
        {
            return;
        }

        if (items.Count > 0)
        {
            SelectTemplate(items[0]);
            return;
        }

        _selected = null;
        LoadForm();
    }

    /// <summary>构建单个元件行：缩略图、名称描述、显示勾选三段组合。</summary>
    /// <param name="template">行对应的模板。</param>
    /// <param name="isSelected">是否为当前选中行。</param>
    /// <returns>列表行控件。</returns>
    private Control BuildListRow(ObjectTemplate template, bool isSelected)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{RowThumbnailSize},*,{RowToggleWidth}"),
        };

        var icon = BuildRowThumbnail(template);
        grid.Children.Add(icon);

        var textArea = BuildRowText(template);
        Grid.SetColumn(textArea, 1);
        grid.Children.Add(textArea);

        var show = BuildVisibilityToggle(template);
        Grid.SetColumn(show, 2);
        grid.Children.Add(show);

        // 与左侧章节/物件列表同款「卡片行」：缩略图边长 + 上下留白构成固定行高
        var row = new ListRowCard(RowHeight, isSelected) { Child = grid };
        row.PointerPressed += (_, _) => SelectTemplate(template);
        return row;
    }

    /// <summary>构建行首缩略图；图层底色与图片均缺失时保留纯色占位。</summary>
    /// <param name="template">行对应的模板。</param>
    /// <returns>缩略图边框。</returns>
    private Control BuildRowThumbnail(ObjectTemplate template)
    {
        var icon = new Border
        {
            Width = RowThumbnailSize,
            Height = RowThumbnailSize,
            CornerRadius = new CornerRadius(RowThumbnailCornerRadius),
            Background = MapLayerPalette.BrushOf(template.LayerKey),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var imagePath = ResolveTemplateImagePath(template);
        if (imagePath is not null && PreviewImageLoader.TryLoad(imagePath) is { } bitmap)
        {
            icon.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }

        return icon;
    }

    /// <summary>构建行中部的名称与「ID · 类型 · 交互」描述。</summary>
    /// <param name="template">行对应的模板。</param>
    /// <returns>纵向文本区。</returns>
    private static Control BuildRowText(ObjectTemplate template)
    {
        var name = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(template.TemplateName) ? template.TemplateId : template.TemplateName,
            Foreground = UiTheme.TextPrimary,
            FontWeight = FontWeight.SemiBold,
        };

        var desc = new TextBlock
        {
            Text = $"{template.TemplateId} · {ObjectTypeNames.Of(template.Type)} · {TemplateInteractTypes.ToDisplay(template.InteractType)}",
            Foreground = UiTheme.TextSecondary,
            FontSize = RowDescFontSize,
        };

        var textArea = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = RowTextMargin,
            Spacing = RowTextSpacing,
        };
        textArea.Children.Add(name);
        textArea.Children.Add(desc);
        return textArea;
    }

    /// <summary>
    /// 构建行尾「显示」勾选：取消勾选即把该模板的摆放实例从画布隐藏（对应 Unity 列表行的 Toggle）。
    /// </summary>
    /// <param name="template">行对应的模板。</param>
    /// <returns>可见性勾选框。</returns>
    private CheckBox BuildVisibilityToggle(ObjectTemplate template)
    {
        var show = new CheckBox
        {
            IsChecked = IsTemplateVisible(template.TemplateId),
            MinWidth = 0,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        show.IsCheckedChanged += (_, _) => SetTemplateVisible(template.TemplateId, show.IsChecked == true);
        return show;
    }

    /// <summary>模板当前是否参与画布显示。</summary>
    /// <param name="templateId">模板 Id。</param>
    /// <returns>true 表示显示。</returns>
    private bool IsTemplateVisible(string templateId) => !_hiddenTemplateIds.Contains(templateId);

    /// <summary>统一维护模板可见性与事件通知，避免列表行内直接操作隐藏集合。</summary>
    /// <param name="templateId">模板 Id。</param>
    /// <param name="visible">true 显示，false 隐藏。</param>
    private void SetTemplateVisible(string templateId, bool visible)
    {
        if (visible)
        {
            _hiddenTemplateIds.Remove(templateId);
        }
        else
        {
            _hiddenTemplateIds.Add(templateId);
        }

        TemplateVisibilityChanged?.Invoke(this, new TemplateVisibilityEventArgs(templateId, visible));
    }

    /// <summary>选中模板并刷新列表高亮与右侧表单。</summary>
    /// <param name="template">目标模板。</param>
    private void SelectTemplate(ObjectTemplate template)
    {
        _selected = template;
        RefreshList();
        LoadForm();
    }

    #endregion
}