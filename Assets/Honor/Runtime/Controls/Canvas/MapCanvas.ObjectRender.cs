/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MapCanvas.ObjectRender.cs
 * author:    云毅
 * created:   2026
 * descrip:   画布物件绘制：精灵、九宫格、平铺、标签与对象层
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

/// <summary>画布物件绘制：精灵、九宫格、平铺、标签与对象层。</summary>
public sealed partial class MapCanvas : Control
{
    #region 占位绘制透明度

    // 「这块地有美术、但我们没像素」的那几种画法共用同一族透明度：以前散在绘制代码里是 0xC0/0x40/0x80/0xD0/0x70 五个魔数，
    // 调浓一点要满文件找，现在集中在这里。
    /// <summary>Spine 资产缺失时的图层色虚线框。</summary>
    private const byte GhostDashedAlpha = 0xC0;

    /// <summary>缺贴图的背景地块：图层色浅填充。</summary>
    private const byte GhostFillAlpha = 0x40;

    /// <summary>贴图包围盒虚线（勾选「显示图像边框」时）。</summary>
    private const byte BoundsDashedAlpha = 0x80;

    /// <summary>纯色占位块的主填充。</summary>
    private const byte PlaceholderFillAlpha = 0xD0;

    /// <summary>纯色占位块外圈黑描边。</summary>
    private const byte PlaceholderEdgeAlpha = 0x70;

    #endregion

    #region 物件与贴图绘制

    /// <summary>
    /// 画一个物件。屋里隐藏预览生效时整体压淡，其余情况直接交给 <see cref="DrawObjectCore"/>。
    /// <para>压淡放在最外层而不是改每个绘制分支的 alpha：贴图、色块、描边、名字一起淡，
    /// 视觉上才是「这面墙进屋后消失了」，也避免某条分支漏改导致半透明混色。</para>
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="item">地图实例。</param>
    private void DrawObject(DrawingContext context, PlacedObject item)
    {
        if (!ShouldFadeForRoomPreview(item))
        {
            DrawObjectCore(context, item);
            return;
        }

        using (context.PushOpacity(RoomPreviewOpacity))
        {
            DrawObjectCore(context, item);
        }
    }

    private void DrawObjectCore(DrawingContext context, PlacedObject item)
    {
        // 预览中的角色若带路径，按预览时钟把脚底插值到路径上；编辑器里始终用落盘坐标
        var bounds = PreviewRenderBounds(item);
        // 预览行走时按路径切向自动翻面；静态摆放仍按实例自身的 FlipX 绘制。
        // ---TODO 朝向：所有视觉源（Spine / 序列帧 / 静态图）共用同一镜像结果。
        var renderFlipX = ResolveRenderFlipX(item);

        // 四方向出图：模板配了朝向变体就只换图、不转图（等距美术的四个透视是画好的，
        // 再绕中心转会变成斜的）；没配变体的旧图元继续按实例角度旋转，行为完全不变。
        var rotation = EffectiveRotationOf(item);

        // 占位色 / 背景特殊绘制都跟随「有效显示层」：手改到背景层的图元按背景画法走，
        // 顶部图层勾选时也能和画面一致（显示层优先，未设置才回落到归属层）。
        var displayLayer = item.IsCollectible ? MapLayerKeys.Collectible : LayerOrder.EffectiveLayer(item);
        var color = MapLayerPalette.Of(displayLayer);

        // Spine 是唯一视觉源：命中时完全替换静态贴图，并且不回退旧 PNG / 旧序列帧；
        // 资产缺失时只画虚线占位，避免把旧背景图重新露出来。
        var spineVisual = UsesSpineVisual(item);
        var spineAsset = spineVisual ? _spineAnimations?.Find(item.SpineAsset) : null;
        var spinePlayer = spineAsset is null ? null : GetSpinePlayer(item);
        var staticSprite = spineVisual ? null : Sprite(item);
        var animation = spineVisual ? null : ResolveClip(item);
        var sprite = spineVisual
            ? null
            : animation is not null ? AnimatedSprite(item, animation) ?? staticSprite : staticSprite;
        var pixelSize = staticSprite is not null
            ? new Size(staticSprite.PixelSize.Width, staticSprite.PixelSize.Height)
            : sprite is null
                ? new Size(0, 0)
                : new Size(sprite.PixelSize.Width, sprite.PixelSize.Height);
        var tiled = sprite is not null && SceneDisplayAudit.ShouldTileGround(item, pixelSize);

        // 九宫格只对「勾选是否缩放」的实例生效；平铺地块、缺贴图、边距越界都自动退回普通绘制。
        var nineSlice = sprite is not null && item.Stretch && !tiled
            ? NineSlices?.Find(item.TemplateId, item.ImagePath, item.Name)
            : null;
        var useNineSlice = nineSlice is not null && nineSlice.IsUsable(pixelSize.Width, pixelSize.Height);

        // 背景层是整幅底图/地块，画半透明色块与虚线框，避免压住其他图元与网格。
        // 用手改后的有效显示层判断：把道具层图元改成背景层后，也应该走背景画法。
        if (displayLayer == MapLayerKeys.Background)
        {
            using (context.PushTransform(SpriteLayout.ContentTransform(rotation, renderFlipX, bounds)))
            {
                if (spinePlayer is not null && spineAsset is not null)
                {
                    SpineRenderer.Draw(context, spinePlayer, spineAsset, bounds, 0, false);
                }
                else if (sprite is not null)
                {
                    if (tiled)
                    {
                        DrawTiled(context, sprite, bounds);
                    }
                    else
                    {
                        context.DrawImage(
                            sprite,
                            new Rect(0, 0, sprite.PixelSize.Width, sprite.PixelSize.Height),
                            bounds);
                    }
                }
                else if (spineVisual)
                {
                    // Spine 资产缺失：只留虚线占位，不铺图层色块，避免看起来像旧背景图。
                    context.DrawRectangle(
                        null,
                        new Pen(new SolidColorBrush(Color.FromArgb(GhostDashedAlpha, color.R, color.G, color.B)), 1.5 * Hairline, DashStyle.Dash),
                        bounds);
                }
                else
                {
                    // 缺贴图的背景地块（Unity 侧烘焙进合成底图的那批）：色块稍重一点，
                    // 好让「这块地有美术、但我们没像素」一眼能看出来，又不是实心地块
                    context.FillRectangle(
                        new SolidColorBrush(Color.FromArgb(GhostFillAlpha, color.R, color.G, color.B)),
                        bounds);
                }

                if (ShowImageBorder && !PreviewMode)
                {
                    context.DrawRectangle(
                        null,
                        new Pen(new SolidColorBrush(Color.FromArgb(BoundsDashedAlpha, color.R, color.G, color.B)), 1.5 * Hairline, DashStyle.Dash),
                        bounds);
                }
            }

            return;
        }

        var fill = new SolidColorBrush(Color.FromArgb(PlaceholderFillAlpha, color.R, color.G, color.B));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(PlaceholderEdgeAlpha, 0x00, 0x00, 0x00)), Hairline);
        var band = LayerBands.Of(item);

        // 「是否缩放」勾选时贴图按包围盒铺满；取消勾选则等比缩放到包围盒内（contain），
        // 与 Unity 场景工具的 GUI.DrawTexture(ScaleMode.ScaleToFit) 一致：不溢出、不变形。
        // 旋转 90/270 复用的切片（宽高与物件框互换）先按「交换后的框」contain，再交给旋转矩阵摆正
        var drawBounds = sprite is not null && item.Stretch is false
            ? SpriteLayout.FitForRotation(bounds, pixelSize, rotation)
            : bounds;

        // 2D 立体物的脚底必须踩在包围盒底边：贴图比占格小时只做水平居中、底部对齐，
        // 不能上下居中（否则树 / 人物会浮在半空，和脚底 Y 的遮挡排序对不上）。
        // 旋转过的图元交给旋转矩阵，不再二次偏移，避免斜放物件被拉离原位。
        if (sprite is not null
            && band == LayerBands.Standing
            && Math.Abs(rotation) < 0.01
            && drawBounds.Height < bounds.Height - 0.5)
        {
            drawBounds = new Rect(
                drawBounds.X,
                bounds.Bottom - drawBounds.Height,
                drawBounds.Width,
                drawBounds.Height);
        }

        // 有贴图与没贴图（色块占位）都要吃旋转与水平镜像，保证改角度后画布上有明确反馈
        using (context.PushTransform(SpriteLayout.ContentTransform(rotation, renderFlipX, bounds)))
        {
            if (spinePlayer is not null && spineAsset is not null)
            {
                SpineRenderer.Draw(context, spinePlayer, spineAsset, bounds, 0, false);
            }
            else if (sprite is not null)
            {
                if (tiled)
                {
                    DrawTiled(context, sprite, bounds);
                }
                else if (useNineSlice)
                {
                    DrawNineSlice(context, sprite, drawBounds, nineSlice!);
                }
                else
                {
                    context.DrawImage(
                        sprite,
                        new Rect(0, 0, sprite.PixelSize.Width, sprite.PixelSize.Height),
                        drawBounds);
                }
            }
            else if (spineVisual)
            {
                // Spine 资产缺失：虚线占位，绝不回退到旧静态图，避免动画位置又出现背景图。
                context.DrawRectangle(
                    null,
                    new Pen(new SolidColorBrush(Color.FromArgb(GhostDashedAlpha, color.R, color.G, color.B)), 1.5 * Hairline, DashStyle.Dash),
                    bounds);
            }
            else
            {
                // 没有贴图时退回图层色块，占格与编辑仍然可见
                context.FillRectangle(fill, bounds);
            }

            if (ShowImageBorder && !PreviewMode)
            {
                context.DrawRectangle(null, borderPen, sprite is null || tiled ? bounds : drawBounds);
            }
        }

        // 图片外框与占格描边受 DrawBorder 开关控制：关掉后只剩贴图本身，方便比对美术效果
        // 拖动平移时也跳过：每帧 500+ 条 Pen 描边纯属无谓开销，松手后补画
        // 选中实例不画：它的占格框就落在同一条足迹上，再补一条白线会和绿框错开十几像素、叠成双框
        if (ShowImageBorder && !_isPanning && !PreviewMode
            && Math.Abs(rotation) < 0.01 && !ReferenceEquals(item, _selected))
        {
            // 未旋转的图元：一条轴对齐格足迹描边，便于核对占格；旋转图元的格子级占格见 DrawSelectedOccupancy。
            // 格数按当前地图的格子边长现算，和占格显示、导出同一口径（原来写死 32，换 16px 格宽就会算错一半）。
            var cell = (double)(_config?.GridSize > 0 ? _config.GridSize : MapToolDefaults.GridSize);
            var footprintPen = new Pen(UiTheme.MapView.Footprint, Hairline);
            var cols = Math.Max(1, (int)Math.Ceiling(item.Bounds.Width / cell));
            var rows = Math.Max(1, (int)Math.Ceiling(item.Bounds.Height / cell));
            context.DrawRectangle(
                null,
                footprintPen,
                new Rect(item.X, item.Y, cols * cell, rows * cell));
        }

        // 物件名是编辑态辅助：预览窗口里再画一遍会盖在角色/载具上，反而挡住美术本来的朝向
        if (_zoom >= 0.45 && !PreviewMode)
        {
            DrawLabel(context, item.Name, bounds);
        }
    }

    /// <summary>
    /// 未勾选「是否缩放」时贴图的绘制矩形：等比缩放到包围盒内居中（contain），
    /// 与 Unity 场景工具的 <c>ScaleMode.ScaleToFit</c> 口径一致——宽高比不失真、绝不溢出包围盒。
    /// </summary>
    /// <param name="bounds">实例包围盒。</param>
    /// <param name="pixelSize">贴图原始像素尺寸。</param>
    /// <param name="degrees">实例旋转角度（度）。</param>
    /// <returns>绘制矩形。</returns>
    /// <summary>
    /// 按九宫格把贴图铺到目标矩形：四角固定为素材原始像素尺寸，四条边只沿单轴拉伸，
    /// 中心按 <see cref="NineSliceSettings.CenterMode"/> 拉伸或按原始尺寸平铺。
    /// 目标比两侧边距之和还小时退回整图拉伸，避免负尺寸。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="sprite">贴图。</param>
    /// <param name="bounds">目标矩形（世界坐标）。</param>
    /// <param name="settings">九宫格配置。</param>
    private static void DrawNineSlice(
        DrawingContext context,
        Bitmap sprite,
        Rect bounds,
        NineSliceSettings settings)
    {
        var sourceWidth = sprite.PixelSize.Width;
        var sourceHeight = sprite.PixelSize.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        var left = Math.Clamp(settings.Left, 0, sourceWidth - 1);
        var right = Math.Clamp(settings.Right, 0, sourceWidth - 1 - left);
        var top = Math.Clamp(settings.Top, 0, sourceHeight - 1);
        var bottom = Math.Clamp(settings.Bottom, 0, sourceHeight - 1 - top);
        var centerWidth = sourceWidth - left - right;
        var centerHeight = sourceHeight - top - bottom;
        if (centerWidth <= 0 || centerHeight <= 0 || bounds.Width <= left + right || bounds.Height <= top + bottom)
        {
            context.DrawImage(
                sprite,
                new Rect(0, 0, sourceWidth, sourceHeight),
                bounds);
            return;
        }

        var rightEdgeX = bounds.Right - right;
        var bottomEdgeY = bounds.Bottom - bottom;

        // 四角：只平移，不缩放
        context.DrawImage(sprite, new Rect(0, 0, left, top), new Rect(bounds.X, bounds.Y, left, top));
        context.DrawImage(sprite, new Rect(sourceWidth - right, 0, right, top),
            new Rect(rightEdgeX, bounds.Y, right, top));
        context.DrawImage(sprite, new Rect(0, sourceHeight - bottom, left, bottom),
            new Rect(bounds.X, bottomEdgeY, left, bottom));
        context.DrawImage(sprite, new Rect(sourceWidth - right, sourceHeight - bottom, right, bottom),
            new Rect(rightEdgeX, bottomEdgeY, right, bottom));

        // 四条边：单轴拉伸
        context.DrawImage(
            sprite,
            new Rect(left, 0, centerWidth, top),
            new Rect(bounds.X + left, bounds.Y, bounds.Width - left - right, top));
        context.DrawImage(
            sprite,
            new Rect(left, sourceHeight - bottom, centerWidth, bottom),
            new Rect(bounds.X + left, bottomEdgeY, bounds.Width - left - right, bottom));
        context.DrawImage(
            sprite,
            new Rect(0, top, left, centerHeight),
            new Rect(bounds.X, bounds.Y + top, left, bounds.Height - top - bottom));
        context.DrawImage(
            sprite,
            new Rect(sourceWidth - right, top, right, centerHeight),
            new Rect(rightEdgeX, bounds.Y + top, right, bounds.Height - top - bottom));

        var centerBounds = new Rect(
            bounds.X + left,
            bounds.Y + top,
            bounds.Width - left - right,
            bounds.Height - top - bottom);
        var centerSource = new Rect(left, top, centerWidth, centerHeight);
        if (!string.Equals(settings.CenterMode, NineSliceModes.Tile, StringComparison.OrdinalIgnoreCase))
        {
            context.DrawImage(sprite, centerSource, centerBounds);
            return;
        }

        // 中心平铺：按素材原始像素重复；数量过多时退回一次拉伸，避免大地图上刷出几万次 DrawImage
        var columns = (int)Math.Ceiling(centerBounds.Width / centerWidth);
        var rows = (int)Math.Ceiling(centerBounds.Height / centerHeight);
        if (columns <= 0 || rows <= 0 || columns * rows > 4096)
        {
            context.DrawImage(sprite, centerSource, centerBounds);
            return;
        }

        using (context.PushClip(centerBounds))
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var x = centerBounds.X + column * centerWidth;
                    var y = centerBounds.Y + row * centerHeight;
                    context.DrawImage(sprite, centerSource, new Rect(x, y, centerWidth, centerHeight));
                }
            }
        }
    }

    /// <summary>
    /// 按贴图原始像素重复铺满包围盒（多用于被放大的草地 / 路面地块）。
    /// 平铺数量过多时退回一次拉伸，避免 4096 地图上几十万次 DrawImage 把帧率拖死。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="sprite">贴图。</param>
    /// <param name="bounds">包围盒（世界坐标）。</param>
    private static void DrawTiled(DrawingContext context, Bitmap sprite, Rect bounds)
    {
        var width = sprite.PixelSize.Width;
        var height = sprite.PixelSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var columns = (int)Math.Ceiling(bounds.Width / width);
        var rows = (int)Math.Ceiling(bounds.Height / height);
        if (columns <= 0 || rows <= 0)
        {
            return;
        }

        var source = new Rect(0, 0, width, height);
        if ((long)columns * rows > 256)
        {
            context.DrawImage(sprite, source, bounds);
            return;
        }

        using var clip = context.PushClip(bounds);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                context.DrawImage(
                    sprite,
                    source,
                    new Rect(bounds.X + column * width, bounds.Y + row * height, width, height));
            }
        }
    }

    private void DrawLabel(DrawingContext context, string text, Rect bounds)
    {
        // 拖动平移时不排版文字：每帧 500+ 段 FormattedText 是平移不跟手的主要原因之一
        if (_isPanning || string.IsNullOrEmpty(text) || bounds.Width * _zoom < 24)
        {
            return;
        }

        var formatted = CanvasText.Create(text, 11.0 * Hairline, LabelBrush);

        var origin = new Point(
            bounds.X + (bounds.Width - formatted.Width / _zoom) / 2.0,
            bounds.Y + (bounds.Height - formatted.Height / _zoom) / 2.0);

        if (formatted.Width / _zoom <= bounds.Width)
        {
            context.DrawText(formatted, origin);
        }
    }
    #endregion
}



