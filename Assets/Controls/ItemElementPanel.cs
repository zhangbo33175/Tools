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
/// 右侧可折叠分组的属性表单。字段编辑实时写回内存模板，点「保存」落盘并通知场景同步。`r`n/// 本文件保留状态与公开接口，工具条、布局骨架、列表与事件参数分别位于`r`n/// Toolbar / Layout / List / Events partial。
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
}
