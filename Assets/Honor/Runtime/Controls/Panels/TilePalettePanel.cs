/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TilePalettePanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   瓦片调色板面板：Edit* 编辑态、笔刷工具、纹理网格与九宫邻接规则表
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 瓦片调色板面板（对照 Unity Tile Palette + Rule Tile）：
/// 上半是纹理调色板与画笔工具，Edit* 打开后画布左键落笔；下半是九宫格邻接规则表，
/// 每条规则用「必须有 / 必须没有 / 无所谓」三态描述邻居，可一键按规则重选贴图。
/// </summary>
public sealed class TilePalettePanel : UserControl
{
    #region 字段

    private readonly MapCanvas _canvas;
    private readonly AssetLibrary _assets;
    private readonly Action<string> _notify;

    /// <summary>调色板格子容器。</summary>
    private readonly WrapPanel _paletteWrap = new()
    {
        Margin = new Thickness(4, 2, 4, 4),
        Orientation = Orientation.Horizontal,
    };

    /// <summary>规则列表容器。</summary>
    private readonly StackPanel _ruleStack = new()
    {
        Margin = new Thickness(4, 2, 4, 4),
        Spacing = 4,
    };

    /// <summary>底部状态行。</summary>
    private readonly TextBlock _status = new()
    {
        FontSize = 11,
        Foreground = UiTheme.TextSecondary,
        Margin = new Thickness(8, 2, 8, 6),
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>调色板里的贴图路径（绝对路径或资源名）。</summary>
    private readonly List<string> _images = new();

    /// <summary>贴图路径 → 缩略图边框，用于刷新选中态。</summary>
    private readonly Dictionary<string, Border> _thumbs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>工具按钮，用于刷新选中态。</summary>
    private readonly Dictionary<TilePaintTool, Button> _toolButtons = new();

    private Button _btnEdit = null!;

    private Button _btnUndo = null!;
    private Button _btnApplyRules = null!;

    /// <summary>当前调色板选中的贴图。</summary>
    private string _selectedImage = string.Empty;

    #endregion

    #region 构造

    /// <summary>创建一个瓦片调色板面板。</summary>
    /// <param name="canvas">目标画布：落笔、规则重算都作用在它上面。</param>
    /// <param name="assets">资源库：缩略图与画布瓦片共用同一套解析。</param>
    /// <param name="notify">状态消息回调（走主窗口底部提示行）。</param>
    public TilePalettePanel(MapCanvas canvas, AssetLibrary assets, Action<string> notify)
    {
        _canvas = canvas;
        _assets = assets;
        _notify = notify;

        Content = BuildLayout();
        LoadPalette();
        RefreshAll();

        _canvas.TileLayerChanged += (_, _) => RefreshStatus();
        _canvas.TilePicked += (_, image) =>
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                _notify("这一格没有瓦片，取不到画笔");
                return;
            }

            SelectImage(image);
            _notify($"已取色：{Path.GetFileName(image)}");
        };
    }

    #endregion

    #region 布局

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Background = UiTheme.PanelSurface,
        };

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        var palette = BuildSection("调色板", BuildPaletteBody(), BuildPaletteActions());
        Grid.SetRow(palette, 1);
        root.Children.Add(palette);

        var rules = BuildSection("邻接规则（九宫格）", BuildRuleBody(), BuildRuleActions());
        Grid.SetRow(rules, 2);
        root.Children.Add(rules);

        Grid.SetRow(_status, 3);
        root.Children.Add(_status);

        return root;
    }

    /// <summary>顶部工具条：Edit* 开关 + 四个画笔工具 + 撤销 / 清空。</summary>
    private Control BuildToolbar()
    {
        var bar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(6, 6, 6, 2),
        };

        _btnEdit = CreateButton("编辑", "进入 / 退出瓦片编辑态（激活时显示 编辑*）", ToggleEdit);
        _btnEdit.Margin = new Thickness(0, 0, 6, 4);
        bar.Children.Add(_btnEdit);

        bar.Children.Add(CreateToolButton(TilePaintTool.Brush, "笔刷", "按格涂抹，按住拖动连画"));
        bar.Children.Add(CreateToolButton(TilePaintTool.Fill, "填充", "把连通的同图格一起换掉"));
        bar.Children.Add(CreateToolButton(TilePaintTool.Erase, "擦除", "按格删除瓦片（右键随时擦除）"));
        bar.Children.Add(CreateToolButton(TilePaintTool.Pick, "取色", "点一格，把它的贴图取成当前画笔"));

        _btnUndo = CreateButton("撤销", "撤销最近一笔瓦片操作", () =>
        {
            var ok = _canvas.UndoLastTileStroke();
            _notify(ok ? "已撤销上一笔瓦片操作" : "没有可撤销的瓦片操作");
            RefreshStatus();
        });
        _btnUndo.Margin = new Thickness(6, 0, 0, 4);
        bar.Children.Add(_btnUndo);

        var clear = CreateButton("清空", "清空整张地图的瓦片（可撤销）", () =>
        {
            var count = _canvas.ClearTileCells();
            _notify(count > 0 ? $"已清空 {count} 格瓦片，点撤销可恢复" : "当前没有瓦片可清空");
        });
        clear.Margin = new Thickness(4, 0, 0, 4);
        bar.Children.Add(clear);

        return bar;
    }

    /// <summary>调色板区域：可滚动的缩略图网格。</summary>
    private Control BuildPaletteBody()
    {
        return new ScrollViewer
        {
            Content = _paletteWrap,
            MaxHeight = 132,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>调色板右上角操作：「＋ 纹理」。</summary>
    private Control BuildPaletteActions()
    {
        var add = CreateButton("＋ 纹理", "把一张贴图加进调色板", async () => await AddImageAsync());
        add.Margin = new Thickness(4, 0, 0, 0);
        return add;
    }

    /// <summary>规则区域：可滚动的规则行列表。</summary>
    private Control BuildRuleBody()
    {
        return new ScrollViewer
        {
            Content = _ruleStack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>规则右上角操作：新增规则 / 按规则重算。</summary>
    private Control BuildRuleActions()
    {
        var add = CreateButton("＋ 规则", "用当前画笔贴图新增一条规则", AddRule);
        add.Margin = new Thickness(4, 0, 0, 0);

        _btnApplyRules = CreateButton("应用", "按规则把已铺瓦片重选成对应贴图", ApplyRules);
        _btnApplyRules.Margin = new Thickness(4, 0, 0, 0);

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(add);
        panel.Children.Add(_btnApplyRules);
        return panel;
    }

    /// <summary>卡片外壳：标题条 + 右上操作 + 内容。</summary>
    /// <param name="title">标题文本。</param>
    /// <param name="body">主体内容。</param>
    /// <param name="actions">右上角操作（可为空）。</param>
    /// <returns>卡片控件。</returns>
    private static Control BuildSection(string title, Control body, Control? actions)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var label = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        header.Children.Add(label);

        if (actions is not null)
        {
            Grid.SetColumn(actions, 1);
            actions.Margin = new Thickness(0, 0, 6, 0);
            actions.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(actions);
        }

        var headerBand = new Border
        {
            Background = UiTheme.Tiles.RuleRow,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 5),
            Child = header,
        };

        var card = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        card.Children.Add(headerBand);
        Grid.SetRow(body, 1);
        card.Children.Add(body);

        return new Border
        {
            Classes = { "card" },
            Margin = new Thickness(6, 4, 6, 2),
            Padding = new Thickness(0),
            Child = card,
        };
    }

    /// <summary>统一按钮外观：沿用主界面的 mini 风格，圆角与色阶由样式表决定。</summary>
    /// <param name="text">按钮文本。</param>
    /// <param name="tip">悬停提示。</param>
    /// <param name="onClick">点击回调。</param>
    /// <returns>按钮控件。</returns>
    private static Button CreateButton(string text, string tip, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Classes = { "mini" },
            Padding = new Thickness(8, 3),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>创建一个画笔工具按钮并登记到可选组。</summary>
    /// <param name="tool">工具类型。</param>
    /// <param name="text">按钮文本。</param>
    /// <param name="tip">悬停提示。</param>
    /// <returns>按钮控件。</returns>
    private Button CreateToolButton(TilePaintTool tool, string text, string tip)
    {
        var button = CreateButton(text, tip, () => SetTool(tool));
        button.Classes.Add("markTool");
        button.Margin = new Thickness(0, 0, 4, 4);
        _toolButtons[tool] = button;
        return button;
    }

    #endregion

    #region 编辑态与工具

    /// <summary>Edit* 开关：打开时画布左键不再选中物件，而是落到瓦片层。</summary>
    private void ToggleEdit()
    {
        if (_canvas.Config is null)
        {
            _notify("请先打开或新建一张地图，再进入瓦片编辑");
            return;
        }

        var next = !_canvas.TileEditEnabled;
        if (next && string.IsNullOrWhiteSpace(_selectedImage) && _canvas.TileTool != TilePaintTool.Erase)
        {
            _notify("先在调色板里选一张瓦片，再进入编辑态");
            return;
        }

        _canvas.TileEditEnabled = next;
        RefreshEditState();
        _notify(next
            ? "瓦片编辑已激活（编辑*）：左键落笔、右键擦除；再点一次退出"
            : "已退出瓦片编辑，画布恢复选中 / 拖动");
    }

    /// <summary>切换工具并刷新按钮选中态。</summary>
    /// <param name="tool">目标工具。</param>
    private void SetTool(TilePaintTool tool)
    {
        _canvas.TileTool = tool;
        foreach (var pair in _toolButtons)
        {
            SetButtonActive(pair.Value, pair.Key == tool);
        }

        RefreshStatus();
    }

    /// <summary>刷新 Edit* 按钮的文案与高亮。</summary>
    private void RefreshEditState()
    {
        var on = _canvas.TileEditEnabled;
        _btnEdit.Content = on ? "编辑*" : "编辑";
        SetButtonActive(_btnEdit, on);
        RefreshStatus();
    }

    /// <summary>按钮的「激活」外观：青底青边，编辑器里统一用它表示当前生效项。</summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="active">是否激活。</param>
    private static void SetButtonActive(Button button, bool active)
    {
        button.Background = active ? UiTheme.Tiles.EditActiveSurface : UiTheme.Card;
        button.BorderBrush = active ? UiTheme.Tiles.EditActiveBorder : UiTheme.PanelBorder;
        button.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
    }

    #endregion

    #region 调色板

    /// <summary>读本机调色板清单（纹理属于编辑偏好，单独存在 output\Config 下）。</summary>
    private void LoadPalette()
    {
        _images.Clear();
        foreach (var path in TilePaletteStore.Load())
        {
            if (!string.IsNullOrWhiteSpace(path) && !_images.Exists(item => TilePaths.Same(item, path)))
            {
                _images.Add(path);
            }
        }
    }

    /// <summary>重建缩略图网格，并恢复选中态。</summary>
    private void RebuildPalette()
    {
        _paletteWrap.Children.Clear();
        _thumbs.Clear();

        foreach (var image in _images)
        {
            var thumb = BuildThumb(image);
            _thumbs[image] = thumb;
            _paletteWrap.Children.Add(thumb);
        }

        if (_images.Count == 0)
        {
            _paletteWrap.Children.Add(new TextBlock
            {
                Text = "点右上「＋ 纹理」挑一张地表图，就有了第一块瓦片。",
                FontSize = 11,
                Foreground = UiTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 8, 4, 4),
            });
        }

        RefreshThumbSelection();
    }

    /// <summary>单张缩略图：46×46 卡片，选中时描边变强调青。</summary>
    /// <param name="image">贴图路径。</param>
    /// <returns>缩略图控件。</returns>
    private Border BuildThumb(string image)
    {
        var bitmap = _assets.Load(image);
        var border = new Border
        {
            Width = 46,
            Height = 46,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = UiTheme.Tiles.ThumbBorder,
            Background = UiTheme.Tiles.ThumbSurface,
            ClipToBounds = true,
            Child = bitmap is null
                ? new TextBlock
                {
                    Text = "×",
                    FontSize = 12,
                    Foreground = UiTheme.Error,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }
                : new Image { Source = bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(2) },
        };

        ToolTip.SetTip(border, Path.GetFileName(image));
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                SelectImage(image);
                e.Handled = true;
            }
        };

        return border;
    }

    /// <summary>选中一张贴图作为画笔。</summary>
    /// <param name="image">贴图路径。</param>
    private void SelectImage(string image)
    {
        if (!_images.Exists(item => TilePaths.Same(item, image)))
        {
            _images.Add(image);
            TilePaletteStore.Save(_images);
            RebuildPalette();
        }

        _selectedImage = image;
        _canvas.TileBrushImage = image;
        RefreshThumbSelection();
        RefreshStatus();
    }

    /// <summary>刷新缩略图选中描边。</summary>
    private void RefreshThumbSelection()
    {
        foreach (var pair in _thumbs)
        {
            var active = TilePaths.Same(pair.Key, _selectedImage);
            pair.Value.BorderBrush = active ? UiTheme.Tiles.ThumbSelected : UiTheme.Tiles.ThumbBorder;
            pair.Value.Background = active ? UiTheme.Tiles.ThumbSelectedSurface : UiTheme.Tiles.ThumbSurface;
            pair.Value.BorderThickness = new Thickness(active ? 2 : 1);
        }
    }

    /// <summary>通过文件对话框挑一张贴图加进调色板。</summary>
    /// <returns>异步任务。</returns>
    private async System.Threading.Tasks.Task AddImageAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var filter = new FilePickerFileType("图片")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" },
        };

        var path = await FileDialogService.PickFileAsync(top, "选择瓦片贴图", filter);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!_images.Exists(item => TilePaths.Same(item, path)))
        {
            _images.Add(path);
            TilePaletteStore.Save(_images);
        }

        SelectImage(path);
        _notify($"已加入调色板：{Path.GetFileName(path)}");
    }

    #endregion

    #region 规则表

    /// <summary>按当前画笔贴图新增一条兜底规则（九宫格全「无所谓」）。</summary>
    private void AddRule()
    {
        var config = _canvas.Config;
        if (config is null)
        {
            _notify("请先打开或新建一张地图");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedImage))
        {
            _notify("先在调色板里选一张瓦片，再新增规则");
            return;
        }

        var rule = new TileRuleData
        {
            Name = $"规则 {config.TileRules.Count + 1}",
            Image = _selectedImage,
        };
        rule.Normalize();
        config.TileRules.Add(rule);
        RebuildRules();
        RefreshStatus();
        _notify("已新增规则：点九宫格里的小格切换「必须有 / 必须没有 / 无所谓」");
    }

    /// <summary>按规则重算整张瓦片层。</summary>
    private void ApplyRules()
    {
        var changed = _canvas.ApplyTileRules();
        if (changed < 0)
        {
            _notify("还没有规则，先点「＋ 规则」新增一条");
            return;
        }

        _notify(changed > 0 ? $"已按规则重选 {changed} 格瓦片（可撤销）" : "所有瓦片已经符合规则，没有变化");
    }

    /// <summary>重建规则行。</summary>
    private void RebuildRules()
    {
        _ruleStack.Children.Clear();
        var config = _canvas.Config;
        if (config is null)
        {
            return;
        }

        if (config.TileRules.Count == 0)
        {
            _ruleStack.Children.Add(new TextBlock
            {
                Text = "还没有规则。点「＋ 规则」用当前画笔建一条，再点九宫格配置邻居。",
                FontSize = 11,
                Foreground = UiTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 6, 4, 6),
            });
            return;
        }

        for (var i = 0; i < config.TileRules.Count; i++)
        {
            _ruleStack.Children.Add(BuildRuleRow(config, config.TileRules[i], i));
        }
    }

    /// <summary>单条规则行：缩略图 + 名称 + 九宫格三态 + 删除。</summary>
    /// <param name="config">地图配置。</param>
    /// <param name="rule">规则数据。</param>
    /// <param name="index">序号，用于交替底色。</param>
    /// <returns>规则行控件。</returns>
    private Control BuildRuleRow(MapConfigData config, TileRuleData rule, int index)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 0, 0, 2),
        };

        var bitmap = _assets.Load(rule.Image);
        var thumb = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = UiTheme.Tiles.ThumbBorder,
            Background = UiTheme.Tiles.ThumbSurface,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 6, 0),
            Child = bitmap is null
                ? null
                : new Image { Source = bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(2) },
        };
        row.Children.Add(thumb);

        var middle = new StackPanel { Spacing = 3 };

        var name = new TextBlock
        {
            Text = $"{rule.Name} · {Path.GetFileName(rule.Image)}",
            FontSize = 11,
            Foreground = UiTheme.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        middle.Children.Add(name);
        middle.Children.Add(BuildPatternGrid(rule));
        Grid.SetColumn(middle, 1);
        row.Children.Add(middle);

        var remove = new Button
        {
            Content = "×",
            Classes = { "mini" },
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4, 2, 0, 0),
        };
        ToolTip.SetTip(remove, "删除这条规则");
        remove.Click += (_, _) =>
        {
            config.TileRules.Remove(rule);
            RebuildRules();
            RefreshStatus();
            _notify($"已删除规则：{rule.Name}");
        };
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);

        return new Border
        {
            Background = index % 2 == 0 ? UiTheme.Tiles.RuleRow : UiTheme.Card,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(4),
            Child = row,
        };
    }

    /// <summary>九宫格三态编辑器：点一下循环「无所谓 → 必须有 → 必须没有」。</summary>
    /// <param name="rule">目标规则。</param>
    /// <returns>九宫格控件。</returns>
    private Control BuildPatternGrid(TileRuleData rule)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        for (var index = 0; index < TilePatternCells.Count; index++)
        {
            if (index == TilePatternCells.Center)
            {
                // 中心是瓦片自己：不参与判定，画个实心青点表示「本格」，不给点击
                var self = new Border
                {
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Background = UiTheme.Tiles.NeighborSurface,
                    BorderThickness = new Thickness(1),
                    BorderBrush = UiTheme.PanelBorder,
                    Child = new Border
                    {
                        Width = 6,
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Background = UiTheme.Accent,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                Grid.SetRow(self, 1);
                Grid.SetColumn(self, 1);
                grid.Children.Add(self);
                continue;
            }

            var cell = BuildPatternCell(rule, index);
            Grid.SetRow(cell, index / TilePatternCells.Size);
            Grid.SetColumn(cell, index % TilePatternCells.Size);
            grid.Children.Add(cell);
        }

        return grid;
    }

    /// <summary>九宫格里的一个邻居位。</summary>
    /// <param name="rule">目标规则。</param>
    /// <param name="index">九宫格索引。</param>
    /// <returns>可点击的小方块。</returns>
    private Button BuildPatternCell(TileRuleData rule, int index)
    {
        var button = new Button
        {
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            Margin = new Thickness(1),
            FontSize = 10,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
        };
        ApplyPatternCellVisual(button, rule.Pattern[index]);

        ToolTip.SetTip(button, "点一下：无所谓 → 必须有 → 必须没有");
        button.Click += (_, _) =>
        {
            rule.Pattern[index] = TileNeighborStates.Cycle(rule.Pattern[index]);
            ApplyPatternCellVisual(button, rule.Pattern[index]);
        };

        return button;
    }

    /// <summary>三态外观：必须有 = 绿▲、必须没有 = 红✕、无所谓 = 灰点。</summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="state">三态值。</param>
    private static void ApplyPatternCellVisual(Button button, int state)
    {
        switch (TileNeighborStates.Normalize(state))
        {
            case TileNeighborStates.Required:
                button.Content = "▲";
                button.Foreground = UiTheme.White;
                button.Background = UiTheme.Tiles.NeighborRequired;
                button.BorderBrush = UiTheme.Tiles.NeighborRequired;
                break;

            case TileNeighborStates.Forbidden:
                button.Content = "✕";
                button.Foreground = UiTheme.White;
                button.Background = UiTheme.Tiles.NeighborForbidden;
                button.BorderBrush = UiTheme.Tiles.NeighborForbidden;
                break;

            default:
                button.Content = "·";
                button.Foreground = UiTheme.Tiles.NeighborAny;
                button.Background = UiTheme.Tiles.NeighborSurface;
                button.BorderBrush = UiTheme.Tiles.ThumbBorder;
                break;
        }
    }

    #endregion

    #region 刷新

    /// <summary>外部（换图 / 打开存档）调用：重建调色板、规则表与状态行。</summary>
    public void RefreshAll()
    {
        // 地图里已经用到的贴图自动进调色板，换图后不用手动再加一遍
        if (_canvas.Config is { } config)
        {
            foreach (var cell in config.TileCells)
            {
                if (!string.IsNullOrWhiteSpace(cell.Image) && !_images.Exists(item => TilePaths.Same(item, cell.Image)))
                {
                    _images.Add(cell.Image);
                }
            }
        }

        RebuildPalette();

        // 没有选中画笔时自动落到第一张：否则新建调色板后点「编辑」会被拦下来，多一步无谓点击
        if (string.IsNullOrWhiteSpace(_selectedImage) && _images.Count > 0)
        {
            _selectedImage = _images[0];
            _canvas.TileBrushImage = _selectedImage;
            RefreshThumbSelection();
        }

        RebuildRules();
        RefreshEditState();
        RefreshStatus();
    }

    /// <summary>底部状态：当前画笔、工具、已铺格数。</summary>
    private void RefreshStatus()
    {
        var config = _canvas.Config;
        var count = config?.TileCells.Count ?? 0;
        var brush = string.IsNullOrWhiteSpace(_selectedImage) ? "未选" : Path.GetFileName(_selectedImage);
        var tool = _canvas.TileTool switch
        {
            TilePaintTool.Brush => "笔刷",
            TilePaintTool.Fill => "填充",
            TilePaintTool.Erase => "擦除",
            _ => "取色",
        };

        // 菱形（等距）网格里一个格只有 4 条共边，规则只判九宫的四角；把口径写在状态行，免得配了半天规则没反应
        var adjacency = DrawShapes.IsDiamond(config?.DrawShape)
            ? " · 菱形只判九宫四角"
            : string.Empty;
        _status.Text = _canvas.TileEditEnabled
            ? $"编辑* · {tool} · 画笔 {brush} · 已铺 {count} 格{adjacency}"
            : $"未编辑 · 已铺 {count} 格 · 点「编辑」激活画笔{adjacency}";
        _btnApplyRules.IsEnabled = config?.TileRules.Count > 0;
        _btnUndo.IsEnabled = _canvas.CanUndoTileStroke;
    }

    #endregion
}



