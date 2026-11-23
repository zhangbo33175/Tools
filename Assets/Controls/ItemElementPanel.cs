/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ItemElementPanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   物品元件编辑面板：元件列表 + 属性分组编辑 + 配置持久化
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

/// <summary>
/// 物品元件编辑面板，按 Unity 版 ItemElementEditModule 的面板结构复刻：
/// 顶部工具条（保存 / 添加 / 删除 / 导入 / 导出）、左侧按类型分组的元件列表、
/// 右侧可折叠分组的属性表单。字段编辑实时写回内存模板，点「保存」落盘并通知场景同步。
/// </summary>
public sealed partial class ItemElementPanel : UserControl
{
    #region 常量与画刷

    /// <summary>表单标签列宽（标签右对齐，所有分组纵向对齐）。</summary>
    private const double LabelWidth = 92.0;

    /// <summary>普通输入控件的列宽：所有字段同宽，行与行之间不参差。</summary>
    private const double FieldWidth = 252.0;

    /// <summary>数字输入控件自身的宽度（列宽仍按 <see cref="FieldWidth"/>，控件靠左摆放）。</summary>
    private const double NumberWidth = 96.0;

    /// <summary>两列行里第二列与第一列之间的间距。</summary>
    private const double PairGap = 16.0;

    /// <summary>勾选框方框与右侧文字之间留出的一个字的空隙（字号 12px，一字约 12px）。</summary>
    private const double CheckLabelGap = 12.0;

    private const double ListPanelWidth = 300.0;

    private static readonly IBrush IdleTabBrush = UiTheme.Form.IdleTab;

    // 分组卡片：白底 + 浅蓝灰标题条 + 更浅的描边，比纯文字标题更容易分组阅读
    private static readonly IBrush GroupCardBrush = UiTheme.Card;
    private static readonly IBrush GroupHeaderBrush = UiTheme.Form.GroupHeader;
    private static readonly IBrush GroupHeaderHoverBrush = UiTheme.Form.GroupHeaderHover;
    private static readonly IBrush GroupBorderBrush = UiTheme.Form.GroupBorder;
    private static readonly IBrush FieldBorderBrush = UiTheme.Form.FieldBorder;
    private static readonly IBrush FieldFillBrush = UiTheme.Card;
    private static readonly IBrush FieldReadOnlyBrush = UiTheme.Form.FieldReadOnly;
    private static readonly IBrush FieldComboBrush = UiTheme.Form.FieldCombo;

    #endregion

    #region 字段

    private readonly TemplateStore _store;
    private readonly Func<ObjectTemplate, string?> _canRemove;
    private readonly Action<string> _notify;
    private readonly List<Action<ObjectTemplate>> _loaders = new();
    private readonly AssetLibrary _assets;
    private readonly HashSet<string> _hiddenTemplateIds = new();

    /// <summary>地图物件配置表集合：有表时，列表与表单直接读表里的行（Unity 同款「分类按钮 ↔ 表」）。</summary>
    private ItemConfigSet? _itemTable;

    /// <summary>九宫格旁路配置缓存（配置表没有九宫格列时由 NineSliceConfig.json 提供）。</summary>
    private NineSliceCatalog _nineSlices = new();

    /// <summary>表行 ↔ 临时模板（列表展示、表单编辑用；保存时按该行回写原表）。</summary>
    private readonly Dictionary<ItemElementRow, ObjectTemplate> _rowTemplates = new();

    private List<ObjectTemplate> _templates = new();
    private MapObjectType _tabType = MapObjectType.Building;
    private ObjectTemplate? _selected;
    private bool _loading;

    private readonly StackPanel _listHost = new();
    private readonly StackPanel _formHost = new();
    private readonly StackPanel _groupsHost = new();
    private readonly TextBlock _emptyHint = new();
    private readonly TextBlock _statusLabel = new();
    private readonly Dictionary<MapObjectType, Button> _tabButtons = new();
    private readonly List<Action> _committers = new();

    #endregion

    #region 构造

    /// <summary>初始化面板。</summary>
    /// <param name="store">模板仓库，负责 JSON / CSV 持久化。</param>
    /// <param name="canRemove">删除前置校验，返回 null 表示允许，否则返回拒绝原因。</param>
    /// <param name="notify">状态提示回调。</param>
    public ItemElementPanel(TemplateStore store, Func<ObjectTemplate, string?> canRemove, Action<string> notify)
    {
        _store = store;
        _canRemove = canRemove;
        _notify = notify;
        _assets = new AssetLibrary(store.OutputDirectory);

        Background = UiTheme.Form.SubtleSurface;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(BuildToolbar());
        root.Children.Add(BuildBody());

        Content = root;
    }

    #endregion

    #region 公开接口

    /// <summary>绑定模板集合与初始类型 Tab。</summary>
    /// <param name="templates">模板集合（与主窗口共享同一引用）。</param>
    /// <param name="tabType">初始 Tab 类型。</param>
    public void Attach(List<ObjectTemplate> templates, MapObjectType tabType)
    {
        _templates = templates;
        _tabType = tabType;
        _selected = null;
        ReloadNineSlices();
        foreach (var template in _templates)
        {
            ApplyNineSlice(template);
        }

        RefreshList();
        LoadForm();
    }

    /// <summary>模板集合被外部替换或改动后整体刷新。</summary>
    public void RefreshAll()
    {
        RefreshList();
        LoadForm();
    }

    /// <summary>
    /// 绑定「地图物件配置表文件夹」：绑定后左侧元件列表与右侧表单都直接读表（建筑→TableBuildings、
    /// 人物→TableCharacters、物件→TableProps），点「保存」把改动写回对应表。
    /// </summary>
    /// <param name="table">配置表集合；null 表示退回 JSON 模板列表。</param>
    public void SetItemTable(ItemConfigSet? table)
    {
        _itemTable = table;
        _rowTemplates.Clear();
        _selected = null;
        ReloadNineSlices();
        RefreshList();
        LoadForm();
        NineSliceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重新读取九宫格旁路配置（配置表保存、外部文件替换后调用）。</summary>
    private void ReloadNineSlices() => _nineSlices = _store.LoadNineSlices();

    /// <summary>把旁路配置套到模板；配置表没有九宫格列时靠这一步恢复。</summary>
    /// <param name="template">目标模板。</param>
    private void ApplyNineSlice(ObjectTemplate template)
    {
        var settings = _nineSlices.Find(template.TemplateId, template.ImagePath, template.TemplateName);
        if (settings is not null)
        {
            template.NineSlice = settings.Clone();
        }
    }

    /// <summary>当前分类对应的配置表（没有表返回 null）。</summary>
    private ItemTableFile? ActiveTableFile() =>
        _itemTable?.FileForCategory(ObjectTypeNames.Of(_tabType));

    /// <summary>表行 → 用于列表 / 表单的临时模板（同一行只造一次）。</summary>
    /// <param name="row">表行。</param>
    /// <returns>临时模板。</param>
    private ObjectTemplate TemplateOf(ItemElementRow row)
    {
        if (_rowTemplates.TryGetValue(row, out var existing))
        {
            return existing;
        }

        var template = ItemConfigSet.CreateTemplate(row, _tabType);
        ApplyNineSlice(template);

        _rowTemplates[row] = template;
        return template;
    }

    /// <summary>把表单里的临时模板写回表行（图片名/ID 不动，其余列跟着改）。</summary>
    /// <param name="row">目标行。</param>
    /// <param name="template">临时模板。</param>
    private static void ApplyTemplateToRow(ItemElementRow row, ObjectTemplate template)
    {
        row.TemplateName = template.TemplateName;
        row.ResName = template.ResName;
        row.AnimName = template.AnimName;
        row.Loop = template.Loop;
        row.Speed = template.Speed;
        row.SortOrder = template.SortOrder;
        row.ShadowEnabled = template.ShadowEnabled;
        row.IsCollectible = template.IsCollectible;
        row.RespawnTime = template.RespawnTime;
        row.Interactable = template.Interactable;
        row.InteractType = template.InteractType;
        row.CollisionEnabled = template.CollisionEnabled;
        row.DialogBubble = template.DialogBubble;
        row.FacingDirection = template.FacingDirection;
        row.DisplayLayer = template.DisplayLayer;
        row.TrafficCategory = template.TrafficCategory;
        row.YSortEnabled = template.YSortEnabled;
        row.YSortOffset = template.YSortOffset;
        row.IsMovable = template.IsMovable;
        row.LoopRestartDelay = template.LoopRestartDelay;
        row.HideWhenOffMap = template.HideWhenOffMap;
        row.IsTeleport = template.IsTeleport;
        row.TeleportTargetScene = template.TeleportTargetScene;
        row.TeleportTargetX = template.TeleportTargetX;
        row.TeleportTargetY = template.TeleportTargetY;

        var nineSlice = template.NineSlice ?? new NineSliceSettings();
        row.NineSliceEnabled = nineSlice.Enabled;
        row.NineSliceLeft = Math.Max(0, nineSlice.Left);
        row.NineSliceRight = Math.Max(0, nineSlice.Right);
        row.NineSliceTop = Math.Max(0, nineSlice.Top);
        row.NineSliceBottom = Math.Max(0, nineSlice.Bottom);
        row.NineSliceCenterMode = NineSliceModes.ToCode(nineSlice.CenterMode);
    }

    /// <summary>列表发生增删、导入替换时触发，主窗口据此重建左侧资源列表。</summary>
    public event EventHandler? CatalogChanged;

    /// <summary>模板保存后触发，主窗口据此把模板属性同步到已摆放实例。</summary>
    public event EventHandler? TemplatesSaved;

    /// <summary>九宫格配置变化时触发，主窗口据此让画布按新边距立即重绘。</summary>
    public event EventHandler? NineSliceChanged;

    /// <summary>列表行「显示」勾选变化，主窗口据此刷新画布上的模板可见性。</summary>
    public event EventHandler<TemplateVisibilityEventArgs>? TemplateVisibilityChanged;

    /// <summary>当前选中的模板。</summary>
    public ObjectTemplate? Selected => _selected;

    /// <summary>
    /// 根据「JSON 模板 + 当前表行模板」的实时值构建九宫格目录，供画布按实例的模板 / 图片名命中。
    /// 只登记启用九宫格的素材，不会把未配置的物件误画成切片。
    /// </summary>
    /// <returns>九宫格目录。</returns>
    public NineSliceCatalog BuildNineSliceCatalog()
    {
        var catalog = new NineSliceCatalog();
        foreach (var template in _templates)
        {
            catalog.Register(template);
        }

        foreach (var template in _rowTemplates.Values)
        {
            catalog.Register(template);
        }

        return catalog;
    }

    #endregion

    #region 工具条

    private Control BuildToolbar()
    {
        var title = new TextBlock
        {
            Text = "物品元件编辑模式",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        buttons.Children.Add(BuildButton("保存", 70, "#1387BE", UiTheme.White, async () => await SaveAsync()));
        buttons.Children.Add(BuildButton("添加元件", 86, null, null, (_, _) => AddTemplate()));
        buttons.Children.Add(BuildButton("导入配置", 86, null, null, async () => await ImportCsvAsync()));
        buttons.Children.Add(BuildButton("导出配置", 86, null, null, async () => await ExportCsvAsync()));

        _statusLabel.Foreground = UiTheme.TextSecondary;
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        _statusLabel.Margin = new Thickness(10, 0, 10, 0);
        _statusLabel.Text = "修改实时生效，点「保存」写盘并同步场景实例";

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(title);
        Grid.SetColumn(_statusLabel, 1);
        grid.Children.Add(_statusLabel);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);

        return new Border
        {
            Background = UiTheme.Form.SubtleSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5),
            Child = grid,
        };
    }

    private static Button BuildButton(string text, double width, string? background, IBrush? foreground,
        EventHandler<RoutedEventArgs>? handler)
    {
        var button = new Button
        {
            Content = text,
            Width = width,
            Padding = new Thickness(6, 4),
        };

        if (background is not null)
        {
            button.Background = new SolidColorBrush(Color.Parse(background));
        }

        if (foreground is not null)
        {
            button.Foreground = foreground;
        }

        if (handler is not null)
        {
            button.Click += handler;
        }

        return button;
    }

    private static Button BuildButton(string text, double width, string? background, IBrush? foreground, Func<Task> action)
    {
        var button = new Button
        {
            Content = text,
            Width = width,
            Padding = new Thickness(6, 4),
        };

        if (background is not null)
        {
            button.Background = new SolidColorBrush(Color.Parse(background));
        }

        if (foreground is not null)
        {
            button.Foreground = foreground;
        }

        button.Click += async (_, _) => await action();
        return button;
    }

    #endregion

    #region 布局骨架

    private Control BuildBody()
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{ListPanelWidth},8,*"),
            Margin = new Thickness(4, 4, 4, 4),
        };

        body.Children.Add(BuildListPanel());
        var property = BuildPropertyPanel();
        Grid.SetColumn(property, 2);
        body.Children.Add(property);
        Grid.SetRow(body, 1);
        return body;
    }

    private Control BuildListPanel()
    {
        var header = new TextBlock
        {
            Text = "元件列表",
            Foreground = UiTheme.AccentDark,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var tabs = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var types = new[] { MapObjectType.Building, MapObjectType.Character, MapObjectType.Prop };
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            var tab = new Button
            {
                Content = ObjectTypeNames.Of(type),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(4, 3),
                Margin = new Thickness(index == 0 ? 0 : 2, 0, index == types.Length - 1 ? 0 : 2, 0),
            };

            tab.Click += (_, _) => SwitchTab(type);
            _tabButtons[type] = tab;
            Grid.SetColumn(tab, index);
            tabs.Children.Add(tab);
        }

        // 卡片内用 Grid：标题/类型页签固定，列表占满剩余高度并在卡片内滚动（StackPanel 会给无限高度，列表会溢出卡片）
        var stack = new Grid
        {
            Margin = new Thickness(6),
            RowSpacing = 6,
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
        };
        Grid.SetRow(header, 0);
        stack.Children.Add(header);
        Grid.SetRow(tabs, 1);
        stack.Children.Add(tabs);

        // 列表区与左侧「章节/物件列表」同款：1px 描边外框 + 右侧 14px 经典箭头滚动条
        _listHost.Margin = new Thickness(4);
        var list = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            Content = _listHost,
        };
        var scrollArea = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14") };
        scrollArea.Children.Add(list);
        var scrollBar = new ClassicScrollBar { Target = list };
        Grid.SetColumn(scrollBar, 1);
        scrollArea.Children.Add(scrollBar);

        var listFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#C9C9C9")),
            BorderThickness = new Thickness(1),
            Child = scrollArea,
        };
        Grid.SetRow(listFrame, 2);
        stack.Children.Add(listFrame);

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = stack,
            // 圆角 + 裁剪，避免列表内容压到卡片右下角的描边上
            ClipToBounds = true,
            CornerRadius = new CornerRadius(2),
        };
    }

    private Control BuildPropertyPanel()
    {
        _formHost.Spacing = 8;
        _formHost.Margin = new Thickness(12, 10, 12, 12);
        _groupsHost.Spacing = 10;

        _emptyHint.Text = "请在左侧元件列表中选择一个元件，右侧将显示它的动画、交互、层级碰撞、交通、收集、YSort、移动循环与传送门配置。";
        _emptyHint.Foreground = UiTheme.TextSecondary;
        _emptyHint.TextWrapping = TextWrapping.Wrap;
        _emptyHint.MaxWidth = 640;
        _emptyHint.HorizontalAlignment = HorizontalAlignment.Left;
        _emptyHint.Margin = new Thickness(0, 16, 0, 8);
        _formHost.Children.Add(_emptyHint);
        _formHost.Children.Add(_groupsHost);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _formHost,
        };

        BuildFormSkeleton();

        return new Border
        {
            Background = UiTheme.PanelSurface,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            Child = scroll,
        };
    }

    #endregion

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

/// <summary>模板可见性变化事件参数。</summary>
public sealed class TemplateVisibilityEventArgs : EventArgs
{
    /// <summary>初始化事件参数。</summary>
    /// <param name="templateId">模板 ID。</param>
    /// <param name="visible">是否显示。</param>
    public TemplateVisibilityEventArgs(string templateId, bool visible)
    {
        TemplateId = templateId;
        Visible = visible;
    }

    /// <summary>模板 ID。</summary>
    public string TemplateId { get; }

    /// <summary>是否显示。</summary>
    public bool Visible { get; }
}
