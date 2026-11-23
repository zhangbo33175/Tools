/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.LeftPanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口左侧装配：章节树、关卡行、资源列表与收集物面板。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{
    #region 左侧面板

    /// <summary>
    /// 读章节表 / 主关卡表（失败时回退 Lua 章节表），结果写到 <see cref="_chapters"/>。
    /// 地图配置弹窗改了表路径后要重调一次。
    /// </summary>
    private void LoadChapterCatalog()
    {
        _chapters = ChapterRepository.Load(_assets.ProjectRoot);
        if (!string.IsNullOrEmpty(_chapters.Error))
        {
            SetMessage($"章节表：{_chapters.Error}");
        }
    }

    /// <summary>
    /// 画左侧「章节 → 关卡」树：章节表里一章一个可折叠节点，ChapterInfoID 里有几个 ID 就挂几个关卡。
    /// 关卡行右侧显示该关卡的地图存档状态（已建时给文件大小，未建显示「空」），和 Unity 的章节树对齐。
    /// </summary>
    private void BuildChapters()
    {
        ChapterList.Children.Clear();

        if (_chapters.Chapters.Count == 0)
        {
            ChapterList.Children.Add(new TextBlock
            {
                Text = "未读到章节配置表",
                Foreground = HintBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(6, 8, 6, 3),
            });
            ChapterList.Children.Add(new TextBlock
            {
                Text = _chapters.ChaptersPath,
                Foreground = HintBrush,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(6, 0, 6, 4),
            });
            return;
        }

        foreach (var chapter in _chapters.Chapters)
        {
            ChapterList.Children.Add(BuildChapterNode(chapter));
        }
    }

    /// <summary>章节节点：标题行（箭头 + 章节名 + 关卡数）＋ 展开后的关卡行。</summary>
    private Control BuildChapterNode(ChapterRow chapter)
    {
        var collapsed = _collapsedChapters.Contains(chapter.Id);
        var node = new StackPanel { Margin = new Avalonia.Thickness(0, 0, 0, 2) };

        var arrow = new TextBlock
        {
            Text = collapsed ? "▸" : "▾",
            Width = 12,
            FontSize = 11,
            Foreground = HintBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var title = new TextBlock
        {
            Text = ChapterHeaderText(chapter),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(arrow);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var header = new Button
        {
            Content = grid,
            Height = 26,
            Padding = new Avalonia.Thickness(6, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF3, 0xF5)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE2, 0xE7)),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(header, $"第 {chapter.Ordinal} 章 · 章节 ID {chapter.Id} · 共 {chapter.LevelIds.Count} 关 · 地图 {chapter.MapName} · 音乐 {chapter.Music}");

        var chapterId = chapter.Id;
        header.Click += (_, _) =>
        {
            if (!_collapsedChapters.Remove(chapterId))
            {
                _collapsedChapters.Add(chapterId);
            }

            BuildChapters();
        };

        node.Children.Add(header);

        if (collapsed)
        {
            return node;
        }

        for (var index = 0; index < chapter.LevelIds.Count; index++)
        {
            node.Children.Add(BuildLevelRow(chapter, chapter.LevelIds[index], index));
        }

        return node;
    }

    /// <summary>章节头文字：保留「第 N 章 · 」前缀，只把后半截换成配置表里的章节名。</summary>
    private static string ChapterHeaderText(ChapterRow chapter)
    {
        var name = chapter.Title;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"章节 {chapter.Id}";
        }

        return $"第{ChineseOrdinal(chapter.Ordinal)}章 · {name}";
    }

    /// <summary>1 → 一、2 → 二 …… 10 → 十、12 → 十二；超过 99 退回阿拉伯数字。</summary>
    private static string ChineseOrdinal(int value)
    {
        if (value <= 0 || value > 99)
        {
            return value.ToString();
        }

        var digits = "零一二三四五六七八九";
        if (value < 10)
        {
            return digits[value].ToString();
        }

        var tens = value / 10;
        var ones = value % 10;
        var head = tens == 1 ? "十" : digits[tens] + "十";
        return ones == 0 ? head : head + digits[ones];
    }

    /// <summary>关卡行：关卡序号 + 关卡资源名（Main_N）+ 存档状态徽标；点击切到该关卡的地图。</summary>
    private Control BuildLevelRow(ChapterRow chapter, string levelId, int index)
    {
        var selected = _selectedChapterId == chapter.Id && _selectedLevelId == levelId;
        var levelRow = _chapters.FindLevel(levelId);
        var fileName = LevelMapFileName(chapter.Id, levelId);
        var (hasFile, bytes) = LevelFileState(chapter.Id, levelId);

        var name = new TextBlock
        {
            Text = $"关卡 {index + 1}",
            FontSize = 12,
            Foreground = selected ? WhiteTextBrush : TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var resource = new TextBlock
        {
            Text = levelRow is null ? levelId : levelRow.LevelId,
            FontSize = 10,
            Foreground = selected ? WhiteTextBrush : HintBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(6, 0, 4, 0),
        };

        var badge = new TextBlock
        {
            Text = hasFile ? FormatFileSize(bytes) : "空",
            FontSize = 10,
            Foreground = selected
                ? WhiteTextBrush
                : hasFile
                    ? new SolidColorBrush(Color.FromRgb(0x2B, 0xA6, 0x6A))
                    : HintBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 0, 6, 0),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(name);
        Grid.SetColumn(resource, 1);
        grid.Children.Add(resource);
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        var row = new Button
        {
            Content = grid,
            Height = 24,
            Margin = new Avalonia.Thickness(2, 1, 2, 0),
            Padding = new Avalonia.Thickness(14, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = selected ? ActiveTabBrush : PlainRowBrush,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        var tip = new System.Text.StringBuilder();
        tip.Append($"{chapter.Title}-关卡{levelId}");
        if (levelRow is not null)
        {
            tip.Append($"\n{levelRow.LevelId} · 背景 {levelRow.Background} · 寻物类型 {levelRow.FindType} · 体力 {levelRow.Life}");
        }

        tip.Append($"\n存档：{fileName}{(!hasFile ? "（尚未创建）" : $"（{FormatFileSize(bytes)}）")}");
        ToolTip.SetTip(row, tip.ToString());

        row.Click += async (_, _) => await SwitchLevelAsync(chapter, levelId);

        return row;
    }

    /// <summary>关卡存档文件名：与 Unity 一致，<c>Map_{章节ID}m{关卡ID}.json</c>。</summary>
    private static string LevelMapFileName(string chapterId, string levelId) => $"Map_{chapterId}m{levelId}.json";

    /// <summary>
    /// 关卡存档状态：优先看 <c>Map_{章节}m{关卡}.json</c>，没有再看旧的 <c>{关卡}.json</c>
    /// （Unity 工具早先按场景 ID 落盘，这里保留一条兼容路径）。
    /// </summary>
    /// <returns>是否存在、文件字节数。</returns>
    private (bool Exists, long Bytes) LevelFileState(string chapterId, string levelId)
    {
        var primary = Path.Combine(_saveDirectory, LevelMapFileName(chapterId, levelId));
        if (File.Exists(primary))
        {
            return (true, new FileInfo(primary).Length);
        }

        var legacy = Path.Combine(_saveDirectory, levelId + ".json");
        if (File.Exists(legacy))
        {
            return (true, new FileInfo(legacy).Length);
        }

        return (false, 0);
    }

    /// <summary>文件大小徽标：MB 保留一位小数，其余按 KB。</summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}MB", bytes / 1048576.0);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0}KB", Math.Max(1, bytes / 1024));
    }

    /// <summary>当前保存文件名：跟随关卡用 <c>Map_{章节}m{关卡}.json</c>，否则回退「场景 ID.json」。</summary>
    private string CurrentSaveFileName() =>
        string.IsNullOrWhiteSpace(_saveFileOverride)
            ? MapJsonService.BuildFileName(_config)
            : _saveFileOverride;

    /// <summary>
    /// 点关卡行：先把当前地图存到它自己的关卡文件（有物件时弹一次确认），
    /// 再载入目标关卡的存档；目标没有存档就开一张空图，保存后才落盘。
    /// 场景名 / 场景 ID 按 Unity 口径写成 <c>{章节名}-关卡{关卡ID}</c> / 关卡 ID。
    /// </summary>
    private async Task SwitchLevelAsync(ChapterRow chapter, string levelId)
    {
        if (_selectedChapterId == chapter.Id && _selectedLevelId == levelId)
        {
            SetMessage($"当前已在 {chapter.Title}-关卡{levelId}");
            return;
        }

        var sceneName = $"{chapter.Title}-关卡{levelId}";
        var primaryPath = Path.Combine(_saveDirectory, LevelMapFileName(chapter.Id, levelId));
        var legacyPath = Path.Combine(_saveDirectory, levelId + ".json");
        var sourcePath = File.Exists(primaryPath)
            ? primaryPath
            : File.Exists(legacyPath)
                ? legacyPath
                : null;

        if (_config.Objects.Count > 0)
        {
            var currentFile = CurrentSaveFileName();
            var dialog = new ConfirmWindow(
                "切换关卡",
                $"当前地图「{_config.SceneName}」有 {_config.Objects.Count} 个物件。\n" +
                $"保存并切换：先写入 {currentFile}；不保存切换：丢弃本次未保存修改，已有存档保持原样。",
                "保存并切换",
                "取消",
                "不保存切换");

            var choice = await dialog.ShowDialog<ConfirmChoice>(this);
            if (choice == ConfirmChoice.Cancel)
            {
                SetMessage("已取消关卡切换");
                return;
            }

            if (choice == ConfirmChoice.Save && !SaveMap())
            {
                // SaveMap 已经把具体失败原因写进状态栏，这里不再覆盖
                return;
            }
        }

        // 离开当前关卡前记住视口；回来时恢复，避免又跳回 FitToView 的居中缩小视角
        CaptureLevelView();

        _selectedChapterId = chapter.Id;
        _selectedLevelId = levelId;

        if (sourcePath is not null)
        {
            LoadMapFile(sourcePath, fitToView: false);
        }
        else
        {
            ApplyEmptyLevel(chapter, levelId, sceneName);
        }
        // 无论存档来自哪里，都以章节表的命名与 ID 为准（Unity 的 ApplyChapterContext 同口径）
        _config.SceneName = sceneName;
        _config.SceneId = levelId;
        _config.ChapterId = chapter.Id;
        _config.ChapterName = chapter.Title;
        _saveFileOverride = LevelMapFileName(chapter.Id, levelId);
        RestoreLevelView(_saveFileOverride);
        RefreshSceneFields();
        RefreshStatus();

        BuildChapters();

        if (sourcePath is not null)
        {
            SetMessage($"已切到 {sceneName}：{(Path.GetFileName(sourcePath) == LevelMapFileName(chapter.Id, levelId) ? string.Empty : $"旧档 {Path.GetFileName(sourcePath)} → ")}" +
                       $"{_config.Objects.Count} 个物件，保存写入 {LevelMapFileName(chapter.Id, levelId)}");
        }
        else
        {
            SetMessage($"{sceneName} 还没有地图存档，已开一张空图；点「保存」写入 {LevelMapFileName(chapter.Id, levelId)}");
        }
    }

    /// <summary>记住当前关卡离开时的视口；未落过盘的关卡用它的目标文件名做键。</summary>
    private void CaptureLevelView()
    {
        var key = string.IsNullOrWhiteSpace(_currentMapPath)
            ? CurrentSaveFileName()
            : Path.GetFileName(_currentMapPath);
        if (string.IsNullOrWhiteSpace(key) || Canvas.Bounds.Width <= 0 || Canvas.Bounds.Height <= 0)
        {
            return;
        }

        _levelViews[key] = Canvas.ViewState;
    }

    /// <summary>恢复目标关卡的视口；第一次进入的关卡才做 FitToView。</summary>
    /// <param name="key">关卡存档文件名。</param>
    private void RestoreLevelView(string key)
    {
        if (_levelViews.TryGetValue(key, out var view))
        {
            Canvas.SetView(view.Zoom, view.Offset);
            return;
        }

        Canvas.FitToView();
    }

    /// <summary>
    /// 开一张属于该关卡的空地图：宽高 / 格子 / 绘制形状沿用当前地图；
    /// 底图不沿用上一关（否则空关卡会把上一关整张合成图当底图显示），只用本章节表里该关自己的背景。
    /// </summary>
    private void ApplyEmptyLevel(ChapterRow chapter, string levelId, string sceneName)
    {
        ResetPsdSession();

        var fresh = TemplateCatalog.CreateEmptyMap();
        fresh.SceneId = levelId;
        fresh.SceneName = sceneName;
        fresh.ChapterId = chapter.Id;
        fresh.ChapterName = chapter.Title;
        fresh.Width = _config.Width;
        fresh.Height = _config.Height;
        fresh.GridSize = _config.GridSize;
        fresh.DrawShape = _config.DrawShape;
        fresh.BgName = string.Empty;

        CloseMapPreview();
        _config = fresh;
        SyncCollectiblePlanFromMap();
        _currentMapPath = string.Empty;
        Canvas.Config = _config;
        Canvas.Select(null);
        ResetPlacementState();
        AttachLevelResourceRoot();

        // 只认本章节表给这张关卡指定的背景，并且必须真的落在本关卡 Background 目录里；
        // 找不到就保持空底图，不能回退到 Unity 工程/上一关的同名整图。
        var levelMeta = _chapters.FindLevel(levelId);
        var background = levelMeta?.Background;
        if (!string.IsNullOrWhiteSpace(background))
        {
            var backgroundFile = Path.Combine(_levelResourceRoot ?? string.Empty, "Background", background + ".png");
            if (File.Exists(backgroundFile))
            {
                _config.BgName = background;
            }
        }

        _fogDoc = new MapFogDocument { SceneId = _config.SceneId };
        Canvas.Fog = _fogDoc;
        _fogPanel?.Attach(_config, _fogDoc, Canvas);
        _resourcePanel?.Attach(_templates, _config);

        RefreshSceneFields();
        RefreshStatus();
        RefreshLightBindList();
        SyncProperties();
        Canvas.FitToView();
        Canvas.Refresh();
        SetSaveHint($"当前：{LevelMapFileName(chapter.Id, levelId)}（尚未保存）", neutral: true);
    }

    private void BuildResourceList()
    {
        ResourceList.Children.Clear();

        // 场景资源列表也走配置表：分类 Tab = 一张表，列出的是表行（和「物品元件编辑」同一套映射）
        var table = _itemTable?.FileForCategory(ObjectTypeNames.Of(_activeType));
        var templates = table is null
            ? _templates.Where(template => template.Type == _activeType).ToList()
            : table.Rows.Select(row => BuildTablePaletteTemplate(row)).ToList();

        ResourceListTitle.Text = $"{ObjectTypeNames.Of(_activeType)}资源";
        ResourceCountText.Text = templates.Count.ToString(CultureInfo.InvariantCulture);

        if (templates.Count == 0 && table is not null)
        {
            ResourceList.Children.Add(new TextBlock
            {
                Text = $"（{table.Name} 没有数据行）",
                Foreground = HintBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(6, 4),
            });
        }

        foreach (var template in templates)
        {
            var isSelected = _pendingTemplate?.TemplateId == template.TemplateId;
            ResourceList.Children.Add(BuildResourceRow(template, isSelected));
        }

        UpdateTypeTabVisual();
    }

    /// <summary>表行 → 可摆放的资源项：图片名换成真实 PNG 路径，其余字段按行值带过来。</summary>
    /// <param name="row">配置表行。</param>
    /// <param name="type">目标实例类型；传 null 时按当前资源 Tab。</param>
    private ObjectTemplate BuildTablePaletteTemplate(ItemElementRow row, MapObjectType? type = null)
    {
        var template = ItemConfigSet.CreateTemplate(row, type ?? _activeType);
        var iconName = Path.GetFileNameWithoutExtension(row.Icon);
        var imagePath = _assets.FindImage(iconName);
        if (imagePath is not null)
        {
            template.ImagePath = imagePath;
        }

        return template;
    }

    private Control BuildResourceRow(ObjectTemplate template, bool isSelected)
    {
        var swatch = BuildResourceIcon(template);

        var label = new TextBlock
        {
            Text = template.TemplateName,
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        // 色块贴左边固定位置，名称在整行范围内居中（长名称不会压到色块）
        var content = new Grid();
        content.Children.Add(label);
        content.Children.Add(swatch);
        swatch.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        swatch.ZIndex = 1;

        var row = new Button
        {
            Content = content,
            Height = 32,
            Padding = new Avalonia.Thickness(6, 0),
            Margin = new Avalonia.Thickness(0, 0, 0, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        // 选中态只覆盖底色与描边，未选中时保持全局 Button 样式的浅灰底 + 1px 灰边
        if (isSelected)
        {
            row.Background = SelectedRowBrush;
            row.BorderBrush = new SolidColorBrush(Color.FromRgb(0x70, 0xB4, 0xDA));
        }

        row.Click += (_, _) =>
        {
            // 选模板 = 进入摆放意图；路径绘制必须先退出，否则画布会把“点地面”解释成加点。
            SetPathEditing(false, notify: false);
            _pendingTemplate = template;
            Canvas.PendingCollectibleTemplate = null;
            Canvas.PendingTemplate = template;
            BuildResourceList();
            SetMessage($"已选择资源：{template.TemplateName}，点击画布摆放");
        };

        return row;
    }

    private Control BuildResourceIcon(ObjectTemplate template)
    {
        Avalonia.Media.Imaging.Bitmap? bitmap = null;
        if (!string.IsNullOrWhiteSpace(template.ImagePath))
        {
            bitmap = _assets.Load(template.ImagePath);
        }

        if (bitmap is not null)
        {
            return new Image
            {
                Source = bitmap,
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        return new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new Avalonia.CornerRadius(3),
            Background = new SolidColorBrush(ParseColor(MapLayerKeys.Hex(template.LayerKey))),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0xD2, 0xD2)),
            BorderThickness = new Avalonia.Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>按当前选中的对象类型刷新类型 Tab 的底色与文字色。</summary>
    private void UpdateTypeTabVisual()
    {
        ApplySideTabVisual(TabBuilding, _activeType == MapObjectType.Building);
        ApplySideTabVisual(TabGround, _activeType == MapObjectType.Character);
        ApplySideTabVisual(TabProp, _activeType == MapObjectType.Prop);
    }

    /// <summary>左侧分段式资源 Tab：选中态用 class 驱动，保留悬停样式不被本地颜色覆盖。</summary>
    /// <param name="button">目标 Tab 按钮。</param>
    /// <param name="active">是否处于选中态。</param>
    private static void ApplySideTabVisual(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
        }
        else
        {
            button.Classes.Remove("active");
        }

        // class 负责悬停态；这里再落一层本地颜色，避免模板默认底色把选中蓝盖掉。
        button.Background = active ? ActiveTabBrush : Brushes.Transparent;
        button.Foreground = active ? WhiteTextBrush : SideTabTextBrush;
        button.BorderThickness = new Thickness(0);
    }

    /// <summary>选中态用蓝色底白字，未选中态用浅灰底深字。</summary>
    /// <param name="button">目标 Tab 按钮。</param>
    /// <param name="active">是否处于选中态。</param>
    private static void ApplyTabVisual(Button button, bool active)
    {
        button.Background = active ? ActiveTabBrush : IdleTabBrush;
        button.Foreground = active ? WhiteTextBrush : TextBrush;
    }

    #endregion
}
