/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficRulePanel.Layout.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则面板布局：操作条、状态带、三页列表与属性卡片
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class TrafficRulePanel : UserControl
{
    #region 常量

    private const double ListWidth = 260.0;

    #endregion

    #region 布局

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("*"),
            Background = UiTheme.PanelSurface,
        };

        root.Children.Add(BuildHeader());

        _lightEmpty = EmptyHint("还没有红绿灯",
            "在场景编辑里选中灯体物件，再点上方「+ 红绿灯」标记出来");
        _crosswalkEmpty = EmptyHint("还没有横道",
            "在场景编辑里选中斑马线物件，再点上方「+ 横道」标记出来");
        _actorEmpty = EmptyHint("没有带路径的角色",
            "先在场景编辑里给车或行人画出至少 2 个路径点");

        _pages.Clear();
        _pages.Add(BuildLightPage());
        _pages.Add(BuildCrosswalkPage());
        _pages.Add(BuildActorPage());
        _pages.Add(BuildMarkerPage());

        _pageHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var content = ContentPage(BuildTabStrip(), _pageHost);
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        SelectPage(0);
        return root;
    }

    /// <summary>页签条 + 内容区上下叠放的外壳。</summary>
    /// <param name="strip">页签条。</param>
    /// <param name="host">内容宿主。</param>
    /// <returns>可直接塞进根网格的控件。</returns>
    private static Control ContentPage(Control strip, Control host)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        grid.Children.Add(strip);
        Grid.SetRow(host, 1);
        grid.Children.Add(host);
        return grid;
    }

    /// <summary>
    /// 三段式页签条：浅色轨道 + 选中项白底胶囊 + 数量徽标。
    /// 比默认 TabControl 少一条贯穿下划线，和面板里的卡片风格一致。
    /// </summary>
    private Control BuildTabStrip()
    {
        var segments = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
        };
        AddPageSegment(segments, "红绿灯", UiTheme.Error, 0);
        AddPageSegment(segments, "横道", UiTheme.ObjectTag.Prop, 1);
        AddPageSegment(segments, "车 / 行人", UiTheme.AnimationTag.Sequence, 2);
        AddPageSegment(segments, "物件标记", UiTheme.Traffic.PhaseGreen, 3, showCount: false);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
        };
        row.Children.Add(new Border
        {
            Background = UiTheme.Form.FilterIdle,
            BorderBrush = UiTheme.Divider,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(3),
            Child = segments,
        });

        // 右侧一句操作提示，替代原来整条页签栏的空白
        var hint = new TextBlock
        {
            Text = "点一行即定位",
            FontSize = UiMetrics.FontCaption,
            Foreground = UiTheme.PreviewMuted,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 2, 0),
        };
        Grid.SetColumn(hint, 1);
        row.Children.Add(hint);

        return new Border
        {
            Margin = new Thickness(8, 3, 8, 6),
            Child = row,
        };
    }

    private void AddPageSegment(StackPanel host, string text, IBrush dot, int index, bool showCount = true)
    {
        var count = new TextBlock
        {
            Text = "0",
            FontSize = 9.5,
            Foreground = UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = showCount,
        };
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var segment = new Button
        {
            Background = UiTheme.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius),
            Padding = new Thickness(10, 4, 11, 4),
            MinHeight = 0,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new Border
                    {
                        Width = 7,
                        Height = 7,
                        CornerRadius = new CornerRadius(4),
                        Background = dot,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    label,
                    count,
                },
            },
        };
        segment.Click += (_, _) => SelectPage(index);
        AutomationProperties.SetName(segment, $"页签 · {text}");

        _pageSegments.Add(segment);
        _pageLabels.Add(label);
        _pageCounts.Add(count);
        host.Children.Add(segment);
    }

    /// <summary>切换页签：只换内容宿主，选中段用白底胶囊 + 强调文字。</summary>
    /// <param name="index">页签序号，0 灯 / 1 横道 / 2 车行人。</param>
    private void SelectPage(int index)
    {
        if (index < 0 || index >= _pages.Count)
        {
            return;
        }

        _pageIndex = index;
        _pageHost.Content = _pages[index];
        for (var i = 0; i < _pageSegments.Count; i++)
        {
            var active = i == index;
            _pageSegments[i].Background = active ? UiTheme.Card : UiTheme.Transparent;
            _pageLabels[i].Foreground = active ? UiTheme.AccentDark : UiTheme.TextSecondary;
            _pageLabels[i].FontWeight = active ? FontWeight.Bold : FontWeight.SemiBold;
            _pageCounts[i].Foreground = active ? UiTheme.Accent : UiTheme.TextSecondary;
            _pageCounts[i].FontWeight = active ? FontWeight.Bold : FontWeight.Normal;
        }
    }

    /// <summary>
    /// 面板头部：一行操作条 + 一行统计胶囊与自检徽标 + 一行缺口提示。
    /// 三行都放在同一张白卡里，把「能做什么 / 现在有多少 / 还差什么」分层呈现。
    /// </summary>
    private Control BuildHeader()
    {
        _statLightsText = StatValue();
        _statCrosswalksText = StatValue();
        _statActorsText = StatValue();
        _statStopsText = StatValue();
        _statPhaseText = StatValue();

        _readyText = new TextBlock
        {
            Text = "未载入地图",
            FontSize = UiMetrics.FontCaption,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _readyBadge = new Border
        {
            Background = UiTheme.Form.FilterIdle,
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _readyText,
        };

        _statusGap = new TextBlock
        {
            Text = "未载入地图",
            FontSize = UiMetrics.FontCaption,
            IsVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        var stats = new WrapPanel { Orientation = Orientation.Horizontal };
        stats.Children.Add(StatPill("红绿灯", _statLightsText, UiTheme.Error));
        stats.Children.Add(StatPill("横道", _statCrosswalksText, UiTheme.ObjectTag.Prop, "已标记的斑马线数量"));
        stats.Children.Add(StatPill("角色", _statActorsText, UiTheme.AnimationTag.Sequence, "带路径、参与仿真的车与行人"));
        stats.Children.Add(StatPill("停点", _statStopsText, UiTheme.Accent, "已拿到停止点的角色 / 全部角色"));
        stats.Children.Add(StatPill("相位", _statPhaseText, UiTheme.Traffic.PhaseGreen, "被角色引用的信号灯数量"));

        var statRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        statRow.Children.Add(stats);
        Grid.SetColumn(_readyBadge, 1);
        statRow.Children.Add(_readyBadge);

        var header = UiFactory.HeaderBand(
            new StackPanel
            {
                Spacing = 8,
                Children = { BuildActionBar(), statRow, _statusGap },
            },
            new Thickness(10, 9, 10, 8),
            UiTheme.PanelBorder);
        return header;
    }

    /// <summary>统计胶囊里的数值文字。</summary>
    private static TextBlock StatValue() => new()
    {
        Text = "—",
        FontSize = UiMetrics.FontBody,
        FontWeight = FontWeight.Bold,
        Foreground = UiTheme.Traffic.StatValueText,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>一枚统计胶囊：类型圆点 + 名称 + 数值。</summary>
    private static Control StatPill(string label, TextBlock value, IBrush dot, string? tip = null)
    {
        var pill = new Border
        {
            Background = UiTheme.Traffic.StatPillSurface,
            BorderBrush = UiTheme.Traffic.StatPillBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(8, 2, 9, 2),
            Margin = new Thickness(0, 0, 5, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    UiFactory.MarkerDot(dot, 5),
                    new TextBlock
                    {
                        Text = label,
                        FontSize = UiMetrics.FontCaption,
                        Foreground = UiTheme.TextSecondary,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    value,
                },
            },
        };
        if (tip is not null)
        {
            ToolTip.SetTip(pill, tip);
        }

        return pill;
    }

    /// <summary>
    /// 操作条：左侧两个「标记」主操作，中间一枚竖分隔线，再右侧查看与删除，
    /// 最右一组是「交通演示」与实心「看效果」。所有按钮都压成一行的宽度，窄面板也不再折行。
    /// </summary>
    private Control BuildActionBar()
    {
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,*,Auto"),
            ColumnSpacing = 6,
        };

        var addLight = BarButton("+ 红绿灯", AddLightFromSelection, ButtonTone.Add,
            "把场景里选中的灯体物件标记成红绿灯");
        var addCrosswalk = BarButton("+ 横道", AddCrosswalkFromSelection, ButtonTone.Add,
            "把场景里选中的斑马线物件标记成横道");
        var locate = BarButton("定位", SelectCurrentOnCanvas, ButtonTone.Ghost,
            "选中该规则对应的地图物件并聚焦到画布");
        var remove = BarButton("删除", RemoveCurrent, ButtonTone.Danger,
            "删除当前页签里选中的规则记录，并清理绑定");

        // 右端两颗入口按钮并排：示例路口演示（独立数据）+ 当前地图的交通仿真预览。
        // 「交通演示」原本在顶部工具条上，按使用场景收进交通规则面板，看得见规则也点得到演示。
        var entryGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        entryGroup.Children.Add(BarButton("交通演示", () => DemoRequested?.Invoke(this, EventArgs.Empty),
            ButtonTone.Add, "打开示例路口：车停停止线、红灯等待、转绿起步、行人路缘等待与过街；示例数据独立，不写入当前地图"));
        entryGroup.Children.Add(BarButton("▶ 看效果", () => PreviewRequested?.Invoke(this, EventArgs.Empty),
            ButtonTone.Primary, "打开地图预览，默认勾选『交通仿真』"));

        bar.Children.Add(addLight);
        Grid.SetColumn(addCrosswalk, 1);
        bar.Children.Add(addCrosswalk);

        var divider = new Border
        {
            Width = 1,
            Height = 16,
            Margin = new Thickness(2, 0, 2, 0),
            Background = UiTheme.PanelBorder,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(divider, 2);
        bar.Children.Add(divider);

        Grid.SetColumn(locate, 3);
        bar.Children.Add(locate);
        Grid.SetColumn(remove, 4);
        bar.Children.Add(remove);
        Grid.SetColumn(entryGroup, 6);
        bar.Children.Add(entryGroup);
        return bar;
    }

    private Control BuildLightPage()
    {
        _lightList = BuildListBox();
        _lightList.SelectionChanged += (_, _) =>
        {
            if (_sync || _config is null)
            {
                return;
            }

            var index = _lightList.SelectedIndex;
            _light = index >= 0 && index < _config.TrafficLights.Count ? _config.TrafficLights[index] : null;
            FillLightForm();
            SyncLightOnCanvas(_light);
        };

        return BuildPage(_lightList, _lightEmpty, BuildLightForm(), (listHost, formScroll) =>
        {
            _lightListHost = listHost;
            _lightFormScroll = formScroll;
        });
    }

    private Control BuildCrosswalkPage()
    {
        _crosswalkList = BuildListBox();
        _crosswalkList.SelectionChanged += (_, _) =>
        {
            if (_sync || _config is null)
            {
                return;
            }

            var index = _crosswalkList.SelectedIndex;
            _crosswalk = index >= 0 && index < _config.Crosswalks.Count ? _config.Crosswalks[index] : null;
            FillCrosswalkForm();
            SyncCrosswalkOnCanvas(_crosswalk);
        };

        return BuildPage(_crosswalkList, _crosswalkEmpty, BuildCrosswalkForm(), (listHost, formScroll) =>
        {
            _crosswalkListHost = listHost;
            _crosswalkFormScroll = formScroll;
        });
    }

    private Control BuildActorPage()
    {
        _actorList = BuildListBox();
        _actorList.SelectionChanged += (_, _) =>
        {
            if (_sync)
            {
                return;
            }

            var index = _actorList.SelectedIndex;
            _actor = index >= 0 && index < _actorSources.Count ? _actorSources[index] : null;
            FillActorForm();
            SyncActorOnCanvas(_actor);
        };

        return BuildPage(_actorList, _actorEmpty, BuildActorForm(), (listHost, formScroll) =>
        {
            _actorListHost = listHost;
            _actorFormScroll = formScroll;
        });
    }

    /// <summary>
    /// 「物件标记」页：把原本挤在场景编辑右栏的交通标记按钮与实例属性收进来，
    /// 打标记、调角色、绑灯都在规则面板里一次完成，右栏只留物件本身的编辑项。
    /// 控件本体由宿主装配时搬入 <see cref="MarkerHost"/>，这里只负责标题与卡片外壳。
    /// </summary>
    private Control BuildMarkerPage()
    {
        _markerHost = new StackPanel { Spacing = 10, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Stretch };
        _markerTarget = new TextBlock
        {
            Text = "当前未选中物件 · 标记按钮已禁用",
            FontSize = UiMetrics.FontBody,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var intro = new Border
        {
            Background = UiTheme.Card,
            BorderBrush = UiTheme.Form.GroupBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(10, 9, 12, 9),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("3,9,*"),
                Children =
                {
                    new Border
                    {
                        Background = UiTheme.Traffic.PhaseGreen,
                        CornerRadius = new CornerRadius(2),
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Margin = new Thickness(0, 1, 0, 1),
                    },
                },
            },
        };

        var introText = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _markerTarget },
                },
                new TextBlock
                {
                    Text = "先在地图上选中物件，再点下面的按钮打标记；按钮变「取消…」表示已标记，再点一次即可取消。",
                    FontSize = UiMetrics.FontCaption,
                    Foreground = UiTheme.TextSecondary,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        Grid.SetColumn(introText, 2);
        ((Grid)intro.Child).Children.Add(introText);

        var body = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(10, 8, 10, 10),
            Children = { intro, _markerHost },
        };

        return UiFactory.VerticalScroll(body);
    }

    /// <summary>列表 + 表单的页面骨架；窄面板自动切成上下布局。</summary>
    private Control BuildPage(ListBox list, Control emptyHint, Control form, Action<Control, Control> register)
    {
        var scroll = UiFactory.VerticalScroll(list, showScrollBar: false);
        var frame = UiFactory.ScrollListFrame(scroll);
        frame.CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius);
        frame.Background = UiTheme.Card;
        frame.ClipToBounds = true;

        var listHost = new Grid();
        listHost.Children.Add(frame);

        var formScroll = UiFactory.VerticalScroll(form, padding: new Thickness(8, 4, 8, 8));

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{ListWidth},8,*"),
            Margin = new Thickness(8, 2, 8, 8),
        };
        body.Children.Add(listHost);
        Grid.SetColumn(formScroll, 2);
        body.Children.Add(formScroll);

        // 一条规则都没有时，整页只留一张空态卡（列表与表单一起隐藏），
        // 避免左边「还没有…」、右边再叠一张「未选中…」两张卡重复。
        emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
        emptyHint.VerticalAlignment = VerticalAlignment.Center;
        emptyHint.Margin = new Thickness(12);
        Grid.SetColumnSpan(emptyHint, 3);
        body.Children.Add(emptyHint);
        register(listHost, formScroll);
        SetPageContent(false, listHost, formScroll, emptyHint);

        // 停靠 / 浮动后面板只有 520~980 宽，放不下「列表 + 表单」的横排，
        // 这里按实际宽度自动切成「列表在上、表单在下」的上下布局。
        var compact = false;
        body.SizeChanged += (_, e) =>
        {
            var wantCompact = e.NewSize.Width < 560;
            if (wantCompact == compact)
            {
                return;
            }

            compact = wantCompact;
            if (compact)
            {
                body.ColumnDefinitions = new ColumnDefinitions("*");
                body.RowDefinitions = new RowDefinitions("168,8,*");
                Grid.SetColumn(listHost, 0);
                Grid.SetRow(listHost, 0);
                Grid.SetColumn(formScroll, 0);
                Grid.SetRow(formScroll, 2);
                Grid.SetColumnSpan(emptyHint, 1);
                Grid.SetRowSpan(emptyHint, 3);
            }
            else
            {
                body.ColumnDefinitions = new ColumnDefinitions($"{ListWidth},8,*");
                body.RowDefinitions = new RowDefinitions("*");
                Grid.SetColumn(listHost, 0);
                Grid.SetRow(listHost, 0);
                Grid.SetColumn(formScroll, 2);
                Grid.SetRow(formScroll, 0);
                Grid.SetColumnSpan(emptyHint, 3);
                Grid.SetRowSpan(emptyHint, 1);
            }
        };

        return body;
    }

    /// <summary>有规则时显示列表 + 表单，没有时只显示整页空态卡。</summary>
    private static void SetPageContent(bool hasItems, Control listHost, Control formScroll, Control emptyHint)
    {
        listHost.IsVisible = hasItems;
        formScroll.IsVisible = hasItems;
        emptyHint.IsVisible = !hasItems;
    }

    /// <summary>表单宿主：有选中项时显示字段，没有时显示居中提示卡，避免整片灰控件。</summary>
    private static Control FormShell(Control body, Control hint)
    {
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.VerticalAlignment = VerticalAlignment.Center;
        hint.Margin = new Thickness(12);
        return new Grid { Children = { body, hint } };
    }

    private static void ToggleFormBody(bool hasItem, Control body, Control hint)
    {
        body.IsVisible = hasItem;
        hint.IsVisible = !hasItem;
    }

    private static ListBox BuildListBox()
    {
        var list = new ListBox
        {
            FontSize = 11.5,
            Background = UiTheme.Card,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2),
        };
        // 行卡自己带类型色带与发丝分隔线，列表项只保留选中 / 悬停底色，
        // 默认那层描边与内边距会顶掉行卡的留白，这里一并抹平。
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, UiTheme.Transparent),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(TemplatedControl.MarginProperty, new Thickness(0)),
                new Setter(TemplatedControl.MinHeightProperty, 0.0),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            },
        });
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":pointerover"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, UiTheme.Form.FilterIdle) },
        });
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, UiTheme.PreviewAccentSoft) },
        });
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, UiTheme.PreviewAccentSoft) },
        });
        // 行模板走 TrafficRuleRow.View：名称 + 绑定徽标 / 元信息 / 相位条三段式，
        // 比早期整串拼接文本更好扫读，也不再依赖 TextWrapping 硬折行。
        list.ItemTemplate = new FuncDataTemplate<TrafficRuleRow>((row, _) =>
            row is null ? new Border() : TrafficRuleRow.View(row));
        return list;
    }

    /// <summary>一条规则都没有时，整页中央的空态卡片。</summary>
    private static Control EmptyHint(string title, string hint)
    {
        return new Border
        {
            MaxWidth = 360,
            Background = UiTheme.PreviewSlot,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiMetrics.CardCornerRadius),
            Padding = new Thickness(16, 14),
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 30,
                        Height = 30,
                        CornerRadius = new CornerRadius(15),
                        Background = UiTheme.PreviewAccentSoft,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "＋",
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Foreground = UiTheme.Accent,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                    new TextBlock
                    {
                        Text = title,
                        FontSize = UiMetrics.FontTitle,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = UiTheme.TextPrimary,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = hint,
                        FontSize = UiMetrics.FontCaption,
                        Foreground = UiTheme.TextSecondary,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        MaxWidth = 260,
                    },
                },
            },
        };
    }

    #endregion

    #region 按钮

    private enum ButtonTone
    {
        Ghost,
        Add,
        Danger,
        Primary,
    }

    /// <summary>操作条按钮：添加用浅青、删除用浅红、看效果用实心强调色。</summary>
    private static Button BarButton(string text, Action onClick, ButtonTone tone, string tip)
    {
        var button = ActionButton(text, onClick, tone);
        button.Padding = new Thickness(9, 4);
        button.MinHeight = 27;
        button.FontSize = UiMetrics.FontBody;
        button.Margin = default;
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static Button ActionButton(string text, Action onClick) =>
        ActionButton(text, onClick, ButtonTone.Ghost);

    /// <summary>工具栏 / 表单里的紧凑按钮；主操作使用强调色底。</summary>
    private static Button ActionButton(string text, Action onClick, ButtonTone tone)
    {
        var background = tone switch
        {
            ButtonTone.Add => UiTheme.PreviewAccentSoft,
            ButtonTone.Danger => UiTheme.ResourceTable.DeleteSurface,
            ButtonTone.Primary => UiTheme.Accent,
            _ => UiTheme.Card,
        };
        var foreground = tone switch
        {
            ButtonTone.Danger => UiTheme.ResourceTable.DeleteText,
            ButtonTone.Primary => Brushes.White,
            _ => UiTheme.TextPrimary,
        };
        var border = tone switch
        {
            ButtonTone.Add => UiTheme.InfoText,
            ButtonTone.Danger => UiTheme.ResourceTable.DeleteBorder,
            ButtonTone.Primary => UiTheme.Accent,
            _ => UiTheme.PanelBorder,
        };

        var button = UiFactory.CompactButton(text, background, foreground, new Thickness(10, 5));
        button.FontSize = UiMetrics.FontBody;
        button.MinHeight = 28;
        button.CornerRadius = new CornerRadius(UiMetrics.ButtonCornerRadius);
        button.BorderThickness = new Thickness(1);
        button.BorderBrush = border;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Click += (_, _) => onClick();
        return button;
    }

    #endregion

    #region 表单骨架

    /// <summary>创建右栏表单容器。</summary>
    private static StackPanel BuildFormStack() => new() { Spacing = 10 };

    /// <summary>卡片内一组字段的纵向堆叠。</summary>
    private static StackPanel Fields(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return stack;
    }

    /// <summary>一张小卡片，把一组字段包在一起：竖条标题 + 细分隔线 + 字段堆叠。</summary>
    /// <param name="title">小节标题。</param>
    /// <param name="body">字段堆叠。</param>
    /// <returns>小节卡片。</param>
    private static Control Section(string title, Control body)
    {
        var head = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                UiFactory.SectionHead(title, 11),
                new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 1, 0, 0),
                    Background = UiTheme.Form.GroupBorder,
                },
            },
        };

        var stack = new StackPanel { Spacing = 8, Children = { head, body } };
        var card = UiFactory.PanelCard(stack, padding: new Thickness(11, 9, 11, 10));
        card.BorderBrush = UiTheme.Form.GroupBorder;
        return card;
    }

    /// <summary>标签在输入框上方的单列字段。</summary>
    private static Control Row(string label, Control field) => Labeled(label, field);

    private static Control Labeled(string label, Control field)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                UiFactory.FieldLabel(label, 10.5, margin: new Thickness(0)),
                field,
            },
        };
    }

    /// <summary>一行放两个「标签在上」的成对字段，用于 X / Y、红 / 绿。</summary>
    private static Grid PairRow(string labelA, Control fieldA, string labelB, Control fieldB)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,10,*"),
        };

        var left = Labeled(labelA, fieldA);
        var right = Labeled(labelB, fieldB);
        grid.Children.Add(left);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>表单底部并排的按钮行。</summary>
    private static Control ActionRow(params Control[] buttons)
    {
        var columns = string.Join(",", new string('*', buttons.Length).ToCharArray());
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            ColumnSpacing = 8,
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(buttons[i], i);
            grid.Children.Add(buttons[i]);
        }

        return grid;
    }

    private static TextBlock Hint(string text) => UiFactory.HintText(text, margin: new Thickness(0, -1, 0, 0));

    private static TextBox Field(double width = double.NaN) => new()
    {
        Width = width,
        FontSize = UiMetrics.FontTitle,
        MinHeight = UiMetrics.FieldHeight,
        Padding = new Thickness(8, 4),
        CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
        Background = UiTheme.Card,
        Foreground = UiTheme.TextPrimary,
        BorderBrush = UiTheme.PanelBorder,
        BorderThickness = new Thickness(1),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static TextBox ReadOnlyField() => new()
    {
        IsReadOnly = true,
        FontSize = UiMetrics.FontBody,
        MinHeight = 26,
        Padding = new Thickness(8, 3),
        CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
        Background = UiTheme.DialogSecondary,
        BorderBrush = UiTheme.PanelBorder,
        BorderThickness = new Thickness(1),
        Foreground = UiTheme.TextSecondary,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static ComboBox Combo(params string[] items)
    {
        var box = new ComboBox
        {
            FontSize = UiMetrics.FontTitle,
            MinHeight = UiMetrics.FieldHeight,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(UiMetrics.InputCornerRadius),
            Background = UiTheme.Card,
            BorderBrush = UiTheme.PanelBorder,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var item in items)
        {
            box.Items.Add(new ComboBoxItem { Content = item });
        }

        return box;
    }

    /// <summary>
    /// 「取选中物件」按钮：把场景里当前选中的物件交给回调，再统一回填表单并提交。
    /// 灯取脚底中心、横道取包围盒，差别只在回调里的几何换算，取选中与收尾完全一样。
    /// </summary>
    /// <typeparam name="T">当前页签对应的规则对象类型。</typeparam>
    /// <param name="text">按钮文案。</param>
    /// <param name="emptyTip">没选中物件（或本页没有规则对象）时的提示。</param>
    /// <param name="target">取当前页选中的规则对象；为空表示表单处于空态。</param>
    /// <param name="apply">几何换算与表单回填。</param>
    /// <param name="refreshLists">提交后是否连左侧列表一起刷新。</param>
    /// <returns>表单底部按钮行里的按钮。</returns>
    private Button PickFromSelectionButton<T>(
        string text,
        string emptyTip,
        Func<T?> target,
        Action<T, PlacedObject> apply,
        bool refreshLists = false)
        where T : class
    {
        return ActionButton(text, () =>
        {
            var item = _canvas?.Selected;
            var current = target();
            if (item is null || current is null)
            {
                _notify(emptyTip);
                return;
            }

            apply(current, item);
            ApplyChange(refreshLists);
        });
    }

    /// <summary>
    /// 「清零 / 清空」类按钮：没选中角色时给提示，选中则改数据、回填角色表单并提交。
    /// 角色页的清除停止线、清空绑定灯都是这条链路，差别只在清什么。
    /// </summary>
    /// <param name="text">按钮文案。</param>
    /// <param name="emptyTip">没选中角色时的提示。</param>
    /// <param name="reset">对选中角色的清除动作。</param>
    /// <param name="tip">按钮悬停提示；为 null 时不挂提示。</param>
    /// <returns>表单底部按钮行里的按钮。</returns>
    private Button ActorResetButton(string text, string emptyTip, Action<PlacedObject> reset, string? tip = null)
    {
        var button = ActionButton(text, () =>
        {
            var actor = _actor;
            if (actor is null)
            {
                _notify(emptyTip);
                return;
            }

            reset(actor);
            FillActorForm();
            ApplyChange();
        });

        if (tip is not null)
        {
            ToolTip.SetTip(button, tip);
        }

        return button;
    }

    /// <summary>
    /// 失焦或回车提交，避免每按一次键就改数据。
    /// 一次挂多个输入框，省掉「每个框写一行 HookCommit」的重复调用。
    /// </summary>
    /// <param name="commit">提交回调。</param>
    /// <param name="boxes">共用同一提交动作的输入框。</param>
    private static void HookCommit(Action commit, params TextBox[] boxes)
    {
        foreach (var box in boxes)
        {
            box.LostFocus += (_, _) => commit();
            UiFactory.CommitOnEnter(box, commit);
        }
    }

    private static void SelectComboByText(ComboBox box, string text)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? string.Empty) == text)
            {
                box.SelectedIndex = i;
                return;
            }
        }

        box.SelectedIndex = -1;
    }

    private static string ReadCombo(ComboBox box, string fallback) =>
        box.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? fallback
            : fallback;

    private static double ParseDouble(string? text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    #endregion
}

