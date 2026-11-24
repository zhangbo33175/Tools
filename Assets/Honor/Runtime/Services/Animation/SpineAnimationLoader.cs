/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineAnimationLoader.cs
 * author:    云毅
 * created:   2026
 * descrip:   Atlas / Skeleton JSON / Skel 解析与纹理裁切
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Spine;

namespace MapEditorTool.Services;

#region 加载器

/// <summary>
/// Spine 资产加载器。支持 JSON 与二进制 .skel 骨架，Atlas 页贴图由 Avalonia <see cref="Bitmap"/> 承载。
/// 解析失败会返回 null 并给出可直接显示在状态栏的中文原因，不抛到 UI 线程。
/// </summary>
public static class SpineAnimationLoader
{
    /// <summary>加载一个 Spine 资产。</summary>
    /// <param name="name">资产名。</param>
    /// <param name="skeletonFile">骨架文件绝对路径。</param>
    /// <param name="atlasFile">Atlas 文件绝对路径。</param>
    /// <param name="defaultAnimation">默认动画名；空串取第一个动画。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>资产；失败返回 null。</returns>
    public static SpineAnimationAsset? Load(
        string name,
        string skeletonFile,
        string atlasFile,
        string defaultAnimation,
        out string error)
    {
        error = string.Empty;
        Atlas? atlas = null;
        var bitmaps = new List<Bitmap>();

        try
        {
            if (!File.Exists(skeletonFile))
            {
                error = $"骨架文件不存在：{skeletonFile}";
                return null;
            }

            if (!File.Exists(atlasFile))
            {
                error = $"Atlas 文件不存在：{atlasFile}";
                return null;
            }

            var atlasDirectory = Path.GetDirectoryName(atlasFile) ?? string.Empty;
            var textureLoader = new AvaloniaSpineTextureLoader(atlasDirectory, bitmaps);
            atlas = new Atlas(atlasFile, textureLoader);
            if (atlas.Pages.Count == 0)
            {
                error = "Atlas 中没有找到任何页。";
                atlas.Dispose();
                return null;
            }

            var skeletonJson = new SkeletonJson(atlas);
            SkeletonData data;
            if (string.Equals(Path.GetExtension(skeletonFile), ".skel", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(skeletonFile);
                data = new SkeletonBinary(atlas).ReadSkeletonData(stream);
            }
            else
            {
                using var reader = new StreamReader(skeletonFile);
                data = skeletonJson.ReadSkeletonData(reader);
            }

            var animations = data.Animations.Items
                .Select(animation => animation.Name)
                .Where(animation => !string.IsNullOrWhiteSpace(animation))
                .ToList();

            if (animations.Count == 0)
            {
                error = "骨架里没有动画，无法用于物体替换。";
                atlas.Dispose();
                return null;
            }

            var resolvedDefault = animations.FirstOrDefault(animation =>
                    string.Equals(animation, defaultAnimation, StringComparison.Ordinal))
                ?? animations[0];

            var pages = BuildPages(atlas, bitmaps);
            var bounds = ComputeSetupBounds(data);
            var asset = new SpineAnimationAsset(
                name,
                Path.GetDirectoryName(skeletonFile) ?? string.Empty,
                skeletonFile,
                atlasFile,
                resolvedDefault,
                data,
                atlas,
                pages,
                bounds,
                animations);

            return asset;
        }
        catch (Exception ex)
        {
            error = $"Spine 解析失败：{ex.Message}";
            TryLogFailure(ex, skeletonFile, atlasFile);
            atlas?.Dispose();
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }

            return null;
        }
    }

    /// <summary>把解析异常写入 output\Logs\spine-load.log，便于定位第三方运行时问题。</summary>
    /// <param name="exception">异常。</param>
    /// <param name="skeletonFile">骨架文件。</param>
    /// <param name="atlasFile">Atlas 文件。</param>
    private static void TryLogFailure(Exception exception, string skeletonFile, string atlasFile)
    {
        EditorLogs.Append(
            "spine-load.log",
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] skeleton={skeletonFile}{Environment.NewLine}" +
            $"atlas={atlasFile}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
    }

    /// <summary>把 Atlas 页转成可绘制位图，并为每个 region 预先裁切。</summary>
    /// <param name="atlas">Atlas 对象。</param>
    /// <param name="bitmaps">已加载的整页位图，失败时统一释放。</param>
    /// <returns>页缓存。</returns>
    private static IReadOnlyList<SpinePageTexture> BuildPages(Atlas atlas, List<Bitmap> bitmaps)
    {
        var result = new List<SpinePageTexture>();
        foreach (var page in atlas.Pages)
        {
            var pageBitmap = page.rendererObject as Bitmap;
            if (pageBitmap == null)
            {
                throw new InvalidOperationException($"Atlas 页贴图未加载：{page.name}");
            }

            var pageTexture = new SpinePageTexture(page, pageBitmap);
            foreach (var region in atlas.Regions.Where(region => ReferenceEquals(region.page, page)))
            {
                var crop = ClampCrop(pageBitmap, region);
                if (crop.Width <= 0 || crop.Height <= 0)
                {
                    continue;
                }

                var cropped = new CroppedBitmap(pageBitmap, crop);
                pageTexture.Add(region, new SpineRegionTexture(cropped, region.degrees, crop.Width, crop.Height));
            }

            result.Add(pageTexture);
        }

        return result;
    }

    /// <summary>把 AtlasRegion 的 bounds 限制在页位图内，避免越界裁切抛异常。</summary>
    /// <param name="bitmap">页位图。</param>
    /// <param name="region">Atlas 区域。</param>
    /// <returns>安全的裁切矩形。</returns>
    private static PixelRect ClampCrop(Bitmap bitmap, AtlasRegion region)
    {
        var pageWidth = bitmap.PixelSize.Width;
        var pageHeight = bitmap.PixelSize.Height;
        var x = Math.Clamp(region.x, 0, Math.Max(0, pageWidth - 1));
        var y = Math.Clamp(region.y, 0, Math.Max(0, pageHeight - 1));
        var width = Math.Clamp(region.width, 0, pageWidth - x);
        var height = Math.Clamp(region.height, 0, pageHeight - y);
        return new PixelRect(x, y, width, height);
    }

    /// <summary>用 Setup Pose 所有 region / mesh 顶点计算包围盒，供渲染器做等比适配。</summary>
    /// <param name="data">骨架数据。</param>
    /// <returns>Spine 坐标系包围盒。</returns>
    private static Rect ComputeSetupBounds(SkeletonData data)
    {
        var skeleton = new Skeleton(data);
        skeleton.SetToSetupPose();
        skeleton.SetBonesToSetupPose();
        skeleton.SetSlotsToSetupPose();
        skeleton.UpdateWorldTransform();

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var hasPoint = false;

        foreach (var slot in skeleton.Slots.Items)
        {
            var attachment = slot.Attachment;
            if (attachment is RegionAttachment regionAttachment)
            {
                var vertices = new float[8];
                regionAttachment.ComputeWorldVertices(slot, vertices, 0, 2);
                Accumulate(vertices, 2, ref minX, ref minY, ref maxX, ref maxY, ref hasPoint);
            }
            else if (attachment is MeshAttachment meshAttachment)
            {
                var vertices = new float[meshAttachment.WorldVerticesLength];
                meshAttachment.ComputeWorldVertices(slot, vertices);
                Accumulate(vertices, 2, ref minX, ref minY, ref maxX, ref maxY, ref hasPoint);
            }
        }

        if (!hasPoint || maxX <= minX || maxY <= minY)
        {
            var width = data.Width > 1 ? data.Width : 100;
            var height = data.Height > 1 ? data.Height : 100;
            return new Rect(data.X, data.Y, width, height);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>把顶点数组并入包围盒。</summary>
    /// <param name="vertices">交错 XY 顶点。</param>
    /// <param name="stride">每点分量数。</param>
    /// <param name="minX">最小 X。</param>
    /// <param name="minY">最小 Y。</param>
    /// <param name="maxX">最大 X。</param>
    /// <param name="maxY">最大 Y。</param>
    /// <param name="hasPoint">是否已有有效点。</param>
    private static void Accumulate(
        float[] vertices,
        int stride,
        ref float minX,
        ref float minY,
        ref float maxX,
        ref float maxY,
        ref bool hasPoint)
    {
        for (var index = 0; index + stride <= vertices.Length; index += stride)
        {
            var x = vertices[index];
            var y = vertices[index + 1];
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            hasPoint = true;
        }
    }

    #endregion

    #region Atlas 纹理加载

    /// <summary>把 Atlas 页文件读成 Avalonia 位图；找不到时尝试常见图片扩展名。</summary>
    private sealed class AvaloniaSpineTextureLoader : TextureLoader
    {
        private readonly string _imagesDirectory;
        private readonly List<Bitmap> _bitmaps;

        /// <summary>创建加载器。</summary>
        /// <param name="imagesDirectory">Atlas 所在目录。</param>
        /// <param name="bitmaps">位图回收列表。</param>
        public AvaloniaSpineTextureLoader(string imagesDirectory, List<Bitmap> bitmaps)
        {
            _imagesDirectory = imagesDirectory;
            _bitmaps = bitmaps;
        }

        /// <inheritdoc />
        public void Load(AtlasPage page, string path)
        {
            var resolved = Resolve(path);
            if (resolved == null)
            {
                throw new FileNotFoundException($"Atlas 页贴图不存在：{path}");
            }

            var bitmap = new Bitmap(resolved);
            _bitmaps.Add(bitmap);
            page.rendererObject = bitmap;
            page.width = bitmap.PixelSize.Width;
            page.height = bitmap.PixelSize.Height;
        }

        /// <inheritdoc />
        public void Unload(object texture)
        {
            if (texture is Bitmap bitmap)
            {
                bitmap.Dispose();
            }
        }

        /// <summary>解析页贴图路径；支持无扩展名与相对路径。</summary>
        /// <param name="path">Atlas 给出的路径。</param>
        /// <returns>存在的绝对路径；找不到返回 null。</returns>
        private string? Resolve(string path)
        {
            var candidates = new List<string>();
            if (Path.IsPathRooted(path))
            {
                candidates.Add(path);
            }
            else
            {
                candidates.Add(Path.Combine(_imagesDirectory, path));
            }

            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
            {
                foreach (var extension in ImageFileTypes.All)
                {
                    candidates.Add(candidates[0] + extension);
                }
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 有些 Atlas 写成 images/page.png，但实际页面就放在 Atlas 同级
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                foreach (var extension in ImageFileTypes.AllWithEmpty)
                {
                    var candidate = Path.Combine(_imagesDirectory, fileName + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }

    #endregion
}
