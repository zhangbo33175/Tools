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

    private static readonly IBrush PanelBorderBrush = UiTheme.PanelBorder;
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));

    private static readonly string[] FilterNames = { "全部", "背景", "建筑", "人物", "物件", "猫猫" };

    #endregion

    #region 字段

    private readonly TemplateStore _store;
    private readonly Action<string> _notify;
    private readonly AssetLibrary _assets;

    private readonly StackPanel _unconfiguredHost = new();
    private readonly StackPanel _configuredHost = new();
    private readonly StackPanel _configuredHeaderHost = new();
    private readonly Grid _previewHost = new();
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
    private string _filter = "全部";
    private string? _selectedName;

    /// <summary>
    /// 「地图保存目录」的绝对路径（来自地图配置弹窗）。扫描只认这一棵目录 ——
    /// 资源整理是整理 Map 的图片（烘焙切片 / 分区底图），不是扫整个工程。
    /// </summary>
    private string _mapDirectory = string.Empty;

    /// <summary>地图物件配置表文件夹（夹内多张表）：按图片名绑定的行数据。</summary>
    private ItemConfigSet? _itemTable;

    /// <summary>正在编辑的已配置模板；非空时「添加到配置表」变成「修改到配置表」。</summary>
    private ObjectTemplate? _editing;

    /// <summary>物件配置表在内存里被改过（新增 / 修改 / 删除行），保存时需要落盘。</summary>
    private bool _tableDirty;

    private readonly TextBlock _csvInfoText = new()
    {
        Foreground = new SolidColorBrush(Color.FromRgb(0x0B, 0x6E, 0x9B)),
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
    };

    private Button? _addButton;

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
        ScanUnconfigured(silent: true);
    }

    /// <summary>接入地图物件配置表文件夹（多张表按图片名 bind，选中即回填、编辑可回写）。</summary>
    /// <param name="table">已加载的表集合；null 表示未配置或读不到。</param>
    public void SetItemTable(ItemConfigSet? table)
    {
        _itemTable = table;
        _tableDirty = false;
        ScanUnconfigured(silent: true);
        RefreshConfiguredTable();
    }

    #endregion

    #region 分类

    private static string CategoryOf(string name)
    {
        var lower = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();

        if (lower.StartsWith("bg_") || lower.Contains("background") || lower.Contains("grass") || lower.Contains("ground"))
        {
            return "bg";
        }

        if (lower.Contains("building") || lower.Contains("house") || lower.Contains("tower") || lower.Contains("wall"))
        {
            return "building";
        }

        if (lower.Contains("cat"))
        {
            return "cat";
        }

        if (lower.Contains("char") || lower.Contains("player") || lower.Contains("npc") || lower.Contains("person"))
        {
            return "character";
        }

        return "prop";
    }

    private static string CategoryDisplay(string category) => category switch
    {
        "bg" => "背景",
        "building" => "建筑",
        "character" => "人物",
        "cat" => "猫猫",
        _ => "物件",
    };

    /// <summary>
    /// 图片的「层」显示名：先按 Unity 八层目录（烘焙产物带 Chapter/Level/{层}）判断，
    /// 认不出来再按文件名前缀推断。这样 <c>Greenery\bg_grass_1.png</c> 显示「绿化」而不是「背景」。
    /// </summary>
    /// <param name="path">图片绝对路径。</param>
    /// <returns>中文层名。</returns>
    private static string LayerNameOf(string path)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        foreach (var layer in MapLayerKeys.AllKeys)
        {
            if (string.Equals(folder, layer, StringComparison.OrdinalIgnoreCase))
            {
                return LayerDisplayName(layer);
            }
        }

        return CategoryDisplay(CategoryOf(path));
    }

    private static string LayerDisplayName(string layerKey) => layerKey switch
    {
        "Background" => "背景",
        "Greenery" => "绿化",
        "Road" => "道路",
        "Buildings" => "建筑",
        "Characters" => "人物",
        "Props" => "物件",
        "Foreground" => "前景",
        "Bubbles" => "气泡",
        _ => layerKey,
    };

    private static MapObjectType TypeOfCategory(string category) => category switch
    {
        "building" => MapObjectType.Building,
        "character" or "cat" => MapObjectType.Character,
        _ => MapObjectType.Prop,
    };

    #endregion
}

