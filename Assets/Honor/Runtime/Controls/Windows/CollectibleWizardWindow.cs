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
/// 窗口壳与结果属性位于本文件；窗口骨架 / 步骤导航位于 Shell partial，
/// 数量页与素材页分别位于 Quantity / Material partial。
/// 第一步对应参考图一：地图卡片 + 目标数量 + 层级；第二步对应参考图二：
/// 已选槽位条 + 可收集素材网格，最多 12 种。结果只回传给主窗口，不直接改地图。
/// </summary>
public sealed partial class CollectibleWizardWindow : Window
{
    #region 常量与画刷

    private const int MaxKinds = 12;

    #endregion

    #region 字段

    private readonly MapConfigData _config;
    private readonly IReadOnlyList<ObjectTemplate> _candidates;
    private readonly AssetLibrary _assets;
    private readonly List<ObjectTemplate> _selected = new();

    private readonly NumberStepper _totalBox;
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
        // ---TODO 视觉改版：淡青灰渐变底替代整块纯色背景。
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(UiTheme.WizardWindowTop, 0),
                new GradientStop(UiTheme.WizardWindowBottom, 1),
            },
        };
        CanResize = true;

        // ---TODO 视觉统一：收集规模改用主题化 NumberStepper，替代原生 NumericUpDown 的黑三角箭头。
        _totalBox = new NumberStepper
        {
            Minimum = 1m,
            Maximum = 9999m,
            Step = 1m,
            Value = plan.Total > 0 ? plan.Total : 50m,
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
        WindowChromeTheme.Attach(this);
    }

    #endregion

    #region 结果

    /// <summary>目标收集物数量。</summary>
    public int TotalCount => (int)Math.Max(1m, _totalBox.Value);

    /// <summary>层级代码：under / over。</summary>
    public string LayerMode => _layerCombo.SelectedIndex == 0
        ? CollectibleLayers.UnderObjects
        : CollectibleLayers.OverObjects;

    /// <summary>已选素材。</summary>
    public IReadOnlyList<ObjectTemplate> SelectedTemplates => _selected;

    #endregion

}

#endregion
