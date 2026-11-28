/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrafficRulePanel.Forms.cs
 * author:    云毅
 * created:   2026
 * descrip:   交通规则面板的三张表单：灯时序、横道几何、角色通行参数的回填与提交
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class TrafficRulePanel : UserControl
{
    #region 控件字段

    private TextBox _lightIdText = null!;
    private TextBox _lightName = null!;
    private TextBox _lightX = null!;
    private TextBox _lightY = null!;
    private ComboBox _lightDir = null!;
    private ComboBox _lightSide = null!;
    private TextBox _lightRed = null!;
    private TextBox _lightGreen = null!;
    private TextBox _lightYellow = null!;
    private TextBox _lightOffset = null!;
    private ComboBox _lightPhase = null!;
    private TextBox _lightGroup = null!;
    private ComboBox _lightCrosswalk = null!;
    private TextBlock _lightBindSummary = null!;
    private TextBlock _lightCycleText = null!;
    private Grid _lightTimeline = null!;

    private TextBox _crosswalkIdText = null!;
    private TextBox _crosswalkName = null!;
    private ComboBox _crosswalkDir = null!;
    private TextBox _crosswalkWidth = null!;
    private TextBox _crosswalkAX = null!;
    private TextBox _crosswalkAY = null!;
    private TextBox _crosswalkBX = null!;
    private TextBox _crosswalkBY = null!;
    private ComboBox _crosswalkLight = null!;

    private TextBox _actorNameText = null!;
    private ComboBox _actorRole = null!;
    private TextBox _actorSpeed = null!;
    private ComboBox _actorMode = null!;
    private CheckBox _actorLoop = null!;
    private TextBox _actorStopX = null!;
    private TextBox _actorStopY = null!;
    private CheckBox _actorYellowRed = null!;
    private TextBlock _actorPathText = null!;
    private StackPanel _actorLightsPanel = null!;

    #endregion

    #region 灯表单

    private Control BuildLightForm()
    {
        _lightIdText = ReadOnlyField();
        _lightName = Field();
        _lightX = Field();
        _lightY = Field();
        _lightDir = Combo("horizontal（横向）", "vertical（纵向）");
        _lightSide = Combo("north", "south", "east", "west");
        _lightRed = Field();
        _lightGreen = Field();
        _lightYellow = Field();
        _lightOffset = Field();
        _lightPhase = Combo("red", "green", "yellow");
        _lightGroup = Field();
        _lightCrosswalk = Combo();
        _lightBindSummary = new TextBlock
        {
            FontSize = UiMetrics.FontCaption,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 14,
            Foreground = UiTheme.TextSecondary,
        };
        _lightCycleText = Hint(string.Empty);

        HookCommit(
            () => CommitLight(),
            _lightName,
            _lightX,
            _lightY,
            _lightRed,
            _lightGreen,
            _lightYellow,
            _lightOffset,
            _lightGroup);

        _lightDir.SelectionChanged += (_, _) => CommitLight(deferFill: true);
        _lightSide.SelectionChanged += (_, _) => CommitLight(deferFill: true);
        _lightPhase.SelectionChanged += (_, _) => CommitLight(deferFill: true);
        _lightCrosswalk.SelectionChanged += (_, _) =>
        {
            if (_sync || _light is null || _config is null)
            {
                return;
            }

            var index = _lightCrosswalk.SelectedIndex;
            var crosswalkId = index > 0 && index <= _config.Crosswalks.Count
                ? _config.Crosswalks[index - 1].Id
                : string.Empty;
            TrafficRuleService.BindLightToCrosswalk(_config, _light.Id, crosswalkId);

            // 这里还在 ComboBox 的 SelectionChanged / 批量选择提交调用栈里。
            // 此刻改 ListBox 的行文本或重建任何下拉 Items 都会撞上
            // “Source collection was modified during selection update”直接崩进程，
            // 所以只落数据，界面回填与画布刷新全部推到下一帧。
            ApplyAfterCombo();
        };

        var pickPosition = PickFromSelectionButton(
            "取选中物件位置",
            "先在场景里选中灯体物件，再点这里取位置",
            () => _light,
            (light, item) =>
            {
                var bounds = item.Bounds;
                light.PosX = bounds.Center.X;
                light.PosY = bounds.Bottom;
                light.Name = item.Name;
                FillLightForm();
            });

        var select = ActionButton("在画布选中该物件", SelectCurrentOnCanvas);

        _lightCycleText.FontSize = 10.5;
        _lightCycleText.Foreground = UiTheme.InfoText;
        _lightCycleText.TextWrapping = TextWrapping.Wrap;

        _lightTimeline = new Grid { Height = 18 };
        var timelineHost = new Border
        {
            Height = 18,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Background = UiTheme.Traffic.PhaseStripTrack,
            Child = _lightTimeline,
        };

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Section("基本", Fields(
                    PairRow("ID", _lightIdText, "名称", _lightName),
                    PairRow("位置 X", _lightX, "Y", _lightY),
                    PairRow("方向", _lightDir, "端位", _lightSide),
                    Hint("灯位取物件脚底中心，只决定规则停点，不移动贴图"))),
                Section("相位时序（秒）", Fields(
                    PairRow("红", _lightRed, "绿", _lightGreen),
                    PairRow("黄", _lightYellow, "偏移", _lightOffset),
                    timelineHost,
                    _lightCycleText,
                    Row("初始相位", _lightPhase),
                    Hint("相位按 (时间 + 偏移) % 周期 推进，正值表示本灯提前；初始相位只在周期为 0 时兜底"))),
                Section("绑定", Fields(
                    Row("绑定横道", _lightCrosswalk),
                    _lightBindSummary,
                    Row("路口分组", _lightGroup),
                    Hint("留空时按灯自身分组；非空值让同一路口的多盏灯共用一组时序"))),
                ActionRow(pickPosition, select),
            },
        };
        _lightFormBody = body;
        _lightFormHint = EmptyHint("未选中红绿灯",
            "点左侧列表的一条规则，或点上方「＋ 红绿灯」新建；新建后会自动选中");
        return FormShell(body, _lightFormHint);
    }

    private void FillLightForm()
    {
        _sync = true;
        var light = _light;
        var available = light is not null && _config is not null;
        ToggleFormBody(available, _lightFormBody, _lightFormHint);
        _lightName.IsEnabled = available;
        _lightX.IsEnabled = available;
        _lightY.IsEnabled = available;
        _lightRed.IsEnabled = available;
        _lightGreen.IsEnabled = available;
        _lightYellow.IsEnabled = available;
        _lightOffset.IsEnabled = available;
        _lightDir.IsEnabled = available;
        _lightSide.IsEnabled = available;
        _lightPhase.IsEnabled = available;
        _lightGroup.IsEnabled = available;
        _lightCrosswalk.IsEnabled = available;

        if (!available)
        {
            _lightIdText.Text = "—";
            _lightName.Text = string.Empty;
            _lightX.Text = "0";
            _lightY.Text = "0";
            _lightRed.Text = string.Empty;
            _lightGreen.Text = string.Empty;
            _lightYellow.Text = string.Empty;
            _lightOffset.Text = string.Empty;
            _lightGroup.Text = string.Empty;
            _lightPhase.SelectedIndex = -1;
            _lightDir.SelectedIndex = -1;
            _lightSide.SelectedIndex = -1;
            FillCrosswalkCombo(string.Empty);
            _lightCycleText.Text = "选中一盏灯后可编辑";
            _lightBindSummary.Text = "未选中灯，暂无绑定信息";
            _lightBindSummary.Foreground = UiTheme.TextSecondary;
            UpdateLightTimeline(null);
            _sync = false;
            return;
        }

        _lightIdText.Text = light!.Id;
        _lightName.Text = light.Name;
        _lightX.Text = Format(light.PosX);
        _lightY.Text = Format(light.PosY);
        _lightRed.Text = Format(light.RedDuration);
        _lightGreen.Text = Format(light.GreenDuration);
        _lightYellow.Text = Format(light.YellowDuration);
        _lightOffset.Text = Format(light.OffsetPhase);
        _lightGroup.Text = light.IntersectionId;
        SelectComboByText(_lightPhase, light.InitialPhase);
        SelectComboByText(_lightDir, light.Direction == TrafficDirections.Vertical
            ? "vertical（纵向）"
            : "horizontal（横向）");
        SelectComboByText(_lightSide, light.Side);
        FillCrosswalkCombo(light.BoundCrosswalkId);
        FillLightBindSummary(light);
        _lightCycleText.Text =
            $"周期 {light.RedDuration + light.GreenDuration + light.YellowDuration:0.#}s｜" +
            $"红 {Format(light.RedDuration)} 绿 {Format(light.GreenDuration)} 黄 {Format(light.YellowDuration)}";
        UpdateLightTimeline(light);
        _sync = false;
    }

    /// <summary>
    /// 相位时序条：按红 / 绿 / 黄秒数比例铺色块，直接看出一个周期里各相位占多长，
    /// 与游戏 (已过时间 + 偏移) % 周期的推进顺序一致。
    /// </summary>
    /// <param name="light">当前灯；null 时清空。</param>
    private void UpdateLightTimeline(MapTrafficLightData? light)
    {
        _lightTimeline.Children.Clear();
        _lightTimeline.ColumnDefinitions.Clear();
        if (light is null)
        {
            return;
        }

        AddTimelineSegment(
            Math.Max(0.05, light.RedDuration),
            UiTheme.Traffic.PhaseRed,
            Brushes.White);
        AddTimelineSegment(
            Math.Max(0.05, light.GreenDuration),
            UiTheme.Traffic.PhaseGreen,
            Brushes.White);
        AddTimelineSegment(
            Math.Max(0, light.YellowDuration),
            UiTheme.Traffic.PhaseYellow,
            UiTheme.Traffic.PhaseYellowText);
    }

    /// <summary>向时序条追加一个相位色块，宽度按秒数做星号权重。</summary>
    private void AddTimelineSegment(double seconds, IBrush background, IBrush foreground)
    {
        var index = _lightTimeline.ColumnDefinitions.Count;
        _lightTimeline.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(Math.Max(0.2, seconds), GridUnitType.Star)));

        var cell = new Border
        {
            Background = background,
            Child = new TextBlock
            {
                Text = seconds >= 1.0 ? $"{seconds:0.#}s" : string.Empty,
                FontSize = 9,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(cell, index);
        _lightTimeline.Children.Add(cell);
    }

    /// <summary>绑定摘要：把绑定横道的方向 / 宽度 / 端点写清楚，未绑时提示停点会回退到灯位推算。</summary>
    /// <param name="light">当前灯。</param>
    private void FillLightBindSummary(MapTrafficLightData light)
    {
        var crosswalk = string.IsNullOrEmpty(light.BoundCrosswalkId)
            ? null
            : _config?.Crosswalks.FirstOrDefault(item => item.Id == light.BoundCrosswalkId);
        if (crosswalk is null)
        {
            _lightBindSummary.Text = string.IsNullOrEmpty(light.BoundCrosswalkId)
                ? "未绑定横道 · 载具停点将回退到灯位推算"
                : $"绑定横道 {light.BoundCrosswalkId} 已不在规则表里 · 请重新选择";
            _lightBindSummary.Foreground = UiTheme.Traffic.UnboundText;
            return;
        }

        var name = string.IsNullOrWhiteSpace(crosswalk.Name) ? crosswalk.Id : crosswalk.Name;
        var direction = crosswalk.Direction == TrafficDirections.Vertical ? "纵向" : "横向";
        _lightBindSummary.Text = $"已绑「{name}」· {direction} · 宽 {crosswalk.Width:0} " +
                                 $"· ({crosswalk.PointA.X:0},{crosswalk.PointA.Y:0})→" +
                                 $"({crosswalk.PointB.X:0},{crosswalk.PointB.Y:0})";
        _lightBindSummary.Foreground = UiTheme.Traffic.BoundText;
    }

    private void FillCrosswalkCombo(string boundId)
    {
        // 因此开到一半的列表推迟到关闭后的下一帧再重建。
        if (_lightCrosswalk.IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(() => FillCrosswalkCombo(boundId));
            return;
        }

        _lightCrosswalk.Items.Clear();
        _lightCrosswalk.Items.Add(new ComboBoxItem { Content = "（不绑定）" });
        var selected = 0;
        if (_config is not null)
        {
            for (var i = 0; i < _config.Crosswalks.Count; i++)
            {
                var crosswalk = _config.Crosswalks[i];
                var name = string.IsNullOrWhiteSpace(crosswalk.Name) ? crosswalk.Id : crosswalk.Name;
                _lightCrosswalk.Items.Add(new ComboBoxItem { Content = name });
                if (crosswalk.Id == boundId)
                {
                    selected = i + 1;
                }
            }
        }

        _lightCrosswalk.SelectedIndex = selected;
    }

    private void CommitLight(bool deferFill = false)
    {
        if (_sync || _light is null)
        {
            return;
        }

        _light.Name = _lightName.Text?.Trim() ?? string.Empty;
        _light.PosX = ParseDouble(_lightX.Text, _light.PosX);
        _light.PosY = ParseDouble(_lightY.Text, _light.PosY);
        _light.RedDuration = ParseDouble(_lightRed.Text, _light.RedDuration);
        _light.GreenDuration = ParseDouble(_lightGreen.Text, _light.GreenDuration);
        _light.YellowDuration = ParseDouble(_lightYellow.Text, _light.YellowDuration);
        _light.OffsetPhase = ParseDouble(_lightOffset.Text, _light.OffsetPhase);
        _light.InitialPhase = ReadCombo(_lightPhase, TrafficLightDefaults.InitialPhase) switch
        {
            "green" => "green",
            "yellow" => "yellow",
            _ => "red",
        };
        _light.Direction = ReadCombo(_lightDir, TrafficDirections.Horizontal).StartsWith("vertical")
            ? TrafficDirections.Vertical
            : TrafficDirections.Horizontal;
        _light.Side = ReadCombo(_lightSide, TrafficLightDefaults.Side);
        _light.IntersectionId = _lightGroup.Text?.Trim() ?? string.Empty;

        TrafficRuleService.NormalizeLight(_light);
        if (deferFill)
        {
            ApplyAfterCombo();
            return;
        }

        FillLightForm();
        ApplyChange();
    }

    /// <summary>
    /// ComboBox 选择回调里只落数据，所有控件回填 + 列表行文本刷新 + 画布刷新
    /// 都推迟到下一帧，避免在选择提交流程中修改集合触发 Avalonia 选择模型崩溃。
    /// </summary>
    private void ApplyAfterCombo()
    {
        Dispatcher.UIThread.Post(() =>
        {
            FillLightForm();
            FillCrosswalkForm();
            ApplyChange();
        });
    }

    #endregion

    #region 横道表单

    private Control BuildCrosswalkForm()
    {
        _crosswalkIdText = ReadOnlyField();
        _crosswalkName = Field();
        _crosswalkDir = Combo("horizontal（横向）", "vertical（纵向）");
        _crosswalkWidth = Field();
        _crosswalkAX = Field();
        _crosswalkAY = Field();
        _crosswalkBX = Field();
        _crosswalkBY = Field();
        _crosswalkLight = Combo();

        HookCommit(
            () => CommitCrosswalk(),
            _crosswalkName,
            _crosswalkWidth,
            _crosswalkAX,
            _crosswalkAY,
            _crosswalkBX,
            _crosswalkBY);
        _crosswalkDir.SelectionChanged += (_, _) => CommitCrosswalk(deferFill: true);
        _crosswalkLight.SelectionChanged += (_, _) =>
        {
            if (_sync || _crosswalk is null || _config is null)
            {
                return;
            }

            var index = _crosswalkLight.SelectedIndex;
            var lightId = index > 0 && index <= _config.TrafficLights.Count
                ? _config.TrafficLights[index - 1].Id
                : string.Empty;
            TrafficRuleService.BindCrosswalkToLight(_config, _crosswalk.Id, lightId);

            // 同上：绑定回调里只改数据，界面回填与画布刷新推迟到 UI 队列下一帧。
            ApplyAfterCombo();
        };

        var pickGeometry = PickFromSelectionButton(
            "取选中物件包围盒",
            "先在场景里选中斑马线物件，再点这里取几何",
            () => _crosswalk,
            (crosswalk, item) =>
            {
                var bounds = item.Bounds;
                var horizontal = bounds.Width >= bounds.Height;
                crosswalk.Name = item.Name;
                crosswalk.Direction = horizontal ? TrafficDirections.Horizontal : TrafficDirections.Vertical;
                crosswalk.Width = horizontal ? bounds.Height : bounds.Width;
                crosswalk.PointA = horizontal
                    ? new MapPathPointData(bounds.Left, bounds.Center.Y)
                    : new MapPathPointData(bounds.Center.X, bounds.Top);
                crosswalk.PointB = horizontal
                    ? new MapPathPointData(bounds.Right, bounds.Center.Y)
                    : new MapPathPointData(bounds.Center.X, bounds.Bottom);
                FillCrosswalkForm();
            },
            refreshLists: true);

        var select = ActionButton("在画布选中该物件", SelectCurrentOnCanvas);

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Section("横道几何", Fields(
                    Row("ID", _crosswalkIdText),
                    Row("名称", _crosswalkName),
                    Row("方向", _crosswalkDir),
                    Row("宽度(px)", _crosswalkWidth),
                    PairRow("A X", _crosswalkAX, "A Y", _crosswalkAY),
                    PairRow("B X", _crosswalkBX, "B Y", _crosswalkBY),
                    Hint("A → B 是行人行进方向；载具停点取中心回退半个宽度，行人停点取较早到达的端点"))),
                Section("绑定", Fields(
                    Row("绑定灯", _crosswalkLight),
                    Hint("一条横道只挂一盏灯；换绑会解除旧灯与被占用灯的旧关系"))),
                ActionRow(pickGeometry, select),
            },
        };
        _crosswalkFormBody = body;
        _crosswalkFormHint = EmptyHint("未选中横道",
            "点左侧列表的一条横道，或点上方「＋ 横道」新建；新建后会自动选中");
        return FormShell(body, _crosswalkFormHint);
    }

    private void FillCrosswalkForm()
    {
        _sync = true;
        var crosswalk = _crosswalk;
        var available = crosswalk is not null;
        ToggleFormBody(available, _crosswalkFormBody, _crosswalkFormHint);
        _crosswalkName.IsEnabled = available;
        _crosswalkDir.IsEnabled = available;
        _crosswalkWidth.IsEnabled = available;
        _crosswalkAX.IsEnabled = available;
        _crosswalkAY.IsEnabled = available;
        _crosswalkBX.IsEnabled = available;
        _crosswalkBY.IsEnabled = available;
        _crosswalkLight.IsEnabled = available;

        if (!available)
        {
            _crosswalkIdText.Text = "—";
            _crosswalkName.Text = string.Empty;
            _crosswalkWidth.Text = string.Empty;
            _crosswalkAX.Text = "0";
            _crosswalkAY.Text = "0";
            _crosswalkBX.Text = "0";
            _crosswalkBY.Text = "0";
            _crosswalkDir.SelectedIndex = -1;
            FillLightCombo(string.Empty);
            _sync = false;
            return;
        }

        _crosswalkIdText.Text = crosswalk!.Id;
        _crosswalkName.Text = crosswalk.Name;
        _crosswalkWidth.Text = Format(crosswalk.Width);
        _crosswalkAX.Text = Format(crosswalk.PointA.X);
        _crosswalkAY.Text = Format(crosswalk.PointA.Y);
        _crosswalkBX.Text = Format(crosswalk.PointB.X);
        _crosswalkBY.Text = Format(crosswalk.PointB.Y);
        SelectComboByText(_crosswalkDir, crosswalk.Direction == TrafficDirections.Vertical
            ? "vertical（纵向）"
            : "horizontal（横向）");
        FillLightCombo(crosswalk.BoundLightId);
        _sync = false;
    }

    private void FillLightCombo(string boundId)
    {
        if (_crosswalkLight.IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(() => FillLightCombo(boundId));
            return;
        }

        _crosswalkLight.Items.Clear();
        _crosswalkLight.Items.Add(new ComboBoxItem { Content = "（不绑定）" });
        var selected = 0;
        if (_config is not null)
        {
            for (var i = 0; i < _config.TrafficLights.Count; i++)
            {
                var light = _config.TrafficLights[i];
                var name = string.IsNullOrWhiteSpace(light.Name) ? light.Id : light.Name;
                _crosswalkLight.Items.Add(new ComboBoxItem { Content = name });
                if (light.Id == boundId)
                {
                    selected = i + 1;
                }
            }
        }

        _crosswalkLight.SelectedIndex = selected;
    }

    private void CommitCrosswalk(bool deferFill = false)
    {
        if (_sync || _crosswalk is null)
        {
            return;
        }

        _crosswalk.Name = _crosswalkName.Text?.Trim() ?? string.Empty;
        _crosswalk.Direction = ReadCombo(_crosswalkDir, TrafficDirections.Horizontal).StartsWith("vertical")
            ? TrafficDirections.Vertical
            : TrafficDirections.Horizontal;
        _crosswalk.Width = Math.Max(1, ParseDouble(_crosswalkWidth.Text, _crosswalk.Width));
        _crosswalk.PointA = new MapPathPointData(
            ParseDouble(_crosswalkAX.Text, _crosswalk.PointA.X),
            ParseDouble(_crosswalkAY.Text, _crosswalk.PointA.Y));
        _crosswalk.PointB = new MapPathPointData(
            ParseDouble(_crosswalkBX.Text, _crosswalk.PointB.X),
            ParseDouble(_crosswalkBY.Text, _crosswalk.PointB.Y));

        if (deferFill)
        {
            ApplyAfterCombo();
            return;
        }

        FillCrosswalkForm();
        ApplyChange();
    }

    #endregion

    #region 角色表单

    private Control BuildActorForm()
    {
        _actorNameText = ReadOnlyField();
        _actorRole = Combo(TrafficRoles.DisplayChoices.ToArray());
        _actorSpeed = Field();
        _actorMode = Combo("循环", "往返", "单程");
        _actorLoop = new CheckBox { Content = "循环播放", FontSize = UiMetrics.FontBody };
        _actorStopX = Field();
        _actorStopY = Field();
        _actorYellowRed = new CheckBox { Content = "黄灯按红灯", FontSize = UiMetrics.FontBody };
        ToolTip.SetTip(_actorYellowRed, "只对载具生效：黄灯也停，而不是抢灯");
        _actorPathText = Hint(string.Empty);
        _actorLightsPanel = new StackPanel { Spacing = 1 };

        HookCommit(CommitActor, _actorSpeed, _actorStopX, _actorStopY);
        _actorRole.SelectionChanged += (_, _) => CommitActor();
        _actorMode.SelectionChanged += (_, _) => CommitActor();
        _actorLoop.IsCheckedChanged += (_, _) => CommitActor();
        _actorYellowRed.IsCheckedChanged += (_, _) => CommitActor();

        var clearStop = ActorResetButton(
            "清除停止线",
            "先在列表里选中一个角色",
            actor =>
            {
                actor.StopLineX = 0;
                actor.StopLineY = 0;
            },
            "停止线清零，改为按绑定灯的横道 / 灯位推算停点");

        var clearLights = ActorResetButton(
            "清空绑定灯",
            "先在列表里选中一个角色",
            actor => actor.BoundLightIds.Clear());

        var select = ActionButton("选中角色", SelectCurrentOnCanvas);

        var speedHint = Hint("预览速度 = 200 px/s（载具）或 80 px/s（行人）× 速度倍率");

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Section("角色", Fields(
                    Row("物件", _actorNameText),
                    Row("角色", _actorRole),
                    Row("速度倍率", _actorSpeed),
                    speedHint,
                    Row("行走模式", _actorMode),
                    _actorLoop,
                    _actorPathText)),
                Section("等待规则", Fields(
                    Labeled("停止线", PairRow("X", _actorStopX, "Y", _actorStopY)),
                    Hint("填 0 表示不用停止线，直接按绑定灯的横道 / 灯位推算停点"),
                    _actorYellowRed)),
                Section("绑定红绿灯", Fields(
                    _actorLightsPanel,
                    Hint("勾选这辆车 / 行人需要等待的灯；多选时按最近的一盏判相位"))),
                ActionRow(clearLights, clearStop, select),
            },
        };
        _actorFormBody = body;
        _actorFormHint = EmptyHint("未选中角色",
            "点左侧列表里带路径的车 / 行人；先在场景编辑里给角色画好路径");
        return FormShell(body, _actorFormHint);
    }

    private void FillActorForm()
    {
        _sync = true;
        var item = _actor;
        var available = item is not null;
        ToggleFormBody(available, _actorFormBody, _actorFormHint);
        _actorRole.IsEnabled = available;
        _actorSpeed.IsEnabled = available;
        _actorMode.IsEnabled = available;
        _actorLoop.IsEnabled = available;
        _actorStopX.IsEnabled = available;
        _actorStopY.IsEnabled = available;
        _actorYellowRed.IsEnabled = available;

        if (!available)
        {
            _actorNameText.Text = "—";
            _actorSpeed.Text = string.Empty;
            _actorStopX.Text = "0";
            _actorStopY.Text = "0";
            _actorRole.SelectedIndex = -1;
            _actorMode.SelectedIndex = -1;
            _actorLoop.IsChecked = false;
            _actorYellowRed.IsChecked = false;
            _actorPathText.Text = "没有可编辑的角色";
            _actorLightsPanel.Children.Clear();
            _sync = false;
            return;
        }

        _actorNameText.Text = $"{item!.Name}（{item.Id}）";
        SelectComboByText(_actorRole, TrafficRoles.ToDisplay(item.TrafficRole));
        _actorSpeed.Text = Format(item.Speed);
        var mode = PathMath.ParseWalkMode(item.WalkMode);
        SelectComboByText(_actorMode, mode switch
        {
            PathPlaybackMode.PingPong => "往返",
            PathPlaybackMode.Once => "单程",
            _ => "循环",
        });
        _actorLoop.IsChecked = item.Loop;
        _actorStopX.Text = Format(item.StopLineX);
        _actorStopY.Text = Format(item.StopLineY);
        _actorYellowRed.IsChecked = item.YellowTreatAsRed;
        _actorPathText.Text = $"路径 {item.PathPoints.Count} 点；停点来源：停止线 > 绑定灯横道 > 灯位";

        _actorLightsPanel.Children.Clear();
        if (_config is not null)
        {
            if (_config.TrafficLights.Count == 0)
            {
                _actorLightsPanel.Children.Add(Hint("（地图还没有红绿灯）"));
            }

            foreach (var light in _config.TrafficLights)
            {
                var id = light.Id;
                var box = new CheckBox
                {
                    Content = $"{light.Name} ({light.GridX},{light.GridY})",
                    IsChecked = item.BoundLightIds.Contains(id),
                    FontSize = UiMetrics.FontBody,
                    Margin = new Thickness(0, 0, 0, 1),
                };
                box.IsCheckedChanged += (_, _) =>
                {
                    if (_sync || _actor is null)
                    {
                        return;
                    }

                    if (box.IsChecked == true)
                    {
                        if (!_actor.BoundLightIds.Contains(id))
                        {
                            _actor.BoundLightIds.Add(id);
                        }
                    }
                    else
                    {
                        _actor.BoundLightIds.Remove(id);
                    }

                    ApplyChange();
                };
                _actorLightsPanel.Children.Add(box);
            }
        }

        _sync = false;
    }

    private void CommitActor()
    {
        if (_sync || _actor is null)
        {
            return;
        }

        var role = TrafficRoles.ToCode(ReadCombo(_actorRole, TrafficRoles.ToDisplay(_actor.TrafficRole)));
        _actor.TrafficRole = role;
        _actor.IsVehicle = role == TrafficRoles.Vehicle;
        _actor.Speed = Math.Max(0.01, ParseDouble(_actorSpeed.Text, _actor.Speed));
        _actor.WalkMode = ReadCombo(_actorMode, "循环") switch
        {
            "往返" => "pingpong",
            "单程" => "once",
            _ => "loop",
        };
        _actor.Loop = _actorLoop.IsChecked == true;
        _actor.StopLineX = Math.Max(0, ParseDouble(_actorStopX.Text, _actor.StopLineX));
        _actor.StopLineY = Math.Max(0, ParseDouble(_actorStopY.Text, _actor.StopLineY));
        _actor.YellowTreatAsRed = _actorYellowRed.IsChecked == true;

        FillActorForm();
        ApplyChange();
    }

    #endregion
}


