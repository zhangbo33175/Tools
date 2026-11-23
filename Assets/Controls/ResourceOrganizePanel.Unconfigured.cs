/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Unconfigured.cs
 * author:    云毅
 * created:   2026
 * descrip:   未配置资源分区：扫描、过滤、预览、补字段与加入配置表。
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
    #region 未配置资源

    private void ApplyFilter(string filter)
    {
        _filter = filter;
        foreach (var (name, button) in _filterButtons)
        {
            var active = name == filter;
            button.Background = active ? UiTheme.Accent : new SolidColorBrush(Color.FromRgb(0xE3, 0xE3, 0xE3));
            button.Foreground = active ? UiTheme.White : UiTheme.TextPrimary;
        }

        RefreshUnconfiguredList();
        if (_itemTable is not null)
        {
            // 分类按钮切过去，右下「已配置资源列表」跟着换成那张表
            RefreshConfiguredTable();
        }
    }

    private void ScanUnconfigured(bool silent)
    {
        var configured = new List<string>();
        foreach (var template in _templates)
        {
            if (template.ResName.Length > 0)
            {
                configured.Add(Path.GetFileNameWithoutExtension(template.ResName));
            }

            if (template.ImagePath.Length > 0)
            {
                configured.Add(Path.GetFileNameWithoutExtension(template.ImagePath));
            }
        }

        var scanRoot = _mapDirectory;
        _unconfigured = _assets
            .ScanUnconfigured(configured, scanRoot)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        ApplyFilter(_filter);
        if (!silent)
        {
            var where = string.IsNullOrWhiteSpace(scanRoot)
                ? string.Join("、", _assets.Roots)
                : scanRoot;
            _notify(_unconfigured.Count == 0
                ? $"扫描完成：{where} 下没有未配置的 PNG"
                : $"扫描完成：{where} 下发现 {_unconfigured.Count} 个未配置资源");
        }
    }

    private void RefreshUnconfiguredList()
    {
        _unconfiguredHost.Children.Clear();

        var keyword = _search.Text?.Trim() ?? string.Empty;
        var items = _unconfigured
            .Where(pair => _filter == "全部" || CategoryDisplay(CategoryOf(pair.Key)) == _filter)
            .Where(pair => keyword.Length == 0
                || pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bound = _unconfigured.Count(pair => _itemTable?.Find(pair.Key) is not null);
        var tableInfo = _itemTable is null
            ? string.Empty
            : $" · 表 {_itemTable.Files.Count} 张 {_itemTable.RowCount} 行";
        _countText.Text = keyword.Length == 0
            ? $"共 {_unconfigured.Count} 个未配置资源" + (bound > 0 ? $"（{bound} 个已绑定物件配置表{tableInfo}）" : string.Empty)
            : $"共 {_unconfigured.Count} 个未配置资源，当前筛选 {items.Count} 个";

        if (items.Count == 0)
        {
            _unconfiguredHost.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_mapDirectory)
                    ? "（暂无未配置资源；先在地图配置里设置「地图保存目录」，再点「扫描」）"
                    : $"（{_mapDirectory} 下暂无未配置资源，点「扫描」重新读取）",
                Foreground = UiTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4),
            });
            return;
        }

        foreach (var (name, path) in items)
        {
            _unconfiguredHost.Children.Add(BuildUnconfiguredRow(name, path));
        }
    }

    private Control BuildUnconfiguredRow(string name, string path)
    {
        var thumb = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            ClipToBounds = true,
        };

        if (TryLoadThumbnail(path) is { } bitmap)
        {
            thumb.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }

        var title = new TextBlock { Text = name, Foreground = UiTheme.TextPrimary };
        var folder = Path.GetDirectoryName(path)?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var csvRow = _itemTable?.Find(name);
        var desc = new TextBlock
        {
            Text = $"{LayerNameOf(path)} · {folder}" + (csvRow is null ? string.Empty : $" · 配置表 #{csvRow.Id}"),
            Foreground = UiTheme.TextSecondary,
            FontSize = 11,
        };

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Margin = new Thickness(8, 0, 0, 0),
            Children = { title, desc },
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal, Children = { thumb, text } };

        // 与左侧章节/物件列表同款「卡片行」：36px 缩略图 + 上下 5px 留白 = 行高 46
        var row = new ListRowCard(46, name == _selectedName) { Child = content };

        row.PointerPressed += (_, _) => SelectUnconfigured(name, path);
        return row;
    }

    private void SelectUnconfigured(string name, string path)
    {
        _selectedName = name;
        _editing = null;
        if (_addButton is not null)
        {
            _addButton.Content = "添加到配置表";
        }

        RefreshUnconfiguredList();
        ShowPreview(path);

        var csvRow = _itemTable?.Find(name);
        if (csvRow is null)
        {
            var category = CategoryOf(name);
            var type = TypeOfCategory(category);
            _fieldId.Text = TemplateStore.NextTemplateId(_templates, type);
            _fieldName.Text = name;
            _fieldAnim.Text = MapToolDefaults.DefaultAnimName;
            _fieldLoop.Text = "1";
            _fieldSpeed.Text = "1";
            _fieldOrder.Text = LayerOrder.Base(type == MapObjectType.Building ? MapLayerKeys.Building : type == MapObjectType.Character ? MapLayerKeys.Character : MapLayerKeys.Prop).ToString();
            _fieldShadow.Text = "1";
            _fieldCollectible.Text = "0";
            _csvInfoText.Text = "配置表未收录该图片名，按默认值新建（保存后写入配置表）。";
            _csvInfoText.Foreground = UiTheme.TextSecondary;
        }
        else
        {
            FillFieldsFromRow(csvRow);
            var source = _itemTable?.FileOf(csvRow)?.Name ?? "配置表";
            _csvInfoText.Text = $"已绑定 {source} 第 {csvRow.RowNumber} 行：{csvRow.Describe()}";
            _csvInfoText.Foreground = UiTheme.AccentDark;
        }

        _fieldImagePath.Text = path;
    }

    /// <summary>把配置表行回填到选中项表单（ID / 名称 / 动画 / 层级 / 阴影 / 收集 / 再生）。</summary>
    /// <param name="row">配置表行。</param>
    private void FillFieldsFromRow(ItemElementRow row)
    {
        _fieldId.Text = row.Id;
        _fieldName.Text = string.IsNullOrWhiteSpace(row.TemplateName) ? row.Icon : row.TemplateName;
        _fieldAnim.Text = string.IsNullOrWhiteSpace(row.AnimName) ? MapToolDefaults.DefaultAnimName : row.AnimName;
        _fieldLoop.Text = row.Loop ? "1" : "0";
        _fieldSpeed.Text = row.Speed.ToString("0.###", CultureInfo.InvariantCulture);
        _fieldOrder.Text = row.SortOrder.ToString(CultureInfo.InvariantCulture);
        _fieldShadow.Text = row.ShadowEnabled ? "1" : "0";
        _fieldCollectible.Text = row.IsCollectible ? "1" : "0";
    }

    /// <summary>
    /// 预览选中的未配置资源。宿主是 <see cref="Grid"/> 而不是 StackPanel —— StackPanel 会给子元素无限高度，
    /// 图片按原始尺寸测量后超出 170px 框，被 <c>ClipToBounds</c> 裁成一个局部；
    /// Grid 给的是有限高度，<c>Stretch=Uniform</c> 才能把整张贴图等比缩进框内。
    /// </summary>
    private void ShowPreview(string path)
    {
        _previewHost.Children.Clear();
        if (TryLoadBitmap(path) is { } bitmap)
        {
            _previewHost.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            });
            return;
        }

        _previewHost.Children.Add(new TextBlock
        {
            Text = "（无预览）",
            Foreground = UiTheme.TextSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }

    /// <summary>缩略图缓存：按「路径 + 目标宽度」缓存缩放后的位图，避免整页加载原图。</summary>
    private static readonly Dictionary<string, Bitmap> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>小尺寸缩略图（列表行用），解码时直接缩小。</summary>
    private static Bitmap? TryLoadThumbnail(string path)
    {
        const int width = 36;
        var key = path + "|" + width;
        if (ThumbnailCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            var bitmap = Bitmap.DecodeToWidth(stream, width);
            if (ThumbnailCache.Count > 400)
            {
                ThumbnailCache.Clear();
            }

            ThumbnailCache[key] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
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

    #endregion
    #region 添加到配置表

    private void AddToConfigTable()
    {
        if (_selectedName is null)
        {
            _notify("请先在左侧选中一个未配置资源，或在下方列表点「编辑」");
            return;
        }

        var name = _fieldName.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            _notify("Name 不能为空");
            return;
        }

        var type = _editing?.Type ?? TypeOfCategory(CategoryOf(_selectedName));
        var isUpdate = _editing is not null;
        var target = _editing ?? new ObjectTemplate
        {
            Type = type,
            IsBuiltIn = false,
            TemplateId = TemplateStore.NextTemplateId(_templates, type),
            ResName = name,
            AnimName = MapToolDefaults.DefaultAnimName,
            Interactable = true,
            InteractType = type == MapObjectType.Prop ? TemplateInteractTypes.Click : TemplateInteractTypes.Talk,
            CollisionEnabled = type != MapObjectType.Prop,
        };

        if (!isUpdate && _templates.Any(template =>
                template.ResName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                template.TemplateName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _notify($"配置表里已有同名资源：{name}");
            return;
        }

        // 配置表行数据（同名的行）：先套未在表单里展示的列（预制体 / 碰撞 / 气泡 / 交通 / 传送门…）
        var boundRow = _itemTable?.Find(_selectedName) ?? _itemTable?.Find(name);
        if (boundRow is not null)
        {
            boundRow.ApplyToTemplate(target);
        }

        // 表单里可见的字段最后写入，用户的手改优先于配置表原值
        var templateId = _fieldId.Text?.Trim() ?? string.Empty;
        target.TemplateId = templateId.Length > 0 && int.TryParse(templateId, out _) ? templateId : target.TemplateId;
        target.TemplateName = name;
        target.ResName = boundRow is not null && !string.IsNullOrWhiteSpace(boundRow.Icon) ? boundRow.Icon : name;
        target.ImagePath = Path.GetFileNameWithoutExtension(_fieldImagePath.Text?.Trim() ?? _selectedName);
        target.AnimName = _fieldAnim.Text?.Trim() is { Length: > 0 } anim ? anim : MapToolDefaults.DefaultAnimName;
        target.Loop = ParseBool(_fieldLoop.Text, true);
        target.Speed = ParseNumber(_fieldSpeed.Text, 1);
        target.SortOrder = (int)ParseNumber(_fieldOrder.Text, target.SortOrder);
        target.ShadowEnabled = ParseBool(_fieldShadow.Text, true);
        target.IsCollectible = ParseBool(_fieldCollectible.Text, false);

        if (!isUpdate)
        {
            _templates.Add(target);
        }

        // 同步配置表：新增或更新同名行（新行落到当前分类那张表，和 Unity 一致）
        _itemTable?.Upsert(target, ActiveTableFile());
        _tableDirty = _itemTable is not null;

        _editing = null;
        if (_addButton is not null)
        {
            _addButton.Content = "添加到配置表";
        }

        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        _notify(isUpdate
            ? $"已修改 {target.TemplateId}（{name}），记得点「保存修改」落盘"
            : $"已添加 {target.TemplateId}（{name}）到配置表，记得点「保存修改」落盘");
    }

    /// <summary>点「编辑」：把已配置模板与配置表行回填到表单，进入修改模式。</summary>
    /// <param name="template">要编辑的模板。</param>
    private void SelectTemplateForEdit(ObjectTemplate template)
    {
        _editing = template;
        _selectedName = template.ImagePath;

        var path = _assets.Resolve(template.ImagePath) ?? template.ImagePath;
        ShowPreview(path);

        _fieldId.Text = template.TemplateId;
        _fieldImagePath.Text = template.ImagePath;
        _fieldName.Text = template.TemplateName;
        _fieldAnim.Text = template.AnimName;
        _fieldLoop.Text = template.Loop ? "1" : "0";
        _fieldSpeed.Text = template.Speed.ToString("0.###", CultureInfo.InvariantCulture);
        _fieldOrder.Text = template.SortOrder.ToString(CultureInfo.InvariantCulture);
        _fieldShadow.Text = template.ShadowEnabled ? "1" : "0";
        _fieldCollectible.Text = template.IsCollectible ? "1" : "0";

        var row = _itemTable?.Find(template.ImagePath) ?? _itemTable?.Find(template.ResName);
        _csvInfoText.Text = row is null
            ? "配置表里没有对应行，保存时会新增一行。"
            : $"已绑定 {_itemTable?.FileOf(row)?.Name ?? "配置表"} 第 {row.RowNumber} 行：{row.Describe()}";
        _csvInfoText.Foreground = row is null ? UiTheme.TextSecondary : UiTheme.AccentDark;

        if (_addButton is not null)
        {
            _addButton.Content = "修改到配置表";
        }

        RefreshUnconfiguredList();
    }

    private static bool ParseBool(string? text, bool fallback)
    {
        var value = text?.Trim() ?? string.Empty;
        return value switch
        {
            "1" or "true" or "True" => true,
            "0" or "false" or "False" => false,
            _ => fallback,
        };
    }

    private static double ParseNumber(string? text, double fallback)
    {
        return double.TryParse(text?.Trim(), out var value) ? value : fallback;
    }

    #endregion
}
