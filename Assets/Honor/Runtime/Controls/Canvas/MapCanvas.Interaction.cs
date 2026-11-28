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

    /// <summary>路径点命中半径（屏幕像素）；按缩放折算成世界半径，缩到整图视角也点得中。</summary>
    private const double PathPointPickRadius = 7.0;

    /// <summary>当前选中的路径点序号；-1 表示没有选中。</summary>
    public int SelectedPathIndex => _selectedPathPoint;

    /// <summary>
    /// 选中 / 取消选中某个路径点。序号会被夹进当前物件的点数范围，越界与 -1 都按「取消选中」处理，
    /// 避免列表删点后残留一个指向空位的序号。
    /// </summary>
    /// <param name="index">路径点序号；传 -1 取消选中。</param>
    public void SelectPathPoint(int index)
    {
        var count = _selected?.PathPoints.Count ?? 0;
        var normalized = index >= 0 && index < count ? index : -1;
        if (_selectedPathPoint == normalized)
        {
            return;
        }

        _selectedPathPoint = normalized;
        InvalidateVisual();
        PathPointSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把某个路径点移到指定世界坐标。吸附、越界拒绝、重绘与文档广播都走这一处，
    /// 画布拖动与列表里的数值编辑共用，不会出现两套校验口径。
    /// </summary>
    /// <param name="index">路径点序号。</param>
    /// <param name="world">目标世界坐标。</param>
    /// <returns>坐标真的变了才返回 true。</returns>
    public bool MovePathPoint(int index, Point world)
    {
        if (_selected is not { } target || index < 0 || index >= target.PathPoints.Count)
        {
            return false;
        }

        var point = SnapPathPoint(world);
        var cell = CellIndexOf(point);
        if (!IsCellInsideMap(cell))
        {
            PathPointFeedback?.Invoke(this, $"路径点超出地图范围（格 {cell.X:0},{cell.Y:0}），已忽略。");
            return false;
        }

        var current = target.PathPoints[index];
        if (Math.Abs(current.X - point.X) < 0.001 && Math.Abs(current.Y - point.Y) < 0.001)
        {
            return false;
        }

        current.X = point.X;
        current.Y = point.Y;
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>把一个路径点挪到新的序号（列表里「上移 / 下移」用），选中态跟着点走。</summary>
    /// <param name="from">原序号。</param>
    /// <param name="to">目标序号。</param>
    /// <returns>真的换了位置才返回 true。</returns>
    public bool ReorderPathPoint(int from, int to)
    {
        if (_selected is not { } target)
        {
            return false;
        }

        var count = target.PathPoints.Count;
        if (from < 0 || from >= count || to < 0 || to >= count || from == to)
        {
            return false;
        }

        var moved = target.PathPoints[from];
        target.PathPoints.RemoveAt(from);
        target.PathPoints.Insert(to, moved);
        _selectedPathPoint = to;
        PathPointFeedback?.Invoke(this, $"已把路径点 P{from} 移到 P{to}。");
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        PathPointSelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>删除指定序号的路径点；选中态顺延到同序号的点（删的是最后一个就清空选中）。</summary>
    /// <param name="index">路径点序号。</param>
    /// <returns>真的删掉了才返回 true。</returns>
    public bool RemovePathPointAt(int index)
    {
        if (_selected is not { } target || index < 0 || index >= target.PathPoints.Count)
        {
            return false;
        }

        target.PathPoints.RemoveAt(index);
        _selectedPathPoint = index < target.PathPoints.Count ? index : -1;
        PathPointFeedback?.Invoke(this, $"已删除路径点 P{index}，当前剩余 {target.PathPoints.Count} 个。");
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        PathPointSelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 在指定位置插入一个路径点。用于「在已画好的路径中间补点」，
    /// 与末尾追加共用同一套吸附 / 去重 / 越界校验。
    /// </summary>
    /// <param name="index">插入后的序号。</param>
    /// <param name="world">世界坐标。</param>
    /// <returns>插入成功返回 true。</returns>
    public bool InsertPathPointAt(int index, Point world)
    {
        if (_selected is not { } target)
        {
            return false;
        }

        var point = SnapPathPoint(world);
        var cell = CellIndexOf(point);
        if (!IsCellInsideMap(cell))
        {
            PathPointFeedback?.Invoke(this, $"路径点超出地图范围（格 {cell.X:0},{cell.Y:0}），已忽略。");
            return false;
        }

        var insertAt = Math.Clamp(index, 0, target.PathPoints.Count);
        target.PathPoints.Insert(insertAt, new MapPathPointData(point.X, point.Y));
        _selectedPathPoint = insertAt;
        PathPointFeedback?.Invoke(
            this,
            $"已在 P{insertAt} 插入路径点（{point.X:0},{point.Y:0}），当前 {target.PathPoints.Count} 个。");
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        PathPointSelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>找出当前物件上离鼠标最近的路径点序号；没命中返回 -1。</summary>
    /// <param name="world">鼠标世界坐标。</param>
    private int FindSelectedPathPoint(Point world)
    {
        if (_selected is not { } target || target.PathPoints.Count == 0)
        {
            return -1;
        }

        var radius = PathPointPickRadius / Math.Max(_zoom, MinRenderableZoom);
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < target.PathPoints.Count; index++)
        {
            var point = target.PathPoints[index];
            var distance = VectorLength(point.X - world.X, point.Y - world.Y);
            if (distance <= radius && distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>找出当前物件路径上离鼠标最近的线段，给出插入序号与投影点；没命中返回 null。</summary>
    /// <param name="world">鼠标世界坐标。</param>
    private (int Index, Point World)? FindSelectedPathSegment(Point world)
    {
        if (_selected is not { } target || target.PathPoints.Count < 2)
        {
            return null;
        }

        var radius = PathPointPickRadius / Math.Max(_zoom, MinRenderableZoom);
        (int Index, Point World)? best = null;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < target.PathPoints.Count - 1; index++)
        {
            var a = target.PathPoints[index];
            var b = target.PathPoints[index + 1];
            var projected = ProjectOnSegment(new Point(a.X, a.Y), new Point(b.X, b.Y), world);
            var distance = VectorLength(projected.X - world.X, projected.Y - world.Y);
            if (distance <= radius && distance < bestDistance)
            {
                best = (index + 1, projected);
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>点到线段的垂足；退化线段（两端重合）直接回退到起点。</summary>
    private static Point ProjectOnSegment(Point a, Point b, Point target)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-9)
        {
            return a;
        }

        var t = Math.Clamp(((target.X - a.X) * dx + (target.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return new Point(a.X + dx * t, a.Y + dy * t);
    }

    /// <summary>向量长度；路径点命中只做距离比较，不需要 <see cref="Vector"/> 的完整语义。</summary>
    private static double VectorLength(double x, double y) => Math.Sqrt(x * x + y * y);

    /// <summary>
    /// 路径模式下按下左键：若落在已有点的命中半径内就进入拖动，否则返回 false 让调用方去加点。
    /// </summary>
    /// <param name="e">指针按下事件，用于捕获指针。</param>
    private bool TryBeginPathPointDrag(PointerPressedEventArgs e, Point world)
    {
        var index = FindSelectedPathPoint(world);
        if (index < 0)
        {
            return false;
        }

        SelectPathPoint(index);
        _pathPointDrag = index;
        _pathPointDragFrom = new Point(_selected!.PathPoints[index].X, _selected.PathPoints[index].Y);
        Cursor = GrabCursor;
        e.Pointer.Capture(this);
        e.Handled = true;
        return true;
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

        // 退出绘制后路径点仍然画在画布上（DrawEditingPath 不受绘制开关约束），
        // 所以「抓点改坐标 / Alt 线上插点」不能只活在绘制状态里，否则列表能改、画布干瞪眼。
        if (!pathEditing
            && properties.IsLeftButtonPressed
            && _selected is { } shownPathOwner
            && shownPathOwner.PathPoints.Count > 0)
        {
            if (TryBeginPathPointDrag(e, world))
            {
                LogPathDebug($"begin path point drag (idle) index={_pathPointDrag} world={world.X:0},{world.Y:0}");
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && FindSelectedPathSegment(world) is { } idleSegment)
            {
                InsertPathPointAt(idleSegment.Index, idleSegment.World);
                LogPathDebug($"insert path point (idle) index={idleSegment.Index} world={idleSegment.World.X:0},{idleSegment.World.Y:0}");
                e.Handled = true;
                return;
            }
        }

        if (pathEditing && properties.IsRightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            RemoveSelectedPathPoint();
            e.Handled = true;
            return;
        }

        if (pathEditing && properties.IsLeftButtonPressed)
        {
            // 先看有没有按在已画好的路径点上：命中即进入「拖动改点」，这是从「只能追加」升级成可修改的关键一步。
            if (TryBeginPathPointDrag(e, world))
            {
                LogPathDebug($"begin path point drag index={_pathPointDrag} world={world.X:0},{world.Y:0}");
                return;
            }

            // Alt+左键：把点插到最近的一段路径上（中间补点），不改变末尾追加的默认手感。
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                if (FindSelectedPathSegment(world) is { } segment)
                {
                    InsertPathPointAt(segment.Index, segment.World);
                    LogPathDebug($"insert path point index={segment.Index} world={segment.World.X:0},{segment.World.Y:0}");
                }
                else
                {
                    PathPointFeedback?.Invoke(this, "Alt+左键需要在已画好的路径线上点击，才会插入中间点。");
                }

                e.Handled = true;
                return;
            }

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

        // 右键拖动平移画布（迷雾绘制 / 瓦片编辑状态下右键为擦除，见上面的分支）
        var rightIsErase = FogDrawing || (TileEditEnabled && !PreviewMode);
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

        // 瓦片编辑（Tile Palette 的 Edit* 态）优先于物件选中与迷雾：
        // 编辑态开着时左键就是落笔、右键就是橡皮，否则用户每点一下都会把底下的树选中，刷不动地面。
        if (TileEditEnabled && (properties.IsLeftButtonPressed || properties.IsRightButtonPressed))
        {
            BeginTileStroke(world, properties.IsRightButtonPressed);
            e.Pointer.Capture(this);
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
            _dragOrigin = new Point(hit.X, hit.Y);
            _dragGrab = new Vector(world.X - hit.X, world.Y - hit.Y);
            Cursor = GrabCursor;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 空白处点击：摆放模式下落位；否则选中该格子并高亮成绿色，同时保留框选能力
        if (PlacementEnabled && PendingTemplate is { } template)
        {
            // 斜边吸附：菱形下把墙脚贴到共边中点，并按那条边的朝向落位
            if (EdgeSnapEnabled && CanEdgeSnap)
            {
                var edgeTarget = SnapToEdge(world, template.DefaultWidth, template.DefaultHeight, out var rotation);
                PlaceObject(template, edgeTarget, false, null, rotation);
            }
            else
            {
                PlaceObject(template, SnapToCell(world));
            }

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
            var grabPoint = new Point(dragWorld.X - _dragGrab.X, dragWorld.Y - _dragGrab.Y);
            var target = SnapToCell(grabPoint);
            if (EdgeSnapEnabled && CanEdgeSnap)
            {
                // 拖动同样贴斜边：以当前物件尺寸求脚底中点，墙不会在拖动时回落到格心
                var edgeTarget = SnapToEdge(grabPoint, _dragged.Width, _dragged.Height, out var dragRotation);
                target = edgeTarget;
                _dragged.Rotation = dragRotation;
            }

            _dragged.X = target.X;
            _dragged.Y = target.Y;
            InvalidateVisual();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // 路径点拖动：只重绘不广播文档，松手时才写一次「已移动」，避免状态栏每帧刷一遍。
        if (_pathPointDrag >= 0 && _selected is { } pathOwner)
        {
            var snapped = SnapPathPoint(ScreenToWorld(position));
            if (IsCellInsideMap(CellIndexOf(snapped)) && _pathPointDrag < pathOwner.PathPoints.Count)
            {
                var point = pathOwner.PathPoints[_pathPointDrag];
                point.X = snapped.X;
                point.Y = snapped.Y;
                InvalidateVisual();
            }

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

        // 瓦片拖笔：笔刷 / 橡皮按住时按格连画，填充与取色不参与拖动
        if (_tilePainting)
        {
            ContinueTileStroke(world);
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
            var moved = _dragged;
            var movedTo = new Point(moved.X, moved.Y);
            var movedFrom = _dragOrigin;
            _dragged = null;
            Cursor = ArrowCursor;
            e.Pointer.Capture(null);

            // 原地点击（没挪动）不算一次编辑，不进撤销栈；终点坐标由画布刷新与文档广播一并处理
            if (Math.Abs(movedTo.X - movedFrom.X) > 1e-6 || Math.Abs(movedTo.Y - movedFrom.Y) > 1e-6)
            {
                ObjectMoveCompleted?.Invoke(this, new ObjectMoveEventArgs(moved, movedFrom, movedTo));
            }

            return;
        }

        if (_pathPointDrag >= 0)
        {
            var index = _pathPointDrag;
            _pathPointDrag = -1;
            Cursor = PathDrawing ? CrossCursor : ArrowCursor;
            e.Pointer.Capture(null);

            if (_selected is { } owner && index < owner.PathPoints.Count)
            {
                var current = owner.PathPoints[index];
                var moved = Math.Abs(current.X - _pathPointDragFrom.X) > 1e-6 ||
                            Math.Abs(current.Y - _pathPointDragFrom.Y) > 1e-6;
                if (moved)
                {
                    PathPointFeedback?.Invoke(
                        this,
                        $"已移动 P{index} 到（{current.X:0},{current.Y:0}，格 {current.GridX},{current.GridY}）。");
                    DocumentChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            InvalidateVisual();
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

        if (_tilePainting)
        {
            EndTileStroke();
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
                _pathPointDrag = -1;
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

