/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.Properties.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口属性同步：对象字段、图层、路径列表、场景字段与地图配置。
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
    #region 数据同步

    private void SyncProperties()
    {
        TrafficMarkService.SyncObjectAreas(_config);
        TrafficMarkService.SyncObjectCrosswalks(_config);
        TrafficMarkService.SyncObjectLights(_config);
        var item = Canvas.Selected;
        _suppressSync = true;

        if (item is null)
        {
            ObjEditTitle.Text = "对象编辑：未选择";
            PropSummary.Text = "未选中物体。";

            // 清空整张属性卡（模板 / 格子 / 层级 / 图层 / 交互 / 交通 / 动画），
            // 不能只清“物体编辑”那几格：否则切关卡后右栏会留着上一个选中实例的 bg_road_5，
            // 看起来像“未选中却有一份属性”，也就是用户说的界面错乱。
            ClearPropertyFields();

            // 区域与变换三块：未选中时收起并清空数值
            ClearAreaFields();
            UpdateAreaSectionVisibility();

            // 未选中实例时「交通」回到默认勾选，交通分组保持显示以便直接刷格
            PropTraffic.IsChecked = true;
            PropBandInfo.Text = string.Empty;
            PropBandInfoBox.IsVisible = false;
            SetPropertyPanelEnabled(false);
            _suppressSync = false;
            RefreshLightBindList();
            RefreshTrafficMarkButtons();
            return;
        }

        SetPropertyPanelEnabled(true);

        ObjEditTitle.Text = $"对象编辑：{item.Name}";
        PropTemplateId.Text = ResolveNumericId(item, out var idFromConfig);
        ApplyIdSourceBadge(idFromConfig, PropTemplateId.Text);
        PropTag.Text = ResolveLayerTag(item);
        ApplyLayerTagColor(item);
        PropCellX.Text = item.CellX.ToString(CultureInfo.InvariantCulture);
        PropCellY.Text = item.CellY.ToString(CultureInfo.InvariantCulture);
        PropCellW.Text = item.Width.ToString(CultureInfo.InvariantCulture);
        PropCellH.Text = item.Height.ToString(CultureInfo.InvariantCulture);
        PropSortOrder.Text = item.SortOrder.ToString(CultureInfo.InvariantCulture);
        PropBand.SelectedIndex = item.BandOverride is >= LayerBands.Ground and <= LayerBands.Overlay
            ? item.BandOverride + 1
            : 0;
        UpdateBandInfo();
        ApplyLayerOptions();
        PropDisplayLayer.SelectedIndex = LayerSelectionIndex(item);
        PropInteractType.SelectedIndex = InteractIndex(item.InteractType);
        PropFacing.SelectedIndex = FacingIndex(item.FacingDirection);
        PropCollision.IsChecked = item.CollisionEnabled;
        PropShadow.IsChecked = item.ShadowEnabled;
        PropCollectible.IsChecked = item.IsCollectible;
        PropTraffic.IsChecked = item.Interactable;
        PropBubble.Text = item.BubbleId.ToString(CultureInfo.InvariantCulture);
        PropDialogBubble.Text = item.DialogBubble;

        // 物体编辑区回填：位置 / 旋转 / 大小 / 是否缩放
        var bounds = item.Bounds;
        EditAngleText.Text = $"{item.Rotation:0.##}°";
        PropPosX.Text = item.X.ToString("0.##", CultureInfo.InvariantCulture);
        PropPosY.Text = item.Y.ToString("0.##", CultureInfo.InvariantCulture);
        PropSizeW.Text = bounds.Width.ToString("0", CultureInfo.InvariantCulture);
        PropSizeH.Text = bounds.Height.ToString("0", CultureInfo.InvariantCulture);
        PropStretch.IsChecked = item.Stretch;

        // 碰撞 / 阴影区域与变换回填
        SyncAreaFields(item);
        ScaleValueText.Text = $"{(int)Math.Round(item.Scale * 100)}%";
        AngleValueText.Text = $"{item.Rotation:0.##}°";
        UpdateAreaSectionVisibility();

        AnimNameField.Text = item.AnimName;
        AnimSpeedField.Text = item.Speed.ToString("0.##", CultureInfo.InvariantCulture);
        AnimLoopToggle.IsChecked = item.Loop;
        RefreshAnimationEditor(item);

        // 交通规则区回填
        PropVehicle.IsChecked = item.IsVehicle;
        SelectComboByText(PropTrafficRole, TrafficRoles.ToDisplay(item.TrafficRole));
        PropStopLine.Text = $"{item.StopLineX:0}, {item.StopLineY:0}";
        PropYellowAsRed.IsChecked = item.YellowTreatAsRed;
        RefreshLightBindList();
        RefreshTrafficMarkButtons();

        var band = LayerBands.Of(item);
        PropSummary.Text =
            $"类型：{TypeName(item.Type)}　图层：{MapLayerKeys.DisplayName(ResolveLayerTagKey(item))}　遮挡：{LayerBands.DisplayName(band)}\n" +
            $"坐标：{item.X:0}, {item.Y:0}　像素尺寸：{bounds.Width:0}×{bounds.Height:0}　路径点：{item.PathPoints.Count}";

        _suppressSync = false;
    }

    /// <summary>
    /// 属性区「ID」的数字口径：优先配置表行 ID；没有配置行时回退 PSD 切片序号。
    /// <para>回退必须同时命中图片路径与左上角，避免同一张切片被摆多个实例时拿到别人的序号。</para>
    /// </summary>
    /// <param name="item">当前选中实例。</param>
    /// <param name="fromConfig">true = 命中配置表行；false = 回退 PSD 切片序号。</param>
    /// <returns>数字 ID；两边都没有时返回 <c>—</c>。</returns>
    private string ResolveNumericId(PlacedObject item, out bool fromConfig)
    {
        fromConfig = false;
        var keys = new[] { item.TemplateId, Path.GetFileNameWithoutExtension(item.ImagePath) };
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var row = _itemTable?.Find(key);
            var id = row?.Id?.Trim() ?? string.Empty;
            if (id.Length > 0 && id.All(char.IsDigit))
            {
                fromConfig = true;
                return id;
            }
        }

        var sliceIndex = ResolveSliceIndex(item);
        return sliceIndex >= 0 ? sliceIndex.ToString(CultureInfo.InvariantCulture) : "—";
    }

    /// <summary>ID 右侧来源角标：配置表命中显示「配置」，PSD 回退显示「切片」，两者都没有显示「无」。</summary>
    /// <param name="fromConfig">是否来自配置表。</param>
    /// <param name="idText">当前显示的数字 ID。</param>
    private void ApplyIdSourceBadge(bool fromConfig, string idText)
    {
        var missing = string.IsNullOrWhiteSpace(idText) || idText == "—";
        PropIdSourceBadge.IsVisible = true;
        PropIdSourceText.Text = missing ? "无" : fromConfig ? "配置" : "切片";
        var accent = missing ? "#7C8790" : fromConfig ? "#3A7F9E" : "#A97831";
        PropIdSourceText.Foreground = new SolidColorBrush(Color.Parse(accent));
        PropIdSourceBadge.Background = new SolidColorBrush(Color.Parse(missing ? "#EEF1F3" : fromConfig ? "#EAF4FA" : "#FFF4E2"));
        PropIdSourceBadge.BorderBrush = new SolidColorBrush(Color.Parse(missing ? "#DCE2E6" : fromConfig ? "#D2E6F1" : "#F0DFC2"));
        ToolTip.SetTip(PropIdSourceBadge, missing
            ? "配置表与 PSD 切片清单都没有命中"
            : fromConfig
                ? "数字 ID 来自物件配置表"
                : "配置表无此行，数字 ID 回退到 PSD 切片序号");
    }

    /// <summary>按图片路径 + 左上角在当前关卡的 slices.json 里找切片序号。</summary>
    /// <param name="item">当前选中实例。</param>
    /// <returns>切片序号；找不到返回 -1。</returns>
    private int ResolveSliceIndex(PlacedObject item)
    {
        EnsureSliceIndexRows();
        if (_sliceIndexRows.Count == 0)
        {
            return -1;
        }

        var file = NormalizeSliceFile(item.ImagePath);
        var left = item.X;
        var top = item.Y;

        var exact = _sliceIndexRows.FirstOrDefault(row =>
            string.Equals(row.File, file, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(row.Left - left) <= 0.5
            && Math.Abs(row.Top - top) <= 0.5);
        if (exact.Index > 0)
        {
            return exact.Index;
        }

        // 同一张图只出现一次时无需坐标也能确定；多次出现则宁可显示「—」也不猜。
        var sameFile = _sliceIndexRows
            .Where(row => string.Equals(row.File, file, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameFile.Count == 1 ? sameFile[0].Index : -1;
    }

    /// <summary>按当前关卡路径惰性读取 <c>slices.json</c>；切关卡后路径变化会自动重读。</summary>
    private void EnsureSliceIndexRows()
    {
        var path = Path.Combine(ResolveLevelResourceDirectory(), "slices.json");
        if (string.Equals(_sliceIndexPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _sliceIndexPath = path;
        _sliceIndexRows.Clear();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("slices", out var slices)
                || slices.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var slice in slices.EnumerateArray())
            {
                if (!slice.TryGetProperty("index", out var indexNode)
                    || !slice.TryGetProperty("file", out var fileNode)
                    || !slice.TryGetProperty("left", out var leftNode)
                    || !slice.TryGetProperty("top", out var topNode))
                {
                    continue;
                }

                var index = indexNode.GetInt32();
                var file = NormalizeSliceFile(fileNode.GetString());
                if (index > 0 && file.Length > 0)
                {
                    _sliceIndexRows.Add((file, leftNode.GetDouble(), topNode.GetDouble(), index));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or FormatException)
        {
            _sliceIndexRows.Clear();
        }
    }

    /// <summary>统一切片路径分隔符，避免 JSON 里的反斜杠与模型里的正斜杠对不上。</summary>
    private static string NormalizeSliceFile(string? path) => (path ?? string.Empty).Replace('\\', '/').Trim();

    /// <summary>属性区 Tag 的键：显示层优先，空串回退归属层类型。</summary>
    private string ResolveLayerTag(PlacedObject item)
    {
        var key = ResolveLayerTagKey(item);
        return string.IsNullOrWhiteSpace(key) ? "—" : key;
    }

    /// <summary>取当前实例真正参与分层的键（显示层覆盖归属层）。</summary>
    private static string ResolveLayerTagKey(PlacedObject item)
    {
        var key = string.IsNullOrWhiteSpace(item.DisplayLayer) ? item.LayerType : item.DisplayLayer;
        return key?.Trim() ?? string.Empty;
    }

    /// <summary>把 Tag 左侧的色条刷成图层代表色，并补一个「中文名 · 技术键」提示。</summary>
    private void ApplyLayerTagColor(PlacedObject item)
    {
        var key = ResolveLayerTagKey(item);
        PropLayerTagDot.Background = new SolidColorBrush(Color.Parse(MapLayerKeys.Hex(key)));
        ToolTip.SetTip(PropLayerTagDot, MapLayerKeys.DisplayName(key));
        ToolTip.SetTip(PropTag, $"{MapLayerKeys.DisplayName(key)} · {ResolveLayerTag(item)}");
    }

    /// <summary>
    /// 清空属性卡的全部实例字段。选中为空时整张卡必须回到“空”，
    /// 不能把上一次选中的模板 ID / 格子 / 层级 / 图层留在禁用控件里。
    /// </summary>
    private void ClearPropertyFields()
    {
        PropTemplateId.Text = string.Empty;
        PropIdSourceBadge.IsVisible = false;
        PropTag.Text = string.Empty;
        PropLayerTagDot.Background = new SolidColorBrush(Color.Parse("#C9D2D8"));
        ToolTip.SetTip(PropLayerTagDot, "未选中物体");
        ToolTip.SetTip(PropTag, "未选中物体");
        PropCellX.Text = string.Empty;
        PropCellY.Text = string.Empty;
        PropCellW.Text = string.Empty;
        PropCellH.Text = string.Empty;
        PropSortOrder.Text = string.Empty;
        PropBand.SelectedIndex = -1;
        PropDisplayLayer.SelectedIndex = -1;
        PropInteractType.SelectedIndex = -1;
        PropFacing.SelectedIndex = -1;
        PropCollision.IsChecked = false;
        PropShadow.IsChecked = false;
        PropCollectible.IsChecked = false;
        PropBubble.Text = string.Empty;
        PropDialogBubble.Text = string.Empty;

        EditAngleText.Text = "—";
        PropPosX.Text = string.Empty;
        PropPosY.Text = string.Empty;
        PropSizeW.Text = string.Empty;
        PropSizeH.Text = string.Empty;
        PropStretch.IsChecked = false;
        ScaleValueText.Text = "—";
        AngleValueText.Text = "—";

        AnimNameField.Text = string.Empty;
        AnimSpeedField.Text = string.Empty;
        AnimLoopToggle.IsChecked = false;
        AnimKindField.SelectedIndex = 0;
        AnimEnabledToggle.IsChecked = false;
        SpineAssetField.SelectedItem = null;
        SpineAnimationField.ItemsSource = Array.Empty<string>();
        UpdateAnimationControlState();

        PropVehicle.IsChecked = false;
        PropTrafficRole.SelectedIndex = -1;
        PropStopLine.Text = string.Empty;
        PropYellowAsRed.IsChecked = false;
        RefreshLightBindList();
    }

    /// <summary>
    /// 「遮挡带」下面那行小字：把选中贴图当前的遮挡口径摊开——哪个带、立体带的脚底 Y、
    /// 层序兜底值，以及依据是图层类型/名字自动判定还是属性面板里手定的。
    /// </summary>
    private void UpdateBandInfo()
    {
        var item = Canvas.Selected;
        if (item is null)
        {
            PropBandInfo.Text = string.Empty;
            PropBandInfoBox.IsVisible = false;
            return;
        }

        var band = LayerBands.Of(item);
        var orderBase = LayerOrder.OrderBase(item);
        var slot = LayerOrder.SlotOf(item);
        var source = item.BandOverride < 0
            ? "按图层判定"
            : item.BandOverrideAuto && item.BandOverride == LayerBands.Standing
                ? "自动提层（可手改）"
                : "遮挡带手改";
        var foot = LayerBands.Baseline(item);
        var placement = band switch
        {
            LayerBands.Ground => "最底 · 按层序",
            LayerBands.Overlay => "最上 · 按层序",
            _ => $"脚底 Y {foot:0} · 越靠下越前",
        };
        var layer = LayerOrder.EffectiveLayer(item);

        PropBandInfo.Text = $"{LayerBands.DisplayName(band)}（{source}）· {placement}\n"
            + $"层序兜底 sortOrder {item.SortOrder}（层基线 {orderBase} · 第 {slot} 档）\n"
            + $"图层 {layer} · 显示层 {MapLayerKeys.DisplayName(layer)} · PSD 层 {item.Name} · {item.Width}×{item.Height} 格";
        PropBandInfoBox.IsVisible = true;
    }

    /// <summary>
    /// 「图层」下拉直接给八层，打开时就高亮这张图在地图里的正式层级（手选层优先、否则归属层），
    /// 与「物品元件编辑」的层级档同一套口径，不再出现「跟随图层」这种伪选项。
    /// 八层只用于表达归属与 Unity 数据，画面前后由「带 + 脚底 Y」决定。
    /// </summary>
    private void ApplyLayerOptions()
    {
        PropDisplayLayer.ItemsSource = TemplateLayers.DisplayChoices;
    }

    /// <summary>
    /// 下拉该高亮哪一项：手选过层就是那一层；没手选（跟随图层）时直接指向图层类型推出来的
    /// 「正式层级」，打开下拉看到的就是它。
    /// </summary>
    private static int LayerSelectionIndex(PlacedObject item)
    {
        var display = TemplateLayers.ResolveDisplay(item.DisplayLayer, item.LayerType);
        for (var index = 0; index < TemplateLayers.DisplayChoices.Count; index++)
        {
            if (TemplateLayers.DisplayChoices[index] == display)
            {
                return index;
            }
        }

        return 0;
    }

    private void SetPropertyPanelEnabled(bool enabled)
    {
        PropertyBody.IsEnabled = enabled;
        BtnDeleteObject.IsEnabled = enabled;
        ObjectEditGroup.IsEnabled = enabled;
    }

    /// <summary>右栏「物体编辑」分组显隐：由属性面板的「编辑」勾选框控制，分隔线与标题一起收起。</summary>
    private void UpdateObjectEditVisibility()
    {
        if (ObjectEditSection.IsVisible == _showObjectEdit)
        {
            return;
        }

        ObjectEditSection.IsVisible = _showObjectEdit;
        TrafficScroll.Offset = default;
    }

    private void BuildPathList()
    {
        PathPointList.Children.Clear();

        var item = Canvas.Selected;
        if (item is null)
        {
            PathPointList.Children.Add(new TextBlock
            {
                Text = "选中任意已放置物件后可编辑移动路径。",
                Foreground = HintBrush,
                Margin = new Avalonia.Thickness(2, 4),
            });
            return;
        }

        if (item.PathPoints.Count == 0)
        {
            PathPointList.Children.Add(new TextBlock
            {
                Text = Canvas.PathDrawing
                    ? "暂无路径点：左键点地面即可加点；点到其他物件会忽略，不会切换选中。"
                    : "暂无路径点，点击「开始绘制路径」后在画布上逐点绘制。",
                Foreground = HintBrush,
                Margin = new Avalonia.Thickness(2, 4),
            });
            return;
        }

        for (int index = 0; index < item.PathPoints.Count; index++)
        {
            var point = item.PathPoints[index];
            var row = new Grid
            {
                Margin = new Avalonia.Thickness(0, 1),
            };

            row.ColumnDefinitions.Add(new ColumnDefinition(40, GridUnitType.Pixel));
            row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(60, GridUnitType.Pixel));

            var label = new TextBlock
            {
                Text = $"P{index}",
                Foreground = HintBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var value = new TextBlock
            {
                Text = $"{point.X:0}, {point.Y:0}　格 {point.GridX},{point.GridY}",
                Foreground = TextBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var remove = new Button
            {
                Content = "×",
                Width = 24,
                Height = 22,
                Padding = new Avalonia.Thickness(0),
            };

            var capturedIndex = index;
            var owner = item;
            remove.Click += (_, _) =>
            {
                if (capturedIndex < owner.PathPoints.Count)
                {
                    owner.PathPoints.RemoveAt(capturedIndex);
                    Canvas.Refresh();
                    Canvas.NotifyDocumentChanged();
                }
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(value, 1);
            Grid.SetColumn(remove, 2);
            row.Children.Add(label);
            row.Children.Add(value);
            row.Children.Add(remove);

            PathPointList.Children.Add(row);
        }
    }

    private void RefreshSceneFields()
    {
        _suppressSync = true;
        SceneNameField.Text = _config.SceneName;
        SceneIdField.Text = _config.SceneId;
        MapWidthField.Text = _config.Width.ToString(CultureInfo.InvariantCulture);
        MapHeightField.Text = _config.Height.ToString(CultureInfo.InvariantCulture);
        DrawShapeField.SelectedIndex = _config.DrawShape == DrawShapes.Diamond ? 1 : 0;
        RegionTypeField.SelectedIndex = _config.RegionType switch
        {
            RegionTypes.Forbid => 1,
            RegionTypes.Common => 2,
            _ => 0,
        };
       RefreshSceneHint();
        _suppressSync = false;
    }

    /// <summary>场景设置提示行（默认文案）：格子尺寸与预制体名，随场景数据刷新覆盖临时消息。</summary>
    private void RefreshSceneHint()
    {
        SceneHint.Text = $"格子 {_config.GridSize}px　预制体 {(_config.BgName.Length == 0 ? "未设置" : _config.BgName)}";
    }

    private void RefreshStatus()
    {
        StatusZoom.Text = $"{Canvas.ZoomPercent}%";
        StatusTileCount.Text = _config.Objects.Count.ToString(CultureInfo.InvariantCulture);
        StatusGridSize.Text = $"{_config.GridCols}×{_config.GridRows}";
        StatusSavePath.Text = _saveDirectory;
    }

    #endregion

    #region 地图配置

    /// <summary>打开地图配置弹窗：确定后落盘，并把预览目录与基线步长广播到资源索引 / 层级重排。</summary>
    private async Task OpenMapConfigAsync()
    {
        var dialog = new MapConfigWindow(EditorSettings.Current, _assets.ProjectRoot);
        var result = await dialog.ShowDialog<EditorSettingsData?>(this);
        if (result is null)
        {
            SetMessage("地图配置已取消，设置未改动");
            return;
        }

        EditorSettings.Apply(result);
        if (!_settingsStore.TrySave(result, out var error))
        {
            SetMessage($"地图配置写入失败：{error}");
            return;
        }

        ApplyPreviewDirectories();
        LoadChapterCatalog();
        BuildChapters();

        // 地图保存目录可能换了：资源整理的重扫根、资源索引、画布贴图缓存一起跟上
        var resourceDirectory = ResolveResourceDirectory();
        if (Directory.Exists(resourceDirectory))
        {
            _assets.AddSearchRoots(new[] { resourceDirectory });
        }

        _resourcePanel?.SetMapDirectory(resourceDirectory);
        _itemTable = LoadItemElementTable();
        _resourcePanel?.SetItemTable(_itemTable);
        _itemPanel?.SetItemTable(_itemTable);
        _pendingTemplate = null;
        Canvas.PendingTemplate = null;
        BuildResourceList();
        _assets.ClearCache();
        Canvas.ClearSpriteCache();

        if (_itemTable is null)
        {
            SetMessage($"地图配置已保存，但「地图物件配置表文件夹」读不到：{EditorSettings.Current.ItemElementTablesDirectory}");
        }

        RefreshStatus();
        Canvas.Refresh();
        if (_itemTable is not null)
        {
            SetMessage($"地图配置已保存：基线步长 {result.BaselineStep}；章节表 {_chapters.Chapters.Count} 章 / {_chapters.Levels.Count} 关；物件配置表 {_itemTable.RowCount} 行（{_itemTable.Files.Count} 张表）");
        }
    }

    /// <summary>把配置里的两个物体预览图目录与分区背景图目录解析成绝对路径后追加进资源索引。</summary>
    private void ApplyPreviewDirectories()
    {
        var projectRoot = _assets.ProjectRoot;
        if (projectRoot is null)
        {
            return;
        }

        var settings = EditorSettings.Current;
        var directories = new[]
        {
            settings.ObjectPreviewPrefabsDirectory,
            settings.ObjectPreviewTexturesDirectory,
            settings.LevelBackgroundDirectory,
            // 烘焙产物（地图保存目录）也要进索引：重开已保存地图时 image 才解析得到
            settings.MapSaveDirectory,
        };

        _assets.AddSearchRoots(directories
            .Select(path => EditorSettings.ResolveProjectPath(path, projectRoot))
            .Where(path => path is not null)
            .Select(path => path!));
    }

    /// <summary>
    /// 加载「地图物件配置表文件夹」。目录取 <c>ItemElementTablesDirectory</c>，
    /// 相对路径按 Unity 口径解析（<c>../Docs/…</c> 相对 Assets）；夹内 CSV + xlsm 一起读，读不到返回 null。
    /// </summary>
    /// <returns>表集合，失败为 null。</returns>
    private ItemConfigSet? LoadItemElementTable()
    {
        var directory = EditorSettings.Current.ItemElementTablesDirectory;
        var absolute = EditorSettings.ResolveProjectPath(directory, _assets.ProjectRoot);

        // 旧存档只填了单张 CSV：用它的父目录迁移
        if (string.IsNullOrWhiteSpace(absolute) || !Directory.Exists(absolute))
        {
            var legacy = EditorSettings.ResolveProjectPath(EditorSettings.Current.ItemElementCsvPath, _assets.ProjectRoot);
            if (!string.IsNullOrWhiteSpace(legacy) && File.Exists(legacy))
            {
                absolute = Path.GetDirectoryName(legacy);
            }
        }

        return ItemConfigSet.LoadDirectory(absolute);
    }

    #endregion
}
