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
            Name = $"迷雾区域 {_fog.Regions.Count + 1}",
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
        RefreshRegionList();
        _canvas.Refresh();
        _sceneCanvas?.Refresh();
        FogChanged?.Invoke(this, EventArgs.Empty);
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
            FontSize = 11,
        };

        var text = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { name, desc },
        };

        var delete = new Button
        {
            Content = "删除",
            Padding = new Thickness(6, 2),
            Background = UiTheme.Error,
            Foreground = UiTheme.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        delete.Click += (_, _) =>
        {
            _fog?.Regions.Remove(region);
            if (ReferenceEquals(_selectedRegion, region))
            {
                _selectedRegion = null;
                _canvas.ActiveFogRegion = null;
            }

            RefreshRegionList();
            _canvas.Refresh();
            _sceneCanvas?.Refresh();
            FogChanged?.Invoke(this, EventArgs.Empty);
            _notify($"已删除 {region.Name}");
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,60") };
        grid.Children.Add(text);
        Grid.SetColumn(delete, 1);
        grid.Children.Add(delete);

        // 两行文字（名称 + 统计）需要 40px，32px 会把副标题压到边框上
        var row = new ListRowCard(40, ReferenceEquals(region, _selectedRegion))
        {
            Child = grid,
        };

        row.DoubleTapped += (_, _) => BeginRename(region, text);

        row.PointerPressed += (_, _) =>
        {
            _selectedRegion = region;
            _canvas.ActiveFogRegion = region;
            _loading = true;
            _canvas.FogBrushSize = region.BrushSize;
            _canvas.FogOpacity = region.Opacity;
            _opacityValue.Text = region.Opacity.ToString("0.00");
            _loading = false;
            RefreshRegionList();
        };

        return row;
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
            FontSize = 12,
            Padding = new Thickness(4, 2),
        };

        void Commit()
        {
            var value = editor.Text?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                region.Name = value;
            }

            RefreshRegionList();
            FogChanged?.Invoke(this, EventArgs.Empty);
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };

        editor.LostFocus += (_, _) => Commit();
        host.Children.Clear();
        host.Children.Add(editor);
        editor.Focus();
        editor.SelectAll();
    }

    #endregion
}
