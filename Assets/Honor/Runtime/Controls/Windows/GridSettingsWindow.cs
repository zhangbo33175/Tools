/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GridSettingsWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   网格设置弹窗：左侧编辑口径（格子边长 / 形状 / 网格线），
 *            右侧 Unity 导出口径（Cell Size / Cell Gap / Cell Layout /
 *            Cell Swizzle / Tile Anchor），下方实时网格预览。
 *            字段口径与 Unity Grid 组件一一对应，画布侧只用编辑口径。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

#region 类定义

/// <summary>
/// 网格设置窗的返回结果：网格口径 + 地图像素尺寸。
/// 边长 / 形状属于编辑器设置，地图宽高属于当前地图，两者同窗编辑但落点不同。
/// </summary>
/// <param name="Settings">确定后的网格与 Unity 导出口径。</param>
/// <param name="MapWidth">地图宽（px）。</param>
/// <param name="MapHeight">地图高（px）。</param>
public sealed record GridSettingsResult(EditorSettingsData Settings, int MapWidth, int MapHeight);

/// <summary>
/// 网格设置弹窗。分三组参数：
/// <list type="bullet">
/// <item><description>地图尺寸：整张地图的宽高（px），随地图存档保存，决定图的物理边界；</description></item>
/// <item><description>画布口径：格子边长 / 网格形状 / 是否显示网格线，确定后直接作用到画布；</description></item>
/// <item><description>导出口径：与 Unity Grid 组件同名同义，发布时按它生成 Grid 与 Tilemap。</description></item>
/// </list>
/// 右侧是随参数实时重绘的网格预览，避免「改完数字不知道长什么样」。
/// </summary>
public sealed class GridSettingsWindow : Window
{
    #region 常量

    /// <summary>弹窗宽度。</summary>
    private const double DialogWidth = 812.0;

    /// <summary>弹窗高度（内容固定，避免左右两栏高度不一致）。</summary>
    private const double DialogHeight = 764.0;

    /// <summary>地图宽高下限，与 <see cref="MapToolDefaults.MapPixelMin"/> 同源。</summary>
    private const int MapSizeMin = MapToolDefaults.MapPixelMin;

    /// <summary>地图宽高上限，与 <see cref="MapToolDefaults.MapPixelMax"/> 同源。</summary>
    private const int MapSizeMax = MapToolDefaults.MapPixelMax;

    /// <summary>左栏宽度：两行双值字段 + 单位注记刚好排满，不挤压右侧预览。</summary>
    private const double LeftColumnWidth = 398.0;

    /// <summary>左右两栏之间的留白。</summary>
    private const double ColumnGap = 12.0;

    /// <summary>标签列宽。</summary>
    private const double LabelWidth = 92.0;

    /// <summary>数字框高度。</summary>
    private const double FieldHeight = UiMetrics.FieldHeight;

    /// <summary>单值数字框宽度。</summary>
    private const double SingleFieldWidth = 76.0;

    /// <summary>双值行里每个数字框宽度。</summary>
    private const double PairFieldWidth = 72.0;

    /// <summary>双值行里 X / Y 前缀徽标列宽。</summary>
    private const double AxisBadgeWidth = 18.0;

    /// <summary>双值行里 X 框与 Y 徽标之间的间距。</summary>
    private const double AxisGap = 10.0;

    /// <summary>底部按钮宽度。</summary>
    private const double DialogButtonWidth = 78.0;

    /// <summary>底部主按钮宽度：比次按钮宽一点，作为落点。</summary>
    private const double PrimaryButtonWidth = 92.0;

    /// <summary>预览画布宽度（固定逻辑尺寸，缩放只改绘制比例）。</summary>
    private const double PreviewWidth = 336.0;

    /// <summary>预览画布高度。</summary>
    private const double PreviewHeight = 336.0;

    /// <summary>预览列数。</summary>
    private const int PreviewCols = 8;

    /// <summary>预览行数。</summary>
    private const int PreviewRows = 5;

    private static readonly IBrush CardBorderBrush = UiTheme.GridDialog.CardBorder;
    private static readonly IBrush FieldBorderBrush = UiTheme.GridDialog.FieldBorder;
    private static readonly IBrush PreviewCellFill = UiTheme.GridDialog.PreviewCellA;
    private static readonly IBrush PreviewCellAltFill = UiTheme.GridDialog.PreviewCellB;
    private static readonly IBrush PreviewStroke = UiTheme.GridDialog.PreviewStroke;

    #endregion

    #region 字段

    private readonly TextBox _gridSizeBox;
    private readonly ComboBox _shapeCombo;
    private readonly CheckBox _gridLinesCheck;

    private readonly TextBox _mapWidthBox;
    private readonly TextBox _mapHeightBox;
    private readonly TextBlock _mapSizeHint;

    /// <summary>打开窗口时的地图尺寸，供「恢复默认」还原。</summary>
    private readonly int _initialMapWidth;
    private readonly int _initialMapHeight;

    private readonly TextBox _cellSizeXBox;
    private readonly TextBox _cellSizeYBox;
    private readonly CheckBox _syncCellSizeCheck;
    private readonly TextBlock _syncCellSizeValue;
    private readonly TextBox _cellGapXBox;
    private readonly TextBox _cellGapYBox;
    private readonly ComboBox _layoutCombo;
    private readonly ComboBox _swizzleCombo;
    private readonly TextBox _anchorXBox;
    private readonly TextBox _anchorYBox;

    private readonly Canvas _preview;
    private readonly TextBlock _summary;
    private readonly TextBlock _previewBadge;
    private readonly TextBlock _hint;

    /// <summary>形状与 Cell Layout 联动时的重入开关，避免两边互相触发。</summary>
    private bool _syncingShape;

    /// <summary>Cell Size 跟随格子边长时的重入开关。</summary>
    private bool _syncingCellSize;

    #endregion

    #region 构造

    /// <summary>初始化弹窗。</summary>
    /// <param name="settings">当前设置，窗口内编辑的是它的副本。</param>
    /// <param name="mapWidth">当前地图宽（px）。</param>
    /// <param name="mapHeight">当前地图高（px）。</param>
    public GridSettingsWindow(EditorSettingsData settings, int mapWidth, int mapHeight)
    {
        Settings = settings.Clone();
        _initialMapWidth = Math.Clamp(mapWidth, MapSizeMin, MapSizeMax);
        _initialMapHeight = Math.Clamp(mapHeight, MapSizeMin, MapSizeMax);

        Title = "网格设置";
        Width = DialogWidth;
        Height = DialogHeight;
        SizeToContent = SizeToContent.Manual;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Light;

        _mapWidthBox = NumberField(_initialMapWidth.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _mapHeightBox = NumberField(_initialMapHeight.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _mapSizeHint = new TextBlock
        {
            Text = string.Empty,
            FontSize = UiMetrics.FontCaption,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(LabelWidth, -2, 0, 0),
        };

        _gridSizeBox = NumberField(Settings.GridSize.ToString(CultureInfo.InvariantCulture), SingleFieldWidth);
        _shapeCombo = OptionCombo(GridShapes.All, Settings.GridShape);
        _gridLinesCheck = new CheckBox
        {
            Content = "在画布上显示",
            IsChecked = Settings.ShowGridLines,
            FontSize = UiMetrics.FontTitle,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Margin = new Thickness(16, 0, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        _cellSizeXBox = NumberField(Settings.CellSizeX.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _cellSizeYBox = NumberField(Settings.CellSizeY.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _cellGapXBox = NumberField(Settings.CellGapX.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _cellGapYBox = NumberField(Settings.CellGapY.ToString(CultureInfo.InvariantCulture), PairFieldWidth);
        _layoutCombo = OptionCombo(GridLayouts.All, Settings.CellLayout);
        _swizzleCombo = OptionCombo(GridSwizzles.All, Settings.CellSwizzle);
        _anchorXBox = NumberField(Settings.TileAnchorX.ToString("0.##", CultureInfo.InvariantCulture), PairFieldWidth, allowDecimal: true);
        _anchorYBox = NumberField(Settings.TileAnchorY.ToString("0.##", CultureInfo.InvariantCulture), PairFieldWidth, allowDecimal: true);

        // 旧存档里 Cell Size 与边长不一致时默认不勾「跟随」，保住用户已经填过的引擎口径
        _syncCellSizeCheck = new CheckBox
        {
            Content = "Cell Size 跟随格子边长",
            IsChecked = Settings.CellSizeX == Settings.GridSize && Settings.CellSizeY == Settings.GridSize,
            FontSize = UiMetrics.FontTitle,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 右侧实时值做成白底小胶囊：勾选显示「32 × 32」，手填显示「手动填写」
        _syncCellSizeValue = new TextBlock
        {
            Text = string.Empty,
            FontSize = UiMetrics.FontBody,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.AccentDark,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        _preview = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
        };

        _summary = new TextBlock
        {
            Text = string.Empty,
            FontSize = UiMetrics.FontBody,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _previewBadge = new TextBlock
        {
            Text = string.Empty,
            FontSize = UiMetrics.FontCaption,
            Foreground = UiTheme.AccentDark,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _hint = new TextBlock
        {
            Text = string.Empty,
            FontSize = UiMetrics.FontBody,
            Foreground = UiTheme.Error,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 6, 2, 0),
        };

        HookPreviewRefresh();
        HookShapeLinkage();
        HookCellSizeLinkage();
        AlignShapeWithLayout();
        ApplyCellSizeLinkState();

        Content = BuildRoot();
        WindowChromeTheme.Attach(this, closeAction: () => Close(null));

        // 首次构造时预览区尚未布局，Bounds 为 0；开窗后再按真实尺寸重绘一次，保证网格居中
        Opened += (_, _) => RebuildPreview();
        RebuildPreview();
    }

    #endregion

    #region 键盘

    /// <summary>Esc 等同取消：弹窗里没有需要提交的半成品，直接关掉即可。</summary>
    /// <param name="e">键事件。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
            return;
        }

        base.OnKeyDown(e);
    }

    #endregion

    #region 公开属性

    /// <summary>窗口里编辑中的设置（确定后交给调用方）。</summary>
    public EditorSettingsData Settings { get; }

    #endregion

    #region 界面装配

    private Control BuildRoot()
    {
        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        var left = new StackPanel { Spacing = ColumnGap, Width = LeftColumnWidth };
        left.Children.Add(BuildEditorCard());
        left.Children.Add(BuildUnityCard());
        Grid.SetColumn(left, 0);
        columns.Children.Add(left);

        var right = BuildPreviewCard();
        right.Margin = new Thickness(ColumnGap, 0, 0, 0);
        Grid.SetColumn(right, 1);
        columns.Children.Add(right);

        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        Grid.SetRow(columns, 0);
        content.Children.Add(columns);
        Grid.SetRow(_hint, 1);
        content.Children.Add(_hint);
        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        content.Children.Add(footer);

        return new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.35, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(UiTheme.GridDialog.BackgroundTop, 0),
                    new GradientStop(UiTheme.GridDialog.BackgroundBottom, 1),
                },
            },
            Padding = new Thickness(16),
            Child = content,
        };
    }

    /// <summary>左卡一：画布实际生效的编辑口径。</summary>
    private Control BuildEditorCard()
    {
        var rows = new StackPanel { Spacing = UiMetrics.FieldGap };

        // 先定整张地图多大，再定一格多大：上面这行写进地图（随地图保存），下面写进编辑器设置
        rows.Children.Add(PairRow("地图尺寸：", _mapWidthBox, _mapHeightBox, "px", "W", "H"));
        rows.Children.Add(_mapSizeHint);

        var sizeRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},16,{SingleFieldWidth},Auto,*"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        sizeRow.Children.Add(UiFactory.FieldLabel("格子边长：", UiMetrics.FontTitle, UiTheme.TextSecondary, HorizontalAlignment.Right));
        Grid.SetColumn(_gridSizeBox, 2);
        sizeRow.Children.Add(_gridSizeBox);
        var unit = UnitText("px");
        Grid.SetColumn(unit, 3);
        sizeRow.Children.Add(unit);
        rows.Children.Add(sizeRow);

        rows.Children.Add(UiFactory.FieldRow("网格形状：", _shapeCombo, RowSkin, UiTheme.TextSecondary));
        rows.Children.Add(UiFactory.FieldRow("网格线：", _gridLinesCheck, RowSkin, UiTheme.TextSecondary));

        var hint = UiFactory.HintText(
            "只影响编辑画布：改边长按「地图宽 ÷ 边长」重算行列（8–256 px），Unity 的 Cell Size 随之同步。",
            11,
            wrap: true,
            margin: new Thickness(0, 2, 0, 0));
        rows.Children.Add(hint);

        return Card("画布网格", rows, "立即生效");
    }

    /// <summary>左卡二：与 Unity Grid 组件对齐的导出口径。</summary>
    private Control BuildUnityCard()
    {
        var rows = new StackPanel { Spacing = UiMetrics.FieldGap };

        rows.Children.Add(BuildSyncStrip());
        rows.Children.Add(PairRow("Cell Size：", _cellSizeXBox, _cellSizeYBox, "px"));
        rows.Children.Add(PairRow("Cell Gap：", _cellGapXBox, _cellGapYBox, "px"));
        rows.Children.Add(UiFactory.FieldRow("Cell Layout：", _layoutCombo, RowSkin, UiTheme.TextSecondary));
        rows.Children.Add(UiFactory.FieldRow("Cell Swizzle：", _swizzleCombo, RowSkin, UiTheme.TextSecondary));
        rows.Children.Add(PairRow("Tile Anchor：", _anchorXBox, _anchorYBox, "0–1"));

        var hint = UiFactory.HintText(
            "与 Unity Grid 同名同义：发布时按它生成 Grid 与 Tilemap。默认等于格子边长；等距图要压扁时才关掉跟随。",
            11,
            wrap: true,
            margin: new Thickness(0, 2, 0, 0));
        rows.Children.Add(hint);

        return Card("Unity 导出口径", rows, "发布时使用");
    }

    /// <summary>同步开关条：浅青底、圆角，勾选后右侧实时显示折算结果。</summary>
    private Control BuildSyncStrip()
    {
        var valuePill = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.GridDialog.SyncBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius),
            Padding = new Thickness(9, 2, 9, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _syncCellSizeValue,
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_syncCellSizeCheck, 0);
        row.Children.Add(_syncCellSizeCheck);
        Grid.SetColumn(valuePill, 1);
        row.Children.Add(valuePill);

        return new Border
        {
            Background = UiTheme.GridDialog.SyncSurface,
            BorderBrush = UiTheme.GridDialog.SyncBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Padding = new Thickness(10, 4, 6, 4),
            Child = row,
        };
    }

    /// <summary>右卡：实时网格预览 + 行列摘要，预览区随窗口高度撑满。</summary>
    private Control BuildPreviewCard()
    {
        var inner = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = UiFactory.SectionHead("网格预览", UiMetrics.FontTitle);
        Grid.SetColumn(title, 0);
        head.Children.Add(title);

        var badge = BadgeBox(_previewBadge);
        Grid.SetColumn(badge, 1);
        head.Children.Add(badge);

        Grid.SetRow(head, 0);
        inner.Children.Add(head);

        // 舞台面：白→浅青渐变 + 细描边，格子落在上面才像「图纸」而不是表格
        var surface = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.4, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(UiTheme.GridDialog.PreviewTop, 0),
                    new GradientStop(UiTheme.GridDialog.PreviewBottom, 1),
                },
            },
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 9, 0, 8),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = _preview,
        };
        Grid.SetRow(surface, 1);
        inner.Children.Add(surface);

        var summaryChip = BuildSummaryChip();
        Grid.SetRow(summaryChip, 2);
        inner.Children.Add(summaryChip);

        return new Border
        {
            Background = UiTheme.Card,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            BoxShadow = UiTheme.GridDialog.CardShadow,
            Padding = new Thickness(14, 12, 14, 12),
            Child = inner,
        };
    }

    /// <summary>摘要胶囊：把「多少 px → 多少格」包进一块浅底，避免文字浮在卡底上。</summary>
    private Control BuildSummaryChip()
    {
        _summary.HorizontalAlignment = HorizontalAlignment.Center;
        _summary.TextAlignment = TextAlignment.Center;

        return new Border
        {
            Background = UiTheme.GridDialog.SummarySurface,
            BorderBrush = UiTheme.GridDialog.SyncBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _summary,
        };
    }

    /// <summary>底部：白底操作条，左侧幽灵「恢复默认」，右侧次按钮 + 主按钮。</summary>
    private Control BuildFooter()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };

        var reset = ActionButton("↺  恢复默认", UiFactory.ActionTone.Neutral, 92);
        reset.Background = UiTheme.Transparent;
        reset.BorderBrush = UiTheme.Transparent;
        reset.Foreground = UiTheme.GridDialog.GhostText;
        reset.Click += (_, _) => ResetToDefaults();
        Grid.SetColumn(reset, 0);
        grid.Children.Add(reset);

        var cancel = ActionButton("取消", UiFactory.ActionTone.Secondary, DialogButtonWidth,
            new UiFactory.ActionPalette(
                UiTheme.Card, UiTheme.White, CardBorderBrush,
                UiTheme.Card, UiTheme.TextSecondary, CardBorderBrush));
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_, _) => Close(null);
        Grid.SetColumn(cancel, 2);
        grid.Children.Add(cancel);

        var confirm = ActionButton("确定", UiFactory.ActionTone.Primary, PrimaryButtonWidth,
            new UiFactory.ActionPalette(
                UiTheme.Accent, UiTheme.White, UiTheme.AccentDark,
                UiTheme.Card, UiTheme.White, CardBorderBrush));
        confirm.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(UiTheme.GridDialog.PrimaryTop, 0),
                new GradientStop(UiTheme.GridDialog.PrimaryBottom, 1),
            },
        };
        confirm.IsDefault = true;
        confirm.Click += (_, _) => Confirm();
        Grid.SetColumn(confirm, 3);
        grid.Children.Add(confirm);

        return new Border
        {
            Background = UiTheme.GridDialog.FooterSurface,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            BoxShadow = UiTheme.GridDialog.CardShadow,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, ColumnGap, 0, 0),
            Child = grid,
        };
    }

    /// <summary>
    /// 统一卡片外壳：白底、1px 青灰描边、8px 圆角、轻投影；
    /// 标题带 3px 青条 + 右侧口径注记 + 细分隔线。
    /// </summary>
    /// <param name="title">卡片标题。</param>
    /// <param name="body">卡片内容。</param>
    /// <param name="caption">标题右侧的小注记（口径说明），空串表示不显示。</param>
    private static Border Card(string title, Control body, string caption = "") => new()
    {
        Background = UiTheme.Card,
        BorderBrush = CardBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
        BoxShadow = UiTheme.GridDialog.CardShadow,
        Padding = new Thickness(14, 11, 14, 12),
        Child = new StackPanel
        {
            Spacing = 9,
            Children =
            {
                CardHead(title, caption),
                new Border { Height = 1, Background = UiTheme.Divider },
                body,
            },
        },
    };

    /// <summary>卡片标题行：青条 + 标题 + 右贴口径注记。</summary>
    /// <param name="title">标题文案。</param>
    /// <param name="caption">右侧注记；空串时整列不占位。</param>
    private static Control CardHead(string title, string caption)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var head = UiFactory.SectionHead(title, UiMetrics.FontTitle);
        Grid.SetColumn(head, 0);
        grid.Children.Add(head);

        if (!string.IsNullOrEmpty(caption))
        {
            var note = new TextBlock
            {
                Text = caption,
                FontSize = UiMetrics.FontCaption,
                Foreground = UiTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(note, 1);
            grid.Children.Add(note);
        }

        return grid;
    }

    /// <summary>双值字段行：标签 + 第一轴徽标 + 框 + 第二轴徽标 + 框 + 单位。</summary>
    private static Control PairRow(string label, TextBox x, TextBox y, string unit, string axisX = "X", string axisY = "Y")
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{LabelWidth},{AxisBadgeWidth},{PairFieldWidth},{AxisGap},{AxisBadgeWidth},{PairFieldWidth},Auto,*"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        grid.Children.Add(UiFactory.FieldLabel(label, 12, UiTheme.TextPrimary, HorizontalAlignment.Right));

        var badgeX = AxisBadge(axisX);
        Grid.SetColumn(badgeX, 1);
        grid.Children.Add(badgeX);
        Grid.SetColumn(x, 2);
        grid.Children.Add(x);

        var badgeY = AxisBadge(axisY);
        Grid.SetColumn(badgeY, 4);
        grid.Children.Add(badgeY);
        Grid.SetColumn(y, 5);
        grid.Children.Add(y);

        var unitText = UnitText(unit);
        Grid.SetColumn(unitText, 6);
        grid.Children.Add(unitText);

        return grid;
    }

    /// <summary>X / Y / W / H 轴徽标：浅青小方块里放 9px 深青字母，贴紧各自动的数字框。</summary>
    private static Border AxisBadge(string axis) => new()
    {
        Width = 16,
        Height = 16,
        Background = UiTheme.GridDialog.SyncSurface,
        BorderBrush = UiTheme.GridDialog.SyncBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(UiMetrics.SmallCornerRadius),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(2, 0, 4, 0),
        Child = new TextBlock
        {
            Text = axis,
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.AccentDark,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>字段尾部的单位注记：小一号、次要色、与输入框留 8 间距。</summary>
    private static TextBlock UnitText(string text) => new()
    {
        Text = text,
        FontSize = UiMetrics.FontCaption,
        Foreground = UiTheme.TextSecondary,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

    /// <summary>标题右侧的小胶囊：浅青底、细描边、6 圆角，用来挂实时计数这类附注。</summary>
    private static Border BadgeBox(Control child) => new()
    {
        Background = UiTheme.GridDialog.SyncSurface,
        BorderBrush = UiTheme.GridDialog.SyncBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
        Padding = new Thickness(8, 2, 8, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = child,
    };

    private static readonly UiFactory.FieldRowSkin RowSkin = new(LabelWidth, UiMetrics.FontTitle);

    /// <summary>底栏按钮：统一 30 高、5 圆角，可单独覆盖主/次两档配色。</summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="tone">强调档。</param>
    /// <param name="width">定宽。</param>
    /// <param name="palette">配色；null 沿用全局按钮样式。</param>
    private static Button ActionButton(
        string text,
        UiFactory.ActionTone tone,
        double width,
        UiFactory.ActionPalette? palette = null) =>
        UiFactory.ActionButton(text, tone, new UiFactory.ActionSkin(
            FieldHeight, UiMetrics.ButtonCornerRadius, 8, FontSize: 12, Width: width), palette);

    /// <summary>
    /// 统一数字输入框：30 高、白底细边、6px 圆角，悬停加深描边、聚焦转青并提亮底色。
    /// 只放行数字（<paramref name="allowDecimal"/> 时额外放行一个小数点），
    /// 键入字母直接吞掉、粘贴内容自动清洗，杜绝「1d」这类脏值。
    /// </summary>
    /// <param name="value">初值。</param>
    /// <param name="width">固定宽度。</param>
    /// <param name="allowDecimal">是否允许小数（Tile Anchor 用）。</param>
    private static TextBox NumberField(string value, double width, bool allowDecimal = false)
    {
        var box = new TextBox
        {
            Text = value,
            Width = width,
            Height = FieldHeight,
            FontSize = UiMetrics.FontTitle,
            FontWeight = FontWeight.Medium,
            Foreground = UiTheme.TextPrimary,
            Padding = new Thickness(6, 2),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Background = UiTheme.GridDialog.FieldSurface,
            BorderBrush = FieldBorderBrush,
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        // 键入：只看这次敲进来的字符本身，不推断选区，避免与控件内部光标状态不同步
        box.TextInput += (_, e) =>
        {
            var typed = e.Text;
            if (string.IsNullOrEmpty(typed))
            {
                return;
            }

            foreach (var ch in typed)
            {
                if (char.IsAsciiDigit(ch) || (allowDecimal && ch == '.'))
                {
                    continue;
                }

                e.Handled = true;
                return;
            }
        };

        // 粘贴 / 输入法不经过 TextInput，这里兜底：剔非数字、小数点最多留一个
        box.TextChanged += (_, _) =>
        {
            var text = box.Text ?? string.Empty;
            var cleaned = CleanNumeric(text, allowDecimal);
            if (string.Equals(cleaned, text, StringComparison.Ordinal))
            {
                return;
            }

            var caret = box.CaretIndex;
            box.Text = cleaned;
            box.CaretIndex = Math.Min(caret, cleaned.Length);
        };

        // 三态：常态细边 → 悬停深一档 → 聚焦青边 + 提亮底；失焦后回到悬停前的那一档
        var focused = false;
        box.GotFocus += (_, _) =>
        {
            focused = true;
            box.BorderBrush = UiTheme.Accent;
            box.Background = UiTheme.GridDialog.FieldFocusSurface;
        };
        box.LostFocus += (_, _) =>
        {
            focused = false;
            box.BorderBrush = FieldBorderBrush;
            box.Background = UiTheme.GridDialog.FieldSurface;
        };
        box.PointerEntered += (_, _) =>
        {
            if (!focused)
            {
                box.BorderBrush = UiTheme.GridDialog.FieldBorderHover;
            }
        };
        box.PointerExited += (_, _) =>
        {
            if (!focused)
            {
                box.BorderBrush = FieldBorderBrush;
            }
        };
        return box;
    }

    /// <summary>清洗成合法数字：剔掉非数字字符，小数点最多留一个。</summary>
    private static string CleanNumeric(string text, bool allowDecimal)
    {
        var builder = new StringBuilder(text.Length);
        var dot = false;
        foreach (var ch in text)
        {
            if (char.IsAsciiDigit(ch))
            {
                builder.Append(ch);
            }
            else if (allowDecimal && ch == '.' && !dot)
            {
                dot = true;
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    /// <summary>下拉框：显示中文名，Tag 存落盘值；与输入框同高同圆角，底色浅一档以区分「可选」。</summary>
    private static ComboBox OptionCombo(IReadOnlyList<GridOption> options, string current)
    {
        var combo = new ComboBox
        {
            Height = FieldHeight,
            FontSize = UiMetrics.FontTitle,
            Foreground = UiTheme.TextPrimary,
            Background = UiTheme.GridDialog.ComboSurface,
            BorderBrush = FieldBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Padding = new Thickness(10, 0, 6, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(16, 0, 0, 0),
        };

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            if (string.Equals(option.Value, current, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
            }
        }

        if (combo.SelectedIndex < 0)
        {
            combo.SelectedIndex = 0;
        }

        return combo;
    }

    #endregion

    #region 预览

    /// <summary>
    /// 让「网格形状」与「Cell Layout」互为镜像：两者是同一件事的两处写法（画布怎么切格 / Unity 怎么认格），
    /// 四种形状与 Four 个 Cell Layout 一一对应，改一边自动对齐另一边。
    /// </summary>
    private void HookShapeLinkage()
    {
        _shapeCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingShape || SelectedValue(_shapeCombo) is not { } shape)
            {
                return;
            }

            _syncingShape = true;
            try
            {
                SelectValue(_layoutCombo, GridLayouts.All, GridShapes.LayoutOf(shape));
            }
            finally
            {
                _syncingShape = false;
            }

            HideHint();
        };

        _layoutCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingShape || SelectedValue(_layoutCombo) is not { } layout)
            {
                return;
            }

            _syncingShape = true;
            try
            {
                SelectValue(_shapeCombo, GridShapes.All, GridShapes.FromLayout(layout));
            }
            finally
            {
                _syncingShape = false;
            }

            HideHint();
            RebuildPreview();
        };
    }

    /// <summary>
    /// Cell Size 与「格子边长」的关系开关：勾选时 Cell Size 恒等于边长（改边长即时同步、两个框置灰），
    /// 取消勾选才允许给等距图单独填 X / Y。这样「配置 16」只需改一处。
    /// </summary>
    private void HookCellSizeLinkage()
    {
        _gridSizeBox.TextChanged += (_, _) =>
        {
            if (_syncingCellSize)
            {
                return;
            }

            if (_syncCellSizeCheck.IsChecked == true)
            {
                ApplyCellSizeFromGridSize();
            }
            else
            {
                RefreshSyncValueText();
            }
        };

        _syncCellSizeCheck.IsCheckedChanged += (_, _) => ApplyCellSizeLinkState();

        _cellSizeXBox.TextChanged += (_, _) => RefreshSyncValueText();
        _cellSizeYBox.TextChanged += (_, _) => RefreshSyncValueText();
    }

    /// <summary>按「跟随」状态切换 Cell Size 两个框的可用性与底纹，并把值对齐。</summary>
    private void ApplyCellSizeLinkState()
    {
        var follow = _syncCellSizeCheck.IsChecked == true;

        _cellSizeXBox.IsEnabled = !follow;
        _cellSizeYBox.IsEnabled = !follow;
        _cellSizeXBox.Background = follow ? UiTheme.PanelSurface : UiTheme.Card;
        _cellSizeYBox.Background = follow ? UiTheme.PanelSurface : UiTheme.Card;

        if (follow)
        {
            ApplyCellSizeFromGridSize();
        }
        else
        {
            RefreshSyncValueText();
        }
    }

    /// <summary>把当前格子边长写进 Cell Size 的 X / Y（边长还不是合法整数时先不同步，等用户补完）。</summary>
    private void ApplyCellSizeFromGridSize()
    {
        if (!int.TryParse(_gridSizeBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
        {
            return;
        }

        _syncingCellSize = true;
        try
        {
            var text = size.ToString(CultureInfo.InvariantCulture);
            _cellSizeXBox.Text = text;
            _cellSizeYBox.Text = text;
        }
        finally
        {
            _syncingCellSize = false;
        }

        RefreshSyncValueText();
    }

    /// <summary>同步条右侧的实时值：跟随显示「16 × 16」，手填显示「手动填写」。</summary>
    private void RefreshSyncValueText()
    {
        _syncCellSizeValue.Text = _syncCellSizeCheck.IsChecked == true
            ? $"{ReadInt(_cellSizeXBox, Settings.CellSizeX)} × {ReadInt(_cellSizeYBox, Settings.CellSizeY)}"
            : "手动填写";
    }

    /// <summary>
    /// 打开窗口时把「Cell Layout」校正到与「网格形状」一致：画布才是真相源，
    /// 旧存档里 shape=diamond / layout=Rectangle 这种自相矛盾的值不能原样摆在界面上。
    /// </summary>
    private void AlignShapeWithLayout()
    {
        var shape = SelectedValue(_shapeCombo);
        var layout = SelectedValue(_layoutCombo);
        if (shape is null || layout is null)
        {
            return;
        }

        var expected = GridShapes.LayoutOf(shape);
        if (string.Equals(layout, expected, StringComparison.Ordinal))
        {
            return;
        }

        _syncingShape = true;
        try
        {
            SelectValue(_layoutCombo, GridLayouts.All, expected);
        }
        finally
        {
            _syncingShape = false;
        }
    }

    /// <summary>任意参数变化后重绘预览，避免用户改完数字看不到结果。</summary>
    private void HookPreviewRefresh()
    {
        foreach (var box in new[] { _gridSizeBox, _cellSizeXBox, _cellGapXBox, _mapWidthBox, _mapHeightBox })
        {
            box.TextChanged += (_, _) => RebuildPreview();
            box.TextChanged += (_, _) => HideHint();
        }

        _shapeCombo.SelectionChanged += (_, _) => RebuildPreview();
        _gridLinesCheck.IsCheckedChanged += (_, _) => RebuildPreview();
        _preview.SizeChanged += (_, _) => RebuildPreview();
    }

    /// <summary>
    /// 按当前参数重画网格示意。三种几何都走与画布 lattice 相同的口径：
    /// 矩形为正方格；菱形宽 2×边长、高 1×边长、行距半格、奇数行右移一格；
    /// 六边形宽 1×边长、行距 0.866×边长、奇数行右移半格。
    /// </summary>
    private void RebuildPreview()
    {
        _preview.Children.Clear();

        var cell = Math.Max(1, ReadInt(_gridSizeBox, Settings.GridSize));
        var mapW = Math.Max(1, ReadInt(_mapWidthBox, _initialMapWidth));
        var mapH = Math.Max(1, ReadInt(_mapHeightBox, _initialMapHeight));
        var shape = GridShapes.Normalize(SelectedValue(_shapeCombo));
        var diamond = GridShapes.IsDiamond(shape);
        var hex = shape == GridShapes.Hex;
        var showLines = _gridLinesCheck.IsChecked == true;

        const double pad = 12.0;

        // 预览区随窗口伸缩：已布局时用真实画布尺寸，首帧为 0 才退回设计尺寸
        var surfaceW = _preview.Bounds.Width > 40 ? _preview.Bounds.Width : PreviewWidth;
        var surfaceH = _preview.Bounds.Height > 40 ? _preview.Bounds.Height : PreviewHeight;
        var usableW = Math.Max(24.0, surfaceW - pad * 2);
        var usableH = Math.Max(24.0, surfaceH - pad * 2);

        // 三种形状各自的格宽 / 格高 / 行列步进 / 错行位移
        var cellW = hex ? (double)cell : diamond ? cell * 2.0 : cell;
        var cellH = hex ? cell * 2.0 / Math.Sqrt(3.0) : cell;
        var stepX = hex ? (double)cell : diamond ? cell * 2.0 : cell;
        var stepY = hex ? cellH * 0.75 : diamond ? cell * 0.5 : cell;
        var rowShift = hex ? cell / 2.0 : diamond ? (double)cell : 0.0;

        var cols = PreviewCols;
        var rows = PreviewRows;
        double totalW;
        double totalH;
        double scale;

        if (hex || diamond)
        {
            // 行数按可用高度撑满，避免菱形 / 六边形只在中间挤成一条窄带
            totalW = cols * stepX + rowShift;
            scale = Math.Max(0.05, usableW / totalW);
            rows = (int)Math.Floor((usableH / scale - cellH) / stepY) + 1;
            rows = Math.Clamp(rows, 2, 64);
            totalH = (rows - 1) * stepY + cellH;
        }
        else
        {
            // 矩形同样先按宽度定格宽，再按高度把行数铺满，避免下方留一大片空白
            totalW = cols * stepX;
            scale = Math.Max(0.05, usableW / totalW);
            rows = Math.Clamp((int)Math.Floor(usableH / (stepY * scale)), 2, 40);
            totalH = rows * stepY;
        }

        // 网格整体居中，格子少时不贴左上角
        var originX = pad + (usableW - totalW * scale) / 2;
        var originY = pad + (usableH - totalH * scale) / 2;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var x = originX + (col * stepX + (row % 2) * rowShift) * scale;
                var y = originY + row * stepY * scale;
                var w = cellW * scale;
                var h = cellH * scale;

                var polygon = new Polygon
                {
                    Fill = ((row + col) % 2 == 0) ? PreviewCellFill : PreviewCellAltFill,
                    Stroke = showLines ? PreviewStroke : Brushes.Transparent,
                    StrokeThickness = 1,
                    Opacity = showLines ? 1.0 : 0.92,
                };

                if (hex)
                {
                    var centerX = x + w * 0.5;
                    var centerY = y + h * 0.5;
                    var radius = h * 0.5;
                    var points = new List<Point>(6);
                    for (var i = 0; i < 6; i++)
                    {
                        var angle = -Math.PI / 2.0 + i * Math.PI / 3.0;
                        points.Add(new Point(
                            centerX + radius * Math.Cos(angle),
                            centerY + radius * Math.Sin(angle)));
                    }

                    polygon.Points = points;
                }
                else if (diamond)
                {
                    polygon.Points = new List<Point>
                    {
                        new(x + w * 0.5, y),
                        new(x + w, y + h * 0.5),
                        new(x + w * 0.5, y + h),
                        new(x, y + h * 0.5),
                    };
                }
                else
                {
                    polygon.Points = new List<Point>
                    {
                        new(x, y),
                        new(x + w, y),
                        new(x + w, y + h),
                        new(x, y + h),
                    };
                }

                _preview.Children.Add(polygon);
            }
        }

        _previewBadge.Text = $"预览 {cols} × {rows} 格";
        _summary.Text = BuildSummary(mapW, mapH, cell, shape);
        UpdateMapSizeHint(mapW, mapH, cell, shape);
    }    /// <summary>摘要行：把当前地图尺寸按形状换算成多少列多少行。</summary>
    private static string BuildSummary(int mapW, int mapH, int cell, string shape)
    {
        var (cols, rows) = CountCells(mapW, mapH, cell, shape);
        return $"{mapW} × {mapH} px  ·  边长 {cell} px  →  {cols} × {rows} 格  ·  {ShapeShortName(shape)}";
    }

    /// <summary>地图尺寸行下面的换算注记：宽高与边长一改就同步，省得用户自己除。</summary>
    private void UpdateMapSizeHint(int mapW, int mapH, int cell, string shape)
    {
        var (cols, rows) = CountCells(mapW, mapH, cell, shape);

        // 宽高不是边长的整数倍时，末行/末列会留残格：规划尺寸时先把这件事说清，免得到了画布上才发现
        var aligned = mapW % cell == 0 && mapH % cell == 0;
        _mapSizeHint.Foreground = aligned ? UiTheme.TextSecondary : UiTheme.GridDialog.WarnText;
        _mapSizeHint.Text = aligned
            ? $"= {cols} × {rows} 格（边长 {cell} px · {ShapeShortName(shape)}）"
            : $"= {cols} × {rows} 格（{cell} px 不整除，末行/末列有残格）";
    }

    /// <summary>
    /// 地图尺寸 ÷ 边长 → 行列，口径与 <see cref="MapConfigData.GridCols"/> / <see cref="MapConfigData.GridRows"/> 一致：
    /// 矩形向上取整；菱形一列 2×边长、一行半格；六边形格宽 = 边长、行距 0.866×边长。
    /// </summary>
    private static (int Cols, int Rows) CountCells(int mapW, int mapH, int cell, string shape)
    {
        if (cell <= 0)
        {
            return (0, 0);
        }

        if (shape == GridShapes.Hex)
        {
            return ((int)Math.Ceiling(mapW / (double)cell),
                    (int)Math.Ceiling(mapH / (cell * 1.5 / Math.Sqrt(3.0))));
        }

        if (GridShapes.IsDiamond(shape))
        {
            return ((int)Math.Ceiling(mapW / (cell * 2.0)),
                    (int)Math.Ceiling(mapH / (cell * 0.5)));
        }

        return ((int)Math.Ceiling(mapW / (double)cell),
                (int)Math.Ceiling(mapH / (double)cell));
    }

    /// <summary>摘要行用的形状短名：下拉里带英文前缀，摘要行只留中文，避免挤成两行。</summary>
    /// <param name="shape">形状值。</param>
    /// <returns>中文短名。</returns>
    private static string ShapeShortName(string shape) => shape switch
    {
        GridShapes.Hex => "六边形",
        GridShapes.Diamond => "等距",
        GridShapes.DiamondZ => "等距 Z 作 Y",
        _ => "矩形",
    };

    #endregion

    #region 确定与重置

    /// <summary>收集字段，越界钳制并提示，全部合法后以设置作为结果关闭。</summary>
    private void Confirm()
    {
        try
        {
            var follow = _syncCellSizeCheck.IsChecked == true;

            if (!RequireFilled(_gridSizeBox, "格子边长"))
            {
                return;
            }

            var rawSize = ReadInt(_gridSizeBox, Settings.GridSize);
            var size = EditorSettings.ClampGridSize(rawSize);
            _gridSizeBox.Text = size.ToString(CultureInfo.InvariantCulture);

            if (size != rawSize)
            {
                ShowHint($"格子边长已钳到 {size}（允许范围 {GridSettingsDefaults.GridSizeMin}–{GridSettingsDefaults.GridSizeMax}）。");
                return;
            }

            // 跟随时以边长为唯一真相，手填时再校验四个格子字段不能为空
            if (!follow && (!RequireFilled(_cellSizeXBox, "Cell Size X") ||
                            !RequireFilled(_cellSizeYBox, "Cell Size Y") ||
                            !RequireFilled(_cellGapXBox, "Cell Gap X") ||
                            !RequireFilled(_cellGapYBox, "Cell Gap Y")))
            {
                return;
            }

            if (!RequireFilled(_anchorXBox, "Tile Anchor X") || !RequireFilled(_anchorYBox, "Tile Anchor Y"))
            {
                return;
            }

            var anchorX = ReadDouble(_anchorXBox, Settings.TileAnchorX);
            var anchorY = ReadDouble(_anchorYBox, Settings.TileAnchorY);
            var clampedAnchorX = Math.Clamp(anchorX, 0d, 1d);
            var clampedAnchorY = Math.Clamp(anchorY, 0d, 1d);
            if (Math.Abs(clampedAnchorX - anchorX) > 0.0001 || Math.Abs(clampedAnchorY - anchorY) > 0.0001)
            {
                _anchorXBox.Text = clampedAnchorX.ToString("0.##", CultureInfo.InvariantCulture);
                _anchorYBox.Text = clampedAnchorY.ToString("0.##", CultureInfo.InvariantCulture);
                ShowHint("Tile Anchor 是格内比例，只能在 0–1 之间。");
                return;
            }

            Settings.GridSize = size;
            Settings.GridShape = SelectedValue(_shapeCombo) ?? GridSettingsDefaults.Shape;
            Settings.ShowGridLines = _gridLinesCheck.IsChecked == true;

            if (follow)
            {
                Settings.CellSizeX = size;
                Settings.CellSizeY = size;
            }
            else
            {
                Settings.CellSizeX = Math.Max(1, ReadInt(_cellSizeXBox, Settings.CellSizeX));
                Settings.CellSizeY = Math.Max(1, ReadInt(_cellSizeYBox, Settings.CellSizeY));
            }

            Settings.CellGapX = Math.Max(0, ReadInt(_cellGapXBox, Settings.CellGapX));
            Settings.CellGapY = Math.Max(0, ReadInt(_cellGapYBox, Settings.CellGapY));
            Settings.CellLayout = SelectedValue(_layoutCombo) ?? GridSettingsDefaults.CellLayout;
            Settings.CellSwizzle = SelectedValue(_swizzleCombo) ?? GridSettingsDefaults.CellSwizzle;
            Settings.TileAnchorX = clampedAnchorX;
            Settings.TileAnchorY = clampedAnchorY;

            if (!TryReadMapSize(out var mapWidth, out var mapHeight))
            {
                return;
            }

            Close(new GridSettingsResult(Settings, mapWidth, mapHeight));
        }
        catch (Exception ex)
        {
            // 关窗失败时不要把异常丢给上层 async void，直接在窗口里说清楚
            ShowHint($"保存失败：{ex.GetType().Name} · {ex.Message}");
        }
    }

    /// <summary>
    /// 校验地图宽高：必填、落在 256–8192。不合格时把合法值写回输入框并给提示，不关窗。
    /// </summary>
    private bool TryReadMapSize(out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!RequireFilled(_mapWidthBox, "地图宽") || !RequireFilled(_mapHeightBox, "地图高"))
        {
            return false;
        }

        var rawW = ReadInt(_mapWidthBox, _initialMapWidth);
        var rawH = ReadInt(_mapHeightBox, _initialMapHeight);
        width = Math.Clamp(rawW, MapSizeMin, MapSizeMax);
        height = Math.Clamp(rawH, MapSizeMin, MapSizeMax);

        if (width != rawW || height != rawH)
        {
            _mapWidthBox.Text = width.ToString(CultureInfo.InvariantCulture);
            _mapHeightBox.Text = height.ToString(CultureInfo.InvariantCulture);
            ShowHint($"地图尺寸已钳到 {width} × {height}（允许范围 {MapSizeMin}–{MapSizeMax} px）。");
            return false;
        }

        return true;
    }

    /// <summary>必填校验：空值直接拦下并把焦点放回该框。</summary>
    private bool RequireFilled(TextBox box, string label)
    {
        if (!string.IsNullOrWhiteSpace(box.Text))
        {
            return true;
        }

        ShowHint($"{label}不能为空。");
        box.Focus();
        return false;
    }

    /// <summary>把界面上的数值退回默认档，只改界面，点确定才落盘；地图尺寸回到打开窗口时的值。</summary>
    private void ResetToDefaults()
    {
        _mapWidthBox.Text = _initialMapWidth.ToString(CultureInfo.InvariantCulture);
        _mapHeightBox.Text = _initialMapHeight.ToString(CultureInfo.InvariantCulture);
        _gridSizeBox.Text = GridSettingsDefaults.GridSize.ToString(CultureInfo.InvariantCulture);
        SelectValue(_shapeCombo, GridShapes.All, GridSettingsDefaults.Shape);
        _gridLinesCheck.IsChecked = GridSettingsDefaults.ShowGridLines;

        // 默认档里 Cell Size 与边长同值，所以「跟随」回到勾选态
        _syncCellSizeCheck.IsChecked = GridSettingsDefaults.CellSizeX == GridSettingsDefaults.GridSize &&
                                       GridSettingsDefaults.CellSizeY == GridSettingsDefaults.GridSize;
        _cellSizeXBox.Text = GridSettingsDefaults.CellSizeX.ToString(CultureInfo.InvariantCulture);
        _cellSizeYBox.Text = GridSettingsDefaults.CellSizeY.ToString(CultureInfo.InvariantCulture);
        _cellGapXBox.Text = GridSettingsDefaults.CellGapX.ToString(CultureInfo.InvariantCulture);
        _cellGapYBox.Text = GridSettingsDefaults.CellGapY.ToString(CultureInfo.InvariantCulture);
        SelectValue(_layoutCombo, GridLayouts.All, GridSettingsDefaults.CellLayout);
        SelectValue(_swizzleCombo, GridSwizzles.All, GridSettingsDefaults.CellSwizzle);
        _anchorXBox.Text = GridSettingsDefaults.TileAnchorX.ToString("0.##", CultureInfo.InvariantCulture);
        _anchorYBox.Text = GridSettingsDefaults.TileAnchorY.ToString("0.##", CultureInfo.InvariantCulture);

        ApplyCellSizeLinkState();
        HideHint();
        RebuildPreview();
    }

    #endregion

    #region 取值

    /// <summary>读整数，空值或非数字回落到 <paramref name="fallback"/>。</summary>
    private static int ReadInt(TextBox box, int fallback) =>
        int.TryParse(box.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>读小数，空值或非数字回落到 <paramref name="fallback"/>。</summary>
    private static double ReadDouble(TextBox box, double fallback) =>
        double.TryParse(box.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>取下拉当前值。</summary>
    private static string? SelectedValue(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    /// <summary>按值选中下拉项。</summary>
    private static void SelectValue(ComboBox combo, IReadOnlyList<GridOption> options, string value)
    {
        for (var i = 0; i < combo.Items.Count && i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    /// <summary>显示错误提示行。</summary>
    private void ShowHint(string text)
    {
        _hint.Text = text;
        _hint.IsVisible = true;
    }

    /// <summary>隐藏错误提示行。</summary>
    private void HideHint()
    {
        _hint.IsVisible = false;
        _hint.Text = string.Empty;
    }

    #endregion
}

#endregion





