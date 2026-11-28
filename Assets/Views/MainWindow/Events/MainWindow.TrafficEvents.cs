/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.TrafficEvents.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口交通规则事件：人行道/道路/横道/红绿灯标记、停止线与绑定灯列表。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;
namespace MapEditorTool;

public partial class MainWindow : Window
{
    private const string SidewalkLabel = "人行道";
    private const string RoadLabel = "道路";
    private const string CrosswalkLabel = "横道";
    private const string TrafficLightLabel = "红绿灯";

    /// <summary>交通标记按钮与业务类型的对应关系，刷新状态时只维护这一张表。</summary>
    private readonly record struct TrafficMarkButtonUi(
        Button Button,
        TrafficMarkKind Kind,
        string Label,
        string Shortcut);

    private IEnumerable<TrafficMarkButtonUi> TrafficMarkButtonUis()
    {
        yield return new TrafficMarkButtonUi(BtnToolSidewalk, TrafficMarkKind.Sidewalk, SidewalkLabel, "F1");
        yield return new TrafficMarkButtonUi(BtnToolRoad, TrafficMarkKind.Road, RoadLabel, "F2");
        yield return new TrafficMarkButtonUi(BtnToolCrosswalk, TrafficMarkKind.Crosswalk, CrosswalkLabel, "F3");
        yield return new TrafficMarkButtonUi(BtnToolLight, TrafficMarkKind.Light, TrafficLightLabel, "F4");
    }

    /// <summary>交通标记入口的统一前置校验：没有选中物件时给出类型化提示并刷新按钮。</summary>
    /// <param name="markLabel">标记类型显示名。</param>
    /// <param name="objectHint">期望选中的物件描述。</param>
    /// <returns>当前选中物件；未选中时返回 null。</returns>
    private PlacedObject? RequireSelectedObjectForMark(string markLabel, string objectHint)
    {
        var item = Canvas.Selected;
        if (item is not null)
        {
            return item;
        }

        SetMessage($"标记{markLabel}：请先在画布上选中{objectHint}");
        RefreshTrafficMarkButtons();
        return null;
    }

    /// <summary>场景编辑右栏「交通规则」区的装配：工具按钮、载具/角色、停止线、绑定灯、黄灯策略。</summary>
    private void WireTrafficPanel()
    {
        foreach (var display in TrafficRoles.DisplayChoices)
        {
            PropTrafficRole.Items.Add(new ComboBoxItem { Content = display });
        }

        PropTrafficRole.SelectedIndex = 0;

        BtnToolSidewalk.Click += (_, _) => ToggleSelectedSidewalkMark();
        BtnToolRoad.Click += (_, _) => ToggleSelectedRoadMark();
        BtnToolCrosswalk.Click += (_, _) => ToggleSelectedCrosswalkMark();
        BtnToolLight.Click += (_, _) => ToggleSelectedTrafficLight();

        PropVehicle.IsCheckedChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var item = Canvas.Selected;
            item.IsVehicle = PropVehicle.IsChecked == true;
            item.TrafficRole = item.IsVehicle ? TrafficRoles.Vehicle : TrafficRoles.Pedestrian;
            SelectComboByText(PropTrafficRole, TrafficRoles.ToDisplay(item.TrafficRole));
            Canvas.Refresh();
        };

        PropTrafficRole.SelectionChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var role = TrafficRoles.ToCode(ReadCombo(PropTrafficRole, "行人"));
            Canvas.Selected.TrafficRole = role;
            Canvas.Selected.IsVehicle = role == TrafficRoles.Vehicle;
            PropVehicle.IsChecked = Canvas.Selected.IsVehicle;
            Canvas.Refresh();
        };

        BindStopLine();

        PropYellowAsRed.IsCheckedChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            Canvas.Selected.YellowTreatAsRed = PropYellowAsRed.IsChecked == true;
        };

        PropLightSelectAll.IsCheckedChanged += (_, _) =>
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var all = PropLightSelectAll.IsChecked == true;
            Canvas.Selected.BoundLightIds.Clear();
            if (all)
            {
                foreach (var light in _config.TrafficLights)
                {
                    Canvas.Selected.BoundLightIds.Add(light.Id);
                }
            }

            RefreshLightBindList();
        };

        // 交通规则列常显（与 Unity 属性区同构），「交通」勾选只决定实例是否参与交通
        TrafficPanel.IsVisible = true;
    }

    /// <summary>
    /// 把右栏那组交通控件整体搬进「交通规则」面板的「物件标记」页。
    /// <para>不重建控件：四个标记按钮、实例属性、绑定灯列表连同事件与刷新逻辑一起搬过去，
    /// 两处不会各自维护一套标记口径；外观改成两张卡片，由 axaml 里的卡片 + markTool 样式负责。</para>
    /// </summary>
    /// <param name="panel">交通规则面板。</param>
    private void MoveTrafficControlsToRulePanel(TrafficRulePanel panel)
    {
        var host = panel.MarkerHost;
        if (TrafficDrawGroup.Parent is Panel oldHost && !ReferenceEquals(oldHost, host))
        {
            oldHost.Children.Remove(TrafficDrawGroup);
        }

        host.Children.Add(TrafficDrawGroup);
        panel.SetMarkerTarget(Canvas.Selected);
    }

    /// <summary>停止线输入框：支持 “x, y” 文本，失焦或回车提交。</summary>
    private void BindStopLine()
    {
        void Commit()
        {
            if (_suppressSync || Canvas.Selected is null)
            {
                return;
            }

            var parts = PropStopLine.Text?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
            var x = parts.Length > 0 && double.TryParse(parts[0], out var px) ? px : 0;
            var y = parts.Length > 1 && double.TryParse(parts[1], out var py) ? py : 0;
            Canvas.Selected.StopLineX = x;
            Canvas.Selected.StopLineY = y;
            Canvas.Refresh();
        }

        PropStopLine.LostFocus += (_, _) => Commit();
        UiFactory.CommitOnEnter(PropStopLine, Commit);
    }

    /// <summary>把当前选中物件标记 / 取消标记为人行道。</summary>
    private void ToggleSelectedSidewalkMark() => ToggleSelectedTrafficArea(RegionTypes.Sidewalk);

    /// <summary>把当前选中物件标记 / 取消标记为道路。</summary>
    private void ToggleSelectedRoadMark() => ToggleSelectedTrafficArea(RegionTypes.Road);

    /// <summary>
    /// 人行道 / 道路标记的统一入口：已标记则取消，未标记则把物件包围盒展开成对应格子。
    /// <para>一个物件只保留一种交通标记；换成另一种时会先清掉旧标记，避免同物件既算道路又算人行道。</para>
    /// </summary>
    /// <param name="type">区域类型：<see cref="RegionTypes.Sidewalk"/> 或 <see cref="RegionTypes.Road"/>。</param>
    private void ToggleSelectedTrafficArea(string type)
    {
        var roleName = type == RegionTypes.Sidewalk ? SidewalkLabel : RoadLabel;
        var item = RequireSelectedObjectForMark(roleName, "一个物件");
        if (item is null)
        {
            return;
        }

        if (TrafficMarkService.RemoveObjectArea(_config, item.Id, type))
        {
            SetMessage($"已取消「{item.Name}」的{roleName}标记，相关格子已清理");
            CommitTrafficMarkChange();
            return;
        }

        var clearedOthers = TrafficMarkService.RemoveObjectTrafficMarks(_config, item.Id);
        var cellRect = TrafficMarkService.BuildObjectCellRect(_config, item);
        var cellCount = TrafficMarkService.AddAreaMark(_config, item, type, cellRect);
        var suffix = OtherMarksClearedSuffix(clearedOthers);
        SetMessage(
            $"已标记「{item.Name}」为{roleName}（{cellRect.X:0},{cellRect.Y:0} " +
            $"{cellRect.Width:0}×{cellRect.Height:0}，{cellCount} 格）{suffix}");
        CommitTrafficMarkChange();
    }

    /// <summary>
    /// 把当前选中物件标记 / 取消标记为横道。
    /// <para>横道起终点取包围盒长边，宽度取短边；长边水平时方向为 horizontal，否则 vertical。</para>
    /// </summary>
    private void ToggleSelectedCrosswalkMark()
    {
        var item = RequireSelectedObjectForMark(CrosswalkLabel, "一个斑马线物件");
        if (item is null)
        {
            return;
        }

        if (TrafficMarkService.RemoveObjectCrosswalk(_config, item.Id))
        {
            SetMessage($"已取消「{item.Name}」的横道标记，并清理红绿灯绑定");
            CommitTrafficMarkChange();
            return;
        }

        var mark = TrafficMarkService.CreateCrosswalk(_config, item);
        var crosswalk = mark.Crosswalk;
        var nearestLight = mark.NearestLight;
        var bound = mark.Bound;
        var clearedOthers = mark.ClearedOtherMarks;

        var bindText = nearestLight is null
            ? "附近没有红绿灯，暂未绑定"
            : bound
                ? $"已绑定红绿灯「{nearestLight.Name}」"
                : $"最近红绿灯「{nearestLight.Name}」已有横道，未自动绑定";
        var suffix = OtherMarksClearedSuffix(clearedOthers);
        SetMessage(
            $"已标记「{item.Name}」为横道（{crosswalk.Direction}，宽 {crosswalk.Width:0}px）；{bindText}{suffix}");
        CommitTrafficMarkChange();
    }

    /// <summary>把当前选中物件标记 / 取消标记为红绿灯。</summary>
    private void ToggleSelectedTrafficLight()
    {
        var item = RequireSelectedObjectForMark(TrafficLightLabel, "一个红绿灯物件");
        if (item is null)
        {
            return;
        }

        if (TrafficMarkService.RemoveObjectLight(_config, item.Id))
        {
            SetMessage($"已取消「{item.Name}」的红绿灯标记，并清理相关绑定");
            CommitTrafficMarkChange();
            return;
        }

        var mark = TrafficMarkService.CreateLight(_config, item);
        var light = mark.Light;
        var nearest = mark.NearestCrosswalk;
        var bound = mark.Bound;
        var clearedOthers = mark.ClearedOtherMarks;

        var bindText = nearest is null
            ? "未找到邻近横道，暂未绑定"
            : bound
                ? $"已绑定横道「{nearest.Name}」"
                : $"最近横道「{nearest.Name}」已被其他灯占用，未自动绑定";
        var suffix = OtherMarksClearedSuffix(clearedOthers);
        SetMessage($"已标记「{item.Name}」为红绿灯（{light.GridX},{light.GridY}）；{bindText}{suffix}");
        CommitTrafficMarkChange();
    }

    /// <summary>
    /// 交通相关 UI 的统一刷新入口：绑定灯列表 + 四个标记按钮。
    /// 选中变化、标记变更和属性回填都走这里，避免只刷新一半控件。
    /// </summary>
    private void RefreshTrafficUi()
    {
        RefreshLightBindList();
        RefreshTrafficMarkButtons();
        _trafficPanel?.SetMarkerTarget(Canvas.Selected);
    }

    /// <summary>同步四个交通标记按钮：未选中禁用，已标记的按钮变成「取消…」。</summary>
    private void RefreshTrafficMarkButtons()
    {
        var item = Canvas.Selected;
        var objectId = item?.Id;
        foreach (var ui in TrafficMarkButtonUis())
        {
            var marked = TrafficMarkService.IsMarked(_config, objectId, ui.Kind);
            RefreshTrafficMarkButton(ui.Button, item, ui.Label, ui.Shortcut, marked);
        }
    }

    /// <summary>交通标记变更后的统一收尾：重绘画布、标记文档已改、刷新全部交通 UI。</summary>
    private void CommitTrafficMarkChange()
    {
        Canvas.CommitEdit();
        RefreshTrafficUi();
    }

    /// <summary>创建标记时是否附带“已清理其他交通标记”提示的统一定稿。</summary>
    /// <param name="cleared">创建前是否确实清掉了其他标记。</param>
    /// <returns>可直接拼到状态消息后的后缀。</returns>
    private static string OtherMarksClearedSuffix(bool cleared) =>
        cleared ? "；已清理该物件的其他交通标记" : string.Empty;

    /// <summary>刷新单个交通标记按钮的文案、可用状态与提示。</summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="item">当前选中物件。</param>
    /// <param name="roleName">标记类型显示名。</param>
    /// <param name="shortcut">快捷键文案。</param>
    /// <param name="marked">当前物件是否已带该标记。</param>
    private static void RefreshTrafficMarkButton(
        Button button,
        PlacedObject? item,
        string roleName,
        string shortcut,
        bool marked)
    {
        button.Content = marked ? $"取消{roleName}" : $"标记{roleName}";
        button.IsEnabled = item is not null;
        button.Classes.Set("marked", marked);
        ToolTip.SetTip(
            button,
            item is null
                ? $"先在地图上选中要标记为{roleName}的物件"
                : marked
                    ? $"取消「{item.Name}」的{roleName}标记"
                    : $"把「{item.Name}」标记为{roleName}（{shortcut}）");
    }
    private void RefreshLightBindList()
    {
        _suppressLightSync = true;
        PropLightList.Children.Clear();

        var selected = Canvas.Selected;
        if (_config.TrafficLights.Count == 0)
        {
            PropLightList.Children.Add(new TextBlock
            {
                Text = "（当前地图无红绿灯）",
                Foreground = UiTheme.TextSecondary,
                Margin = new Avalonia.Thickness(2, 2),
            });
            PropLightSelectAll.IsChecked = false;
            _suppressLightSync = false;
            return;
        }

        foreach (var light in _config.TrafficLights)
        {
            var box = new CheckBox
            {
                Content = $"{light.Name}  ({light.GridX},{light.GridY})",
                IsChecked = selected?.BoundLightIds.Contains(light.Id) == true,
                FontSize = UiMetrics.FontBody,
                Margin = new Avalonia.Thickness(0, 0, 0, 1),
            };

            var id = light.Id;
            box.IsCheckedChanged += (_, _) =>
            {
                if (_suppressLightSync || Canvas.Selected is null)
                {
                    return;
                }

                if (box.IsChecked == true)
                {
                    if (!Canvas.Selected.BoundLightIds.Contains(id))
                    {
                        Canvas.Selected.BoundLightIds.Add(id);
                    }
                }
                else
                {
                    Canvas.Selected.BoundLightIds.Remove(id);
                }
            };

            PropLightList.Children.Add(box);
        }

        PropLightSelectAll.IsChecked = selected is not null &&
            _config.TrafficLights.Count > 0 &&
            selected.BoundLightIds.Count == _config.TrafficLights.Count;
        _suppressLightSync = false;
    }

    /// <summary>按显示文本选中下拉项。</summary>
    private static void SelectComboByText(ComboBox box, string text)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? string.Empty) == text)
            {
                box.SelectedIndex = i;
                return;
            }
        }
    }
}

