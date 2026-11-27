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
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 物品元件编辑面板，按 Unity 版 ItemElementEditModule 的面板结构复刻：
/// 顶部工具条（保存 / 添加 / 删除 / 导入 / 导出）、左侧按类型分组的元件列表、
/// 右侧可折叠分组的属性表单。字段编辑实时写回内存模板，点「保存」落盘并通知场景同步。
/// 本文件保留状态与公开接口，工具条、布局骨架、列表与事件参数分别位于
/// Toolbar / Layout / List / Events partial。
/// </summary>
public sealed partial class ItemElementPanel : UserControl
{
    #region 常量与画刷

    /// <summary>表单标签列宽（标签右对齐，所有分组纵向对齐）。</summary>
    private const double LabelWidth = 92.0;

    /// <summary>普通输入控件的列宽：所有字段同宽，行与行之间不参差。</summary>
    private const double FieldWidth = 252.0;

    /// <summary>字段放在弹性列里的最小宽度：比整列窄，窗宽变化时可以跟着拉宽。</summary>
    private const double FlexibleFieldMinWidth = 150.0;

    /// <summary>数字输入控件自身的宽度（列宽仍按 <see cref="FieldWidth"/>，控件靠左摆放）。</summary>
    private const double NumberWidth = 96.0;

    /// <summary>两列行里第二列与第一列之间的间距。</summary>
    private const double PairGap = 16.0;

    /// <summary>勾选框方框与右侧文字之间留出的一个字的空隙（字号 12px，一字约 12px）。</summary>
    private const double CheckLabelGap = 12.0;

    /// <summary>表单正文与标签的统一下限字号，保证分组之间字号一致。</summary>
    private const double FormFontSize = 12.0;

    /// <summary>字段标签右侧与控件之间的固定留白（Row 与 RowUnit 共用）。</summary>
    private const double LabelRightGap = 8.0;

    /// <summary>字段控件统一高度（输入框与下拉框对齐同一基线）。</summary>
    private const double FieldHeight = 30.0;

    /// <summary>输入框内边距：左右 6、上下 2。</summary>
    private static readonly Thickness FieldTextPadding = new(6, 2);

    /// <summary>下拉框内边距：左右 6、上下 1（下拉箭头自身占一点高度）。</summary>
    private static readonly Thickness FieldComboPadding = new(6, 1);

    /// <summary>输入框与下拉框统一的圆角。</summary>
    private const double FieldCornerRadius = 6.0;

    /// <summary>输入框与下拉框统一的描边线宽。</summary>
    private const double FieldBorderThickness = 1.0;

    /// <summary>分组标题条左侧箭头的固定宽度，避免标题文字随箭头字体变化左右跳动。</summary>
    private const double GroupArrowWidth = 14.0;

    /// <summary>分组标题条左侧箭头字号。</summary>
    private const double GroupArrowFontSize = 13.0;

    /// <summary>分组标题条左侧竖条宽度。</summary>
    private const double GroupAccentWidth = 3.0;

    /// <summary>分组标题条左侧竖条高度。</summary>
    private const double GroupAccentHeight = 14.0;

    /// <summary>分组标题条左侧竖条的圆角。</summary>
    private const double GroupAccentRadius = 1.5;

    /// <summary>分组标题文字与左侧竖条之间的间距。</summary>
    private static readonly Thickness GroupTitleMargin = new(7, 0, 0, 0);

    /// <summary>分组标题条内边距：左右 9、上下 6。</summary>
    private static readonly Thickness GroupHeaderPadding = new(10, 7);

    /// <summary>分组内容区行间距：字段框 30 高，配 8 的行距让整组更紧凑、少留空。</summary>
    private const double GroupContentSpacing = 8.0;

    /// <summary>分组内容区四周留白：左 12、上 10、右 12、下 12。</summary>
    private static readonly Thickness GroupContentMargin = new(14, 12, 14, 14);

    /// <summary>分组卡片描边线宽。</summary>
    private const double GroupCardBorderThickness = 1.0;

    /// <summary>分组卡片圆角。</summary>
    private const double GroupCardCornerRadius = 8.0;

    /// <summary>单位后缀与数字控件之间的间距。</summary>
    private static readonly Thickness UnitLabelMargin = new(6, 0, 0, 0);

    /// <summary>九宫格预览的固定宽度（保持与表单两列同宽）。</summary>
    private const double NineSlicePreviewWidth = 520.0;

    /// <summary>九宫格预览的固定高度，保证拖动边距线时有足够操作空间。</summary>
    private const double NineSlicePreviewHeight = 300.0;

    /// <summary>九宫格预览上下留白，避免与分组标题条贴死。</summary>
    private static readonly Thickness NineSlicePreviewMargin = new(0, 2, 0, 2);

    /// <summary>九宫格说明文字字号，略小于正文以示层级。</summary>
    private const double FormHintFontSize = 11.0;

    /// <summary>分组展开时的箭头字符。</summary>
    private const string ExpandGlyph = "▾";

    /// <summary>分组收起时的箭头字符。</summary>
    private const string CollapseGlyph = "▸";

    private const double ListPanelWidth = 300.0;

    /// <summary>左侧列表与右侧属性区之间的间距。</summary>
    private const double BodyPanelGap = 8.0;

    /// <summary>面板整体四周留白。</summary>
    private static readonly Thickness BodyMargin = new(4);

    /// <summary>列表标题与下方页签之间的留白。</summary>
    private static readonly Thickness ListHeaderMargin = new(0, 0, 0, 4);

    /// <summary>类型页签与下方列表之间的留白。</summary>
    private static readonly Thickness ListTabsMargin = new(0, 0, 0, 6);

    /// <summary>类型页签内边距。</summary>
    private static readonly Thickness TypeTabPadding = new(4, 3);

    /// <summary>类型页签之间的间距。</summary>
    private const double TypeTabGap = 2.0;

    /// <summary>列表卡片内容区四周留白。</summary>
    private static readonly Thickness ListCardMargin = new(6);

    /// <summary>列表卡片标题、页签、列表三行之间的间距。</summary>
    private const double ListCardRowSpacing = 6.0;

    /// <summary>元件列表宿主自身的内边距。</summary>
    private static readonly Thickness ListHostMargin = new(4);

    /// <summary>元件列表行缩略图边长。</summary>
    private const double RowThumbnailSize = 52.0;

    /// <summary>元件列表行缩略图圆角。</summary>
    private const double RowThumbnailCornerRadius = 3.0;

    /// <summary>元件列表行上下留白。</summary>
    private const double RowVerticalPadding = 5.0;

    /// <summary>元件列表行高：缩略图边长加两侧留白，保持与章节列表同款节奏。</summary>
    private const double RowHeight = RowThumbnailSize + RowVerticalPadding * 2;

    /// <summary>行尾「显示」勾选列宽。</summary>
    private const double RowToggleWidth = 24.0;

    /// <summary>行文本区外边距：左 8、上 0、右 4、下 0。</summary>
    private static readonly Thickness RowTextMargin = new(8, 0, 4, 0);

    /// <summary>行名称与描述之间的间距。</summary>
    private const double RowTextSpacing = 1.0;

    /// <summary>行描述文字字号。</summary>
    private const double RowDescFontSize = 11.0;

    /// <summary>空列表提示外边距。</summary>
    private static readonly Thickness EmptyListHintMargin = new(2, 6);

    /// <summary>面板卡片描边线宽。</summary>
    private const double PanelBorderThickness = 1.0;

    /// <summary>左右两栏面板卡片的圆角半径，与全局卡片语言保持一致（卡片 8）。</summary>
    private const double PanelCornerRadius = 8.0;

    /// <summary>属性表单宿主的外边距。</summary>
    private static readonly Thickness FormHostMargin = new(12, 10, 12, 12);

    /// <summary>属性表单内部分组之间的间距。</summary>
    private const double FormHostSpacing = 8.0;

    /// <summary>属性分组之间的间距。</summary>
    private const double FormGroupsSpacing = 10.0;

    /// <summary>空选中提示的最大宽度，避免长提示横跨整屏。</summary>
    private const double EmptyHintMaxWidth = 640.0;

    /// <summary>空选中提示的外边距。</summary>
    private static readonly Thickness EmptyHintMargin = new(0, 16, 0, 8);

    #region 工具条常量

    /// <summary>工具条标题文字。</summary>
    private const string ToolbarTitle = "物品元件编辑模式";

    /// <summary>工具条状态提示初始文字。</summary>
    private const string ToolbarHint = "修改实时生效，点「保存」写盘并同步场景实例";

    /// <summary>保存按钮文字。</summary>
    private const string SaveButtonText = "保存";

    /// <summary>添加按钮文字。</summary>
    private const string AddButtonText = "添加元件";

    /// <summary>导入按钮文字。</summary>
    private const string ImportButtonText = "导入配置";

    /// <summary>导出按钮文字。</summary>
    private const string ExportButtonText = "导出配置";

    /// <summary>保存按钮固定宽度，略窄以突出主操作。</summary>
    private const double SaveButtonWidth = 70.0;

    /// <summary>普通操作按钮固定宽度，保证三个按钮等宽。</summary>
    private const double ActionButtonWidth = 86.0;

    /// <summary>工具条按钮之间的间距。</summary>
    private const double ToolbarButtonSpacing = 6.0;

    /// <summary>工具条状态文字左右留白。</summary>
    private static readonly Thickness ToolbarStatusMargin = new(10, 0, 10, 0);

    /// <summary>工具条外框内边距。</summary>
    private static readonly Thickness ToolbarPadding = new(8, 5);

    /// <summary>工具条按钮内边距。</summary>
    private static readonly Thickness ToolbarButtonPadding = new(6, 4);

    #endregion

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

    #region 持久化常量

    /// <summary>导入对话框标题。</summary>
    private const string ImportDialogTitle = "导入元件配置表";

    /// <summary>导出对话框标题。</summary>
    private const string ExportDialogTitle = "导出元件配置表";

    #endregion

    #region 字段

    private readonly TemplateStore _store;
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
    // 分组按左右两栏排布：宽窗口里不再把所有卡片挤在左边半幅、右边整片空白
    private readonly Grid _groupsHost = new();
    private readonly StackPanel _groupColumnLeft = new();
    private readonly StackPanel _groupColumnRight = new();
    private readonly TextBlock _emptyHint = new();
    private readonly TextBlock _statusLabel = new();
    private readonly Dictionary<MapObjectType, Button> _tabButtons = new();
    private readonly List<Action> _committers = new();

    #endregion

    #region 构造

    /// <summary>初始化面板。</summary>
    /// <param name="store">模板仓库，负责 JSON / CSV 持久化。</param>
    /// <param name="notify">状态提示回调。</param>
    public ItemElementPanel(TemplateStore store, Action<string> notify)
    {
        _store = store;
        _notify = notify;
        _assets = new AssetLibrary(store.OutputDirectory);

        // 页面底色统一成浅青，和迷雾 / 资源 / 剧情页保持一致，不再用中性灰
        Background = UiTheme.PanelSurface;

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

    /// <summary>
    /// 解析模板对应的素材路径：优先 <see cref="ObjectTemplate.ImagePath"/> 的文件名主干，
    /// 再退回 <see cref="ObjectTemplate.ResName"/>。列表缩略图与右侧预览共用同一口径。
    /// </summary>
    /// <param name="template">目标模板。</param>
    /// <returns>找到的素材绝对路径；找不到为 <see langword="null"/>。</returns>
    private string? ResolveTemplateImagePath(ObjectTemplate template)
    {
        var imageName = !string.IsNullOrWhiteSpace(template.ImagePath)
            ? Path.GetFileNameWithoutExtension(template.ImagePath)
            : template.ResName;
        return _assets.FindImage(imageName) ?? _assets.FindImage(template.ResName);
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

    /// <summary>把表单里的临时模板写回表行：编辑态整体覆盖名称、资源名、显示层级与共有可编辑字段。</summary>
    /// <param name="row">目标行。</param>
    /// <param name="template">临时模板。</param>
    private static void ApplyTemplateToRow(ItemElementRow row, ObjectTemplate template)
    {
        // 表列映射由行模型统一维护，面板只负责编辑态的身份字段覆盖
        row.ApplyEditableFields(template);
        row.ResName = template.ResName;
        row.DisplayLayer = template.DisplayLayer;
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
