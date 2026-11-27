/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficRulePanel.Rows.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则列表行模型：名称 / 几何元信息 / 绑定关系 / 相位条的
 *            结构化数据与行模板，替代早期「一整串字符串」的行文案
 ***************************************************************/

using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 交通规则列表的一行：第一行是名称 + 右侧徽标，第二行是几何 / 角色元信息，
/// 第三行是绑定对象，灯行再补一条红 / 绿 / 黄相位条，让人一眼看出周期分配。
/// </summary>
/// <param name="Title">主名称。</param>
/// <param name="Meta">第二行几何或角色元信息。</param>
/// <param name="Bind">第三行绑定关系；空串表示没有这一行。</param>
/// <param name="Tag">右侧徽标文字；空串表示不显示。</param>
/// <param name="TagText">徽标文字色，同时用于绑定行文字。</param>
/// <param name="TagSurface">徽标底色。</param>
/// <param name="Dot">行首类型圆点色，与页签头颜色一致。</param>
/// <param name="RedSeconds">红灯秒数；0 表示不画相位条。</param>
/// <param name="GreenSeconds">绿灯秒数。</param>
/// <param name="YellowSeconds">黄灯秒数。</param>
internal sealed record TrafficRuleRow(
    string Title,
    string Meta,
    string Bind,
    string Tag,
    IBrush TagText,
    IBrush TagSurface,
    IBrush Dot,
    double RedSeconds = 0,
    double GreenSeconds = 0,
    double YellowSeconds = 0)
{
    #region 工厂

    /// <summary>由一盏灯生成行：名称 + 位置 / 方向 + 绑定横道 + 相位条。</summary>
    /// <param name="light">灯数据。</param>
    /// <param name="boundName">绑定的横道名；空表示未绑。</param>
    /// <returns>列表行模型。</returns>
    public static TrafficRuleRow ForLight(MapTrafficLightData light, string boundName)
    {
        var title = string.IsNullOrWhiteSpace(light.Name) ? light.Id : light.Name;
        var direction = light.Direction == TrafficDirections.Vertical ? "纵向" : "横向";
        var side = string.IsNullOrWhiteSpace(light.Side) ? "—" : MapSide(light.Side);
        var bound = !string.IsNullOrEmpty(light.BoundCrosswalkId);
        return new TrafficRuleRow(
            title,
            $"{direction}·{side}  ({light.PosX:0},{light.PosY:0})",
            bound ? $"↳ {boundName}" : "↳ 未绑横道",
            bound ? "已绑" : "未绑",
            bound ? UiTheme.Traffic.BoundText : UiTheme.Traffic.UnboundText,
            bound ? UiTheme.Traffic.BoundSurface : UiTheme.Traffic.UnboundSurface,
            UiTheme.Error,
            Math.Max(0, light.RedDuration),
            Math.Max(0, light.GreenDuration),
            Math.Max(0, light.YellowDuration));
    }

    /// <summary>由一条横道生成行：名称 + 方向 / 宽度 / 跨度 + 绑定灯。</summary>
    /// <param name="crosswalk">横道数据。</param>
    /// <param name="boundName">绑定的灯名；空表示未绑。</param>
    /// <param name="displayName">列表显示名（同名横道会被补成 <c>#1 / #2</c>）；空则用数据里的名字。</param>
    /// <returns>列表行模型。</returns>
    public static TrafficRuleRow ForCrosswalk(MapCrosswalkData crosswalk, string boundName, string? displayName = null)
    {
        var title = string.IsNullOrWhiteSpace(displayName)
            ? string.IsNullOrWhiteSpace(crosswalk.Name) ? crosswalk.Id : crosswalk.Name
            : displayName;
        var vertical = crosswalk.Direction == TrafficDirections.Vertical;
        var direction = vertical ? "纵向" : "横向";
        var span = vertical
            ? $"y {Math.Min(crosswalk.PointA.Y, crosswalk.PointB.Y):0}→{Math.Max(crosswalk.PointA.Y, crosswalk.PointB.Y):0}"
            : $"x {Math.Min(crosswalk.PointA.X, crosswalk.PointB.X):0}→{Math.Max(crosswalk.PointA.X, crosswalk.PointB.X):0}";
        var bound = !string.IsNullOrEmpty(crosswalk.BoundLightId);
        return new TrafficRuleRow(
            title,
            $"{direction} · 宽 {crosswalk.Width:0} · {span}",
            bound ? $"↳ {boundName}" : "↳ 未绑灯",
            bound ? "已绑" : "未绑",
            bound ? UiTheme.Traffic.BoundText : UiTheme.Traffic.UnboundText,
            bound ? UiTheme.Traffic.BoundSurface : UiTheme.Traffic.UnboundSurface,
            UiTheme.ObjectTag.Prop);
    }

    /// <summary>由车 / 行人生成行：名称 + 角色 / 路径 + 绑定灯数 + 行进方向徽标。</summary>
    /// <param name="item">带路径的物件。</param>
    /// <returns>列表行模型。</returns>
    public static TrafficRuleRow ForActor(PlacedObject item)
    {
        var role = TrafficRoles.ToDisplay(item.TrafficRole);
        var isVehicle = item.IsVehicle || item.TrafficRole == TrafficRoles.Vehicle;
        var lights = item.BoundLightIds.Count;
        var (tag, text, surface) = isVehicle
            ? VehicleDirection(item)
            : ("步行", UiTheme.InfoText, UiTheme.PreviewAccentSoft);
        return new TrafficRuleRow(
            item.Name,
            $"{role} · 路径 {item.PathPoints.Count} 点 · ×{item.Speed:0.##}",
            lights == 0 ? "↳ 未绑灯" : $"↳ {lights} 盏灯",
            tag, text, surface,
            UiTheme.AnimationTag.Sequence);
    }

    #endregion

    #region 行模板

    /// <summary>把行模型装配成「名称 + 徽标 / 元信息 / 绑定 / 相位条」的卡片。</summary>
    /// <param name="row">行模型。</param>
    /// <returns>可直接放进列表 ItemTemplate 的控件。</returns>
    public static Control View(TrafficRuleRow row)
    {
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 7,
        };

        var title = new TextBlock
        {
            Text = row.Title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiTheme.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 0);
        head.Children.Add(title);

        if (!string.IsNullOrEmpty(row.Tag))
        {
            var badge = new Border
            {
                Background = row.TagSurface,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = row.Tag,
                    FontSize = 9.5,
                    Foreground = row.TagText,
                },
            };
            Grid.SetColumn(badge, 1);
            head.Children.Add(badge);
        }

        var stack = new StackPanel { Spacing = 4, Children = { head } };
        stack.Children.Add(new TextBlock
        {
            Text = row.Meta,
            FontSize = 10.5,
            Foreground = UiTheme.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 15,
        });

        if (!string.IsNullOrEmpty(row.Bind))
        {
            stack.Children.Add(new TextBlock
            {
                Text = row.Bind,
                FontSize = 10,
                Foreground = row.TagText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                LineHeight = 14,
            });
        }

        if (row.RedSeconds > 0 || row.GreenSeconds > 0 || row.YellowSeconds > 0)
        {
            stack.Children.Add(PhaseStrip(row));
        }

        // 行首一条 3px 类型色带：灯 / 横道 / 角色各一色，扫一眼就能分清行类型
        var accent = new Border
        {
            Background = row.Dot,
            Opacity = 0.85,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 0, 2),
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3,*"),
            ColumnSpacing = 7,
        };
        body.Children.Add(accent);
        Grid.SetColumn(stack, 1);
        body.Children.Add(stack);

        var card = new Border
        {
            Margin = new Thickness(0),
            Padding = new Thickness(7, 8, 7, 8),
            Background = UiTheme.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = UiTheme.Divider,
            Child = body,
        };

        // 行卡片对自动化 / 读屏只报名称，不要把整条行模型（含画刷色值）当名字扔出去
        AutomationProperties.SetName(card, row.Title);
        return card;
    }

    /// <summary>按秒数比例铺红 / 绿 / 黄三段的细相位条，宽度为零的相位不占位。</summary>
    /// <param name="row">行模型。</param>
    /// <returns>相位条控件。</returns>
    private static Control PhaseStrip(TrafficRuleRow row)
    {
        var grid = new Grid { Height = 4, ColumnSpacing = 1 };
        var column = 0;
        AddSegment(grid, row.RedSeconds, UiTheme.Traffic.PhaseRed, ref column);
        AddSegment(grid, row.GreenSeconds, UiTheme.Traffic.PhaseGreen, ref column);
        AddSegment(grid, row.YellowSeconds, UiTheme.Traffic.PhaseYellow, ref column);
        ToolTip.SetTip(grid,
            $"红 {row.RedSeconds:0.#}s · 绿 {row.GreenSeconds:0.#}s · 黄 {row.YellowSeconds:0.#}s");

        return new Border
        {
            Height = 4,
            Margin = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(2),
            ClipToBounds = true,
            Background = UiTheme.Traffic.PhaseStripTrack,
            Child = grid,
        };
    }

    /// <summary>向相位条追加一段；秒数为 0 时跳过。</summary>
    /// <param name="grid">相位条网格。</param>
    /// <param name="seconds">该相位秒数。</param>
    /// <param name="brush">该相位颜色。</param>
    /// <param name="column">当前列号，追加后自增。</param>
    private static void AddSegment(Grid grid, double seconds, IBrush brush, ref int column)
    {
        if (seconds <= 0)
        {
            return;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(seconds, GridUnitType.Star)));
        var segment = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(2),
        };
        Grid.SetColumn(segment, column);
        grid.Children.Add(segment);
        column++;
    }

    #endregion

    #region 文案

    /// <summary>把 north / south / east / west 换成中文方位。</summary>
    /// <param name="side">原始端位。</param>
    /// <returns>中文方位。</returns>
    private static string MapSide(string side) => side.ToLowerInvariant() switch
    {
        "north" => "北侧",
        "south" => "南侧",
        "east" => "东侧",
        "west" => "西侧",
        _ => side,
    };

    /// <summary>按路径首末点的位移判断载具行进方向，供列表徽标显示。</summary>
    /// <param name="item">带路径的载具。</param>
    /// <returns>徽标文字、文字色与底色。</returns>
    private static (string Tag, IBrush Text, IBrush Surface) VehicleDirection(PlacedObject item)
    {
        if (item.PathPoints.Count < 2)
        {
            return ("无路径", UiTheme.TextSecondary, UiTheme.Form.FilterIdle);
        }

        var from = item.PathPoints[0];
        var to = item.PathPoints[^1];
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? ("向东", UiTheme.InfoText, UiTheme.PreviewAccentSoft)
                : ("向西", UiTheme.InfoText, UiTheme.PreviewAccentSoft);
        }

        return dy >= 0 ? ("向南", UiTheme.InfoText, UiTheme.PreviewAccentSoft)
            : ("向北", UiTheme.InfoText, UiTheme.PreviewAccentSoft);
    }

    #endregion
}
