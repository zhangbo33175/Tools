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
        RebuildRowList(
            _lightList, _lightItems, _config!.TrafficLights,
            light => TrafficRuleRow.ForLight(light, CrosswalkName(light.BoundCrosswalkId)),
            light => light.Id, keepId,
            _lightListHost, _lightFormScroll, _lightEmpty,
            current => _light = current);
    }

    private void RefreshCrosswalkList(string? keepId)
    {
        RebuildRowList(
            _crosswalkList, _crosswalkItems, _config!.Crosswalks,
            crosswalk => TrafficRuleRow.ForCrosswalk(
                crosswalk, LightName(crosswalk.BoundLightId), CrosswalkName(crosswalk.Id)),
            crosswalk => crosswalk.Id, keepId,
            _crosswalkListHost, _crosswalkFormScroll, _crosswalkEmpty,
            current => _crosswalk = current);
    }

    private void RefreshActorList(string? keepId)
    {
        _actorSources.Clear();
        _actorSources.AddRange(TrafficRuleService.BuildActors(_config!));
        RebuildRowList(
            _actorList, _actorItems, _actorSources,
            item => TrafficRuleRow.ForActor(item),
            item => item.Id, keepId,
            _actorListHost, _actorFormScroll, _actorEmpty,
            current => _actor = current);
    }

    /// <summary>
    /// 三张列表共用的重建骨架：丢弃旧行容器 → 逐条生成展示行 → 按 ID 恢复选中 → 回写当前编辑对象。
    /// <para>列表整体重建而不是原位替换，是因为 Avalonia 的 ListBox 对集合项替换不重建行容器，
    /// 行文案与相位条会停在旧值；行数只有几十条，重建成本可忽略。</para>
    /// </summary>
    /// <typeparam name="T">数据源元素类型（灯 / 横道 / 角色）。</typeparam>
    /// <param name="list">目标列表控件。</param>
    /// <param name="rows">该列表的展示行集合。</param>
    /// <param name="source">数据源，通常直接取 <see cref="MapConfigData"/> 上的集合。</param>
    /// <param name="rowOf">把一条数据转换成展示行。</param>
    /// <param name="idOf">取数据的稳定 ID，用于重建后恢复选中。</param>
    /// <param name="keepId">重建前选中的数据 ID，找不到或为 null 时回退到首条。</param>
    /// <param name="host">列表区容器（决定空态是否显示）。</param>
    /// <param name="scroll">表单滚动区。</param>
    /// <param name="empty">数据为空时的占位提示。</param>
    /// <param name="onCurrent">回写当前编辑对象；空表时传 null。</param>
    private void RebuildRowList<T>(
        ListBox list,
        ObservableCollection<TrafficRuleRow> rows,
        IList<T> source,
        Func<T, TrafficRuleRow> rowOf,
        Func<T, string> idOf,
        string? keepId,
        Control host,
        Control scroll,
        Control empty,
        Action<T?> onCurrent)
    {
        _sync = true;
        list.ItemsSource = null;
        rows.Clear();
        foreach (var item in source)
        {
            rows.Add(rowOf(item));
        }

        list.ItemsSource = rows;
        SetPageContent(rows.Count > 0, host, scroll, empty);

        var index = -1;
        for (var i = 0; i < source.Count; i++)
        {
            if (idOf(source[i]) == keepId)
            {
                index = i;
                break;
            }
        }

        list.SelectedIndex = index >= 0 ? index : (rows.Count > 0 ? 0 : -1);
        onCurrent(index >= 0 ? source[index] : source.Count > 0 ? source[0] : default);
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

    /// <summary>
    /// 编辑提交后的统一收尾：刷新画布、通知文档已改、重建预览仿真、重建三张列表并刷新状态行。
    /// 列表一律整体重建：Avalonia 的 ListBox 对集合项原位替换不会重建行容器，
    /// 行文案与相位条会停在旧值；列表本身只有几十行，重建成本可忽略，且重建后仍按 ID 保持选中。
    /// </summary>
    /// <param name="refreshLists">是否连整页（表单与空态）一起重建。</param>
    private void ApplyChange(bool refreshLists = false)
    {
        _canvas?.CommitEdit();
        _canvas?.RebuildTrafficSimulation();
        if (refreshLists)
        {
            RefreshAll();
            return;
        }

        RefreshLightList(_light?.Id);
        RefreshCrosswalkList(_crosswalk?.Id);
        RefreshActorList(_actor?.Id);
        RefreshStatusLine();
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
    /// 按 ID 找规则对应的地图物件；ID 对不上再按名字找最近的一个。
    /// <para>灯是从物件标记出来的独立记录，横道的 ID 也可能与物件 ID 不同，
    /// 所以统一走「先 ID、后同名 + 锚点最近」这两步，三个页签共用一份兜底逻辑。</para>
    /// </summary>
    /// <param name="id">规则记录里的 ID。</param>
    /// <param name="name">规则记录里的名字，同名的多份实例按锚点距离挑最近。</param>
    /// <param name="anchorX">锚点 X（灯位 / 横道端点 / 物件中心）。</param>
    /// <param name="anchorY">锚点 Y。</param>
    /// <returns>对应物件，找不到返回 null。</returns>
    private PlacedObject? ResolveObject(string id, string? name, double anchorX, double anchorY)
    {
        if (_config is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(id))
        {
            var byId = _config.Objects.FirstOrDefault(item => item.Id == id);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        PlacedObject? best = null;
        var bestDistance = double.MaxValue;
        foreach (var item in _config.Objects)
        {
            if (!string.Equals(item.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            var center = item.Bounds.Center;
            var dx = center.X - anchorX;
            var dy = center.Y - anchorY;
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
    /// 把一条规则对应的物件定位到画布：有物件就选中并居中；
    /// 只有规则坐标时只移视口，并通过状态栏说明没有对应物件。
    /// </summary>
    /// <param name="target">解析出的物件，null 表示只有规则坐标。</param>
    /// <param name="label">规则名称，用于提示文案。</param>
    /// <param name="fallbackX">没有物件时的兜底视口中心 X。</param>
    /// <param name="fallbackY">没有物件时的兜底视口中心 Y。</param>
    /// <param name="quiet">true 时成功不弹提示（列表行切换用），失败仍会说明。</param>
    private void LocateRule(PlacedObject? target, string label, double fallbackX, double fallbackY, bool quiet)
    {
        if (_canvas is null)
        {
            return;
        }

        if (target is not null)
        {
            SelectObjectOnCanvas(target);
            if (!quiet)
            {
                _notify($"已在画布上选中「{target.Name}」");
            }

            return;
        }

        _canvas.FocusPoint(fallbackX, fallbackY);
        _notify($"「{label}」没有对应的地图物件，已把视口移到规则坐标"
                + $" ({fallbackX:0},{fallbackY:0})");
    }

    /// <summary>
    /// 灯行：灯位来自规则数据，未必有对应的地图物件。
    /// 有物件就选中它；没有就保持选中不变，只把视口移到灯位上。
    /// </summary>
    /// <param name="light">当前灯。</param>
    private void SyncLightOnCanvas(MapTrafficLightData? light)
    {
        if (light is null)
        {
            return;
        }

        LocateRule(ResolveObject(light.Id, light.Name, light.PosX, light.PosY),
            light.Name, light.PosX, light.PosY, quiet: true);
    }

    /// <summary>横道行：解析对应物件后定位；横道 ID 与物件 ID 不同时按名字 + 端点最近兜底。</summary>
    /// <param name="crosswalk">当前横道。</param>
    private void SyncCrosswalkOnCanvas(MapCrosswalkData? crosswalk)
    {
        if (crosswalk is null)
        {
            return;
        }

        var anchor = crosswalk.PointA;
        LocateRule(ResolveObject(crosswalk.Id, crosswalk.Name, anchor.X, anchor.Y),
            crosswalk.Name, anchor.X, anchor.Y, quiet: true);
    }

    /// <summary>角色行：解析对应物件后定位；按名字兜底时取包围盒中心最近的一份实例。</summary>
    /// <param name="actor">当前角色物件。</param>
    private void SyncActorOnCanvas(PlacedObject? actor)
    {
        if (actor is null)
        {
            return;
        }

        var center = actor.Bounds.Center;
        LocateRule(ResolveObject(actor.Id, actor.Name, center.X, center.Y),
            actor.Name, center.X, center.Y, quiet: true);
    }

    /// <summary>把当前行对应的物件选中到画布上并居中，方便对照位置。</summary>
    private void SelectCurrentOnCanvas()
    {
        switch (_pageIndex)
        {
            case 0 when _light is not null:
                LocateRule(ResolveObject(_light.Id, _light.Name, _light.PosX, _light.PosY),
                    _light.Name, _light.PosX, _light.PosY, quiet: false);
                return;
            case 1 when _crosswalk is not null:
                LocateRule(ResolveObject(_crosswalk.Id, _crosswalk.Name,
                        _crosswalk.PointA.X, _crosswalk.PointA.Y),
                    _crosswalk.Name, _crosswalk.PointA.X, _crosswalk.PointA.Y, quiet: false);
                return;
            case 2 when _actor is not null:
            {
                var center = _actor.Bounds.Center;
                LocateRule(ResolveObject(_actor.Id, _actor.Name, center.X, center.Y),
                    _actor.Name, center.X, center.Y, quiet: false);
                return;
            }
            default:
                _notify("当前页签没有可定位的规则记录");
                return;
        }
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
