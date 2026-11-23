/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CollectibleWizardWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   添加收集物两步向导：先选数量，再选素材
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

#region 收集物向导

/// <summary>
/// 「添加收集」两步向导。
/// 第一步对应参考图一：地图卡片 + 目标数量 + 层级；第二步对应参考图二：
/// 已选槽位条 + 可收集素材网格，最多 12 种。结果只回传给主窗口，不直接改地图。
/// </summary>
public sealed class CollectibleWizardWindow : Window
{
    #region 常量与画刷

    private const int MaxKinds = 12;


    #endregion

    #region 字段

    private readonly MapConfigData _config;
    private readonly IReadOnlyList<ObjectTemplate> _candidates;
    private readonly AssetLibrary _assets;
    private readonly List<ObjectTemplate> _selected = new();

    private readonly NumericUpDown _totalBox;
    private readonly ComboBox _layerCombo;
    private readonly ContentControl _contentHost = new();
    private readonly TextBlock _statusText = new();
    private readonly WrapPanel _slotPanel = new();
    private readonly WrapPanel _candidatePanel = new();
    private readonly TextBlock _selectedCountText = new();
    private int _step;

    #endregion

    #region 构造

    /// <summary>构造向导窗。</summary>
    /// <param name="config">当前地图配置，用于卡片标题与底图预览。</param>
    /// <param name="candidates">可收集素材候选池。</param>
    /// <param name="plan">已有计划，用于回显数量和已选素材。</param>
    /// <param name="assets">贴图资源库。</param>
    public CollectibleWizardWindow(
        MapConfigData config,
        IReadOnlyList<ObjectTemplate> candidates,
        CollectiblePlanData plan,
        AssetLibrary assets)
    {
        _config = config;
        _candidates = candidates;
        _assets = assets;

        Title = "添加收集物";
        Width = 980;
        Height = 760;
        MinWidth = 860;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.PreviewPanel;
        CanResize = true;

        _totalBox = new NumericUpDown
        {
            Minimum = 1m,
            Maximum = 9999m,
            Increment = 1m,
            Value = plan.Total > 0 ? plan.Total : 50m,
            Width = 150,
            Height = 34,
            FontSize = 15,
            FormatString = "0",
            HorizontalContentAlignment = HorizontalAlignment.Right,
        };
        _layerCombo = new ComboBox
        {
            ItemsSource = new[] { "物件下", "物件上" },
            SelectedIndex = CollectibleLayers.IsOver(plan.LayerMode) ? 1 : 0,
            Width = 150,
            Height = 34,
            FontSize = 14,
        };

        foreach (var kind in plan.Kinds)
        {
            var template = _candidates.FirstOrDefault(item =>
                string.Equals(item.TemplateId, kind.TemplateId, StringComparison.Ordinal));
            if (template is not null && _selected.All(item => item.TemplateId != template.TemplateId))
            {
                _selected.Add(template);
            }
        }

        Content = BuildShell();
        ShowStep(0);
    }

    #endregion

    #region 结果

    /// <summary>目标收集物数量。</summary>
    public int TotalCount => (int)Math.Max(1, _totalBox.Value ?? 50m);

    /// <summary>层级代码：under / over。</summary>
    public string LayerMode => _layerCombo.SelectedIndex == 0
        ? CollectibleLayers.UnderObjects
        : CollectibleLayers.OverObjects;

    /// <summary>已选素材。</summary>
    public IReadOnlyList<ObjectTemplate> SelectedTemplates => _selected;

    #endregion

    #region 外壳

    private Control BuildShell()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(18),
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
        };
        var titleStack = new StackPanel { Spacing = 3 };
        titleStack.Children.Add(new TextBlock
        {
            Text = "添加收集物",
            Foreground = UiTheme.PreviewText,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
        });
        var subtitle = new TextBlock
        {
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
        };
        titleStack.Children.Add(subtitle);
        header.Children.Add(titleStack);

        var stepBadge = new Border
        {
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Avalonia.Thickness(12, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        stepBadge.Child = new TextBlock
        {
            Text = "2 步完成",
            Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x8F, 0x66)),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        };
        Grid.SetColumn(stepBadge, 1);
        header.Children.Add(stepBadge);
        root.Children.Add(header);

        _contentHost.Margin = new Avalonia.Thickness(0, 2, 0, 8);
        Grid.SetRow(_contentHost, 1);
        root.Children.Add(_contentHost);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        _statusText.Foreground = UiTheme.PreviewHint;
        _statusText.FontSize = 12;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(_statusText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(buttons, 2);
        footer.Children.Add(buttons);
        _footerButtons = buttons;
        // footer 必须落在第三行，否则默认回到第 0 行，会和标题栏 / 步骤徽标叠在一起
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private StackPanel _footerButtons = null!;
    private Button? _startButton;

    #endregion

    #region 步骤切换

    private void ShowStep(int step)
    {
        _step = step;
        _contentHost.Content = step == 0 ? BuildQuantityStep() : BuildMaterialStep();
        RebuildFooter();
        _statusText.Text = step == 0
            ? "先定目标数量；层级可以之后在左侧收集物切页里继续调整。"
            : $"从物件表选择要作为收集物的素材，最多 {MaxKinds} 种；已选内容会保留在左侧切页。";
    }

    private void RebuildFooter()
    {
        _footerButtons.Children.Clear();
        if (_step == 0)
        {
            _startButton = null;
            var cancel = MakeButton("取消", false);
            cancel.Click += (_, _) => Close(false);
            _footerButtons.Children.Add(cancel);

            var next = MakeButton("下一步 →", true);
            next.Click += (_, _) =>
            {
                if (_candidates.Count == 0)
                {
                    _statusText.Text = "没有可选素材。";
                    return;
                }

                ShowStep(1);
            };
            _footerButtons.Children.Add(next);
        }
        else
        {
            var back = MakeButton("← 上一步", false);
            back.Click += (_, _) => ShowStep(0);
            _footerButtons.Children.Add(back);

            var cancel = MakeButton("取消", false);
            cancel.Click += (_, _) => Close(false);
            _footerButtons.Children.Add(cancel);

            var start = MakeButton("开始制作", true);
            _startButton = start;
            start.IsEnabled = _selected.Count > 0;
            start.Click += (_, _) =>
            {
                if (_selected.Count == 0)
                {
                    _statusText.Text = "至少选择一种收集物素材。";
                    return;
                }

                Close(true);
            };
            _footerButtons.Children.Add(start);
        }
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 96,
            Height = 34,
            Padding = new Avalonia.Thickness(16, 0),
            FontSize = 13,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (primary)
        {
            button.Background = UiTheme.WizardAccent;
            button.Foreground = Brushes.White;
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(0x23, 0xA3, 0x71));
            button.FontWeight = FontWeight.SemiBold;
        }

        return button;
    }

    #endregion

    #region 第一步：数量与层级

    private Control BuildQuantityStep()
    {
        var card = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Avalonia.Thickness(16),
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 14,
        };

        var eyebrow = new TextBlock
        {
            Text = "步骤 1 / 2 · 目标数量",
            Foreground = UiTheme.WizardAccent,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
        };
        root.Children.Add(eyebrow);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.15*,1*"),
            ColumnSpacing = 18,
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var preview = BuildMapPreview();
        body.Children.Add(preview);

        var form = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(form, 1);
        body.Children.Add(form);

        form.Children.Add(new TextBlock
        {
            Text = "这张地图最多能收集多少个收集物？",
            Foreground = UiTheme.PreviewText,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        form.Children.Add(new TextBlock
        {
            Text = "数量用于随机生成和进度统计，之后仍可手动增删。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var numberRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
        };
        numberRow.Children.Add(new TextBlock
        {
            Text = "目标数量",
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_totalBox, 1);
        numberRow.Children.Add(_totalBox);
        form.Children.Add(numberRow);

        var layerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
        };
        layerRow.Children.Add(new TextBlock
        {
            Text = "显示层级",
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_layerCombo, 1);
        layerRow.Children.Add(_layerCombo);
        form.Children.Add(layerRow);

        form.Children.Add(new TextBlock
        {
            Text = "「物件下」会被建筑、树、道具挡住；「物件上」始终盖在普通物件之上、前景之下。",
            Foreground = UiTheme.PreviewHint,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        var tip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xFB, 0xFD)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xEA, 0xF3)),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Avalonia.Thickness(10, 8),
        };
        tip.Child = new TextBlock
        {
            Text = "完成后左侧会出现「收集物」切页；点素材再点地图，或直接把素材拖进地图，也可一次随机补齐。",
            Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0x66, 0x78)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        form.Children.Add(tip);

        card.Child = root;
        return card;
    }

    private Control BuildMapPreview()
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xFA, 0xFD)),
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Avalonia.Thickness(10),
            MinHeight = 300,
        };

        var stack = new StackPanel { Spacing = 8 };
        card.Child = stack;

        Bitmap? bitmap = _assets.LoadByLayer(
            _config.BgName,
            MapLayerKeys.Background,
            _config.ChapterId,
            _config.SceneId);
        bitmap ??= _assets.Load(_config.BgName);

        var imageFrame = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x25, 0x33)),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Height = 255,
        };
        if (bitmap is not null)
        {
            imageFrame.Child = new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            imageFrame.Child = new TextBlock
            {
                Text = "当前地图暂无底图预览",
                Foreground = Brushes.White,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        stack.Children.Add(imageFrame);
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_config.SceneName) ? "当前关卡" : _config.SceneName,
            Foreground = UiTheme.PreviewText,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{_config.Width} × {_config.Height} · {_config.GridCols} × {_config.GridRows} 格 · {_config.Objects.Count} 个物件",
            Foreground = UiTheme.PreviewHint,
            FontSize = 11,
        });
        return card;
    }

    #endregion

    #region 第二步：素材网格

    private Control BuildMaterialStep()
    {
        var card = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Avalonia.Thickness(14),
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 10,
        };
        card.Child = root;

        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        head.Children.Add(new TextBlock
        {
            Text = "步骤 2 / 2 · 选择收集物素材",
            Foreground = UiTheme.PreviewText,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
        });
        _selectedCountText.Foreground = UiTheme.WizardAccent;
        _selectedCountText.FontSize = 12;
        _selectedCountText.FontWeight = FontWeight.SemiBold;
        _selectedCountText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_selectedCountText, 1);
        head.Children.Add(_selectedCountText);
        root.Children.Add(head);

        _slotPanel.Orientation = Orientation.Horizontal;
        var slotScroll = new ScrollViewer
        {
            Content = _slotPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 82,
        };
        Grid.SetRow(slotScroll, 1);
        root.Children.Add(slotScroll);

        _candidatePanel.Orientation = Orientation.Horizontal;
        var gridScroll = new ScrollViewer
        {
            Content = _candidatePanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(gridScroll, 2);
        root.Children.Add(gridScroll);

        RefreshMaterialStep();
        return card;
    }

    private void RefreshMaterialStep()
    {
        _slotPanel.Children.Clear();
        _candidatePanel.Children.Clear();
        _selectedCountText.Text = $"已选 {_selected.Count}/{MaxKinds}";
        // 选中 / 移除素材只刷新网格，不会重建底部按钮；这里同步「开始制作」可用状态
        if (_startButton is not null)
        {
            _startButton.IsEnabled = _selected.Count > 0;
        }

        for (var i = 0; i < MaxKinds; i++)
        {
            _slotPanel.Children.Add(i < _selected.Count
                ? BuildSelectedSlot(_selected[i])
                : BuildEmptySlot());
        }

        foreach (var template in _candidates)
        {
            _candidatePanel.Children.Add(BuildCandidateTile(template));
        }
    }

    private Control BuildSelectedSlot(ObjectTemplate template)
    {
        var image = BuildPreview(template, 48);
        var remove = new Button
        {
            Content = "×",
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Avalonia.Thickness(0),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, -2, -2, 0),
            CornerRadius = new CornerRadius(10),
        };
        ToolTip.SetTip(remove, "从已选移除");
        remove.Click += (_, _) =>
        {
            _selected.RemoveAll(item => item.TemplateId == template.TemplateId);
            RefreshMaterialStep();
        };

        var content = new Grid();
        content.Children.Add(image);
        content.Children.Add(remove);

        var border = new Border
        {
            Width = 64,
            Height = 64,
            Background = UiTheme.WizardAccentSoft,
            BorderBrush = UiTheme.WizardAccent,
            BorderThickness = new Avalonia.Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Avalonia.Thickness(6),
            Margin = new Avalonia.Thickness(0, 0, 6, 0),
            Child = content,
        };
        ToolTip.SetTip(border, template.TemplateName);
        return border;
    }

    private static Control BuildEmptySlot() => new Border
    {
        Width = 64,
        Height = 64,
        Background = UiTheme.PreviewSlot,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xDD, 0xE7)),
        BorderThickness = new Avalonia.Thickness(1),
        CornerRadius = new CornerRadius(8),
        Margin = new Avalonia.Thickness(0, 0, 6, 0),
        Child = new TextBlock
        {
            Text = "＋",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0xC1, 0xCE)),
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private Control BuildCandidateTile(ObjectTemplate template)
    {
        var selected = _selected.Any(item => item.TemplateId == template.TemplateId);
        var image = BuildPreview(template, 58);
        var name = new TextBlock
        {
            Text = template.TemplateName,
            Foreground = UiTheme.PreviewText,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 74,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var stack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(image);
        stack.Children.Add(name);

        var button = new Button
        {
            Width = 88,
            MinHeight = 92,
            Background = selected ? UiTheme.WizardAccentSoft : UiTheme.Card,
            BorderBrush = selected ? UiTheme.WizardAccent : UiTheme.Divider,
            BorderThickness = new Avalonia.Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(9),
            Padding = new Avalonia.Thickness(6, 5),
            Margin = new Avalonia.Thickness(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = stack,
        };
        ToolTip.SetTip(button, selected ? "点击取消选择" : "点击加入已选");
        button.Click += (_, _) =>
        {
            var existing = _selected.FindIndex(item => item.TemplateId == template.TemplateId);
            if (existing >= 0)
            {
                _selected.RemoveAt(existing);
            }
            else if (_selected.Count >= MaxKinds)
            {
                _statusText.Text = $"最多选择 {MaxKinds} 种素材；先移除一个已选再添加。";
                return;
            }
            else
            {
                _selected.Add(template);
            }

            RefreshMaterialStep();
        };
        return button;
    }

    private Control BuildPreview(ObjectTemplate template, double size)
    {
        Bitmap? bitmap = _assets.Load(template.ImagePath);
        if (bitmap is not null)
        {
            return new Border
            {
                Width = size,
                Height = size,
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF8, 0xFB)),
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }

        return new Border
        {
            Width = size,
            Height = size,
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF8)),
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                Text = "无图",
                Foreground = UiTheme.PreviewHint,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    #endregion
}

#endregion
