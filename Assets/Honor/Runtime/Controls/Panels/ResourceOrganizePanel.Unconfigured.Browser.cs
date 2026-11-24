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
        _resourceListScroll?.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 0));
        if (_itemTable is not null)
        {
            // 分类按钮切过去，右下「已配置资源列表」跟着换成那张表
            RefreshConfiguredTable();
        }
    }

    private void ScanUnconfigured(bool silent)
    {
        _mapImageIndex = null;
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

        // ---TODO 资源列表：配置表行即使没有对应模板，也要从「未配置」扫描中排除，避免左侧出现重复卡片。
        if (_itemTable is not null)
        {
            foreach (var row in _itemTable.Rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Icon))
                {
                    configured.Add(Path.GetFileNameWithoutExtension(row.Icon));
                }
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
            var tableRows = _itemTable?.RowCount ?? 0;
            var configuredHint = tableRows > 0 ? $"，配置表 {tableRows} 行已显示在资源列表" : string.Empty;
            _notify(_unconfigured.Count == 0
                ? $"扫描完成：{where} 下没有未配置的 PNG{configuredHint}"
                : $"扫描完成：{where} 下发现 {_unconfigured.Count} 个未配置资源{configuredHint}");
        }
    }

    #region 资源列表条目

    // ---TODO 资源列表：未配置图片与配置表行统一成同一套卡片，副标题按「分类 · 表 · 配置表 #ID」显示。
    /// <summary>左侧统一资源条目：未配置图片与配置表行共用一张卡片。</summary>
    /// <param name="Name">资源名（不含扩展名）。</param>
    /// <param name="Path">磁盘 PNG 路径；配置表行没有图片时为空。</param>
    /// <param name="Row">命中的配置表行；未配置资源为空。</param>
    /// <param name="Table">配置表行所在表；未配置资源为空。</param>
    private readonly record struct ResourceListEntry(
        string Name,
        string? Path,
        ItemElementRow? Row,
        ItemTableFile? Table);

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

    /// <summary>
    /// 左侧资源列表的完整条目：未配置图片 + 当前分类的配置表行。
    /// 配置表行即使没有磁盘图片也保留，缩略图缺失时显示占位块。
    /// </summary>
    private List<ResourceListEntry> FilteredResourceEntries()
    {
        var entries = new List<ResourceListEntry>();
        foreach (var pair in _unconfigured)
        {
            if (MatchesResourceFilter(pair.Key, pair.Value, null, null))
            {
                entries.Add(new ResourceListEntry(pair.Key, pair.Value, null, null));
            }
        }

        if (_itemTable is not null)
        {
            var categoryTable = ResourceCategories.IsAll(_filter) ? null : _itemTable.FileForCategory(_filter);
            IReadOnlyList<ItemTableFile> files = categoryTable is null ? _itemTable.Files : new[] { categoryTable };
            var seen = new HashSet<string>(entries.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                foreach (var row in file.Rows)
                {
                    var name = Path.GetFileNameWithoutExtension(row.Icon);
                    if (name.Length == 0)
                    {
                        name = row.TemplateName.Trim();
                    }

                    if (name.Length == 0 || !seen.Add(name))
                    {
                        continue;
                    }

                    var path = ResolveConfiguredImage(row);
                    if (MatchesResourceFilter(name, path, row, file))
                    {
                        entries.Add(new ResourceListEntry(name, path, row, file));
                    }
                }
            }
        }

        return entries
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>分类与搜索词过滤；配置表行优先按所在表归类，混合表再回退资源名分类。</summary>
    private bool MatchesResourceFilter(string name, string? path, ItemElementRow? row, ItemTableFile? table)
    {
        var keyword = _search.Text?.Trim() ?? string.Empty;
        if (keyword.Length > 0
            && !name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            && !(path?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            && !(row?.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            && !(row?.TemplateName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        if (ResourceCategories.IsAll(_filter))
        {
            return true;
        }

        var category = CategoryOfResource(name, path, row, table);
        return string.Equals(category, _filter, StringComparison.Ordinal);
    }

    /// <summary>资源条目的分类显示名。</summary>
    private string CategoryOfResource(
        string name,
        string? path,
        ItemElementRow? row,
        ItemTableFile? table)
    {
        if (row is null || table is null)
        {
            return CategoryDisplay(ResourceCategories.Classify(path ?? name));
        }

        var namedCategory = NamedCategoryOfTable(table);
        // 分类表里可能混入其他分类行（例如 TableProps 里的 cat_）；按当前筛选回退到资源名分类。
        if (namedCategory is not null
            && (ResourceCategories.IsAll(_filter) || namedCategory == _filter))
        {
            return namedCategory;
        }

        return CategoryDisplay(ResourceCategories.Classify(name));
    }

    /// <summary>表名对应的分类；汇总表等无明确分类的表返回 null。</summary>
    private static string? NamedCategoryOfTable(ItemTableFile table)
    {
        var stem = Path.GetFileNameWithoutExtension(table.Name);
        return ResourceCategories.ConfiguredCategories
            .FirstOrDefault(definition => definition.TableHints.Any(hint =>
                stem.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            ?.DisplayName;
    }

    /// <summary>配置表行补磁盘图片：先走资源库，再查地图保存目录索引。</summary>
    private string? ResolveConfiguredImage(ItemElementRow row)
    {
        var resolved = _assets.Resolve(row.Icon);
        if (resolved is not null)
        {
            return resolved;
        }

        EnsureMapImageIndex();
        var name = Path.GetFileNameWithoutExtension(row.Icon);
        return _mapImageIndex is not null && _mapImageIndex.TryGetValue(name, out var path) ? path : null;
    }

    /// <summary>构建地图保存目录的 PNG 索引；只在需要给配置表行补图时惰性扫描一次。</summary>
    private void EnsureMapImageIndex()
    {
        if (_mapImageIndex is not null || string.IsNullOrWhiteSpace(_mapDirectory))
        {
            return;
        }

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_mapDirectory, "*.png", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length > 0 && !index.ContainsKey(name))
                {
                    index[name] = file;
                }
            }
        }
        catch
        {
            // 目录被移除 / 无权限时保留空索引，列表仍可按配置表显示。
        }

        _mapImageIndex = index;
    }

    /// <summary>卡片副标题：未配置显示「层 · 目录」，已配置按图一追加「配置表 #ID」。</summary>
    private string BuildResourceDescription(ResourceListEntry entry)
    {
        if (entry.Row is not null)
        {
            var category = CategoryOfResource(entry.Name, entry.Path, entry.Row, entry.Table);
            var table = TableLabelOf(entry.Table);
            return $"{category} · {table} · 配置表 #{entry.Row.Id}";
        }

        var folder = Path.GetDirectoryName(entry.Path)?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return $"{LayerNameOf(entry.Path ?? entry.Name)} · {folder}";
    }

    /// <summary>表名去掉 Table 前缀，显示为 Props / Buildings 这类短名。</summary>
    private static string TableLabelOf(ItemTableFile? table)
    {
        var stem = Path.GetFileNameWithoutExtension(table?.Name ?? string.Empty);
        if (stem.StartsWith("Table", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem["Table".Length..];
        }

        return stem.Length > 0 ? stem : "Props";
    }

    private void RefreshUnconfiguredList()
    {
        _unconfiguredHost.Children.Clear();

        var keyword = _search.Text?.Trim() ?? string.Empty;
        var entries = FilteredResourceEntries();
        var unconfiguredCount = entries.Count(item => item.Row is null);
        var configuredCount = entries.Count(item => item.Row is not null);
        var summary = $"共 {entries.Count} 个资源（{unconfiguredCount} 个未配置，{configuredCount} 个已配置）";
        _countText.Text = keyword.Length == 0 ? summary : $"筛选出 {entries.Count} 个资源（{unconfiguredCount} 个未配置，{configuredCount} 个已配置）";

        if (entries.Count == 0)
        {
            _unconfiguredHost.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_mapDirectory)
                    ? "（暂无资源；先在地图配置里设置「地图保存目录」，再点「扫描」）"
                    : $"（{_mapDirectory} 下暂无资源，点「扫描」重新读取）",
                Foreground = UiTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4),
            });
            return;
        }

        foreach (var entry in entries)
        {
            _unconfiguredHost.Children.Add(BuildResourceRow(entry));
        }
    }

    private Control BuildResourceRow(ResourceListEntry entry)
    {
        var thumb = new Border
        {
            Width = ThumbnailPixelSize,
            Height = ThumbnailPixelSize,
            CornerRadius = ThumbnailCornerRadius,
            Background = UiTheme.LeftPanel.ThumbnailFill,
            ClipToBounds = true,
        };

        if (entry.Path is { Length: > 0 } path && TryLoadThumbnail(path) is { } bitmap)
        {
            thumb.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }

        var title = new TextBlock { Text = entry.Name, Foreground = UiTheme.TextPrimary };
        var desc = new TextBlock
        {
            Text = BuildResourceDescription(entry),
            Foreground = UiTheme.TextSecondary,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
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
        var row = new ListRowCard(UnconfiguredRowHeight, IsEntrySelected(entry)) { Child = content };

        row.PointerPressed += (_, e) =>
        {
            if (entry.Row is not null)
            {
                SelectConfiguredRow(entry);
                return;
            }

            if (entry.Path is not null)
            {
                SelectUnconfigured(entry.Name, entry.Path, e.KeyModifiers);
            }
        };
        return row;
    }

    /// <summary>配置表行点击：复用「编辑已配置模板」流程，避免把它当作未配置资源再添加一遍。</summary>
    private void SelectConfiguredRow(ResourceListEntry entry)
    {
        if (entry.Row is null)
        {
            return;
        }

        _selection.Clear();
        _selectionAnchorPath = entry.Path;
        var template = _templates.FirstOrDefault(item => _itemTable?.FindByTemplate(item) == entry.Row);
        if (template is null)
        {
            var category = CategoryOfResource(entry.Name, entry.Path, entry.Row, entry.Table);
            template = ItemConfigSet.CreateTemplate(entry.Row, ResourceCategories.ObjectTypeOfKey(
                ResourceCategories.FindByDisplay(category)?.Key ?? ResourceCategories.PropKey));
            _templates.Add(template);
        }

        SelectTemplateForEdit(template);
    }

    /// <summary>条目是否选中：未配置按路径，已配置按正在编辑的配置表行。</summary>
    private bool IsEntrySelected(ResourceListEntry entry)
    {
        if (entry.Row is not null)
        {
            return _editing is not null && ReferenceEquals(_itemTable?.FindByTemplate(_editing), entry.Row);
        }

        return entry.Path is not null && IsPathSelected(entry.Path);
    }

    #endregion

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
