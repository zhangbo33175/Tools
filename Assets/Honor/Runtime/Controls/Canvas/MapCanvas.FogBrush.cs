/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.FogBrush.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布迷雾笔刷分区：笔刷采样、落笔、擦除与区域提交。
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

public sealed partial class MapCanvas : Control
{
    #region 迷雾笔刷

    /// <summary>开始一笔迷雾：创建区域（绘制）或进入擦除状态。</summary>
    private void BeginFogStroke(Point world, bool erase)
    {
        _fogPainting = true;
        _fogErasing = erase;
        _fogStroke = null;
        _fogRecord = null;

        if (!erase && Fog is not null)
        {
            if (ActiveFogRegion is { } active)
            {
                _fogStroke = active;
                active.Opacity = FogOpacity;
                active.BrushSize = FogBrushSize;
            }
            else
            {
                var region = new FogRegionData
                {
                    Name = $"迷雾区域 {Fog.Regions.Count + 1}",
                    Opacity = FogOpacity,
                    BrushSize = FogBrushSize,
                };
                Fog.Regions.Add(region);
                _fogStroke = region;
                ActiveFogRegion = region;
            }
        }

        // 登记这一笔的变更，供 Ctrl+Z 撤销
        _fogRecord = new FogStrokeRecord { Erase = erase, Region = _fogStroke };
        _fogHistory.Add(_fogRecord);
        if (_fogHistory.Count > 50)
        {
            _fogHistory.RemoveAt(0);
        }

        PaintFogAt(world);
    }

    /// <summary>在指定世界坐标涂抹一笔：按格子中心落在半径内判定。</summary>
    private void PaintFogAt(Point world)
    {
        var fog = Fog;
        var config = _config;
        if (fog is null || config is null)
        {
            return;
        }

        var cell = (double)config.GridSize;
        var radius = Math.Max(FogBrushSize, cell) / 2.0;
        int startX = (int)Math.Floor((world.X - radius) / cell);
        int endX = (int)Math.Floor((world.X + radius) / cell);
        int startY = (int)Math.Floor((world.Y - radius) / cell);
        int endY = (int)Math.Floor((world.Y + radius) / cell);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                var centerX = x * cell + cell / 2.0;
                var centerY = y * cell + cell / 2.0;
                var dx = centerX - world.X;
                var dy = centerY - world.Y;
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                if (_fogErasing)
                {
                    RemoveFogCell(fog, x, y, _fogRecord);
                }
                else if (_fogStroke is not null && !HasFogCell(_fogStroke, x, y))
                {
                    var cellData = new FogCellData { X = x, Y = y, Density = FogOpacity };
                    _fogStroke.Cells.Add(cellData);
                    _fogRecord?.Added.Add(cellData);
                }
            }
        }

        InvalidateVisual();
        FogChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool HasFogCell(FogRegionData region, int x, int y)
    {
        foreach (var item in region.Cells)
        {
            if (item.X == x && item.Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveFogCell(MapFogDocument fog, int x, int y, FogStrokeRecord? record)
    {
        foreach (var region in fog.Regions)
        {
            for (var i = region.Cells.Count - 1; i >= 0; i--)
            {
                var cell = region.Cells[i];
                if (cell.X != x || cell.Y != y)
                {
                    continue;
                }

                record?.Removed.Add((region, cell));
                region.Cells.RemoveAt(i);
            }
        }

        fog.Regions.RemoveAll(region => region.Cells.Count == 0);
    }

    /// <summary>结束一笔迷雾：丢弃空区域。</summary>
    private void EndFogStroke()
    {
        if (_fogStroke is { } stroke && Fog is not null)
        {
            stroke.Cells.RemoveAll(cell => cell.Density <= 0);
            if (stroke.Cells.Count == 0)
            {
                Fog.Regions.Remove(stroke);
            }
        }

        if (_fogRecord is { } record && record.Added.Count == 0 && record.Removed.Count == 0)
        {
            _fogHistory.Remove(record);
        }

        _fogPainting = false;
        _fogErasing = false;
        _fogStroke = null;
        _fogRecord = null;
        InvalidateVisual();
        FogChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>撤销最后一笔迷雾涂抹或擦除。</summary>
    /// <returns>是否发生了撤销。</returns>
    public bool UndoFogStroke()
    {
        var fog = Fog;
        if (fog is null || _fogHistory.Count == 0)
        {
            return false;
        }

        var record = _fogHistory[^1];
        _fogHistory.RemoveAt(_fogHistory.Count - 1);

        if (record.Erase)
        {
            foreach (var (region, cell) in record.Removed)
            {
                if (!fog.Regions.Contains(region))
                {
                    fog.Regions.Add(region);
                }

                if (!HasFogCell(region, cell.X, cell.Y))
                {
                    region.Cells.Add(cell);
                }
            }
        }
        else if (record.Region is { } target)
        {
            foreach (var cell in record.Added)
            {
                target.Cells.RemoveAll(item => item.X == cell.X && item.Y == cell.Y);
            }

            if (target.Cells.Count == 0)
            {
                fog.Regions.Remove(target);
            }
        }

        InvalidateVisual();
        FogChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>清空撤销历史（换图或重载迷雾后调用）。</summary>
    public void ClearFogHistory()
    {
        _fogHistory.Clear();
        _fogRecord = null;
    }

    /// <summary>一笔迷雾的变更记录：新增格子或按区域记录的被擦除格子。</summary>
    private sealed class FogStrokeRecord
    {
        /// <summary>是否为擦除笔。</summary>
        public bool Erase { get; init; }

        /// <summary>绘制笔的目标区域（擦除笔为空）。</summary>
        public FogRegionData? Region { get; init; }

        /// <summary>本笔新增的格子。</summary>
        public List<FogCellData> Added { get; } = new();

        /// <summary>本笔擦除的格子及其所属区域。</summary>
        public List<(FogRegionData Region, FogCellData Cell)> Removed { get; } = new();
    }

    #endregion
}
