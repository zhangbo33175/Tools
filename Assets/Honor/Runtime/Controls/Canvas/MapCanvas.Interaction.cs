/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Interaction.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布交互分区：缩放、平移、拖动、右键与键盘操作。
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
    #region 交互

    /// <summary>写一条路径诊断日志；只在 MAP_PATH_DEBUG=1 时落盘，避免正常使用产生 IO。</summary>
    /// <param name="message">诊断内容。</param>
    private static void LogPathDebug(string message)
    {
        if (!PathDebugEnabled)
        {
            return;
        }

        EditorLogs.Append(
            "path-edit.log",
            $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
    }

    /// <summary>
    /// 给当前选中物件追加一个路径点。角色、建筑、物件、载具都走同一入口，
    /// 保证吸附、去重、重绘、文档广播只做一次。Ctrl+左键仍保留为兼容快捷键。
    /// </summary>
    /// <param name="world">鼠标世界坐标。</param>
    private void AddSelectedPathPoint(Point world)
    {
        if (_selected is not { } target)
        {
            return;
        }

        var point = SnapPathPoint(world);
        var cell = CellIndexOf(point);
        if (!IsCellInsideMap(cell))
        {
            // 画布外的深色留白也能收到点击；路径点必须落在地图格内，否则会写出 y > 4096 的非法点
            LogPathDebug($"skip outside point name={target.Name} world={point.X:0},{point.Y:0} cell={cell.X:0},{cell.Y:0}");
            PathPointFeedback?.Invoke(this, $"路径点超出地图范围（格 {cell.X:0},{cell.Y:0}），已忽略。");
            return;
        }

        if (target.PathPoints.Count > 0)
        {
            var last = target.PathPoints[^1];
            if (Math.Abs(last.X - point.X) < 0.001 && Math.Abs(last.Y - point.Y) < 0.001)
            {
                PathPointFeedback?.Invoke(this, "与上一个路径点重合，已忽略；请点其他地面位置。");
                return;
            }
        }

        target.PathPoints.Add(new MapPathPointData(point.X, point.Y));
        LogPathDebug($"add point name={target.Name} world={point.X:0},{point.Y:0} total={target.PathPoints.Count}");
        PathPointFeedback?.Invoke(
            this,
            $"已添加路径点 P{target.PathPoints.Count - 1}（{point.X:0},{point.Y:0}，格 {cell.X:0},{cell.Y:0}），当前 {target.PathPoints.Count} 个。");
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ctrl+右键：删掉当前选中物件最后一个路径点；空路径时无动作。</summary>
    private void RemoveSelectedPathPoint()
    {
        if (_selected is not { } target)
        {
            return;
        }

        if (target.PathPoints.Count == 0)
        {
            PathPointFeedback?.Invoke(this, "当前物件没有可撤销的路径点。");
            return;
        }

        target.PathPoints.RemoveAt(target.PathPoints.Count - 1);
        PathPointFeedback?.Invoke(
            this,
            $"已撤销最后一个路径点，当前剩余 {target.PathPoints.Count} 个。");
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_config is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        var before = ScreenToWorld(position);
        var factor = e.Delta.Y > 0 ? ZoomStep : 1.0 / ZoomStep;
        var target = Math.Clamp(_zoom * factor, MapToolDefaults.MinZoom, MapToolDefaults.MaxZoom);

        if (Math.Abs(target - _zoom) < 1e-9)
        {
            e.Handled = true;
            return;
        }

        _zoom = target;
        _offset = new Vector(position.X - before.X * _zoom, position.Y - before.Y * _zoom);
        LogPathDebug($"wheel delta={e.Delta.Y:0.##} zoom={_zoom:0.###} pos={position.X:0},{position.Y:0}");
        RebuildInverse();
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (_config is null)
        {
            return;
        }

        Focus();
        var position = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsMiddleButtonPressed || (_spaceDown && properties.IsLeftButtonPressed))
        {
            BeginPan(e, position);
            return;
        }

        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
        {
            return;
        }

        var world = ScreenToWorld(position);

        LogPathDebug($"press left={properties.IsLeftButtonPressed} right={properties.IsRightButtonPressed} ctrl={e.KeyModifiers.HasFlag(KeyModifiers.Control)} path={PathDrawing} selected={_selected?.Type.ToString() ?? "null"}/{_selected?.Name ?? "-"} zoom={_zoom:0.###} pos={position.X:0},{position.Y:0} world={world.X:0},{world.Y:0}");

        // 路径编辑优先。路径绘制期间左键只有一个语义：落点加点。
        // 以前「点到树 / 建筑 / 人物先走选中」会让路径模式把点击交给选中 / 拖动，
        // 点一下别的物件就换目标，甚至把拖拽改坐标，这是路径绘制被误触的根因。
        // 现在非地面物件一律吞掉点击并提示，不选中、不拖动、不换目标；
        // 想选别的物件，先停止绘制路径或按 Esc 退出。
        var pathEditing = PathDrawing && _selected is not null;

        if (pathEditing && properties.IsRightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            RemoveSelectedPathPoint();
            e.Handled = true;
            return;
        }

        if (pathEditing && properties.IsLeftButtonPressed)
        {
            // 路径加点使用 PathPoint 命中口径：普通选中仍按包围盒，这里额外过滤透明像素。
            var pathHit = HitTest(world, ObjectHitKind.PathPoint);
            var pathDecision = ObjectHitRules.EvaluatePathPoint(pathHit);
            if (pathDecision.Accepted)
            {
                AddSelectedPathPoint(world);
            }
            else
            {
                PathPointRejected?.Invoke(this, pathDecision.BuildRejectionMessage());
            }

            LogPathDebug($"path point accepted={pathDecision.Accepted} blocker={pathDecision.BlockingObject?.Name ?? "-"} world={world.X:0},{world.Y:0}");
            e.Handled = true;
            return;
        }

        // 右键拖动平移画布（迷雾绘制状态下右键为擦除，见上面的分支）
        var rightIsErase = FogDrawing;
        if (properties.IsRightButtonPressed && !rightIsErase)
        {
            BeginPan(e, position);
            return;
        }

        // 预览模式只保留缩放 / 平移：左键不选中、不摆放、不画框，避免误改地图数据
        if (PreviewMode)
        {
            e.Handled = true;
            return;
        }

        // 迷雾笔刷优先：左键绘制，右键擦除
        if (FogDrawing && (properties.IsLeftButtonPressed || properties.IsRightButtonPressed))
        {
            BeginFogStroke(world, properties.IsRightButtonPressed);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 交通标记（人行道 / 道路 / 横道 / 红绿灯）不在画布上落点，必须先选中已有物件再由右栏按钮打标记
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        // 收集物摆放优先于普通选中，且不要求全局「摆放模式」：用户已经在左侧选中了某一个收集物素材，
        // 这次左键就只可能是“把它放下”，与普通资源模板需要先开摆放模式的约束解耦。
        if (PendingCollectibleTemplate is { } collectibleTemplate)
        {
            PlaceObject(collectibleTemplate, SnapToCell(world), true, PendingCollectibleLayer);
            e.Handled = true;
            return;
        }

        // 非路径模式下按正常选中 / 拖拽逻辑处理鼠标位置。
        var hit = HitTest(world);
        if (hit is not null)
        {
            SetSelectedCell(null);
            Select(hit);
            _dragged = hit;
            _dragGrab = new Vector(world.X - hit.X, world.Y - hit.Y);
            Cursor = GrabCursor;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 空白处点击：摆放模式下落位；否则选中该格子并高亮成绿色，同时保留框选能力
        if (PlacementEnabled && PendingTemplate is { } template)
        {
            PlaceObject(template, SnapToCell(world));
            e.Handled = true;
            return;
        }

        var pressedCell = CellIndexOf(world);
        SetSelectedCell(IsCellInsideMap(pressedCell) ? pressedCell : null);
        Select(null);
        _isMarquee = true;
        _marqueeAnchor = position;
        _marqueeRect = NormalizeRect(position, position);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>进入画布平移状态：记录锚点、切换抓取光标并捕获指针。</summary>
    /// <param name="e">触发平移的指针事件。</param>
    /// <param name="position">按下位置（画布坐标）。</param>
    // ---TODO 重构：中键 / 空格左键 / 右键平移共用同一入口。
    private void BeginPan(PointerEventArgs e, Point position)
    {
        _isPanning = true;
        _panAnchor = position;
        Cursor = GrabCursor;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_isPanning)
        {
            var delta = position - _panAnchor;
            _offset += new Vector(delta.X, delta.Y);
            _panAnchor = position;
            RebuildInverse();

            // 平移不改缩放：状态栏那几行（缩放 / 物件数 / 网格 / 存档路径）都不会变，
            // 这里不再逐帧触发 ViewChanged → 状态栏 TextBlock 重排（实测这是拖动发涩的大头）
            RequestPanRepaint();
            return;
        }

        if (_dragged is not null)
        {
            var dragWorld = ScreenToWorld(position);
            var target = SnapToCell(new Point(dragWorld.X - _dragGrab.X, dragWorld.Y - _dragGrab.Y));
            _dragged.X = target.X;
            _dragged.Y = target.Y;
            InvalidateVisual();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_isMarquee)
        {
            _marqueeRect = NormalizeRect(_marqueeAnchor, position);
            InvalidateVisual();
            return;
        }

        var world = ScreenToWorld(position);

        // 迷雾笔刷：记录悬停位置用于画出笔刷范围，拖动时连续涂抹
        if (FogDrawing)
        {
            _fogHover = world;
            Cursor = CrossCursor;
            InvalidateVisual();
        }

        if (_fogPainting)
        {
            PaintFogAt(world);
            PointerWorldChanged?.Invoke(this, world);
            return;
        }

        if (_config is not null)
        {
            PointerWorldChanged?.Invoke(this, world);
        }

        if (PendingTemplate is not null && PlacementEnabled)
        {
            Cursor = CrossCursor;
        }
        else if (PathDrawing)
        {
            Cursor = CrossCursor;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Cursor = FogDrawing ? CrossCursor : ArrowCursor;
            e.Pointer.Capture(null);
            // 平移中省掉了标签与占格，松手补一次完整重绘
            InvalidateVisual();
            return;
        }

        if (_dragged is not null)
        {
            _dragged = null;
            Cursor = ArrowCursor;
            e.Pointer.Capture(null);
            return;
        }

        if (_isMarquee)
        {
            ApplyMarqueeSelection();
            _isMarquee = false;
            _marqueeRect = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_fogPainting)
        {
            EndFogStroke();
            e.Pointer.Capture(null);
            Cursor = ArrowCursor;
            return;
        }

        base.OnPointerReleased(e);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        Cursor = ArrowCursor;
        if (_fogHover is not null)
        {
            _fogHover = null;
            InvalidateVisual();
        }

        base.OnPointerExited(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _spaceDown = true;
                Cursor = PanCursor;
                break;

            case Key.Delete when !PreviewMode && _selected is not null && _config is not null:
                _config.Objects.Remove(_selected);
                Select(null);
                InvalidateVisual();
                DocumentChanged?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Escape:
                PendingTemplate = null;
                PendingCollectibleTemplate = null;
                PathDrawing = false;
                SetSelectedCell(null);
                Select(null);
                Cursor = ArrowCursor;
                break;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _spaceDown = false;
            Cursor = ArrowCursor;
        }

        base.OnKeyUp(e);
    }

    #endregion
}
