/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SceneDisplayAudit.cs
 * author:    云毅
 * created:   2026
 * descrip:   「刷新画面」巡检 + 窄口自动修复：贴图、尺寸、旋转、拉伸、平铺、分带、接缝
 ***************************************************************/

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using MapEditorTool.Models;

namespace MapEditorTool.Services;

/// <summary>
/// 「刷新画面」的巡检结果。记录本轮核对到的数量，供状态栏给出可验证的反馈；
/// 只做两类高置信度自动修复（背景组立体美术分带提升、平面件窄缝贴合），
/// 不把用户手工调过的尺寸 / 拉伸 / 旋转强行改回去。
/// </summary>
public sealed record SceneDisplayReport(
    int Instances,
    int Ground,
    int Standing,
    int Overlay,
    int Images,
    int MemoryPreviews,
    int Missing,
    int Rotated,
    int Stretched,
    int TileCandidates,
    int PixelSizeCompleted,
    int PixelSizeMismatch,
    int BandPromoted,
    int SeamSnapped,
    int OffMap);

/// <summary>
/// 2D 地图显示规则巡检。
/// <para>
/// 画布本身每一帧都会按「遮挡带 + 脚底 Y」排序，并按 rotation / flipX / stretch 出图；
/// 但「刷新画面」还需要回答三个问题：贴图能不能解析到、PSD 切片尺寸和 JSON 是否一致、
/// 哪些地块大到应该平铺而不是拉糊；写入型修复统一委托给
/// <see cref="OcclusionAdjustmentStrategy"/>：背景组立体美术提层、池塘/水面/地毯接缝贴合。
/// 尺寸 / 拉伸 / 旋转只报告，不覆盖用户改过的值。
/// </para>
/// </summary>
public static class SceneDisplayAudit
{
    #region 公开方法

    /// <summary>
    /// 巡检当前配置。返回的数量用于状态栏说明「刷新画面到底做了什么」。
    /// </summary>
    /// <param name="config">当前地图配置。</param>
    /// <param name="assets">资源库（按关卡根解析贴图）。</param>
    /// <param name="hasRuntimeSprite">该实例是否已有 PSD 导入的内存预览图（尚未落盘时不算缺图）。</param>
    /// <returns>巡检结果。</returns>
    public static SceneDisplayReport Apply(
        MapConfigData config,
        AssetLibrary assets,
        Func<PlacedObject, bool>? hasRuntimeSprite = null)
    {
        var ground = 0;
        var standing = 0;
        var overlay = 0;
        var images = 0;
        var memoryPreviews = 0;
        var missing = 0;
        var rotated = 0;
        var stretched = 0;
        var tileCandidates = 0;
        var completed = 0;
        var mismatched = 0;
        var offMap = 0;

        // 所有写入型遮挡修复都走同一策略入口，刷新画面与刷新层级不再各写一套。
        // 视觉语义只在刷新链路里补充：名称混进 bg_ 的树/灌木由像素轮廓识别，导入阶段没有图片时仍按名称。
        var adjustment = OcclusionAdjustmentStrategy.Apply(
            config,
            item => hasRuntimeSprite?.Invoke(item) == true
                ? null
                : ResolveVisualBitmap(assets, item, config));
        var bandPromoted = adjustment.BandPromoted;
        var seamSnapped = adjustment.SeamSnapped;

        foreach (var item in config.Objects)
        {
            switch (LayerBands.Of(item))
            {
                case LayerBands.Ground:
                    ground++;
                    break;
                case LayerBands.Standing:
                    standing++;
                    break;
                default:
                    overlay++;
                    break;
            }

            if (Math.Abs(item.Rotation) >= 0.01)
            {
                rotated++;
            }

            // 越界只统计「部分超出地图包围盒」的实例；美术裁不裁由画布「裁剪越界」开关决定。
            var itemBounds = item.Bounds;
            if (itemBounds.Left < 0
                || itemBounds.Top < 0
                || itemBounds.Right > config.Width
                || itemBounds.Bottom > config.Height)
            {
                offMap++;
            }

            if (item.Stretch)
            {
                stretched++;
            }

            var path = ResolveImagePath(assets, item, config);
            if (path is null)
            {
                if (hasRuntimeSprite?.Invoke(item) == true)
                {
                    memoryPreviews++;
                }
                else
                {
                    missing++;
                }

                continue;
            }

            images++;

            if (!TryReadPngSize(path, out var pngWidth, out var pngHeight))
            {
                continue;
            }

            // PSD 里「旋转 90/270 后与已有切片相同」的图层会复用同一张 PNG：
            // 物件的 PixelWidth/PixelHeight 记的是图层自身朝向，PNG 的宽高是交换过的
            var quarterTurn = (int)Math.Round(Math.Abs(item.Rotation) / 90.0) % 2 == 1;
            var expectedWidth = quarterTurn ? pngHeight : pngWidth;
            var expectedHeight = quarterTurn ? pngWidth : pngHeight;

            if (item.PixelWidth <= 0 || item.PixelHeight <= 0)
            {
                if (item.PixelWidth <= 0)
                {
                    item.PixelWidth = expectedWidth;
                }

                if (item.PixelHeight <= 0)
                {
                    item.PixelHeight = expectedHeight;
                }

                completed++;
                continue;
            }

            if (Math.Abs(item.PixelWidth - expectedWidth) > 1
                || Math.Abs(item.PixelHeight - expectedHeight) > 1)
            {
                // 不覆盖：用户可能故意把包围盒调得比贴图大（contain / 平铺），
                // 这里只报告，方便在状态栏看到「切片换过、JSON 还是旧尺寸」
                mismatched++;
            }

            if (ShouldTileGround(item, new Avalonia.Size(expectedWidth, expectedHeight)))
            {
                tileCandidates++;
            }
        }

        return new SceneDisplayReport(
            config.Objects.Count,
            ground,
            standing,
            overlay,
            images,
            memoryPreviews,
            missing,
            rotated,
            stretched,
            tileCandidates,
            completed,
            mismatched,
            bandPromoted,
            seamSnapped,
            offMap);
    }

    /// <summary>
    /// 地表地块是否应该平铺：贴图明显小于占格、且没有勾「是否缩放」时，
    /// 按原始像素重复铺满包围盒，比把 32×32 的小图拉成 200×200 更清晰。
    /// 只对地表带生效；立体物与叠加层永远保持 contain，避免树 / 人物被铺成花屏。
    /// </summary>
    /// <param name="item">地图实例。</param>
    /// <param name="pixelSize">贴图原始像素尺寸。</param>
    /// <returns>true = 按平铺绘制。</returns>
    public static bool ShouldTileGround(PlacedObject item, Avalonia.Size pixelSize)
    {
        if (item.Stretch || pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            return false;
        }

        if (LayerBands.Of(item) != LayerBands.Ground)
        {
            return false;
        }

        var bounds = item.Bounds;
        return bounds.Width >= pixelSize.Width * 1.5
            && bounds.Height >= pixelSize.Height * 1.5;
    }

    #endregion

    #region 私有方法

    /// <summary>解析并加载用于视觉语义判断的切片；找不到时返回 null。</summary>
    /// <param name="assets">资源库。</param>
    /// <param name="item">地图实例。</param>
    /// <param name="config">地图配置。</param>
    /// <returns>已解码位图；复用 <see cref="AssetLibrary"/> 的位图缓存。</returns>
    private static Bitmap? ResolveVisualBitmap(AssetLibrary assets, PlacedObject item, MapConfigData config)
    {
        var path = ResolveImagePath(assets, item, config);
        if (path is null
            || !TryReadPngSize(path, out var width, out var height)
            || !ArtSilhouetteInspector.IsProfileable(width, height))
        {
            return null;
        }

        return assets.Load(path);
    }

    /// <summary>按与画布 / 导出相同的顺序解析贴图：image 字段 → 关卡八层目录 → Unity 三级目录 → 资源名。</summary>
    private static string? ResolveImagePath(AssetLibrary assets, PlacedObject item, MapConfigData config)
    {
        if (!string.IsNullOrWhiteSpace(item.ImagePath))
        {
            var byPath = assets.Resolve(item.ImagePath);
            if (byPath is not null)
            {
                return byPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            // 本关卡自己的切片优先：`Background/bg_rock_1.png` 和 Unity 原图同名时，
            // 必须先看关卡目录里真实写出的那张，否则核对的是另一张图，尺寸永远对不上
            var folder = MapLayerKeys.FolderName(item.LayerType);
            var stem = Path.GetFileNameWithoutExtension(item.Name);
            var byLevel = assets.Resolve(folder + "/" + stem + ".png");
            if (byLevel is not null)
            {
                return byLevel;
            }

            // Unity 三级目录同样只吃资源名主干：名字已经带 .png 时再拼一次会变成 .png.png
            var byLayer = MapTextureResolver.Resolve(
                assets.ProjectRoot,
                stem,
                item.LayerType,
                config.ChapterId,
                config.SceneId);
            if (byLayer is not null)
            {
                return byLayer;
            }

            return assets.Resolve(item.Name);
        }

        return null;
    }

    /// <summary>只读 PNG 头部的 IHDR，拿原始像素宽高；不是 PNG 或文件损坏返回 false。</summary>
    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            var header = new byte[24];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.ReadExactly(header);
            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
            {
                return false;
            }

            width = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
            return width > 0 && height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}
