/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapJsonService.GridTraffic.cs
 * author:    云毅
 * created:   2026
 * descrip:   地图网格与交通载荷组装：占据格、区域、格子、红绿灯与横道
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public static partial class MapJsonService
{
    #region 网格与交通组装

    private static List<CellPayload> BuildOccupiedCells(MapConfigData config)
    {
        var cells = new List<CellPayload>();
        var seen = new HashSet<(int, int)>();

        if (config.DrawShape == DrawShapes.Diamond)
        {
            var cell = config.GridSize;
            foreach (var item in config.Objects)
            {
                var bounds = item.Bounds;
                var topLeft = DiamondLattice.IndexAt(bounds.Left, bounds.Top, cell);
                var bottomRight = DiamondLattice.IndexAt(bounds.Right, bounds.Bottom, cell);
                var minCol = (int)Math.Min(topLeft.X, bottomRight.X) - 1;
                var maxCol = (int)Math.Max(topLeft.X, bottomRight.X) + 1;
                var minRow = (int)Math.Min(topLeft.Y, bottomRight.Y) - 1;
                var maxRow = (int)Math.Max(topLeft.Y, bottomRight.Y) + 1;

                for (var col = minCol; col <= maxCol; col++)
                {
                    for (var row = minRow; row <= maxRow; row++)
                    {
                        var center = DiamondLattice.Center(col, row, cell);
                        if (!bounds.Contains(center) || !seen.Add((col, row)))
                        {
                            continue;
                        }

                        cells.Add(new CellPayload { X = col, Y = row });
                    }
                }
            }

            return cells;
        }

        foreach (var item in config.Objects)
        {
            int startX = item.CellX;
            int startY = item.CellY;
            int endX = startX + Math.Max(1, item.ExportWidth);
            int endY = startY + Math.Max(1, item.ExportHeight);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (seen.Add((x, y)))
                    {
                        cells.Add(new CellPayload { X = x, Y = y });
                    }
                }
            }
        }

        return cells;
    }

    private static List<RegionPayload> BuildRegions(MapConfigData config)
    {
        var regions = new List<RegionPayload>();
        foreach (var region in config.Regions)
        {
            regions.Add(new RegionPayload
            {
                Id = region.Id,
                Type = region.Type,
                X = region.X,
                Y = region.Y,
                Width = region.Width,
                Height = region.Height,
            });
        }

        return regions;
    }

    private static List<CellPayload> BuildCells(IEnumerable<MapGridCellData> cells)
    {
        var result = new List<CellPayload>();
        foreach (var cell in cells)
        {
            result.Add(new CellPayload { X = cell.X, Y = cell.Y });
        }

        return result;
    }

    /// <summary>红绿灯序列化。</summary>
    private static List<TrafficLightPayload> BuildTrafficLights(MapConfigData config)
    {
        var result = new List<TrafficLightPayload>();
        foreach (var light in config.TrafficLights)
        {
            result.Add(new TrafficLightPayload
            {
                Id = light.Id,
                TemplateId = light.TemplateId,
                Name = light.Name,
                PosX = light.PosX,
                PosY = light.PosY,
                GridX = light.GridX,
                GridY = light.GridY,
                Direction = light.Direction,
                Side = light.Side,
                IntersectionId = light.IntersectionId,
                RedDuration = light.RedDuration,
                GreenDuration = light.GreenDuration,
                YellowDuration = light.YellowDuration,
                OffsetPhase = light.OffsetPhase,
                BoundCrosswalkId = light.BoundCrosswalkId,
                InitialPhase = light.InitialPhase,
                Image = light.Image,
                Anim = light.Anim,
            });
        }

        return result;
    }

    /// <summary>斑马线序列化。</summary>
    private static List<CrosswalkPayload> BuildCrosswalks(MapConfigData config)
    {
        var result = new List<CrosswalkPayload>();
        foreach (var crosswalk in config.Crosswalks)
        {
            result.Add(new CrosswalkPayload
            {
                Id = crosswalk.Id,
                Name = crosswalk.Name,
                PointA = new PathPointPayload
                {
                    X = crosswalk.PointA.X,
                    Y = crosswalk.PointA.Y,
                    GridX = crosswalk.PointA.GridX,
                    GridY = crosswalk.PointA.GridY,
                },
                PointB = new PathPointPayload
                {
                    X = crosswalk.PointB.X,
                    Y = crosswalk.PointB.Y,
                    GridX = crosswalk.PointB.GridX,
                    GridY = crosswalk.PointB.GridY,
                },
                Width = crosswalk.Width,
                Direction = crosswalk.Direction,
                BoundLightId = crosswalk.BoundLightId,
            });
        }

        return result;
    }

    #endregion
}
