/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficRulePanel.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则编辑面板：灯 / 横道 / 角色的列表镜像、编辑提交、
 *            规则自检，并把改动实时同步到画布与预览仿真
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using MapEditorTool.Controls;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>
/// 「交通规则」页：在编辑器里直接编写灯、横道与人/车的通行参数，而不是只看示例演示。
/// <para>数据全部落在 <see cref="MapConfigData"/> 上，所以「保存」会把规则一起写进地图 JSON；
/// 每次提交都会刷新画布与预览仿真，预览窗打开的就是这份数据。</para>
/// </summary>
public sealed partial class TrafficRulePanel : UserControl
{
    #region 字段

    private readonly Action<string> _notify;
    private MapConfigData? _config;
    private MapCanvas? _canvas;
    private bool _sync;

    private MapTrafficLightData? _light;
    private MapCrosswalkData? _crosswalk;
    private PlacedObject? _actor;

    private readonly ObservableCollection<TrafficRuleRow> _lightItems = new();
    private readonly ObservableCollection<TrafficRuleRow> _crosswalkItems = new();
    private readonly ObservableCollection<TrafficRuleRow> _actorItems = new();
    private readonly List<PlacedObject> _actorSources = new();

    private ListBox _lightList = null!;
    private ListBox _crosswalkList = null!;
    private ListBox _actorList = null!;
    private TextBlock _statLightsText = null!;
    private TextBlock _statCrosswalksText = null!;
    private TextBlock _statActorsText = null!;
    private TextBlock _statStopsText = null!;
    private TextBlock _statPhaseText = null!;
    private Border _readyBadge = null!;
    private TextBlock _readyText = null!;
    private TextBlock _statusGap = null!;
    private Control _lightEmpty = null!;
    private Control _crosswalkEmpty = null!;
    private Control _actorEmpty = null!;
    private Control _lightListHost = null!;
    private Control _crosswalkListHost = null!;
    private Control _actorListHost = null!;
    private Control _lightFormScroll = null!;
    private Control _crosswalkFormScroll = null!;
    private Control _actorFormScroll = null!;
    private Control _lightFormBody = null!;
    private Control _lightFormHint = null!;
    private Control _crosswalkFormBody = null!;
    private Control _crosswalkFormHint = null!;
    private Control _actorFormBody = null!;
    private Control _actorFormHint = null!;
    private ContentControl _pageHost = null!;
    private StackPanel _markerHost = null!;
    private TextBlock _markerTarget = null!;
    private readonly List<Control> _pages = new();
    private readonly List<Button> _pageSegments = new();
    private readonly List<TextBlock> _pageLabels = new();
    private readonly List<TextBlock> _pageCounts = new();
    private int _pageIndex;

    #endregion

    #region 构造与挂载

    /// <summary>构造面板。</summary>
    /// <param name="notify">状态栏消息回调。</param>
    public TrafficRulePanel(Action<string> notify)
    {
        _notify = notify;
        Content = BuildLayout();
        RefreshStatusLine();
    }

    /// <summary>请求打开示例路口交通演示窗（面板内「交通演示」按钮触发）。</summary>
    public event EventHandler? DemoRequested;

    /// <summary>请求宿主打开整图预览（面板只负责发信号，不自己造窗口）。</summary>
    public event EventHandler? PreviewRequested;

    /// <summary>
    /// 「物件标记」页的挂载点。标记按钮与实例属性由宿主（场景编辑）组装，
    /// 装配时整体搬进来，事件与刷新逻辑仍留在宿主侧，面板只提供位置与卡片容器。
    /// </summary>
    public Panel MarkerHost => _markerHost;

    /// <summary>同步「物件标记」页顶部的当前选中提示：没选中时标灰并说明按钮为什么禁用。</summary>
    /// <param name="item">画布上当前选中的物件，未选中传 null。</param>
    public void SetMarkerTarget(PlacedObject? item)
    {
        if (_markerTarget is null)
        {
            return;
        }

        _markerTarget.Text = item is null
            ? "当前未选中物件 · 标记按钮已禁用"
            : $"当前选中：{item.Name}　（ID {item.Id}）";
        _markerTarget.Foreground = item is null ? UiTheme.TextSecondary : UiTheme.TextPrimary;
    }

    /// <summary>切换到新的地图数据后重新挂载。</summary>
    /// <param name="config">地图数据。</param>
    /// <param name="canvas">用于选中物件与刷新画面的画布。</param>
    public void Attach(MapConfigData config, MapCanvas canvas)
    {
        _config = config;
        _canvas = canvas;
        _light = null;
        _crosswalk = null;
        _actor = null;
        RefreshAll();
        SetMarkerTarget(canvas.Selected);
    }

    #endregion

    #region 刷新

    /// <summary>重建三张列表与当前表单，尽量保留原选中项。</summary>
    public void RefreshAll()
    {
        if (_config is null)
        {
            return;
        }

        var lightId = _light?.Id;
        var crosswalkId = _crosswalk?.Id;
        var actorId = _actor?.Id;

        RefreshLightList(lightId);
        RefreshCrosswalkList(crosswalkId);
        RefreshActorList(actorId);

        FillLightForm();
        FillCrosswalkForm();
        FillActorForm();
        RefreshStatusLine();
    }

    private void RefreshLightList(string? keepId)
    {
        _sync = true;
        _lightItems.Clear();
        foreach (var light in _config!.TrafficLights)
        {
            _lightItems.Add(TrafficRuleRow.ForLight(light, CrosswalkName(light.BoundCrosswalkId)));
        }

        _lightList.ItemsSource = _lightItems;
        SetPageContent(_lightItems.Count > 0, _lightListHost, _lightFormScroll, _lightEmpty);
        var index = _config.TrafficLights.FindIndex(light => light.Id == keepId);
        _lightList.SelectedIndex = index >= 0 ? index : (_lightItems.Count > 0 ? 0 : -1);
        _light = index >= 0 ? _config.TrafficLights[index] : _config.TrafficLights.FirstOrDefault();
        _sync = false;
    }

    private void RefreshCrosswalkList(string? keepId)
    {
        _sync = true;
        _crosswalkItems.Clear();
        foreach (var crosswalk in _config!.Crosswalks)
        {
            _crosswalkItems.Add(TrafficRuleRow.ForCrosswalk(
                crosswalk, LightName(crosswalk.BoundLightId), CrosswalkName(crosswalk.Id)));
        }

        _crosswalkList.ItemsSource = _crosswalkItems;
        SetPageContent(_crosswalkItems.Count > 0, _crosswalkListHost, _crosswalkFormScroll, _crosswalkEmpty);
        var index = _config.Crosswalks.FindIndex(crosswalk => crosswalk.Id == keepId);
        _crosswalkList.SelectedIndex = index >= 0 ? index : (_crosswalkItems.Count > 0 ? 0 : -1);
        _crosswalk = index >= 0 ? _config.Crosswalks[index] : _config.Crosswalks.FirstOrDefault();
        _sync = false;
    }

    private void RefreshActorList(string? keepId)
    {
        _sync = true;
        _actorSources.Clear();
        _actorSources.AddRange(TrafficRuleService.BuildActors(_config!));
        _actorItems.Clear();
        foreach (var item in _actorSources)
        {
            _actorItems.Add(TrafficRuleRow.ForActor(item));
        }

        _actorList.ItemsSource = _actorItems;
        SetPageContent(_actorItems.Count > 0, _actorListHost, _actorFormScroll, _actorEmpty);
        var index = _actorSources.FindIndex(item => item.Id == keepId);
        _actorList.SelectedIndex = index >= 0 ? index : (_actorItems.Count > 0 ? 0 : -1);
        _actor = index >= 0 ? _actorSources[index] : _actorSources.FirstOrDefault();
        _sync = false;
    }

    /// <summary>按 ID 取横道名，供灯行显示绑定对象；找不到时回退 ID。</summary>
    /// <param name="id">横道 ID。</param>
    /// <returns>横道名或空串。</returns>
    private string CrosswalkName(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return string.Empty;
        }

        var crosswalk = _config?.Crosswalks.FirstOrDefault(item => item.Id == id);
        if (crosswalk is null)
        {
            return id;
        }

        if (string.IsNullOrWhiteSpace(crosswalk.Name))
        {
            return id;
        }

        // 原图里两条斑马线都叫 bg_road_9：同名时补 #n，列表行与绑定摘要才分得开。
        // 只在显示层编号，不改地图数据里的名字。
        var sameName = _config!.Crosswalks.Where(item => item.Name == crosswalk.Name).ToList();
        return sameName.Count < 2
            ? crosswalk.Name
            : $"{crosswalk.Name} #{sameName.IndexOf(crosswalk) + 1}";
    }

    /// <summary>按 ID 取灯名，供横道行显示绑定对象；找不到时回退 ID。</summary>
    /// <param name="id">灯 ID。</param>
    /// <returns>灯名或空串。</returns>
    private string LightName(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return string.Empty;
        }

        var light = _config?.TrafficLights.FirstOrDefault(item => item.Id == id);
        return light is null
            ? id
            : string.IsNullOrWhiteSpace(light.Name) ? id : light.Name;
    }

    /// <summary>编辑提交后的统一收尾：刷新画布、通知文档已改、重建预览仿真、更新列表行文案。</summary>
    private void ApplyChange(bool refreshLists = false)
    {
        _canvas?.Refresh();
        _canvas?.NotifyDocumentChanged();
        _canvas?.RebuildTrafficSimulation();

        if (refreshLists)
        {
            RefreshAll();
            return;
        }

        // 只改参数时刷新当前行的显示内容，避免输入过程中列表重建抢走焦点
        ReplaceRow(_lightList, _lightItems, _config?.TrafficLights.IndexOf(_light!) ?? -1,
            _light is null ? null : TrafficRuleRow.ForLight(_light, CrosswalkName(_light.BoundCrosswalkId)));
        ReplaceRow(_crosswalkList, _crosswalkItems, _config?.Crosswalks.IndexOf(_crosswalk!) ?? -1,
            _crosswalk is null ? null : TrafficRuleRow.ForCrosswalk(
            _crosswalk, LightName(_crosswalk.BoundLightId), CrosswalkName(_crosswalk.Id)));
        ReplaceRow(_actorList, _actorItems, _actor is null ? -1 : _actorSources.IndexOf(_actor),
            _actor is null ? null : TrafficRuleRow.ForActor(_actor));
        RefreshStatusLine();
    }

    private static void ReplaceRow(
        ListBox list,
        ObservableCollection<TrafficRuleRow> items,
        int index,
        TrafficRuleRow? row)
    {
        if (row is null || index < 0 || index >= items.Count || items[index] == row)
        {
            return;
        }

        var selected = list.SelectedIndex;
        items[index] = row;
        list.SelectedIndex = selected;
    }

    #endregion

    #region 工具栏动作

    /// <summary>把当前场景选中的物件标记成一盏灯；已在规则表里时提示改用列表。</summary>
    private void AddLightFromSelection()
    {
        if (!TryGetSelectedObject("红绿灯物件", out var item))
        {
            return;
        }

        if (_config!.TrafficLights.Any(light => light.Id == item.Id))
        {
            _notify($"「{item.Name}」已经是红绿灯，直接在列表里改参数");
            return;
        }

        var mark = TrafficMarkService.CreateLight(_config, item);
        TrafficRuleService.NormalizeLight(mark.Light);
        _notify(mark.Bound
            ? $"已新增红绿灯「{item.Name}」，并绑定横道「{mark.NearestCrosswalk!.Name}」"
            : $"已新增红绿灯「{item.Name}」；附近没有可绑定横道，请手动选择");
        RefreshAll();
        ApplyChange();
    }

    /// <summary>把当前场景选中的物件标记成一条横道。</summary>
    private void AddCrosswalkFromSelection()
    {
        if (!TryGetSelectedObject("斑马线物件", out var item))
        {
            return;
        }

        if (_config!.Crosswalks.Any(crosswalk => crosswalk.Id == item.Id))
        {
            _notify($"「{item.Name}」已经是横道，直接在列表里改参数");
            return;
        }

        var mark = TrafficMarkService.CreateCrosswalk(_config, item);
        _notify(mark.Bound
            ? $"已新增横道「{item.Name}」，并绑定红绿灯「{mark.NearestLight!.Name}」"
            : $"已新增横道「{item.Name}」；附近没有空闲红绿灯，请手动选择");
        RefreshAll();
        ApplyChange();
    }

    /// <summary>删除当前页选中的规则记录（灯或横道）。</summary>
    private void RemoveCurrent()
    {
        if (_pageIndex == 0)
        {
            if (_light is null || !TrafficRuleService.RemoveLight(_config!, _light.Id))
            {
                _notify("请先在红绿灯列表里选中一盏灯");
                return;
            }

            _notify($"已删除红绿灯「{_light.Name}」，相关绑定与角色引用已清理");
            _light = null;
            RefreshAll();
            ApplyChange();
            return;
        }

        if (_pageIndex == 1)
        {
            if (_crosswalk is null || !TrafficRuleService.RemoveCrosswalk(_config!, _crosswalk.Id))
            {
                _notify("请先在横道列表里选中一条横道");
                return;
            }

            _notify($"已删除横道「{_crosswalk.Name}」，灯的绑定已清理");
            _crosswalk = null;
            RefreshAll();
            ApplyChange();
            return;
        }

        _notify("角色页不提供删除：角色的增减由场景里的路径决定");
    }

    /// <summary>
    /// 列表换行时把对应物件同步选中到画布上：点一条规则，场景里立刻高亮它管的灯 / 斑马线 / 车，
    /// 不必再点「定位」。已经选中的物件直接跳过，避免每次刷新都重复触发属性回填。
    /// </summary>
    /// <param name="objectId">规则对应的地图物件 ID。</param>
    /// <returns>找到并选中（或已选中）物件时返回 true。</returns>
    private bool SyncCanvasSelection(string? objectId)
    {
        if (_canvas is null || _config is null || string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        var target = _config.Objects.FirstOrDefault(item => item.Id == objectId);
        if (target is null)
        {
            return false;
        }

        SelectObjectOnCanvas(target);
        return true;
    }

    /// <summary>把物件选中到画布并居中；规则行与「定位」按钮共用。</summary>
    /// <param name="target">目标物件。</param>
    private void SelectObjectOnCanvas(PlacedObject target)
    {
        if (_canvas is null)
        {
            return;
        }

        if (!ReferenceEquals(_canvas.Selected, target))
        {
            _canvas.Select(target);
        }

        // 选中还不够：物件可能在视口外，缩到 16% 时连选中框都看不见，所以顺带居中一次
        _canvas.FocusObject(target);
    }

    /// <summary>
    /// 找到灯对应的地图物件。灯是从物件标记出来的独立记录，ID 未必继承物件 ID，
    /// 所以先按 ID 找，找不到再按同名 + 灯位最近兜底。
    /// </summary>
    /// <param name="light">当前灯。</param>
    /// <returns>对应物件，找不到返回 null。</returns>
    private PlacedObject? ResolveLightObject(MapTrafficLightData light)
    {
        if (_config is null)
        {
            return null;
        }

        var byId = _config.Objects.FirstOrDefault(item => item.Id == light.Id);
        if (byId is not null)
        {
            return byId;
        }

        PlacedObject? best = null;
        var bestDistance = double.MaxValue;
        foreach (var item in _config.Objects)
        {
            if (!string.Equals(item.Name, light.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var center = item.Bounds.Center;
            var dx = center.X - light.PosX;
            var dy = center.Y - light.PosY;
            var distance = dx * dx + dy * dy;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = item;
        }

        return best;
    }

    /// <summary>
    /// 灯行：灯位来自规则数据，未必有对应的地图物件。
    /// 有物件就选中它；没有就保持选中不变，只把视口移到灯位上。
    /// </summary>
    /// <param name="light">当前灯。</param>
    private void SyncLightOnCanvas(MapTrafficLightData? light)
    {
        if (light is null || _canvas is null)
        {
            return;
        }

        var target = ResolveLightObject(light);
        if (target is not null)
        {
            SelectObjectOnCanvas(target);
            return;
        }

        _canvas.FocusPoint(light.PosX, light.PosY);
    }

    /// <summary>把当前行对应的物件选中到画布上并居中，方便对照位置。</summary>
    private void SelectCurrentOnCanvas()
    {
        var target = _pageIndex switch
        {
            0 => _light is null ? null : ResolveLightObject(_light),
            1 => _config?.Objects.FirstOrDefault(item => item.Id == _crosswalk?.Id),
            _ => _config?.Objects.FirstOrDefault(item => item.Id == _actor?.Id),
        };
        if (target is not null && _canvas is not null)
        {
            _canvas.Select(target);
            _canvas.FocusObject(target);
            _notify($"已在画布上选中「{target.Name}」");
            return;
        }

        if (_pageIndex == 0 && _light is not null && _canvas is not null)
        {
            _canvas.FocusPoint(_light.PosX, _light.PosY);
            _notify($"「{_light.Name}」只有规则坐标，已把视口移到灯位"
                    + $" ({_light.PosX:0},{_light.PosY:0})");
            return;
        }

        _notify("当前规则没有对应的地图物件，无法在画布上选中");
    }

    private bool TryGetSelectedObject(string expect, out PlacedObject item)
    {
        item = _canvas?.Selected!;
        if (item is null)
        {
            _notify($"请先在场景编辑里选中一个{expect}");
            return false;
        }

        return true;
    }

    #endregion

    #region 状态与自检

    /// <summary>把三页签尾部的数量徽标一起刷新；面板还没建好时静默跳过。</summary>
    /// <param name="lights">红绿灯数量文字。</param>
    /// <param name="crosswalks">横道数量文字。</param>
    /// <param name="actors">车 / 行人数量文字。</param>
    private void SetPageCounts(string lights, string crosswalks, string actors)
    {
        if (_pageCounts.Count < 3)
        {
            return;
        }

        _pageCounts[0].Text = lights;
        _pageCounts[1].Text = crosswalks;
        _pageCounts[2].Text = actors;
    }

    /// <summary>刷新顶部统计胶囊与自检徽标：数量、停点覆盖与绑定缺口。</summary>
    private void RefreshStatusLine()
    {
        if (_config is null)
        {
            _statLightsText.Text = "—";
            _statCrosswalksText.Text = "—";
            _statActorsText.Text = "—";
            _statStopsText.Text = "—";
            _statPhaseText.Text = "—";
            _readyText.Text = "未载入地图";
            _readyBadge.Background = UiTheme.Form.FilterIdle;
            _readyText.Foreground = UiTheme.TextSecondary;
            _statusGap.IsVisible = false;
            SetPageCounts("—", "—", "—");
            return;
        }

        TrafficRuleService.DescribeReadiness(_config, out var actors, out var armed, out var waitingLights);
        _statLightsText.Text = _config.TrafficLights.Count.ToString();
        _statCrosswalksText.Text = _config.Crosswalks.Count.ToString();
        _statActorsText.Text = actors.ToString();
        _statStopsText.Text = $"{armed}/{actors}";
        _statPhaseText.Text = waitingLights.ToString();
        SetPageCounts(
            _config.TrafficLights.Count.ToString(),
            _config.Crosswalks.Count.ToString(),
            actors.ToString());

        var gap = TrafficRuleService.DescribeGaps(_config);
        var ready = gap.Length == 0;
        _readyText.Text = ready ? "规则齐全" : "待补缺口";
        _readyText.Foreground = ready ? UiTheme.Traffic.ReadyText : UiTheme.Traffic.GapText;
        _readyBadge.Background = ready ? UiTheme.Traffic.ReadySurface : UiTheme.Traffic.GapSurface;
        ToolTip.SetTip(_readyBadge, ready
            ? "灯、横道、角色与停点都齐；打开地图预览勾选『交通仿真』即可看到等待与起步"
            : gap);

        _statusGap.Text = ready
            ? "停点已覆盖全部等待角色 · 打开「地图预览」勾选『交通仿真』查看效果"
            : $"待补 · {gap}";
        _statusGap.Foreground = ready ? UiTheme.Traffic.ReadyText : UiTheme.Traffic.UnboundText;
        ToolTip.SetTip(_statusGap, _statusGap.Text);
        _statusGap.IsVisible = true;
    }

    #endregion
}
