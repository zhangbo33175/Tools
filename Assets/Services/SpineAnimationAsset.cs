/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineAnimationAsset.cs
 * author:    云毅
 * created:   2026
 * descrip:   已导入 Spine 资产的解析结果与纹理页缓存
 ***************************************************************/

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Spine;

namespace MapEditorTool.Services;

#region 资产模型

/// <summary>
/// 一个已导入的 Spine 资产。<see cref="Data"/> 是 spine-csharp 解析后的骨架数据，
/// <see cref="Pages"/> 缓存每个 Atlas 页及裁切后的区域贴图，供画布和预览窗直接绘制。
/// </summary>
public sealed class SpineAnimationAsset : IDisposable
{
    private readonly Atlas _atlas;

    /// <summary>创建资产。</summary>
    /// <param name="name">资产名，对应配置里的名字。</param>
    /// <param name="directory">资产目录。</param>
    /// <param name="skeletonFile">骨架文件（.json/.skel）。</param>
    /// <param name="atlasFile">Atlas 文件。</param>
    /// <param name="defaultAnimation">默认动画名。</param>
    /// <param name="data">已解析的骨架数据。</param>
    /// <param name="atlas">Atlas 对象，持有页纹理生命周期。</param>
    /// <param name="pages">页纹理缓存。</param>
    /// <param name="setupBounds">Setup Pose 的骨架包围盒，Spine 坐标系（Y 向上）。</param>
    /// <param name="animations">动画名列表。</param>
    internal SpineAnimationAsset(
        string name,
        string directory,
        string skeletonFile,
        string atlasFile,
        string defaultAnimation,
        SkeletonData data,
        Atlas atlas,
        IReadOnlyList<SpinePageTexture> pages,
        Rect setupBounds,
        IReadOnlyList<string> animations)
    {
        Name = name;
        Directory = directory;
        SkeletonFile = skeletonFile;
        AtlasFile = atlasFile;
        DefaultAnimation = defaultAnimation;
        Data = data;
        _atlas = atlas;
        Pages = pages;
        SetupBounds = setupBounds;
        Animations = animations;
    }

    /// <summary>资产名。</summary>
    public string Name { get; }

    /// <summary>资产目录。</summary>
    public string Directory { get; }

    /// <summary>骨架文件绝对路径。</summary>
    public string SkeletonFile { get; }

    /// <summary>Atlas 文件绝对路径。</summary>
    public string AtlasFile { get; }

    /// <summary>默认动画名；空串时取第一个动画。</summary>
    public string DefaultAnimation { get; }

    /// <summary>spine-csharp 骨架数据。</summary>
    public SkeletonData Data { get; }

    /// <summary>Atlas 页纹理缓存。</summary>
    public IReadOnlyList<SpinePageTexture> Pages { get; }

    /// <summary>Setup Pose 包围盒，Spine 坐标系（Y 向上）。</summary>
    public Rect SetupBounds { get; }

    /// <summary>骨架内全部动画名。</summary>
    public IReadOnlyList<string> Animations { get; }

    /// <summary>创建一个独立播放器；同一资产的不同实例互不影响骨骼状态。</summary>
    /// <returns>新的播放器。</returns>
    public SpinePlayer CreatePlayer() => new(this);

    /// <summary>按 AtlasRegion 查页纹理。</summary>
    /// <param name="region">spine-csharp 的纹理区域。</param>
    /// <returns>页纹理；找不到返回 null。</param>
    internal SpinePageTexture? PageOf(AtlasRegion region)
    {
        foreach (var page in Pages)
        {
            if (ReferenceEquals(page.Page, region.page))
            {
                return page;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var page in Pages)
        {
            page.Dispose();
        }

        _atlas.Dispose();
    }
}

/// <summary>Atlas 页：整页位图 + 每个区域的裁切结果。</summary>
public sealed class SpinePageTexture : IDisposable
{
    private readonly Dictionary<AtlasRegion, SpineRegionTexture> _regions = new();

    /// <summary>创建页缓存。</summary>
    /// <param name="page">spine-csharp Atlas 页。</param>
    /// <param name="bitmap">整页位图。</param>
    internal SpinePageTexture(AtlasPage page, Bitmap bitmap)
    {
        Page = page;
        Bitmap = bitmap;
    }

    /// <summary>spine-csharp Atlas 页。</summary>
    public AtlasPage Page { get; }

    /// <summary>整页位图。</summary>
    public Bitmap Bitmap { get; }

    /// <summary>注册区域裁切。</summary>
    /// <param name="region">Atlas 区域。</param>
    /// <param name="texture">裁切结果。</param>
    internal void Add(AtlasRegion region, SpineRegionTexture texture) => _regions[region] = texture;

    /// <summary>查区域裁切。</summary>
    /// <param name="region">Atlas 区域。</param>
    /// <returns>裁切结果；找不到返回 null。</returns>
    public SpineRegionTexture? Find(AtlasRegion region) => _regions.TryGetValue(region, out var value) ? value : null;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var texture in _regions.Values)
        {
            texture.Dispose();
        }

        _regions.Clear();
        Bitmap.Dispose();
    }
}

/// <summary>Atlas 区域的裁切位图与旋转信息。</summary>
public sealed class SpineRegionTexture : IDisposable
{
    /// <summary>创建区域贴图。</summary>
    /// <param name="bitmap">裁切位图。</param>
    /// <param name="degrees">Atlas 打包旋转角（0/90）。</param>
    /// <param name="cropWidth">裁切像素宽。</param>
    /// <param name="cropHeight">裁切像素高。</param>
    internal SpineRegionTexture(CroppedBitmap bitmap, int degrees, int cropWidth, int cropHeight)
    {
        Bitmap = bitmap;
        Degrees = degrees;
        CropWidth = cropWidth;
        CropHeight = cropHeight;
    }

    /// <summary>裁切位图。</summary>
    public CroppedBitmap Bitmap { get; }

    /// <summary>Atlas 打包旋转角（0/90）。</summary>
    public int Degrees { get; }

    /// <summary>裁切像素宽。</summary>
    public int CropWidth { get; }

    /// <summary>裁切像素高。</summary>
    public int CropHeight { get; }

    /// <inheritdoc />
    public void Dispose() => Bitmap.Dispose();
}

#endregion
