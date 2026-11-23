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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class ItemElementPanel : UserControl
{
    #region 列表

    private void SwitchTab(MapObjectType type)
    {
        _tabType = type;
        RefreshList();
    }

    private void RefreshList()
    {
        _listHost.Children.Clear();

        foreach (var (type, button) in _tabButtons)
        {
            var active = type == _tabType;
            button.Background = active ? UiTheme.Accent : IdleTabBrush;
            button.Foreground = active ? UiTheme.White : UiTheme.TextPrimary;
        }

        // 有对应配置表时，列表直接按表行渲染（Unity 同款：分类按钮 = 一张表）；没有表才退回 JSON 模板
        var table = ActiveTableFile();
        var items = table is null
            ? _templates.Where(template => template.Type == _tabType).ToList()
            : table.Rows.Select(TemplateOf).ToList();

        if (items.Count == 0)
        {
            _listHost.Children.Add(new TextBlock
            {
                Text = table is null
                    ? "（该分类下暂无元件，点击「添加元件」新建）"
                    : $"（{table.Name} 暂无数据行）",
                Foreground = UiTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6),
            });
        }

        foreach (var template in items)
        {
            _listHost.Children.Add(BuildListRow(template, ReferenceEquals(template, _selected)));
        }

        // 首次进入或切换分类后列表为空选中时，自动选中首行，右侧表单始终有内容可编辑
        if (_selected is null || items.All(template => !ReferenceEquals(template, _selected)))
        {
            if (items.Count > 0)
            {
                SelectTemplate(items[0]);
            }
            else
            {
                _selected = null;
                LoadForm();
            }
        }
    }

    private Control BuildListRow(ObjectTemplate template, bool isSelected)
    {
        var icon = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.Parse(MapLayerKeys.Hex(template.LayerKey))),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 表行的图片名就是配置里的 Icon；JSON 模板则优先用 ResName
        var imagePath = _assets.FindImage(
            !string.IsNullOrWhiteSpace(template.ImagePath) ? Path.GetFileNameWithoutExtension(template.ImagePath) : template.ResName);
        imagePath ??= _assets.FindImage(template.ResName);
        if (imagePath is not null && TryLoadBitmap(imagePath) is { } bitmap)
        {
            icon.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }

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
            FontSize = 11,
        };

        var textArea = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0),
            Spacing = 1,
        };
        textArea.Children.Add(name);
        textArea.Children.Add(desc);

        // 行尾「显示」勾选：取消勾选即把该模板的摆放实例从画布隐藏（对应 Unity 列表行的 Toggle）
        var show = new CheckBox
        {
            IsChecked = !_hiddenTemplateIds.Contains(template.TemplateId),
            MinWidth = 0,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var captured = template;
        show.IsCheckedChanged += (_, _) =>
        {
            var visible = show.IsChecked == true;
            if (visible)
            {
                _hiddenTemplateIds.Remove(captured.TemplateId);
            }
            else
            {
                _hiddenTemplateIds.Add(captured.TemplateId);
            }

            TemplateVisibilityChanged?.Invoke(this, new TemplateVisibilityEventArgs(captured.TemplateId, visible));
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("52,*,24"),
        };
        grid.Children.Add(icon);
        Grid.SetColumn(textArea, 1);
        grid.Children.Add(textArea);
        Grid.SetColumn(show, 2);
        grid.Children.Add(show);

        // 与左侧章节/物件列表同款「卡片行」：52px 缩略图 + 上下 5px 留白 = 行高 62
        var row = new ListRowCard(62, isSelected) { Child = grid };

        row.PointerPressed += (_, _) => SelectTemplate(template);
        return row;
    }

    private static Bitmap? TryLoadBitmap(string path)
    {
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private void SelectTemplate(ObjectTemplate template)
    {
        _selected = template;
        RefreshList();
        LoadForm();
    }

    #endregion
}
