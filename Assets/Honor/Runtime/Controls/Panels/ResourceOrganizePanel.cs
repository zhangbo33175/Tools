/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOrganizePanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   资源整理面板：扫描未配置资源、补字段、写回配置表
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

/// <summary>
/// 资源整理面板，结构对齐 Unity 版 ResourceOrganizeModule：
/// 左侧「未配置资源」+ 扫描 + 分类过滤 + 预览图；右侧「选中项」字段表 + 「添加到配置表」，
/// 下方「已配置资源」表格（四行表头 + 可编辑单元格 + 操作列），底部三枚文件级按钮。
/// </summary>
public sealed partial class ResourceOrganizePanel : UserControl
{
    #region 常量与画刷

    private const double LeftWidth = 356.0;

    /// <summary>「添加到配置表」按钮文案。</summary>
    private const string AddToTableButtonText = "添加到配置表";

    /// <summary>编辑已配置模板时的按钮文案。</summary>
    private const string UpdateTableButtonText = "修改到配置表";

    /// <summary>未配置资源列表缩略图像素宽高。</summary>
    private const int ThumbnailPixelSize = 36;

    /// <summary>缩略图圆角。</summary>
    private static readonly CornerRadius ThumbnailCornerRadius = new(3);

    /// <summary>缩略图缓存上限，超过后整体清空，避免长时间扫描占用内存。</summary>
    private const int ThumbnailCacheLimit = 400;

    /// <summary>未配置资源行高度：36px 缩略图 + 上下各 5px 留白。</summary>
    private const double UnconfiguredRowHeight = 46.0;

    /// <summary>配置表目录名（相对模板输出目录）。</summary>
    private const string TablesDirectoryName = "Tables";

    private static readonly IBrush PanelBorderBrush = UiTheme.PanelBorder;

    /// <summary>页面里外层卡片的圆角，与全局卡片语言一致（卡片 8）。</summary>
    private const double PanelCornerRadius = 8.0;

    /// <summary>分类筛选按钮各自的圆角，与全局按钮语言一致（按钮 5）。</summary>
    private const double FilterButtonCornerRadius = 5.0;

    private static readonly string[] FilterNames = ResourceCategories.FilterNames;

    #endregion

    #region 字段

    private readonly TemplateStore _store;
    private readonly Action<string> _notify;
    private readonly AssetLibrary _assets;

    private readonly StackPanel _unconfiguredHost = new();
    private readonly StackPanel _configuredHost = new();
    private readonly StackPanel _configuredHeaderHost = new();
    private readonly Grid _previewHost = new();

    /// <summary>左侧资源列表滚动容器；切分类时回到顶部，避免沿用上一张长表的偏移。</summary>
    private ScrollViewer? _resourceListScroll;

    /// <summary>已配置表当前行号 → 该行全部单元格，供整行悬停高亮使用。</summary>
    private readonly Dictionary<int, List<Border>> _rowHoverCells = new();

    /// <summary>当前鼠标悬停的配置表行号；-1 表示没有。</summary>
    private int _hoveredRow = -1;

    private readonly TextBox _search = new();
    private readonly TextBlock _countText = new();
    private readonly Dictionary<string, Button> _filterButtons = new();
    private readonly TextBox _fieldId = new();
    private readonly TextBox _fieldImagePath = new();
    private readonly TextBox _fieldName = new();
    private readonly TextBox _fieldAnim = new();
    private readonly TextBox _fieldLoop = new();
    private readonly TextBox _fieldSpeed = new();
    private readonly TextBox _fieldOrder = new();
    private readonly TextBox _fieldShadow = new();
    private readonly TextBox _fieldCollectible = new();

    private List<ObjectTemplate> _templates = new();
    private MapConfigData? _config;
    private Dictionary<string, string> _unconfigured = new();
    private string _filter = ResourceCategories.AllDisplayName;

    /// <summary>上一次扫描在「地图保存目录」里枚举到的 PNG 总数（用于空态区分「没图」与「都已配置」）。</summary>
    private int _lastScanTotal;
    private string? _selectedName;

    /// <summary>当前选中资源的磁盘路径；分类判断与默认收集物标记都以它为准。</summary>
    private string? _selectedPath;

    /// <summary>
    /// 多选集合：普通点击清空后只放一项，Ctrl 点击切换单项，Shift 点击按当前筛选顺序连选。
    /// 第一项仅作为表单回填的主项，批量添加时逐项生成 ID / Name / ImagePath。
    /// </summary>
    private readonly List<(string Name, string Path)> _selection = new();

    /// <summary>Shift 连选的锚点路径。</summary>
    private string? _selectionAnchorPath;

    /// <summary>
    /// 「地图保存目录」的绝对路径（来自地图配置弹窗）。扫描只认这一棵目录 ——
    /// 资源整理是整理 Map 的图片（烘焙切片 / 分区底图），不是扫整个工程。
    /// </summary>
    private string _mapDirectory = string.Empty;

    /// <summary>地图保存目录的「资源名 → PNG 路径」惰性索引，供配置表行补缩略图。</summary>
    private Dictionary<string, string>? _mapImageIndex;

    /// <summary>地图物件配置表文件夹（夹内多张表）：按图片名绑定的行数据。</summary>
    private ItemConfigSet? _itemTable;

    /// <summary>正在编辑的已配置模板；非空时「添加到配置表」变成「修改到配置表」。</summary>
    private ObjectTemplate? _editing;

    /// <summary>物件配置表在内存里被改过（新增 / 修改 / 删除行），保存时需要落盘。</summary>
    private bool _tableDirty;

    private readonly TextBlock _csvInfoText = new()
    {
        Foreground = UiTheme.InfoText,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
    };

    private Button? _addButton;

    /// <summary>「新建收集物表」按钮：只在收集物分类尚无独立表时显示。</summary>
    private Button? _createTableButton;

    #endregion

    #region 事件

    /// <summary>配置表内容变化。</summary>
    public event EventHandler? CatalogChanged;

    #endregion

    #region 构造

    /// <summary>初始化资源整理面板。</summary>
    /// <param name="store">模板存储。</param>
    /// <param name="notify">状态栏提示回调。</param>
    public ResourceOrganizePanel(TemplateStore store, Action<string> notify)
    {
        _store = store;
        _notify = notify;
        _assets = new AssetLibrary(store.OutputDirectory);
        // ---TODO ID 管理：页面提供数据源，ID 规则与顺延统一交给协调器。
        _idCoordinator = new TemplateIdCoordinator(
            () => _itemTable,
            () => _templates,
            ApplyIdMapping);
        Content = BuildLayout();
    }

    #endregion

    #region 绑定

    /// <summary>接入模板集合与地图配置。</summary>
    /// <param name="templates">模板集合（与主窗口共享）。</param>
    /// <param name="config">当前地图配置，用于删除前的引用校验。</param>
    public void Attach(List<ObjectTemplate> templates, MapConfigData config)
    {
        _templates = templates;
        _config = config;
        // ---TODO ID 管理：接入后修复历史重复 ID；改号由协调器统一处理。
        RepairAllTableIdsIfNeeded();

        RefreshAll();
    }

    /// <summary>刷新未配置列表、已配置表格与预览。</summary>
    public void RefreshAll()
    {
        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
    }

    /// <summary>
    /// 设置「地图保存目录」：扫描/预览/添加配置表都以这棵目录为准。
    /// 主窗口在地图配置保存后与启动时各调一次。
    /// </summary>
    /// <param name="directory">地图保存目录的绝对路径。</param>
    public void SetMapDirectory(string? directory)
    {
        var normalized = string.IsNullOrWhiteSpace(directory) ? string.Empty : directory!;
        if (string.Equals(_mapDirectory, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _mapDirectory = normalized;
        _mapImageIndex = null;
        ScanUnconfigured(silent: true);
    }

    /// <summary>当前接入的配置表集合；未接入为 null。</summary>
    public ItemConfigSet? ItemTable => _itemTable;

    /// <summary>接入地图物件配置表文件夹（多张表按图片名 bind，选中即回填、编辑可回写）。</summary>
    /// <param name="table">已加载的表集合；null 表示未配置或读不到。</param>
    public void SetItemTable(ItemConfigSet? table)
    {
        _itemTable = table;
        _tableDirty = false;
        // ---TODO ID 管理：换表后同样走统一修复入口，避免只在 Attach 时补漏。
        RepairAllTableIdsIfNeeded();

        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
    }

    #endregion

    #region 分类

    private static string CategoryDisplay(string category) => ResourceCategories.DisplayNameOfKey(category);

    /// <summary>
    /// 图片的「层」显示名：先按 Unity 八层目录（烘焙产物带 Chapter/Level/{层}）判断，
    /// 认不出来再按文件名关键词推断分类。
    /// </summary>
    /// <param name="path">图片绝对路径。</param>
    /// <returns>中文层名。</returns>
    private static string LayerNameOf(string path)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        return ResourceCategories.LayerDisplayName(folder)
            ?? CategoryDisplay(ResourceCategories.Classify(path));
    }

    #endregion
}

