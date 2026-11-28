/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.TileBrush.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布瓦片分区：地表瓦片绘制、笔刷 / 填充 / 擦除 / 取色与撤销
 ***************************************************************/
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;

namespace MapEditorTool.Controls;

/// <summary>瓦片编辑工具：与 Unity Tile Palette 的笔刷 / 油漆桶 / 橡皮 / 吸管一一对应。</summary>
public enum TilePaintTool
{
    /// <summary>笔刷：按格涂抹，按住拖动连画。</summary>
    Brush,

    /// <summary>填充：把连通的同图格一起换掉。</summary>
    Fill,

    /// <summary>擦除：按格删除。</summary>
    Erase,

    /// <summary>取色：点一格，把它的贴图取到调色板当前画笔。</summary>
    Pick,
}

public sealed partial class MapCanvas
{
    #region 瓦片字段与属性

    /// <summary>瓦片贴图缓存：key = 贴图路径，避免每帧走资源库解析。</summary>
    private readonly Dictionary<string, Bitmap?> _tileSpriteCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>是否正在拖笔。</summary>
    private bool _tilePainting;

    /// <summary>这一笔是否为擦除笔（右键按下时为 true，画笔工具也会临时变橡皮）。</summary>
    private bool _tileErasing;

    /// <summary>当前这一笔的撤销记录；空笔会被丢弃。</summary>
    private TileStrokeRecord? _tileRecord;

    /// <summary>瓦片撤销栈，只保留最近 50 笔。</summary>
    private readonly List<TileStrokeRecord> _tileHistory = new();

    /// <summary>瓦片编辑态：只有在「编辑」开关打开时画布左键才落到瓦片层。</summary>
    public bool TileEditEnabled { get; set; }

    /// <summary>当前瓦片工具。</summary>
    public TilePaintTool TileTool { get; set; } = TilePaintTool.Brush;

    /// <summary>当前画笔贴图（调色板选中的那一张）。</summary>
    public string TileBrushImage { get; set; } = string.Empty;

    /// <summary>瓦片层内容变化（绘制 / 擦除 / 按规则重算后触发，供主窗口标记未保存）。</summary>
    public event EventHandler? TileLayerChanged;

    /// <summary>取色结果：参数是命中的贴图路径，空串表示该格没有瓦片。</summary>
    public event EventHandler<string>? TilePicked;

    #endregion

    #region 绘制

    /// <summary>
    /// 画指定楼层的地板瓦片：一格一张图，铺满整格，画在本层所有物件之下。
    /// 瓦片走的是「地面」语义，因此不受图层显隐里的建筑 / 道具开关影响，只跟地图层和楼层共存。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="config">地图配置。</param>
    /// <param name="level">楼层号。</param>
    private void DrawTileCells(DrawingContext context, MapConfigData config, int level)
    {
        if (config.TileCells.Count == 0 || _assets is null)
        {
            return;
        }

        var cell = Math.Max(1, config.GridSize);

        // 视口裁剪：4096 地图铺满瓦片时逐格画几千次会拖慢拖动，先按可见范围掐掉画布外的格子
        var topLeft = ScreenToWorld(new Point(0, 0));
        var bottomRight = ScreenToWorld(new Point(Bounds.Width, Bounds.Height));
        var minX = (int)Math.Floor(topLeft.X / cell) - 2;
        var maxX = (int)Math.Ceiling(bottomRight.X / cell) + 2;
        var minY = (int)Math.Floor(topLeft.Y / cell) - 2;
        var maxY = (int)Math.Ceiling(bottomRight.Y / cell) + 2;

        foreach (var item in config.TileCells)
        {
            if (item.Level != level)
            {
                continue;
            }

            if (item.X < minX || item.X > maxX || item.Y < minY || item.Y > maxY)
            {
                continue;
            }

            var bitmap = TileSprite(item.Image);
            if (bitmap is null)
            {
                continue;
            }

            var rect = TileCellRect(item.X, item.Y, cell);
            context.DrawImage(bitmap, new Rect(bitmap.Size), rect);
        }
    }

    /// <summary>取瓦片贴图：按路径缓存，找不到返回 null（该格跳过绘制，不画占位色块）。</summary>
    /// <param name="image">贴图路径或资源名。</param>
    /// <returns>位图或 null。</returns>
    private Bitmap? TileSprite(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        if (_tileSpriteCache.TryGetValue(image, out var cached))
        {
            return cached;
        }

        var bitmap = _assets?.Load(image);
        _tileSpriteCache[image] = bitmap;
        return bitmap;
    }

    /// <summary>清空瓦片贴图缓存（资源根变化、导入新图后调用）。</summary>
    public void ClearTileSpriteCache() => _tileSpriteCache.Clear();

    /// <summary>
    /// 一格瓦片的绘制矩形。矩形网格贴格子左上角；
    /// 菱形 / 六边形让「一格大小的贴图」居中坐在格心上，与物件落位口径一致。
    /// </summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    /// <param name="cell">格子边长。</param>
    /// <returns>世界坐标矩形。</returns>
    private Rect TileCellRect(int x, int y, int cell)
    {
        if (IsLatticeMode)
        {
            var center = LatticeCenter(new Point(x, y), cell);
            return new Rect(center.X - cell / 2.0, center.Y - cell / 2.0, cell, cell);
        }

        return new Rect(x * cell, y * cell, cell, cell);
    }

    #endregion

    #region 涂抹

    /// <summary>落笔：按当前工具决定这一笔做什么。</summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="erase">是否为擦除笔（右键落笔）。</param>
    private void BeginTileStroke(Point world, bool erase)
    {
        var config = _config;
        if (config is null)
        {
            return;
        }

        var index = CellIndexOf(world);
        var x = (int)index.X;
        var y = (int)index.Y;
        if (!IsCellInsideMap(new Point(x, y)))
        {
            return;
        }

        if (TileTool == TilePaintTool.Pick && !erase)
        {
            TilePicked?.Invoke(this, config.TileAt(x, y, CurrentLevel) ?? string.Empty);
            return;
        }

        _tilePainting = true;
        _tileErasing = erase;
        _tileRecord = new TileStrokeRecord();

        if (TileTool == TilePaintTool.Fill && !erase)
        {
            FillTiles(x, y);
        }
        else
        {
            ApplyTileAt(x, y);
        }
    }

    /// <summary>拖动续笔：笔刷 / 橡皮按住不放时按格连续落笔；填充与取色只响应单击。</summary>
    /// <param name="world">世界坐标。</param>
    private void ContinueTileStroke(Point world)
    {
        if (!_tilePainting || TileTool is TilePaintTool.Fill or TilePaintTool.Pick)
        {
            return;
        }

        var index = CellIndexOf(world);
        ApplyTileAt((int)index.X, (int)index.Y);
    }

    /// <summary>抬笔：收笔入历史并结束这一笔。</summary>
    private void EndTileStroke()
    {
        _tilePainting = false;
        _tileErasing = false;
        CommitTileStroke();
    }

    /// <summary>收笔入栈：这一笔真的改到了格子才记进撤销历史。</summary>
    private void CommitTileStroke()
    {
        if (_tileRecord is { Changed.Count: > 0 } record)
        {
            _tileHistory.Add(record);
            if (_tileHistory.Count > 50)
            {
                _tileHistory.RemoveAt(0);
            }
        }

        _tileRecord = null;
    }

    /// <summary>对一格落笔：擦除删除、笔刷写入当前贴图。</summary>
    /// <param name="x">列号。</param>
    /// <param name="y">行号。</param>
    private void ApplyTileAt(int x, int y)
    {
        var config = _config;
        if (config is null || !IsCellInsideMap(new Point(x, y)))
        {
            return;
        }

        var level = CurrentLevel;
        var before = config.TileAt(x, y, level);

        if (TileTool == TilePaintTool.Erase || _tileErasing)
        {
            if (before is null)
            {
                return;
            }

            TileLayerOps.Remove(config.TileCells, x, y, level);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(TileBrushImage) || TilePaths.Same(before, TileBrushImage))
            {
                return;
            }

            TileLayerOps.Set(config.TileCells, x, y, level, TileBrushImage);
        }

        _tileRecord?.Record(level, x, y, before);
        InvalidateVisual();
        TileLayerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 连通填充：从落点向外扩，把「和起点同一张图」的连通区域换成当前画笔。
    /// 这样在已经铺好的地块里补丁时不会把别的材质一起吃掉。
    /// </summary>
    /// <param name="startX">起始列号。</param>
    /// <param name="startY">起始行号。</param>
    private void FillTiles(int startX, int startY)
    {
        var config = _config;
        if (config is null || string.IsNullOrWhiteSpace(TileBrushImage))
        {
            return;
        }

        var level = CurrentLevel;
        var source = config.TileAt(startX, startY, level) ?? string.Empty;
        if (TilePaths.Same(source, TileBrushImage))
        {
            return;
        }

        var queue = new Queue<(int X, int Y)>();
        var visited = new HashSet<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));

        // 安全阀：整图 4096 也才 16K 格，超过 64K 说明判定出错，直接停手避免卡死界面
        var guard = 0;
        while (queue.Count > 0 && guard++ < 65536)
        {
            var (x, y) = queue.Dequeue();
            if (!IsCellInsideMap(new Point(x, y)))
            {
                continue;
            }

            var current = config.TileAt(x, y, level) ?? string.Empty;
            if (!TilePaths.Same(current, source))
            {
                continue;
            }

            ApplyTileAt(x, y);

            // 连通判定同样按形状走：菱形必须只走 4 条共边，否则填充会顺着斜角漏到隔壁街区
            foreach (var slot in TileAdjacency.ForShape(config.DrawShape, y))
            {
                var nx = x + slot.Dx;
                var ny = y + slot.Dy;
                if (visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }


    #endregion

    #region 规则重算（Rule Tile 口径）

    /// <summary>
    /// 按邻接规则重选贴图：对每个已有瓦片格，把「8 邻是否有瓦片」交给规则表，
    /// 选中匹配且最具体的那条规则，把它的贴图写到该格。
    /// 与 Unity Rule Tile 一致：图案判定的是「邻居存在」，不区分邻居用的是哪张图。
    /// </summary>
    /// <returns>被改写的格数；规则为空时返回 -1。</returns>
    public int ApplyTileRules()
    {
        var config = _config;
        if (config is null || config.TileRules.Count == 0)
        {
            return -1;
        }

        var record = new TileStrokeRecord();
        var changed = 0;

        // 先把「哪一层哪一格有瓦片」快照成集合：重算过程中贴图在变，实时查询会把结果带偏。
        // 楼层参与键，邻居判定只在本层内进行——二层地板不该因为一层铺了草地而被认成「内部」。
        var occupied = new HashSet<(int Level, int X, int Y)>();
        foreach (var cell in config.TileCells)
        {
            occupied.Add((cell.Level, cell.X, cell.Y));
        }

        foreach (var cell in config.TileCells)
        {
            var x = cell.X;
            var y = cell.Y;
            var level = cell.Level;
            // 邻接口径按形状取：矩形 8 邻，菱形 4 条共边（行奇偶会改列偏移），六边形沿用 8 邻
            var slots = TileAdjacency.ForShape(config.DrawShape, y);
            var rule = TileRuleMatcher.Pick(
                config.TileRules,
                (dx, dy) => occupied.Contains((level, x + dx, y + dy)),
                slots);
            if (rule is null || string.IsNullOrWhiteSpace(rule.Image))
            {
                continue;
            }

            if (TilePaths.Same(cell.Image, rule.Image))
            {
                continue;
            }

            record.Record(level, x, y, cell.Image);
            cell.Image = rule.Image;
            changed++;
        }

        if (changed > 0)
        {
            _tileHistory.Add(record);
            if (_tileHistory.Count > 50)
            {
                _tileHistory.RemoveAt(0);
            }

            InvalidateVisual();
            TileLayerChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    #endregion

    #region 撤销

    /// <summary>是否还有可撤销的瓦片操作。</summary>
    public bool CanUndoTileStroke => _tileHistory.Count > 0;

    /// <summary>撤销最近一笔瓦片操作（涂抹 / 擦除 / 填充 / 规则重算）。</summary>
    /// <returns>是否发生了撤销。</returns>
    public bool UndoLastTileStroke()
    {
        var config = _config;
        if (config is null || _tileHistory.Count == 0)
        {
            return false;
        }

        var record = _tileHistory[^1];
        _tileHistory.RemoveAt(_tileHistory.Count - 1);
        foreach (var (level, x, y, before) in record.Changed)
        {
            if (string.IsNullOrEmpty(before))
            {
                TileLayerOps.Remove(config.TileCells, x, y, level);
            }
            else
            {
                TileLayerOps.Set(config.TileCells, x, y, level, before);
            }
        }

        InvalidateVisual();
        TileLayerChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>清空瓦片撤销历史（换图后调用）。</summary>
    public void ClearTileHistory() => _tileHistory.Clear();

    /// <summary>把当前瓦片层整体清空（面板「清空瓦片」用），可撤销。</summary>
    /// <returns>被清掉的格数。</returns>
    public int ClearTileCells()
    {
        var config = _config;
        if (config is null || config.TileCells.Count == 0)
        {
            return 0;
        }

        var record = new TileStrokeRecord();
        foreach (var cell in config.TileCells)
        {
            record.Record(cell.Level, cell.X, cell.Y, cell.Image);
        }

        var count = config.TileCells.Count;
        config.TileCells.Clear();
        _tileHistory.Add(record);
        if (_tileHistory.Count > 50)
        {
            _tileHistory.RemoveAt(0);
        }

        InvalidateVisual();
        TileLayerChanged?.Invoke(this, EventArgs.Empty);
        return count;
    }

    /// <summary>一笔瓦片操作的变更记录：格坐标 + 操作前的贴图（空串表示原本没有瓦片）。</summary>
    private sealed class TileStrokeRecord
    {
        /// <summary>本笔改动过的格：楼层 + 行列 + 操作前的贴图（空串表示原本没有瓦片）。</summary>
        public List<(int Level, int X, int Y, string Before)> Changed { get; } = new();

        /// <summary>登记一格变更；同一格只记最早的那份旧值，撤销才能还原到落笔前。</summary>
        /// <param name="level">楼层号。</param>
        /// <param name="x">列号。</param>
        /// <param name="y">行号。</param>
        /// <param name="before">操作前的贴图。</param>
        public void Record(int level, int x, int y, string? before)
        {
            foreach (var item in Changed)
            {
                if (item.Level == level && item.X == x && item.Y == y)
                {
                    return;
                }
            }

            Changed.Add((level, x, y, before ?? string.Empty));
        }
    }

    #endregion
}







