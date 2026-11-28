/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NamingAuditWindow.cs
 * author:    云毅
 * created:   2026
 * descrip:   命名体检窗：列出每行的中文名建议与依据，一键改写模板名称或显示名称
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

#region 命名体检窗

/// <summary>
/// 命名体检：把「图片名 → 中文名称」的建议摊开给策划看，确认后写回配置表。
/// 默认改写「模板名称」（表格里的建筑 / 道具 / 角色名称），也可只写备注用的「显示名称」列。
/// 只写高可信条目，缺词表的一律标「待补词表」，不会自作主张改数据。
/// </summary>
public sealed class NamingAuditWindow : Window
{
    #region 常量

    private const double ColumnGap = 8;

    #endregion

    #region 字段

    private readonly ItemConfigSet _set;
    private readonly NamingGlossary _glossary;
    private readonly List<NamingAuditEntry> _entries;
    private readonly Action? _onSaved;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _hint = new();
    private readonly ListBox _auditList = new();
    private readonly Dictionary<NamingTarget, Button> _targetButtons = new();
    private NamingTarget _target = NamingTarget.TemplateName;
    private CheckBox _onlyPending = null!;
    private CheckBox _onlyRepeated = null!;

    #endregion

    #region 构造

    /// <summary>构造命名体检窗。</summary>
    /// <param name="set">配置表集合。</param>
    /// <param name="glossary">命名词表。</param>
    /// <param name="onSaved">写回成功后的回调（用于刷新资源整理页）。</param>
    public NamingAuditWindow(ItemConfigSet set, NamingGlossary glossary, Action? onSaved = null)
    {
        _set = set;
        _glossary = glossary;
        _onSaved = onSaved;
        _entries = NamingAuditService.Audit(set, glossary);

        Title = "命名体检";
        Width = 1120;
        Height = 760;
        MinWidth = 920;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiTheme.DialogSurface;
        CanResize = true;

        Content = BuildShell();
        WindowChromeTheme.Attach(this);
    }

    #endregion

    #region 布局

    private Control BuildShell()
    {
        var card = UiFactory.PaneCard(cornerRadius: 12, padding: 16);
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 10,
        };
        card.Child = root;

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 3 };
        titles.Children.Add(new TextBlock
        {
            Text = "命名体检",
            Foreground = UiTheme.TextPrimary,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
        });
        titles.Children.Add(new TextBlock
        {
            Text = "按图片名生成中文名称；只写高可信条目，缺词表的保持原样，同表重名自动顺延序号。",
            Foreground = UiTheme.TextSecondary,
            FontSize = UiMetrics.FontBody,
        });
        head.Children.Add(titles);

        _summary.Foreground = UiTheme.AccentDark;
        _summary.FontSize = UiMetrics.FontTitle;
        _summary.FontWeight = FontWeight.SemiBold;
        _summary.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_summary, 1);
        head.Children.Add(_summary);
        root.Children.Add(head);

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*") };

        var targets = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 14, 0),
        };
        AddTargetButton(targets, NamingTarget.TemplateName, "改模板名称", "把配置表的「建筑 / 道具 / 角色名称」直接改成中文（图片名留在 ImagePath 列）");
        AddTargetButton(targets, NamingTarget.DisplayName, "写显示名称列", "不动原名称，另写一列「显示名称」，适合只想加中文备注");
        toolbar.Children.Add(targets);

        _onlyPending = new CheckBox
        {
            Content = "只看待处理",
            FontSize = UiMetrics.FontBody,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        };
        _onlyPending.IsCheckedChanged += (_, _) => FillList();
        Grid.SetColumn(_onlyPending, 1);
        toolbar.Children.Add(_onlyPending);

        // 「重复」是这个项目最实的病灶：同一张图在汇总 CSV 与分类表各登记一份，先能筛出来才谈得上优化
        _onlyRepeated = new CheckBox
        {
            Content = "只看重复",
            FontSize = UiMetrics.FontBody,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        };
        ToolTip.SetTip(_onlyRepeated, "只列出「同一张图出现多行」的条目：同图同 ID 的跨表镜像已在去重中收敛，这里的多行通常是同图不同 ID 的多用途模板");
        _onlyRepeated.IsCheckedChanged += (_, _) => FillList();
        Grid.SetColumn(_onlyRepeated, 2);
        toolbar.Children.Add(_onlyRepeated);

        _hint.Foreground = UiTheme.TextSecondary;
        _hint.FontSize = UiMetrics.FontCaption;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.TextTrimming = TextTrimming.CharacterEllipsis;
        _hint.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_hint, 3);
        toolbar.Children.Add(_hint);
        Grid.SetRow(toolbar, 1);
        root.Children.Add(toolbar);

        _auditList.Background = UiTheme.Transparent;
        _auditList.BorderThickness = new Thickness(0);
        _auditList.Padding = new Thickness(0);
        _auditList.ItemTemplate = new FuncDataTemplate<NamingAuditEntry>((entry, _) => BuildRow(entry), supportsRecycling: true);

        var listScroll = UiFactory.VerticalScroll(_auditList, showScrollBar: false);
        var listBar = new ClassicScrollBar { Target = listScroll };
        var listArea = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"*,{ClassicScrollBar.BarWidth}"),
        };
        listArea.Children.Add(listScroll);
        Grid.SetColumn(listBar, 1);
        listArea.Children.Add(listBar);

        var listCard = UiFactory.PanelCard(listArea, clipToBounds: true);
        Grid.SetRow(listCard, 2);
        root.Children.Add(listCard);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var cancel = UiFactory.CompactButton("关闭");
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(cancel);

        var apply = UiFactory.CompactButton("写入并刷新", UiTheme.Accent, UiTheme.White);
        apply.BorderBrush = UiTheme.Accent;
        apply.FontWeight = FontWeight.SemiBold;
        ToolTip.SetTip(apply, "把当前目标列写进配置表并保存；没有需要写入的行时会在这里直接说明，不会静默无反应");
        apply.Click += (_, _) => ApplyAndSave();
        buttons.Children.Add(apply);

        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        RefreshTargetButtons();
        FillList();
        return card;
    }

    private void AddTargetButton(StackPanel host, NamingTarget target, string text, string tip)
    {
        var button = new Button
        {
            Content = text,
            Height = 26,
            Padding = new Thickness(10, 2),
            FontSize = UiMetrics.FontBody,
            CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius),
        };
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) =>
        {
            if (_target == target)
            {
                return;
            }

            _target = target;
            RefreshTargetButtons();
            _hint.Text = string.Empty;
            FillList();
        };
        _targetButtons[target] = button;
        host.Children.Add(button);
    }

    private void RefreshTargetButtons()
    {
        foreach (var pair in _targetButtons)
        {
            var selected = pair.Key == _target;
            pair.Value.Background = selected ? UiTheme.Accent : UiTheme.Card;
            pair.Value.BorderBrush = selected ? UiTheme.Accent : UiTheme.PanelBorder;
            pair.Value.Foreground = selected ? UiTheme.White : UiTheme.TextPrimary;
            pair.Value.BorderThickness = new Thickness(1);
            pair.Value.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }

    private void FillList()
    {
        var onlyPending = _onlyPending.IsChecked == true;
        var onlyRepeated = _onlyRepeated.IsChecked == true;
        IEnumerable<NamingAuditEntry> rows = _entries;
        if (onlyPending)
        {
            rows = rows.Where(entry => entry.NeedsWrite(_target));
        }

        if (onlyRepeated)
        {
            rows = rows.Where(entry => entry.IsRepeatedIcon);
        }

        var view = rows.ToList();

        // ItemTemplate 按行内容构建，切换目标后必须重建，否则「现名」还停在旧目标列
        _auditList.ItemsSource = null;
        _auditList.ItemsSource = view;

        var pending = NamingAuditService.PendingCount(_entries, _target);
        _summary.Text = $"{NamingAuditService.Describe(_entries)} · 待写回 {pending}";
        if (string.IsNullOrEmpty(_hint.Text))
        {
            _hint.Text = _glossary.IsEmpty
                ? "词表缺失：output/Config/NamingGlossary.json 读不到，所有行都会标「待补词表」"
                : $"写入目标：{TargetText(_target)}";
        }
    }

    private static string TargetText(NamingTarget target) =>
        target == NamingTarget.TemplateName ? "模板名称（建筑 / 道具 / 角色名称）" : "「显示名称」列";

    #endregion

    #region 行模板

    private Control BuildRow(NamingAuditEntry entry)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("152,52,182,*,Auto,*,64"),
            Margin = new Thickness(8, 5),
        };

        AddCell(grid, 0, entry.TableName, UiTheme.TextSecondary, UiMetrics.FontCaption);

        var id = new TextBlock
        {
            Text = entry.Id,
            Foreground = UiTheme.TextSecondary,
            FontSize = UiMetrics.FontCaption,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(id, 1);
        grid.Children.Add(id);

        var iconPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        iconPanel.Children.Add(new TextBlock
        {
            Text = entry.Icon,
            Foreground = UiTheme.TextPrimary,
            FontSize = UiMetrics.FontCaption,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (entry.IsRepeatedIcon)
        {
            // 同一张图同时登记在汇总 CSV 与分类表里，是「重复」问题的直接证据，就地标出来
            var repeat = new Border
            {
                Background = UiTheme.Transparent,
                BorderBrush = UiTheme.RowSelectedBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 0),
                Child = new TextBlock
                {
                    Text = $"×{entry.IconRepeat}",
                    FontSize = UiMetrics.FontCaption,
                    Foreground = UiTheme.InfoText,
                },
            };
            ToolTip.SetTip(repeat, $"同一张图片在整个表集合里共 {entry.IconRepeat} 行：同图同 ID 的跨表镜像已收敛，这里通常是用同一张图做的多个用途模板，按需保留");
            iconPanel.Children.Add(repeat);
        }

        Grid.SetColumn(iconPanel, 2);
        grid.Children.Add(iconPanel);

        AddCell(grid, 3, entry.Current(_target), UiTheme.TextSecondary, UiMetrics.FontCaption);
        AddCell(grid, 4, "→", UiTheme.TextSecondary, UiMetrics.FontCaption);
        AddCell(grid, 5, entry.Suggestion, entry.NeedsWrite(_target) ? UiTheme.AccentDark : UiTheme.TextPrimary, UiMetrics.FontBody);

        var action = entry.Action(_target);
        var actionText = new TextBlock
        {
            Text = action,
            FontSize = UiMetrics.FontCaption,
            Foreground = action switch
            {
                "新增" => UiTheme.Accent,
                "覆盖" => UiTheme.InfoText,
                "待补词表" => UiTheme.Error,
                _ => UiTheme.TextSecondary,
            },
        };
        ToolTip.SetTip(actionText, entry.Reason);

        var badge = new Border
        {
            Background = UiTheme.Transparent,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            BorderBrush = action switch
            {
                "新增" => UiTheme.Accent,
                "覆盖" => UiTheme.RowSelectedBorder,
                "待补词表" => UiTheme.Error,
                _ => UiTheme.Divider,
            },
            Padding = new Thickness(6, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = actionText,
        };
        Grid.SetColumn(badge, 6);
        grid.Children.Add(badge);

        return new Border
        {
            Background = UiTheme.RowBack,
            BorderBrush = UiTheme.RowBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        };
    }

    private static void AddCell(Grid grid, int column, string text, IBrush brush, double fontSize)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, ColumnGap, 0),
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    #endregion

    #region 写回

    private void ApplyAndSave()
    {
        var written = NamingAuditService.Apply(_set, _entries, _glossary, _target);
        if (written == 0)
        {
            // 没改动也要给明确答复：早期版本在这里直接 return，用户点按钮像没反应
            _summary.Text = $"{NamingAuditService.Describe(_entries)} · 待写回 0";
            _hint.Text = $"没有需要写入的行：{_entries.Count} 行在「{TargetText(_target)}」上已全部一致，本次未改动任何表。";
            return;
        }

        if (!_set.TrySave(out var error))
        {
            _hint.Text = UiFeedback.Failed("配置表写入", error);
            return;
        }

        _onSaved?.Invoke();
        Close();
    }

    #endregion
}

#endregion
