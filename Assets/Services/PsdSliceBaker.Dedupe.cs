/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdSliceBaker.Dedupe.cs
 * author:    云毅
 * created:   2026
 * descrip:   切片去重：按尺寸索引、0° 指纹与 90° 旋转匹配已生成切片
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Services;
public static partial class PsdSliceBaker
{
    #region 切片去重

    /// <summary>已写 / 已生成过的一张切片：路径 + 0° 像素指纹。</summary>
    private readonly record struct WrittenSlice(string Relative, string Hash0);

    /// <summary>尺寸键（宽高对），用于「只有尺寸对得上才去算旋转」。</summary>
    private readonly record struct SizeKey(int Width, int Height);

    /// <summary>一次去重命中的结果。</summary>
    private readonly record struct DuplicateMatch(string Relative, int Rotation);

    private static void AddSize(Dictionary<SizeKey, List<string>> bySize, int width, int height, string relative)
    {
        var key = new SizeKey(width, height);
        if (!bySize.TryGetValue(key, out var list))
        {
            list = new List<string>();
            bySize[key] = list;
        }

        list.Add(relative);
    }

    /// <summary>
    /// 判重：先按 0° 指纹查同尺寸候选项；只有尺寸能对上（含宽高互换）才去算 90 / 180 / 270 的指纹，
    /// 命中即复用同一张 PNG，并记录「本层相对复用文件转多少度」。
    /// <para>
    /// 大多数唯一图只做一次哈希；同尺寸（如所有 84×205 的道具）也只在「候选集合」内多做三次旋转哈希，
    /// 不会把 578 层全部旋转四遍。
    /// </para>
    /// </summary>
    private static DuplicateMatch? ResolveDuplicate(
        byte[] rgba,
        int width,
        int height,
        IReadOnlyDictionary<string, WrittenSlice> written,
        IReadOnlyDictionary<SizeKey, List<string>> bySize)
    {
        var hash0 = HashPixels(rgba, width, height);
        var sameSize = bySize.TryGetValue(new SizeKey(width, height), out var straight) ? straight : null;
        var swappedSize = bySize.TryGetValue(new SizeKey(height, width), out var swapped) ? swapped : null;
        if (sameSize is null && swappedSize is null)
        {
            return null;
        }

        // 0° 同尺寸：最常见，直接比指纹
        foreach (var relative in sameSize ?? Enumerable.Empty<string>())
        {
            if (string.Equals(written[relative].Hash0, hash0, StringComparison.Ordinal))
            {
                return new DuplicateMatch(relative, 0);
            }
        }

        // 旋转候选：把本层转 90 / 180 / 270 再比指纹（尺寸互换只可能来自 90 / 270）
        var hashes = new[] { hash0, null, null, null };
        for (int turn = 1; turn < 4; turn++)
        {
            if ((turn == 1 || turn == 3) && swappedSize is null)
            {
                continue;
            }

            if (turn == 2 && sameSize is null)
            {
                continue;
            }

            var rotated = RotatePixels(rgba, width, height, turn);
            hashes[turn] = HashPixels(rotated.Rgba, rotated.Width, rotated.Height);
        }

        var candidates = new List<string>();
        if (sameSize is not null)
        {
            candidates.AddRange(sameSize);
        }

        if (swappedSize is not null)
        {
            candidates.AddRange(swappedSize);
        }

        foreach (var relative in candidates)
        {
            var slice = written[relative];
            for (int turn = 1; turn < 4; turn++)
            {
                if (hashes[turn] is null || !string.Equals(slice.Hash0, hashes[turn], StringComparison.Ordinal))
                {
                    continue;
                }

                // hashes[turn] 是「把本层转 turn 个 90° 后得到已落盘那张图」，
                // 反过来说：显示时要拿已落盘的图转 (4 - turn) 个 90° 才是本层的样子
                int rotation = ((4 - turn) % 4) * 90;
                return new DuplicateMatch(relative, rotation);
            }
        }

        return null;
    }

    /// <summary>
    /// 顺时针旋转 RGBA 缓冲：turn=1 → 90°，2 → 180°，3 → 270°（图像坐标系 y 向下，与引擎顺时针一致）。
    /// </summary>
    private static (byte[] Rgba, int Width, int Height) RotatePixels(byte[] rgba, int width, int height, int turn)
    {
        turn %= 4;
        if (turn == 0)
        {
            return (rgba, width, height);
        }

        if (turn == 2)
        {
            var flipped = new byte[rgba.Length];
            int pixels = width * height;
            for (int i = 0; i < pixels; i++)
            {
                int source = i * 4;
                int target = (pixels - 1 - i) * 4;
                flipped[target] = rgba[source];
                flipped[target + 1] = rgba[source + 1];
                flipped[target + 2] = rgba[source + 2];
                flipped[target + 3] = rgba[source + 3];
            }

            return (flipped, width, height);
        }

        int newWidth = height;
        int newHeight = width;
        var result = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int source = (y * width + x) * 4;
                // 顺时针 90°：(x, y) → (height - 1 - y, x)
                int targetX = turn == 1 ? height - 1 - y : y;
                int targetY = turn == 1 ? x : width - 1 - x;
                int target = (targetY * newWidth + targetX) * 4;
                result[target] = rgba[source];
                result[target + 1] = rgba[source + 1];
                result[target + 2] = rgba[source + 2];
                result[target + 3] = rgba[source + 3];
            }
        }

        return (result, newWidth, newHeight);
    }

    /// <summary>像素指纹：宽 + 高 + RGBA 逐字节的 SHA-256。</summary>
    private static string HashPixels(byte[] rgba, int width, int height)
    {
        var payload = new byte[8 + rgba.Length];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), width);
        BitConverter.TryWriteBytes(payload.AsSpan(4, 4), height);
        Buffer.BlockCopy(rgba, 0, payload, 8, rgba.Length);
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    #endregion
}
