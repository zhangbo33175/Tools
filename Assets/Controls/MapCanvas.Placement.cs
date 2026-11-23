/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.Placement.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布摆放与拾取分区：点击落位、命中检测、路径加点与选中。
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
    #region 摆放与拾取

    /// <summary>按模板在指定世界坐标放置一个新实例。</summary>
    /// <param name="template">模板。</param>
    /// <param name="worldTopLeft">世界坐标左上角，已对齐格子。</param>
    /// <param name="isCollectible">true = 按收集物落位，写入 <see cref="PlacedObject.IsCollectible"/>。</param>
    /// <param name="collectibleLayer">收集物层级 under / over，非收集物忽略。</param>
    public void PlaceObject(ObjectTemplate template, Point worldTopLeft, bool isCollectible = false, string? collectibleLayer = null)
    {
        if (_config is null)
        {
            return;
        }

        var instance = CreateInstance(template, worldTopLeft, isCollectible, collectibleLayer);
        _config.Objects.Add(instance);
        Select(instance);
        InvalidateVisual();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>创建一个待加入地图的实例（不写集合、不选中，供单点摆放与批量随机生成共用）。</summary>
    /// <param name="template">模板。</param>
    /// <param name="worldTopLeft">世界坐标左上角。</param>
    /// <param name="isCollectible">是否收集物。</param>
    /// <param name="collectibleLayer">收集物层级。</param>
    /// <returns>新实例。</returns>
    private PlacedObject CreateInstance(
        ObjectTemplate template,
        Point worldTopLeft,
        bool isCollectible,
        string? collectibleLayer)
    {
        var config = _config;
        var layerKey = LayerOrder.Normalize(template.LayerKey);
        var slot = config is null
            ? 0
            : config.Objects
                .Where(item => LayerOrder.EffectiveLayer(item) == layerKey)
                .Select(LayerOrder.SlotOf)
                .DefaultIfEmpty(-1)
                .Max() + 1;
        var baseSort = LayerOrder.Build(layerKey, Math.Clamp(slot, 0, LayerOrder.SlotCount - 1));
        var collectible = isCollectible || template.IsCollectible;

        return new PlacedObject
        {
            TemplateId = template.TemplateId,
            Name = template.TemplateName,
            Type = template.Type,
            X = worldTopLeft.X,
            Y = worldTopLeft.Y,
            Width = template.DefaultWidth,
            Height = template.DefaultHeight,
            LayerType = template.LayerKey,
            DisplayLayer = template.DisplayLayer,
            ImagePath = template.ImagePath,
            AnimName = template.AnimName,
            Loop = template.Loop,
            Speed = template.Speed,
            Interactable = template.Interactable,
            InteractType = template.InteractType,
            DialogBubble = template.DialogBubble,
            BubbleId = template.BubbleId,
            SortOrder = baseSort,
            YSortEnabled = template.YSortEnabled,
            YSortOffset = template.YSortOffset,
            CollisionEnabled = template.CollisionEnabled,
            FacingDirection = template.FacingDirection,
            ShadowEnabled = template.ShadowEnabled,
            IsCollectible = collectible,
            CollectibleOverObjects = CollectibleLayers.IsOver(collectibleLayer),
            RespawnTime = template.RespawnTime,
            IsMovable = template.IsMovable,
            MoveAnimName = template.MoveAnimName,
            IsTeleport = template.IsTeleport,
            TeleportTargetScene = template.TeleportTargetScene,
            TeleportTargetX = template.TeleportTargetX,
            TeleportTargetY = template.TeleportTargetY,
        };
    }

    /// <summary>在画布坐标处按格子对齐摆放一个收集物；用于收集物列表拖拽落点。</summary>
    /// <param name="template">素材模板。</param>
    /// <param name="canvasPoint">画布坐标系内的点。</param>
    /// <param name="layerMode">物件下 / 物件上。</param>
    /// <returns>落位后的实例；点在画布外或无模板时返回 null。</returns>
    public PlacedObject? PlaceCollectibleAt(ObjectTemplate template, Point canvasPoint, string layerMode)
    {
        // 显式拖拽落点不受全局「摆放模式」限制：用户已经把收集物素材拖到画布上，动作本身就是摆放意图。
        if (_config is null)
        {
            return null;
        }

        var world = SnapToCell(ScreenToWorld(canvasPoint));
        PlaceObject(template, world, true, layerMode);
        return _selected;
    }

    /// <summary>
    /// 随机生成收集物：先按当前所有实例的包围盒标记占用格，再在空格里均匀取样；
    /// 批量只发一次 <see cref="DocumentChanged"/>，避免 50 个物件触发 50 次属性面板重建。
    /// </summary>
    /// <param name="templates">可用素材。</param>
    /// <param name="total">目标数量。</param>
    /// <param name="layerMode">物件下 / 物件上。</param>
    /// <returns>实际生成数量。</returns>
    public int PlaceCollectiblesRandom(IReadOnlyList<ObjectTemplate> templates, int total, string layerMode)
    {
        var config = _config;
        if (config is null || templates.Count == 0 || total <= 0)
        {
            return 0;
        }

        var grid = Math.Max(1, config.GridSize);
        var cols = Math.Max(1, config.Width / grid);
        var rows = Math.Max(1, config.Height / grid);
        var diamond = config.DrawShape == DrawShapes.Diamond;
        var occupied = new HashSet<(int Col, int Row)>();
        foreach (var item in config.Objects)
        {
            foreach (var cell in OccupiedCells(item, grid, diamond))
            {
                occupied.Add(cell);
            }
        }

        var random = new Random();
        var added = 0;
        for (var i = 0; i < total; i++)
        {
            var template = templates[random.Next(templates.Count)];
            var width = Math.Max(1, Math.Min(template.DefaultWidth, cols - 2));
            var height = Math.Max(1, Math.Min(template.DefaultHeight, rows - 2));
            var placed = false;

            for (var attempt = 0; attempt < 600 && !placed; attempt++)
            {
                var col = random.Next(1, Math.Max(2, cols - width));
                var row = random.Next(1, Math.Max(2, rows - height));
                var free = true;
                for (var dx = 0; dx < width && free; dx++)
                {
                    for (var dy = 0; dy < height; dy++)
                    {
                        if (occupied.Contains((col + dx, row + dy)))
                        {
                            free = false;
                            break;
                        }
                    }
                }

                if (!free)
                {
                    continue;
                }

                var point = new Point(col * grid, row * grid);
                config.Objects.Add(CreateInstance(template, point, true, layerMode));
                for (var dx = 0; dx < width; dx++)
                {
                    for (var dy = 0; dy < height; dy++)
                    {
                        occupied.Add((col + dx, row + dy));
                    }
                }

                added++;
                placed = true;
            }
        }

        if (added > 0)
        {
            Select(null);
            InvalidateVisual();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        return added;
    }

    /// <summary>
    /// 按绘制顺序从下到上找点击命中；后命中的候选覆盖先命中的，最终得到画面最上层物件。
    /// </summary>
    /// <param name="world">世界坐标。</param>
    /// <param name="kind">命中模式：选中看包围盒，路径加点额外过滤透明像素。</param>
    /// <returns>最上层命中物件；空白返回 null。</returns>
    private PlacedObject? HitTest(Point world, ObjectHitKind kind = ObjectHitKind.Selection)
    {
        if (_config is null)
        {
            return null;
        }

        // 透明像素只在 PathPoint 模式按需读取，普通选中不额外解码 / 采样，保持原有手感与性能。
        var spriteProvider = kind == ObjectHitKind.PathPoint
            ? new Func<PlacedObject, Bitmap?>(Sprite)
            : null;

        // 命中按「绘制顺序」取最上层：OrderedObjects 是从下到上排好的，
        // 点到的最后一个候选就是当前画面最前面的图元。这样点击被大地块覆盖的道具时，
        // 不会再因为地块包围盒更小/更大而选错；只有整幅 backdrop 继续跳过。
        PlacedObject? best = null;
        foreach (var item in OrderedObjects(_config))
        {
            // 不再按「背景层」整层跳过：自动提层的 bg_rock_1_550_867 虽然归属 background，
            // 但已进入立体带，必须能点中查看属性；普通背景地块（石板/草地/沙滩）也应可选中。
            // 只跳过真正覆盖整张画布的 backdrop，避免它吃掉所有点击。
            if (IsCanvasBackdrop(item, _config))
            {
                continue;
            }

            if (ObjectHitRules.Matches(item, world, kind, spriteProvider))
            {
                best = item;
            }
        }

        return best;
    }

    /// <summary>
    /// 是否是铺满整张地图的整幅底图。底图由 <see cref="DrawMapLayer"/> 单独绘制，
    /// 不在实例命中链路里；只有实例层里误导入的整幅背景才需要跳过点击。
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <param name="config">地图配置。</param>
    /// <returns>true = 整幅 backdrop，应跳过命中。</returns>
    private static bool IsCanvasBackdrop(PlacedObject item, MapConfigData config)
    {
        if (LayerOrder.EffectiveLayer(item) != MapLayerKeys.Background
            || LayerBands.Of(item) != LayerBands.Ground)
        {
            return false;
        }

        // 与地图 bgName 同名的整幅实例：即使尺寸字段不完整也按 backdrop 处理。
        if (!string.IsNullOrWhiteSpace(config.BgName)
            && (string.Equals(item.Name, config.BgName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TemplateId, config.BgName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var bounds = item.Bounds;
        return bounds.Width >= config.Width * 0.85
            && bounds.Height >= config.Height * 0.85;
    }

    private void ApplyMarqueeSelection()
    {
        if (_marqueeRect is not { } marquee || _config is null)
        {
            return;
        }

        var start = ScreenToWorld(marquee.TopLeft);
        var end = ScreenToWorld(marquee.BottomRight);
        var worldRect = new Rect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));

        if (worldRect.Width <= 2 || worldRect.Height <= 2)
        {
            return;
        }

        PlacedObject? found = null;
        foreach (var item in OrderedObjects(_config))
        {
            if (worldRect.Intersects(item.Bounds))
            {
                found = item;
            }
        }

        if (found is not null)
        {
            SetSelectedCell(null);
        }

        Select(found);
    }

    /// <summary>设置空白处点选的格子并广播变化；传入 null 表示取消选中。</summary>
    /// <param name="cell">格子行列（X = 列，Y = 行），null 为取消。</param>
    private void SetSelectedCell(Point? cell)
    {
        if (_selectedCell == cell)
        {
            return;
        }

        _selectedCell = cell;
        InvalidateVisual();
        CellSelectionChanged?.Invoke(this, cell);
    }

    private static Rect NormalizeRect(Point a, Point b)
    {
        return new Rect(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));
    }

    #endregion
}
