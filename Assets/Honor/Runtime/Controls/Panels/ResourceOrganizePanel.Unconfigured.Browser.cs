/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.Unconfigured.Browser.cs
 * author:    云毅
 * created:   2026
 * descrip:   未配置资源浏览：扫描、筛选、缩略图预览与多选。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // ---TODO 重构：扫描 / 筛选 / 预览 / 多选独立成浏览 partial，写入配置表留在 Unconfigured。
    #region 未配置资源

    private void ApplyFilter(string filter)
    {
        _filter = filter;
        ResetSelection();
        // ---TODO 重构：筛选按钮底色统一走主题语义色，禁止再写死 RGB。
        foreach (var (name, button) in _filterButtons)
        {
            var active = name == filter;
            button.Background = active ? UiTheme.Accent : UiTheme.Form.FilterIdle;
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

    /// <summary>当前分类 + 搜索词过滤后的未配置资源；Shift 连选也按这同一份顺序取区间。</summary>
    private List<(string Name, string Path)> FilteredUnconfiguredItems()
    {
        var keyword = _search.Text?.Trim() ?? string.Empty;
        return _unconfigured
            .Where(pair => ResourceCategories.IsAll(_filter) || CategoryDisplay(ResourceCategories.Classify(pair.Value)) == _filter)
            .Where(pair => keyword.Length == 0
                || pair.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => (Name: pair.Key, Path: pair.Value))
            .ToList();
    }

    private void RefreshUnconfiguredList()
    {
        _unconfiguredHost.Children.Clear();

        var keyword = _search.Text?.Trim() ?? string.Empty;
        var items = FilteredUnconfiguredItems();

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
            Width = ThumbnailPixelSize,
            Height = ThumbnailPixelSize,
            CornerRadius = ThumbnailCornerRadius,
            Background = UiTheme.LeftPanel.ThumbnailFill,
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

        // 与左侧章节/物件列表同款「卡片行」：缩略图 + 上下 5px 留白
        var row = new ListRowCard(UnconfiguredRowHeight, IsPathSelected(path)) { Child = content };

        row.PointerPressed += (_, e) => SelectUnconfigured(name, path, e.KeyModifiers);
        return row;
    }

    /// <summary>是否为当前多选集合中的资源。</summary>
    private bool IsPathSelected(string path)
    {
        return _selection.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 处理列表点击：普通点击单选、Ctrl 点击追加/取消、Shift 点击按当前筛选顺序连选。
    /// 多选只影响批量添加集合；右侧表单始终回填最后点击的主项。
    /// </summary>
    /// <param name="name">资源名（不含扩展名）。</param>
    /// <param name="path">资源磁盘路径。</param>
    /// <param name="modifiers">点击时按下的修饰键。</param>
    private void SelectUnconfigured(string name, string path, KeyModifiers modifiers)
    {
        var additive = modifiers.HasFlag(KeyModifiers.Control);
        var range = modifiers.HasFlag(KeyModifiers.Shift);
        var removed = false;

        if (range && _selectionAnchorPath is not null)
        {
            var items = FilteredUnconfiguredItems();
            var anchorIndex = items.FindIndex(item => string.Equals(item.Path, _selectionAnchorPath, StringComparison.OrdinalIgnoreCase));
            var targetIndex = items.FindIndex(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            if (anchorIndex >= 0 && targetIndex >= 0)
            {
                _selection.Clear();
                var start = Math.Min(anchorIndex, targetIndex);
                var end = Math.Max(anchorIndex, targetIndex);
                for (var index = start; index <= end; index++)
                {
                    _selection.Add(items[index]);
                }
            }
            else
            {
                _selection.Clear();
                _selection.Add((name, path));
                _selectionAnchorPath = path;
            }
        }
        else if (additive)
        {
            var index = _selection.FindIndex(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                removed = string.Equals(_selectedPath, path, StringComparison.OrdinalIgnoreCase);
                _selection.RemoveAt(index);
            }
            else
            {
                _selection.Add((name, path));
            }

            _selectionAnchorPath = path;
        }
        else
        {
            _selection.Clear();
            _selection.Add((name, path));
            _selectionAnchorPath = path;
        }

        ExitEditMode();

        if (_selection.Count == 0)
        {
            ClearPrimarySelection();
        }
        else if (removed)
        {
            // 主项被 Ctrl 取消时退到集合里最后一项，避免表单继续指向未选资源
            var primary = _selection[^1];
            SetPrimarySelection(primary.Name, primary.Path);
        }
        else
        {
            SetPrimarySelection(name, path);
        }

        RefreshUnconfiguredList();
        RefreshSelectedItemDetails();
    }

    /// <summary>切换主项并同步预览与字段。</summary>
    private void SetPrimarySelection(string name, string path)
    {
        _selectedName = name;
        _selectedPath = path;
    }

    /// <summary>清空整个多选集合与右侧表单。</summary>
    private void ResetSelection()
    {
        _selection.Clear();
        _selectionAnchorPath = null;
        _selectedName = null;
        _selectedPath = null;
        ExitEditMode();
        ClearPrimarySelection();
    }

    /// <summary>只清主项与字段，多选集合保留（Ctrl 取消主项后仍可批量添加其它项）。</summary>
    private void ClearPrimarySelection()
    {
        _selectedName = null;
        _selectedPath = null;
        _previewHost.Children.Clear();
        foreach (var box in new[]
                 {
                     _fieldId, _fieldImagePath, _fieldName, _fieldAnim, _fieldLoop,
                     _fieldSpeed, _fieldOrder, _fieldShadow, _fieldCollectible,
                 })
        {
            box.Text = string.Empty;
        }

        _csvInfoText.Text = string.Empty;
    }

    /// <summary>按当前主项回填预览、建议字段与配置表绑定信息。</summary>
    private void RefreshSelectedItemDetails()
    {
        if (_selectedPath is null)
        {
            return;
        }

        ShowPreview(_selectedPath);

        var name = _selectedName ?? Path.GetFileNameWithoutExtension(_selectedPath);
        var csvRow = _itemTable?.Find(name);
        if (csvRow is null)
        {
            var category = ResourceCategories.Classify(_selectedPath);
            var type = ResourceCategories.ObjectTypeOfKey(category);
            var isCollectible = category == ResourceCategories.CollectibleKey;
            var draft = new ObjectTemplate
            {
                Type = type,
                // ---TODO ID 管理：分配统一走协调器，配置表与模板共用同一来源。
                TemplateId = _idCoordinator.Allocate(type),
                TemplateName = name,
                ImagePath = name,
                IsCollectible = isCollectible,
            };

            FillFields(
                draft.TemplateId,
                _selectedPath,
                name,
                draft.AnimName,
                draft.Loop,
                draft.Speed,
                LayerOrder.Base(TemplateDefaults.LayerKey(type)),
                draft.ShadowEnabled,
                draft.IsCollectible);

            _csvInfoText.Text = isCollectible
                && _itemTable?.FileForCategory(ResourceCategories.CollectibleCategory.DisplayName) is null
                    ? "配置表未收录该图片名；首次添加会自动创建 TableCollectibles.csv。"
                    : "配置表未收录该图片名，按默认值新建（保存后写入配置表）。";
            _csvInfoText.Foreground = UiTheme.TextSecondary;
        }
        else
        {
            FillFieldsFromRow(csvRow, _selectedPath);
            _csvInfoText.Text = csvRow.DescribeBinding(_itemTable?.FileOf(csvRow)?.Name);
            _csvInfoText.Foreground = UiTheme.AccentDark;
        }
    }

    /// <summary>把配置表行回填到选中项表单。</summary>
    /// <param name="row">配置表行。</param>
    /// <param name="imagePath">图片在磁盘上的完整路径。</param>
    private void FillFieldsFromRow(ItemElementRow row, string imagePath)
    {
        FillFields(
            row.Id,
            imagePath,
            string.IsNullOrWhiteSpace(row.TemplateName) ? row.Icon : row.TemplateName,
            row.AnimName,
            row.Loop,
            row.Speed,
            row.SortOrder,
            row.ShadowEnabled,
            row.IsCollectible);
    }

    /// <summary>
    /// 选中项表单的唯一回填入口：ID、图片路径、名称、动画、循环、速度、层级、阴影、收集。
    /// 新建默认值、配置表行与模板编辑都走这里，避免三套字段赋值漂移。
    /// </summary>
    /// <param name="id">模板序号。</param>
    /// <param name="imagePath">图片路径。</param>
    /// <param name="name">模板名称。</param>
    /// <param name="animName">默认动画名；为空时回退全局默认。</param>
    /// <param name="loop">动画是否循环。</param>
    /// <param name="speed">播放速度。</param>
    /// <param name="sortOrder">渲染层级。</param>
    /// <param name="shadow">是否显示阴影。</param>
    /// <param name="collectible">是否收集物。</param>
    private void FillFields(
        string id,
        string imagePath,
        string name,
        string animName,
        bool loop,
        double speed,
        int sortOrder,
        bool shadow,
        bool collectible)
    {
        _fieldId.Text = id;
        _fieldImagePath.Text = imagePath;
        _fieldName.Text = name;
        _fieldAnim.Text = TemplateDefaults.AnimNameOrDefault(animName);
        _fieldLoop.Text = TableTextFormat.Flag(loop);
        _fieldSpeed.Text = TableTextFormat.Number(speed);
        _fieldOrder.Text = TableTextFormat.Integer(sortOrder);
        _fieldShadow.Text = TableTextFormat.Flag(shadow);
        _fieldCollectible.Text = TableTextFormat.Flag(collectible);
    }

    /// <summary>退出修改模式，并把主按钮恢复为新增 / 批量文案。</summary>
    // ---TODO 重构：编辑态退出统一走此入口，避免各处重复清空 _editing 与按钮文案。
    private void ExitEditMode()
    {
        _editing = null;
        SetAddButtonMode(editing: false);
    }

    /// <summary>切换表单底部主按钮文案：新增模式 / 修改模式 / 多选批量模式。</summary>
    /// <param name="editing">是否正在修改已配置模板。</param>
    private void SetAddButtonMode(bool editing)
    {
        if (_addButton is null)
        {
            return;
        }

        _addButton.Content = editing
            ? UpdateTableButtonText
            : _selection.Count > 1
                ? $"添加 {_selection.Count} 项到配置表"
                : AddToTableButtonText;
    }

    /// <summary>
    /// 预览选中的未配置资源。宿主是 <see cref="Grid"/> 而不是 StackPanel —— StackPanel 会给子元素无限高度，
    /// 图片按原始尺寸测量后超出 170px 框，被 <c>ClipToBounds</c> 裁成一个局部；
    /// Grid 给的是有限高度，<c>Stretch=Uniform</c> 才能把整张贴图等比缩进框内。
    /// </summary>
    private void ShowPreview(string path)
    {
        _previewHost.Children.Clear();
        if (PreviewImageLoader.TryLoad(path) is { } bitmap)
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
        var key = path + "|" + ThumbnailPixelSize;
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
            var bitmap = Bitmap.DecodeToWidth(stream, ThumbnailPixelSize);
            if (ThumbnailCache.Count > ThumbnailCacheLimit)
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

    #endregion
}
