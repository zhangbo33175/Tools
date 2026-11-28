/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MainWindow.CollectibleEvents.cs
 * author:    云毅
 * created:   2026
 * descrip:   主窗口收集物事件：向导接线、计划同步、随机生成、清空与添加收集。
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
    private void WireCollectiblePanel()
    {
        BtnCollectibleReset.Click += async (_, _) => await AddCollectibleAsync();
        BtnCollectibleDock.Click += (_, _) =>
        {
            if (_collectiblePanelDockSide != CollectibleDockSide.None)
            {
                FloatCollectiblePanel();
                return;
            }

            var areaWidth = OverlayWidth();
            var center = Avalonia.Controls.Canvas.GetLeft(CollectiblePanelHost) + CollectiblePanelHost.Width / 2;
            DockCollectiblePanel(center <= areaWidth / 2
                ? CollectibleDockSide.Left
                : CollectibleDockSide.Right);
        };
        BtnCollectibleClose.Click += (_, _) =>
        {
            HideCollectiblePanel();
            SetMessage("收集物面板已关闭；收集计划和已摆放实例仍保留。");
        };

        CollectiblePanelDragArea.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(CollectiblePanelDragArea).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var point = e.GetPosition(CollectibleOverlay);
            _collectiblePanelDragging = true;
            _collectiblePanelDragMoved = false;
            _collectiblePanelDragOffset = new Point(
                point.X - Avalonia.Controls.Canvas.GetLeft(CollectiblePanelHost),
                point.Y - Avalonia.Controls.Canvas.GetTop(CollectiblePanelHost));
            e.Pointer.Capture(CollectiblePanelDragArea);
            e.Handled = true;
        };

        CollectiblePanelDragArea.PointerCaptureLost += (_, _) =>
        {
            _collectiblePanelDragging = false;
            _collectiblePanelDragMoved = false;
            CollectibleDockHint.IsVisible = false;
        };

        CollectiblePanelDragArea.PointerMoved += (_, e) =>
        {
            if (!_collectiblePanelDragging || !e.GetCurrentPoint(CollectiblePanelDragArea).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            var point = e.GetPosition(CollectibleOverlay);
            if (!_collectiblePanelDragMoved
                && Math.Abs(point.X - _collectiblePanelDragOffset.X - Avalonia.Controls.Canvas.GetLeft(CollectiblePanelHost)) < 4
                && Math.Abs(point.Y - _collectiblePanelDragOffset.Y - Avalonia.Controls.Canvas.GetTop(CollectiblePanelHost)) < 4)
            {
                return;
            }

            if (!_collectiblePanelDragMoved)
            {
                _collectiblePanelDragMoved = true;
                UndockCollectiblePanelForDrag();
            }

            var areaWidth = OverlayWidth();
            var areaHeight = OverlayHeight();
            var left = Math.Clamp(point.X - _collectiblePanelDragOffset.X, 8,
                Math.Max(8, areaWidth - CollectiblePanelHost.Width - 8));
            var top = Math.Clamp(point.Y - _collectiblePanelDragOffset.Y, 8,
                Math.Max(8, areaHeight - CollectiblePanelHost.Height - 8));
            Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, left);
            Avalonia.Controls.Canvas.SetTop(CollectiblePanelHost, top);

            var side = left <= CollectibleDockSnapThreshold
                ? CollectibleDockSide.Left
                : left + CollectiblePanelHost.Width >= areaWidth - CollectibleDockSnapThreshold
                    ? CollectibleDockSide.Right
                    : CollectibleDockSide.None;
            ShowCollectibleDockHint(side);
        };

        CollectiblePanelDragArea.PointerReleased += (_, e) =>
        {
            if (!_collectiblePanelDragging)
            {
                return;
            }

            e.Handled = true;
            var moved = _collectiblePanelDragMoved;
            _collectiblePanelDragging = false;
            _collectiblePanelDragMoved = false;
            e.Pointer.Capture(null);
            CollectibleDockHint.IsVisible = false;

            if (!moved)
            {
                return;
            }

            var areaWidth = OverlayWidth();
            var areaHeight = OverlayHeight();
            var left = Avalonia.Controls.Canvas.GetLeft(CollectiblePanelHost);
            var top = Avalonia.Controls.Canvas.GetTop(CollectiblePanelHost);
            if (left <= CollectibleDockSnapThreshold)
            {
                DockCollectiblePanel(CollectibleDockSide.Left);
            }
            else if (left + CollectiblePanelHost.Width >= areaWidth - CollectibleDockSnapThreshold)
            {
                DockCollectiblePanel(CollectibleDockSide.Right);
            }
            else
            {
                _collectiblePanelDockSide = CollectibleDockSide.None;
                MapCanvasHost.Margin = new Avalonia.Thickness(0);
                Avalonia.Controls.Canvas.SetLeft(CollectiblePanelHost, Math.Max(8, left));
                Avalonia.Controls.Canvas.SetTop(CollectiblePanelHost,
                    Math.Clamp(top, 8, Math.Max(8, areaHeight - CollectiblePanelHost.Height - 8)));
                UpdateCollectiblePanelChrome();
            }
        };

        CollectiblePanelResizeHandle.PointerPressed += (_, e) =>
        {
            if (_collectiblePanelDockSide == CollectibleDockSide.None
                || !e.GetCurrentPoint(CollectiblePanelResizeHandle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _collectiblePanelResizing = true;
            _collectiblePanelResizeStartX = e.GetPosition(CollectibleOverlay).X;
            _collectiblePanelResizeStartWidth = CollectiblePanelHost.Width;
            e.Pointer.Capture(CollectiblePanelResizeHandle);
            e.Handled = true;
        };

        CollectiblePanelResizeHandle.PointerCaptureLost += (_, _) => _collectiblePanelResizing = false;

        CollectiblePanelResizeHandle.PointerMoved += (_, e) =>
        {
            if (!_collectiblePanelResizing
                || !e.GetCurrentPoint(CollectiblePanelResizeHandle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            var delta = e.GetPosition(CollectibleOverlay).X - _collectiblePanelResizeStartX;
            var width = _collectiblePanelDockSide == CollectibleDockSide.Right
                ? _collectiblePanelResizeStartWidth - delta
                : _collectiblePanelResizeStartWidth + delta;
            var areaWidth = OverlayWidth();
            var maxWidth = Math.Max(
                CollectiblePanelMinDockWidth,
                Math.Min(CollectiblePanelMaxDockWidth, areaWidth - CollectiblePanelMinCanvasWidth));
            _collectiblePanelDockWidth = Math.Clamp(width, CollectiblePanelMinDockWidth, maxWidth);
            ApplyCollectibleDockLayout();
        };

        CollectiblePanelResizeHandle.PointerReleased += (_, e) =>
        {
            if (!_collectiblePanelResizing)
            {
                return;
            }

            _collectiblePanelResizing = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        };

        CollectibleOverlay.SizeChanged += (_, _) => ClampCollectiblePanel();
    }

    /// <summary>从地图实例反推面板素材（老存档 / 手工导入的 isCollectible 物件），并补齐目标数量。</summary>
    private void SyncCollectiblePlanFromMap()
    {
        var instances = _config.Objects.Where(item => item.IsCollectible).ToList();
        if (instances.Count == 0)
        {
            SyncCollectibleEntry();
            return;
        }

        if (_config.Collectibles.Kinds.Count == 0)
        {
            foreach (var group in instances.GroupBy(item => item.TemplateId, StringComparer.Ordinal))
            {
                var first = group.First();
                _config.Collectibles.Kinds.Add(new CollectibleKindData
                {
                    TemplateId = first.TemplateId,
                    Name = first.Name,
                    ImagePath = first.ImagePath,
                    Width = Math.Max(1, first.Width),
                    Height = Math.Max(1, first.Height),
                    LayerType = string.IsNullOrWhiteSpace(first.LayerType) ? MapLayerKeys.Prop : first.LayerType,
                    AnimName = first.AnimName,
                });
            }
        }

        if (_config.Collectibles.Total < instances.Count)
        {
            _config.Collectibles.Total = instances.Count;
        }

        if (!_config.Objects.Any(item => item.IsCollectible && item.CollectibleOverObjects))
        {
            _config.Collectibles.LayerMode = CollectibleLayers.UnderObjects;
        }

        SyncCollectibleEntry();
    }

    /// <summary>随机补齐剩余目标数量；空间不足时把实际生成数反馈到状态栏。</summary>
    private void GenerateCollectiblesRandom()
    {
        var plan = _config.Collectibles;
        var placed = _config.Objects.Count(item => item.IsCollectible);
        var remaining = Math.Max(0, plan.Total - placed);
        if (plan.Kinds.Count == 0)
        {
            SetMessage("还没有选择收集物素材，无法随机生成。");
            return;
        }

        if (remaining == 0)
        {
            SetMessage($"收集物已达到目标数量 {plan.Total}，没有剩余可生成。");
            return;
        }

        var templates = plan.Kinds.Select(kind => kind.ToTemplate()).ToList();
        var added = Canvas.PlaceCollectiblesRandom(templates, remaining, plan.LayerMode);
        RefreshCollectibleSummary();
        SetMessage(added == remaining
            ? $"已随机生成 {added} 个收集物；目标 {plan.Total} 已全部摆放。"
            : $"已随机生成 {added} 个收集物；地图空白格不足，还差 {remaining - added} 个，可手动拖放补齐。");
    }

    /// <summary>清空所有收集物实例，保留面板素材与目标数量，方便重新随机或手动摆。</summary>
    private void ClearCollectibles()
    {
        var removed = _config.Objects.RemoveAll(item => item.IsCollectible);
        if (removed == 0)
        {
            SetMessage("当前地图没有收集物可清空。");
            return;
        }

        Canvas.Select(null);
        Canvas.CommitEdit();
        BuildCollectiblePanelList();
        SetMessage($"已清空 {removed} 个收集物；面板素材与目标数量保留。");
    }

    /// <summary>「添加收集」：两步向导（数量 → 素材），完成后打开收集物浮窗。</summary>
    private async Task AddCollectibleAsync()
    {
        var candidates = BuildCollectibleCandidates();
        if (candidates.Count == 0)
        {
            SetMessageError("没有可用作收集物的物件表素材：请先在「物品元件编辑」里配置物件素材。");
            return;
        }

        var wizard = new CollectibleWizardWindow(_config, candidates, _config.Collectibles, _assets);
        bool confirmed;
        try
        {
            confirmed = await wizard.ShowDialog<bool>(this);
        }
        catch (Exception error)
        {
            SetMessageError(UiFeedback.Failed("打开添加收集向导", error));
            return;
        }

        if (!confirmed)
        {
            return;
        }

        _config.Collectibles.Total = wizard.TotalCount;
        _config.Collectibles.LayerMode = wizard.LayerMode;
        _config.Collectibles.Kinds = wizard.SelectedTemplates
            .Select(CollectibleKindData.FromTemplate)
            .ToList();

        _pendingTemplate = null;
        Canvas.PendingTemplate = null;
        Canvas.PendingCollectibleTemplate = null;
        // 新建计划时确保收集物层处于可见状态：即使用户之前关过该层，也能立刻看到摆放结果
        if (LayerCollectible.IsChecked != true)
        {
            LayerCollectible.IsChecked = true;
        }

        Canvas.HiddenLayers.Remove(MapLayerKeys.Collectible);
        SyncCollectibleEntry();
        ShowCollectiblePanel(CollectibleDockSide.None);
        SetMessage($"收集物计划已建立：目标 {_config.Collectibles.Total} 个、{_config.Collectibles.Kinds.Count} 种素材、{CollectibleLayers.DisplayName(_config.Collectibles.LayerMode)}。可在右侧收集物面板拖放或随机生成。");
    }

    /// <summary>
    /// 收集物候选池：优先读独立的收集物表（TableCollectibles），
    /// 表为空或还没建表时退回模板库里的收集物标记 / Prop，保持旧地图可继续添加。
    /// </summary>
    /// <returns>去重后的模板列表，且统一标记为收集物。</returns>
    private List<ObjectTemplate> BuildCollectibleCandidates()
    {
        var table = _itemTable?.FileForCategory(ResourceCategories.CollectibleCategory.DisplayName);
        List<ObjectTemplate> candidates;
        if (table is { Rows.Count: > 0 })
        {
            candidates = table.Rows
                .Select(row => BuildTablePaletteTemplate(row, MapObjectType.Prop))
                .ToList();
        }
        else
        {
            candidates = _templates.Where(template => template.IsCollectible).ToList();
            if (candidates.Count == 0)
            {
                candidates = _templates.Where(template => template.Type == MapObjectType.Prop).ToList();
            }
        }

        foreach (var candidate in candidates)
        {
            candidate.IsCollectible = true;
        }

        return candidates
            .Where(template => !string.IsNullOrWhiteSpace(template.TemplateId))
            .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

}

