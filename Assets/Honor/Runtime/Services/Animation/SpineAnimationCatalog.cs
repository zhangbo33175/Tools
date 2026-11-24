/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpineAnimationCatalog.cs
 * author:    云毅
 * created:   2026
 * descrip:   已导入 Spine 资产的内存目录
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace MapEditorTool.Services;

#region 目录

/// <summary>
/// Spine 资产目录。UI 只通过资产名查资产，不直接持有 Atlas / SkeletonData。
/// </summary>
public sealed class SpineAnimationCatalog : IDisposable
{
    private readonly Dictionary<string, SpineAnimationAsset> _assets =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>资产数量。</summary>
    public int Count => _assets.Count;

    /// <summary>全部资产，按名字排序。</summary>
    public IReadOnlyList<SpineAnimationAsset> Assets =>
        _assets.Values.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>全部资产名。</summary>
    public IReadOnlyList<string> Names =>
        _assets.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>注册资产；同名覆盖并释放旧资产。</summary>
    /// <param name="asset">资产。</param>
    public void Register(SpineAnimationAsset asset)
    {
        if (_assets.TryGetValue(asset.Name, out var old))
        {
            old.Dispose();
        }

        _assets[asset.Name] = asset;
    }

    /// <summary>按名字查资产。</summary>
    /// <param name="name">资产名。</param>
    /// <returns>资产；找不到返回 null。</returns>
    public SpineAnimationAsset? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _assets.TryGetValue(name, out var asset) ? asset : null;
    }

    /// <summary>清空并释放全部资产。</summary>
    public void Clear()
    {
        foreach (var asset in _assets.Values)
        {
            asset.Dispose();
        }

        _assets.Clear();
    }

    /// <inheritdoc />
    public void Dispose() => Clear();
}

#endregion
