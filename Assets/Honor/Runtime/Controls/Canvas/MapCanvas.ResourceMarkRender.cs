/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.ResourceMarkRender.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布资源标记：把「配置表里查不到的资源」就地框出来
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MapEditorTool.Models;
using MapEditorTool.Services;

namespace MapEditorTool.Controls;

/// <summary>画布资源标记：配置表未登记的资源实例就地画红框与「未登记」标签。</summary>
public sealed partial class MapCanvas : Control
{
    #region 资源标记

    private ItemConfigSet? _itemTable;

    /// <summary>配置表集合。设置后画布会在每个实例上标出「未登记」的资源；null 时不做标记。</summary>
    public ItemConfigSet? ItemTable
    {
        get => _itemTable;
        set
        {
            if (_itemTable == value)
            {
                return;
            }

            _itemTable = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 在画布上框出未登记配置表的实例：砖红描边 + 左上角「未登记」标签。
    /// 只做提示，不改实例字段，也不写表；平移时跳过（和标签 / 占格同一口径，避免拖慢拖拽）。
    /// </summary>
    /// <param name="context">绘制上下文（世界坐标）。</param>
    /// <param name="config">地图配置。</param>
    private void DrawUnregisteredMarks(DrawingContext context, MapConfigData config)
    {
        if (_itemTable is null || _isPanning || PreviewMode || _zoom < 0.12)
        {
            return;
        }

        var cell = (double)config.GridSize;
        var stroke = new Pen(UiTheme.MapView.ResourceMissingStroke, 2.0 * Hairline);
        var marks = new List<(Rect Bounds, string Key)>(8);

        foreach (var item in config.Objects)
        {
            var key = MapTemplateBinder.UnregisteredKey(item.TemplateId, item.Name, item.ImagePath, _itemTable);
            if (key.Length > 0)
            {
                marks.Add((PreviewRenderBounds(item), key));
            }
        }

        var objectIds = config.Objects
            .Select(instance => instance.Id)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var light in config.TrafficLights)
        {
            // 灯已经是地图上的普通物件时由物件那条标记负责，避免同一盏灯画两圈
            if (light.Id.Length > 0 && objectIds.Contains(light.Id))
            {
                continue;
            }

            var key = MapTemplateBinder.UnregisteredKey(
                light.TemplateId,
                light.Name,
                light.Image,
                _itemTable,
                allowFallback: false);
            if (key.Length > 0)
            {
                marks.Add((new Rect(light.PosX - cell * 0.5, light.PosY - cell * 1.2, cell, cell * 1.6), key));
            }
        }

        foreach (var (bounds, key) in marks)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            context.FillRectangle(UiTheme.MapView.ResourceMissingFill, bounds);
            context.DrawRectangle(null, stroke, bounds);
            DrawMissingLabel(context, bounds, key);
        }
    }

    /// <summary>在包围盒左上角画「未登记 · 资源名」标签。</summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="key">资源美术名。</param>
    private void DrawMissingLabel(DrawingContext context, Rect bounds, string key)
    {
        if (_zoom < 0.3)
        {
            return;
        }

        var text = _zoom >= 0.6 ? $"未登记 · {key}" : "未登记";
        var formatted = CanvasText.Create(text, 10.0 * Hairline, UiTheme.MapView.ResourceMissingLabel);
        var textWidth = formatted.Width / Hairline;
        var textHeight = formatted.Height / Hairline;
        var plate = new Rect(
            bounds.X - 1.5 * Hairline,
            bounds.Y - textHeight - 3.0 * Hairline,
            textWidth + 5.0 * Hairline,
            textHeight + 2.0 * Hairline);

        context.FillRectangle(UiTheme.MapView.ResourceMissingPlate, plate);
        context.DrawText(formatted, new Point(plate.X + 2.0 * Hairline, plate.Y + 1.0 * Hairline));
    }

    #endregion
}
