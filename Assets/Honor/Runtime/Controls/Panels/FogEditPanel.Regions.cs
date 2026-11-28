/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FogEditPanel.Regions.cs
 * author:    云毅
 * created:   2026
 * descrip:   迷雾区域列表：新增、清空、刷新、行构建、撤销与重命名
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

public sealed partial class FogEditPanel : UserControl
{
    #region 区域操作

    private void AddRegion()
    {
        if (_fog is null)
        {
            return;
        }

        var region = new FogRegionData
        {
            Name = $"{DefaultRegionNamePrefix}{_fog.Regions.Count + 1}",
            Opacity = _canvas.FogOpacity,
            BrushSize = _canvas.FogBrushSize,
        };

        _fog.Regions.Add(region);
        _selectedRegion = region;
        _canvas.ActiveFogRegion = region;
        RefreshRegionList();
        _notify($"已添加 {region.Name}，在画布上涂抹即可填充");
    }

    private void ClearRegions()
    {
        if (_fog is null || _fog.Regions.Count == 0)
        {
            _notify("当前地图没有迷雾区域");
            return;
        }

        _fog.Regions.Clear();
        _selectedRegion = null;
        _canvas.ActiveFogRegion = null;
        AfterFogChange();
        _notify("已清空迷雾");
    }

    private void RefreshRegionList()
    {
        _regionHost.Children.Clear();

        if (_fog is null || _fog.Regions.Count == 0)
        {
            _regionTitle.Text = "迷雾区域列表（0 个区域）";
            _regionHost.Children.Add(_regionHint);
            return;
        }

        _regionTitle.Text = $"迷雾区域列表（{_fog.Regions.Count} 个区域 · 共 {_fog.Regions.Sum(item => item.CellCount)} 格）";

        foreach (var region in new List<FogRegionData>(_fog.Regions))
        {
            _regionHost.Children.Add(BuildRegionRow(region));
        }
    }

    private Control BuildRegionRow(FogRegionData region)
    {
        var name = new TextBlock
        {
            Text = region.Name,
            Foreground = UiTheme.TextPrimary,
            FontWeight = FontWeight.SemiBold,
        };

        var desc = new TextBlock
        {
            Text = $"{region.CellCount} 格 · 不透明度 {region.Opacity:0.00} · 笔刷 {region.BrushSize:0}px",
            Foreground = UiTheme.TextSecondary,
            FontSize = RegionDescFontSize,
        };

        var text = new StackPanel
        {
            Spacing = RegionTextSpacing,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { name, desc },
        };

        var delete = new Button
        {
            Content = RegionDeleteText,
            Padding = RegionDeletePadding,
            Background = UiTheme.Error,
            Foreground = UiTheme.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        delete.Click += (_, _) => DeleteRegion(region);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"*,{RegionDeleteColumnWidth}"),
        };
        grid.Children.Add(text);
        Grid.SetColumn(delete, 1);
        grid.Children.Add(delete);

        // 两行文字（名称 + 统计）需要固定行高，避免副标题压到边框
        var row = new ListRowCard(RegionRowHeight, ReferenceEquals(region, _selectedRegion))
        {
            Child = grid,
        };

        row.DoubleTapped += (_, _) => BeginRename(region, text);
        row.PointerPressed += (_, _) => SelectRegion(region);

        return row;
    }

    /// <summary>删除一个迷雾区域；若删的是当前区域，同时清空画布的活动区域。</summary>
    /// <param name="region">目标区域。</param>
    private void DeleteRegion(FogRegionData region)
    {
        _fog?.Regions.Remove(region);
        if (ReferenceEquals(_selectedRegion, region))
        {
            _selectedRegion = null;
            _canvas.ActiveFogRegion = null;
        }

        AfterFogChange();
        _notify($"已删除 {region.Name}");
    }

    /// <summary>选中区域并把其笔刷大小、不透明度回填到左侧控件。</summary>
    /// <param name="region">目标区域。</param>
    private void SelectRegion(FogRegionData region)
    {
        _selectedRegion = region;
        _canvas.ActiveFogRegion = region;
        _loading = true;
        _canvas.FogBrushSize = region.BrushSize;
        _canvas.FogOpacity = region.Opacity;
        _opacityValue.Text = region.Opacity.ToString("0.00");
        _loading = false;
        RefreshRegionList();
    }

    /// <summary>
    /// 迷雾数据变更后的统一收尾：刷新区域列表、按需重绘画布、广播变更。
    /// </summary>
    /// <param name="repaintCanvas">是否重绘内嵌画布与场景画布；仅改名时不需要。</param>
    private void AfterFogChange(bool repaintCanvas = true)
    {
        RefreshRegionList();
        if (repaintCanvas)
        {
            _canvas.Refresh();
            _sceneCanvas?.Refresh();
        }

        FogChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>撤销最后一笔迷雾涂抹（面板按钮与 Ctrl+Z 共用）。</summary>
    public void UndoLastStroke()
    {
        if (!_canvas.UndoFogStroke())
        {
            _notify("没有可撤销的迷雾笔迹");
            return;
        }

        RefreshRegionList();
        _notify("已撤销最后一笔迷雾");
    }

    /// <summary>双击区域行进入改名状态：原地换成输入框，回车或失焦提交。</summary>
    private void BeginRename(FogRegionData region, StackPanel host)
    {
        var editor = new TextBox
        {
            Text = region.Name,
            FontSize = RenameEditorFontSize,
            Padding = RenameEditorPadding,
        };

        void Commit()
        {
            var value = editor.Text?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                region.Name = value;
            }

            AfterFogChange(repaintCanvas: false);
        }

        UiFactory.CommitOnEnter(editor, Commit);
        editor.LostFocus += (_, _) => Commit();
        host.Children.Clear();
        host.Children.Add(editor);
        editor.Focus();
        editor.SelectAll();
    }

    #endregion
}
