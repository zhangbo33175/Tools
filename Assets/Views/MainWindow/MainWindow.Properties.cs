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

    /// <summary>
    /// 把当前选中实例的完整状态回填到右侧属性区，并按选中状态启用 / 清空控件。
    /// <para>回填期间用 <c>_suppressSync</c> 防止控件事件反向写回实例；交通标记先统一同步再刷新 UI。</para>
    /// </summary>
    private void SyncProperties()
    {
        TrafficMarkService.SyncObjectMarks(_config);
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
            ResetRoomFields();
            PropBandInfo.Text = string.Empty;
            PropBandInfoBox.IsVisible = false;
            PropSceneIndexBadge.IsVisible = false;
            SetPropertyPanelEnabled(false);
            _suppressSync = false;
            RefreshTrafficUi();
            return;
        }

        SetPropertyPanelEnabled(true);

        ObjEditTitle.Text = $"对象编辑：{item.Name}";
        // ID 框放物件自己的 8 位数字 ID（全图唯一、可对外引用）；配置表数字 ID 收进右侧徽标
        var numericId = ResolveNumericId(item, out var idFromConfig);
        PropTemplateId.Text = item.Id;
        ApplyIdSourceBadge(idFromConfig, numericId);
        ApplySceneIndexBadge(item);
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
        PropSpanLevels.IsChecked = item.SpanLevels;
        SyncRoomFields(item);
        PropTraffic.IsChecked = item.Interactable;
        PropBubble.Text = item.BubbleId.ToString(CultureInfo.InvariantCulture);
        PropDialogBubble.Text = item.DialogBubble;

        // 物体编辑区回填：位置 / 旋转 / 大小 / 是否缩放
        var bounds = item.Bounds;
        EditAngleText.Text = $"{item.Rotation:0.##}°";
        PropPosX.Text = EditorNumberText.Compact(item.X);
        PropPosY.Text = EditorNumberText.Compact(item.Y);
        PropSizeW.Text = bounds.Width.ToString("0", CultureInfo.InvariantCulture);
        PropSizeH.Text = bounds.Height.ToString("0", CultureInfo.InvariantCulture);
        PropStretch.IsChecked = item.Stretch;

        // 碰撞 / 阴影区域与变换回填
        SyncAreaFields(item);
        ScaleValueText.Text = $"{(int)Math.Round(item.Scale * 100)}%";
        AngleValueText.Text = $"{item.Rotation:0.##}°";
        UpdateAreaSectionVisibility();

        AnimNameField.Text = item.AnimName;
        AnimSpeedField.Text = EditorNumberText.Compact(item.Speed);
        AnimLoopToggle.IsChecked = item.Loop;
        RefreshAnimationEditor(item);

        // 交通规则区回填
        PropVehicle.IsChecked = item.IsVehicle;
        SelectComboByText(PropTrafficRole, TrafficRoles.ToDisplay(item.TrafficRole));
        PropStopLine.Text = $"{item.StopLineX:0}, {item.StopLineY:0}";
        PropYellowAsRed.IsChecked = item.YellowTreatAsRed;
        RefreshTrafficUi();

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

    /// <summary>ID 右侧来源角标：显示配置表数字 ID；PSD 回退时显示切片序号，两者都没有显示「无」。</summary>
    /// <param name="fromConfig">是否来自配置表。</param>
    /// <param name="numericId">配置表行 ID 或切片序号。</param>
    private void ApplyIdSourceBadge(bool fromConfig, string numericId)
    {
        var missing = string.IsNullOrWhiteSpace(numericId) || numericId == "—";
        PropIdSourceBadge.IsVisible = true;
        PropIdSourceText.Text = missing ? "无" : numericId;
        var style = UiTheme.IdSource.For(missing, fromConfig);
        PropIdSourceText.Foreground = style.Text;
        PropIdSourceBadge.Background = style.Surface;
        PropIdSourceBadge.BorderBrush = style.Border;
        ToolTip.SetTip(PropIdSourceBadge, missing
            ? "配置表与 PSD 切片清单都没有命中，这个物件没有配置数字 ID"
            : fromConfig
                ? $"配置表数字 ID {numericId}：表示这是哪一种物件，同一种摆多处时共用一个"
                : $"配置表无此行，{numericId} 是 PSD 切片序号");
    }

    /// <summary>同模板摆多个时补一个位次徽标：数字 ID 只到「哪一种」，<c>#07</c> 指「第几个」。</summary>
    /// <param name="item">当前选中实例。</param>
    private void ApplySceneIndexBadge(PlacedObject item)
    {
        var slot = SceneObjectIndex.Locate(_config.Objects, item, SceneKeyOf);
        PropSceneIndexBadge.IsVisible = slot.HasPeers;
        PropSceneIndexText.Text = slot.HasPeers ? $"#{slot.Index:00}" : string.Empty;
        if (slot.HasPeers)
        {
            ToolTip.SetTip(PropSceneIndexBadge, $"同一种物件共 {slot.Total} 个，这是第 {slot.Index} 个（按实例 ID 排序）");
        }
    }

    /// <summary>位次分组键：数字 ID 优先，拿不到数字 ID 时退回类型 + 模板 ID。</summary>
    /// <param name="item">物件实例。</param>
    /// <returns>分组键。</returns>
    private string SceneKeyOf(PlacedObject item)
    {
        var numeric = ResolveNumericId(item, out _);
        return numeric.Length == 0 || numeric == "—" ? SceneObjectIndex.KeyOf(item) : numeric;
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
            using var document = JsonDocument.Parse(FileSystemIo.ReadText(path));
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
        PropLayerTagDot.Background = MapLayerPalette.BrushOf(key);
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
        PropLayerTagDot.Background = UiTheme.Chrome.EmptyLayerDot;
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
        PropSpanLevels.IsChecked = false;
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
        SpineWalkAnimationField.ItemsSource = Array.Empty<string>();
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
            PropBandOrderInfo.Text = string.Empty;
            PropBandLayerInfo.Text = string.Empty;
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

        PropBandInfo.Text = $"{(_config?.LevelName(item.Level) ?? $"{item.Level + 1}F")} · {LayerBands.DisplayName(band)}（{source}）· {placement}";
        PropBandOrderInfo.Text = $"层序兜底 sortOrder {item.SortOrder}（层基线 {orderBase} · 第 {slot} 档）";
        PropBandLayerInfo.Text = $"图层 {layer} · 显示层 {MapLayerKeys.DisplayName(layer)} · PSD 层 {item.Name} · {item.Width}×{item.Height} 格";
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

    /// <summary>路径点行里需要跟着选中态单独刷新的控件；只在选中态变化时复用，不重建行。</summary>
    /// <param name="Row">行外框，负责底色与左侧强调线。</param>
    /// <param name="Label">序号文本，选中时改为强调色。</param>
    /// <param name="Index">该行对应的路径点序号。</param>
    private sealed record PathPointRowSlot(Border Row, TextBlock Label, int Index);

    private readonly List<PathPointRowSlot> _pathPointRows = new();

    private void BuildPathList()
    {
        PathPointList.Children.Clear();
        _pathPointRows.Clear();

        var item = Canvas.Selected;
        if (item is null)
        {
            ShowPathEmpty("选中任意已放置物件后可编辑移动路径。");
            return;
        }

        if (item.PathPoints.Count == 0)
        {
            ShowPathEmpty(Canvas.PathDrawing
                ? "正在绘制：左键点地面即可加点；点到其他物件会忽略，不会切换选中。"
                : "暂无路径点，点「开始绘制路径」后在画布上逐点绘制。");
            return;
        }

        PathEmptyHint.IsVisible = false;

        for (int index = 0; index < item.PathPoints.Count; index++)
        {
            PathPointList.Children.Add(BuildPathPointRow(item, index));
        }
    }

    /// <summary>
    /// 构建一行路径点：序号、可直接改的坐标输入框、上移 / 下移 / 删除。
    /// 行与画布上的选中点双向联动：点画布上的点会高亮这一行，点这一行也会选中画布上的点。
    /// </summary>
    /// <param name="owner">路径点所属物件。</param>
    /// <param name="index">路径点序号。</param>
    /// <returns>行控件。</returns>
    private Control BuildPathPointRow(PlacedObject owner, int index)
    {
        var point = owner.PathPoints[index];
        var selected = Canvas.SelectedPathIndex == index;

        var label = new TextBlock
        {
            Text = $"P{index}",
            FontSize = 10.5,
            FontFamily = UiFonts.Mono,
            Foreground = selected ? UiTheme.Accent : UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var coord = new TextBox
        {
            Text = $"{point.X:0}, {point.Y:0}",
            FontSize = 10.5,
            FontFamily = UiFonts.Mono,
            Height = 22,
            MinHeight = 22,
            Padding = new Thickness(5, 0),
            CornerRadius = new CornerRadius(UiMetrics.SmallCornerRadius),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(coord, "改坐标后按回车生效；格式 x, y（世界像素）");

        // 输入框会吃掉指针事件，行上的选中就靠「拿到焦点」补一手；这里只刷新列表配色不重建，
        // 所以刚点进来的输入框不会被自己触发的高亮刷新夺走焦点。
        coord.GotFocus += (_, _) => Canvas.SelectPathPoint(index);
        UiFactory.CommitOnEnter(coord, () =>
        {
            if (TryParsePathPoint(coord.Text, out var world))
            {
                Canvas.SelectPathPoint(index);
                if (Canvas.MovePathPoint(index, world))
                {
                    SetMessage($"P{index} 已改为（{owner.PathPoints[index].X:0},{owner.PathPoints[index].Y:0}）。");
                }
                else
                {
                    coord.Text = $"{owner.PathPoints[index].X:0}, {owner.PathPoints[index].Y:0}";
                }
            }
            else
            {
                // 解析失败要把输入框退回当前值，否则框里会留着一段非法文本，用户下次回车还是错
                coord.Text = $"{owner.PathPoints[index].X:0}, {owner.PathPoints[index].Y:0}";
                SetMessage($"坐标格式应为「x, y」，例如 {owner.PathPoints[index].X:0}, {owner.PathPoints[index].Y:0}");
            }
        });

        var up = CompactIconButton("↑", index > 0);
        up.Click += (_, _) => Canvas.ReorderPathPoint(index, index - 1);

        var down = CompactIconButton("↓", index < owner.PathPoints.Count - 1);
        down.Click += (_, _) => Canvas.ReorderPathPoint(index, index + 1);

        var remove = CompactIconButton("×", true);
        remove.Foreground = UiTheme.ResourceTable.DeleteText;
        remove.Click += (_, _) => Canvas.RemovePathPointAt(index);

        var row = new Border
        {
            Padding = new Thickness(4, 1),
            CornerRadius = new CornerRadius(UiMetrics.SmallCornerRadius),
            Background = UiTheme.Transparent,
            BorderBrush = UiTheme.Transparent,
            BorderThickness = new Thickness(0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("24,*,20,20,20"),
                ColumnSpacing = 3,
                Children =
                {
                    UiFactory.InCell(label, 0, 0),
                    UiFactory.InCell(coord, 0, 1),
                    UiFactory.InCell(up, 0, 2),
                    UiFactory.InCell(down, 0, 3),
                    UiFactory.InCell(remove, 0, 4),
                },
            },
        };

        RowState.Apply(row, selected, hovered: false, UiTheme.PathPointRow);
        RowState.AttachHover(row, hover =>
        {
            RowState.Apply(row, Canvas.SelectedPathIndex == index, hover, UiTheme.PathPointRow);
        });

        // 点行（含序号与空白处）即选中这个点；输入框和按钮自己吃掉点击，不会误触选中。
        row.PointerPressed += (_, _) => Canvas.SelectPathPoint(index);

        _pathPointRows.Add(new PathPointRowSlot(row, label, index));
        return row;
    }

    /// <summary>
    /// 只刷新路径点行的「选中态」，不重建行控件。
    /// 重建会连带销毁正在编辑的坐标输入框（焦点与光标位置全丢），而选中态只是换底色和序号颜色。
    /// </summary>
    private void RefreshPathPointSelection()
    {
        foreach (var slot in _pathPointRows)
        {
            var selected = Canvas.SelectedPathIndex == slot.Index;
            slot.Label.Foreground = selected ? UiTheme.Accent : UiTheme.TextSecondary;
            RowState.Apply(slot.Row, selected, false, UiTheme.PathPointRow);
        }
    }

    /// <summary>列表行里的小图标按钮：统一走紧凑按钮骨架，只覆盖尺寸与内边距。</summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="enabled">是否可用。</param>
    /// <returns>按钮控件。</returns>
    private static Button CompactIconButton(string text, bool enabled)
    {
        var button = UiFactory.CompactButton(text, padding: new Thickness(0));
        button.Width = 20;
        button.Height = 20;
        button.MinWidth = 20;
        button.MinHeight = 20;
        button.FontSize = UiMetrics.FontCaption;
        button.IsEnabled = enabled;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        return button;
    }

    /// <summary>解析路径点坐标文本：接受「x, y」「x，y」「x y」三种写法，只认十进制实数。</summary>
    /// <param name="text">用户输入。</param>
    /// <param name="world">解析出的世界坐标。</param>
    /// <returns>解析成功返回 true。</returns>
    private static bool TryParsePathPoint(string? text, out Point world)
    {
        world = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = Regex.Split(text.Trim(), "[,，\\s]+");
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        world = new Point(x, y);
        return true;
    }

    /// <summary>路径列表为空时显示居中的空态文案；动画页签激活时保持隐藏。</summary>
    /// <param name="text">空态文案。</param>
    private void ShowPathEmpty(string text)
    {
        PathEmptyHint.Text = text;
        PathEmptyHint.IsVisible = PathScroll.IsVisible;
    }

    private void RefreshSceneFields()
    {
        _suppressSync = true;
        SceneNameField.Text = _config.SceneName;
        SceneIdField.Text = _config.SceneId;
        MapWidthField.Text = _config.Width.ToString(CultureInfo.InvariantCulture);
        MapHeightField.Text = _config.Height.ToString(CultureInfo.InvariantCulture);
        DrawShapeField.SelectedIndex = ShapeIndexOf(_config.DrawShape);
        RegionTypeField.SelectedIndex = _config.RegionType switch
        {
            RegionTypes.Forbid => 1,
            RegionTypes.Common => 2,
            _ => 0,
        };
        RefreshLevelField();
        RefreshSceneHint();
        _suppressSync = false;
    }

    /// <summary>场景设置提示行（默认文案）：格子尺寸、换算格数与预制体名，随场景数据刷新覆盖临时消息。</summary>
    private void RefreshSceneHint()
    {
        var cells = _config.GridSize > 0
            ? $" = {_config.GridCols}×{_config.GridRows} 格"
            : string.Empty;
        SceneHint.Text = $"格子 {_config.GridSize}px{cells}　预制体 {(_config.BgName.Length == 0 ? "未设置" : _config.BgName)}";
    }

    private void RefreshStatus()
    {
        StatusZoom.Text = $"{Canvas.ZoomPercent}%";
        StatusTileCount.Text = _config.Objects.Count.ToString(CultureInfo.InvariantCulture);
        StatusGridSize.Text = FormatGridCount();
        StatusMapSize.Text = $"{_config.Width}×{_config.Height}";
        StatusSavePath.Text = _saveDirectory;

        RefreshEmptyMapOverlay();
    }

    /// <summary>
    /// 状态栏格数：菱形按「错缝铺满」口径汇报并去掉补边格（<see cref="MapConfigData.GridCols"/> 多算的那一格
    /// 是为覆盖边缘），否则 4096px 地图会显示成 129×513 这种看着像出错的大数字。
    /// </summary>
    /// <returns>形如 <c>128×128</c> 或 <c>128×512·菱形</c> 的文本。</returns>
    private string FormatGridCount()
    {
        if (_config.GridSize <= 0)
        {
            return "—";
        }

        var shape = _config.DrawShape;
        if (!DrawShapes.IsDiamond(shape) && !DrawShapes.IsHex(shape))
        {
            return $"{_config.GridCols}×{_config.GridRows}";
        }

        var cols = Math.Max(0, _config.GridCols - 1);
        var rows = Math.Max(0, _config.GridRows - 1);
        var tag = DrawShapes.IsHex(shape)
            ? "·六边形"
            : shape == DrawShapes.DiamondZ ? "·等距Z" : "·菱形";
        return $"{cols}×{rows}{tag}";
    }

    /// <summary>绘制形状 → 左下「摆放模式」下拉索引（顺序与 <c>MainWindow.axaml</c> 的 ComboBoxItem 一致）。</summary>
    /// <param name="drawShape">绘制形状值。</param>
    /// <returns>下拉索引。</returns>
    private static int ShapeIndexOf(string drawShape) => drawShape switch
    {
        DrawShapes.Hex => 1,
        DrawShapes.DiamondZ => 3,
        DrawShapes.Diamond => 2,
        _ => 0,
    };

    /// <summary>左下「摆放模式」下拉索引 → 绘制形状值。</summary>
    /// <param name="index">下拉索引。</param>
    /// <returns>绘制形状值。</returns>
    private static string ShapeAtIndex(int index) => index switch
    {
        1 => DrawShapes.Hex,
        2 => DrawShapes.Diamond,
        3 => DrawShapes.DiamondZ,
        _ => DrawShapes.Rect,
    };

    /// <summary>
    /// 空地图占位层的唯一显隐入口。
    /// <para>
    /// 只在「编辑器还没进入任何关卡」时显示：没有从章节树选中关卡、没有载入地图存档、画布上也没有物件。
    /// 一旦选中关卡（哪怕该关卡还没有存档、只是开了一张空图）就隐藏，右侧画布立刻成为可编辑的空地图；
    /// 否则会出现「左侧关卡 2 已高亮、状态栏说已开空图，画布却仍停在 No map loaded」的观感错位。
    /// </para>
    /// </summary>
    private void RefreshEmptyMapOverlay()
    {
        var enteredLevel = !string.IsNullOrWhiteSpace(_selectedLevelId);
        EmptyMapOverlay.IsVisible = !enteredLevel
                                    && string.IsNullOrWhiteSpace(_currentMapPath)
                                    && _config.Objects.Count == 0;
    }

    #endregion

    #region 地图配置

    /// <summary>
    /// 打开网格设置弹窗。编辑口径（边长 / 形状 / 网格线）确定后立即作用到当前画布，
    /// Unity 导出口径（Cell Size / Gap / Layout / Swizzle / Anchor）只落盘，供发布时生成 Grid 与 Tilemap。
    /// </summary>
    private async Task OpenGridSettingsAsync()
    {
        // 画布是唯一真相源：先取当前地图实际生效的边长 / 形状 / 网格线当种子，
        // 否则上次存档与新开的地图不一致时，窗口一确定就会把画布改回旧值
        var seed = EditorSettings.Current.Clone();
        if (Canvas.Config is { } live)
        {
            seed.GridSize = live.GridSize;
            seed.GridShape = GridShapes.Normalize(live.DrawShape);
        }

        seed.ShowGridLines = Canvas.ShowGrid;

        // 地图尺寸也交给这个窗：写进 _config（随地图存档），不是编辑器设置
        var mapWidth = Canvas.Config?.Width ?? MapToolDefaults.LevelPixelSize;
        var mapHeight = Canvas.Config?.Height ?? MapToolDefaults.LevelPixelSize;

        var dialog = new GridSettingsWindow(seed, mapWidth, mapHeight);
        var result = await dialog.ShowDialog<GridSettingsResult?>(this);
        if (result is null)
        {
            SetMessage("网格设置已取消，参数未改动");
            return;
        }

        EditorSettings.Apply(result.Settings);
        if (!_settingsStore.TrySave(result.Settings, out var error))
        {
            SetMessage(UiFeedback.Failed("网格设置写入", error));
            return;
        }

        ApplyGridToCanvas(result.Settings);
        ApplyMapSizeToCanvas(result.MapWidth, result.MapHeight);
        SetMessage($"网格已更新：{result.MapWidth}×{result.MapHeight}px · {result.Settings.GridSize}px · " +
                   $"{GridShapes.LabelOf(result.Settings.GridShape)} · " +
                   $"Unity Cell {result.Settings.CellSizeX}×{result.Settings.CellSizeY} / {result.Settings.CellLayout}");
    }

    /// <summary>
    /// 地图尺寸的唯一写入口：场景属性宽/高、网格设置窗「地图尺寸」、以及之后新增的入口都走这里，
    /// 保证「钳制范围 / 画布刷新 / 两处输入框回填」只有一份实现。
    /// 物件坐标是绝对像素，不随尺寸缩放：改小后越界部分只是不可见。
    /// </summary>
    /// <param name="width">地图宽（px）。</param>
    /// <param name="height">地图高（px）。</param>
    private void ApplyMapSizeToCanvas(int width, int height)
    {
        if (Canvas.Config is not { } config)
        {
            return;
        }

        var nextWidth = Math.Clamp(width, MapToolDefaults.MapPixelMin, MapToolDefaults.MapPixelMax);
        var nextHeight = Math.Clamp(height, MapToolDefaults.MapPixelMin, MapToolDefaults.MapPixelMax);

        if (config.Width != nextWidth || config.Height != nextHeight)
        {
            config.Width = nextWidth;
            config.Height = nextHeight;
            Canvas.Refresh();
            RefreshStatus();
        }

        // 改没改都要回填：手输 100 会被钳到 256，输入框得显示真实值，不能留着用户的原话
        RefreshSceneFields();
    }

    /// <summary>
    /// 把编辑口径同步到当前画布：边长写 <c>GridSize</c>，形状写 <c>DrawShape</c>，
    /// 网格线开关写 <c>ShowGrid</c>；未加载地图时只改开关并重绘。
    /// </summary>
    /// <param name="settings">刚保存的设置。</param>
    private void ApplyGridToCanvas(EditorSettingsData settings)
    {
        Canvas.ShowGrid = settings.ShowGridLines;

        if (Canvas.Config is { } config)
        {
            config.GridSize = settings.GridSize;
            config.DrawShape = GridShapes.Normalize(settings.GridShape);
        }

        SyncGridControls(settings);
        Canvas.Refresh();
    }

    /// <summary>
    /// 把网格设置回写到主界面上的同名控件：顶部「摆放网格层」与左下「摆放模式」的形状下拉。
    /// 这两处本来就是同一个状态的两处入口，不同步就会出现「设置里菱形、左下显示矩形」。
    /// </summary>
    /// <param name="settings">刚保存的设置。</param>
    private void SyncGridControls(EditorSettingsData settings)
    {
        _suppressSync = true;
        try
        {
            LayerPlaceGrid.IsChecked = settings.ShowGridLines;
            DrawShapeField.SelectedIndex = ShapeIndexOf(GridShapes.Normalize(settings.GridShape));
        }
        finally
        {
            _suppressSync = false;
        }
    }

    /// <summary>
    /// 左下「摆放模式」切换形状时回写设置：下次打开网格设置直接看到同一形状，
    /// 新建地图也沿用这个默认，避免两处各存一份、越用越偏。
    /// </summary>
    /// <param name="drawShape">画布当前的 <see cref="DrawShapes"/> 值。</param>
    private void PersistGridShape(string drawShape)
    {
        var shape = GridShapes.Normalize(drawShape);
        if (string.Equals(EditorSettings.Current.GridShape, shape, StringComparison.Ordinal))
        {
            return;
        }

        var next = EditorSettings.Current.Clone();
        next.GridShape = shape;
        next.CellLayout = GridShapes.LayoutOf(shape);
        EditorSettings.Apply(next);

        // 落盘失败只影响「下次启动的默认值」，不该打断当前编辑
        _settingsStore.TrySave(next, out _);
    }

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
            SetMessage(UiFeedback.Failed("地图配置写入", error));
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
        _storyPanel?.SetItemTable(_itemTable);
        Canvas.ItemTable = _itemTable;
        Canvas.Refresh();
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

