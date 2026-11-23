/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PngWriter.cs
 * author:    云毅
 * created:   2026
 * descrip:   零依赖 RGBA PNG 编码（自裁切片与整幅底图都走这里）
 ***************************************************************/

using System;
using System.IO;
using System.IO.Compression;

namespace MapEditorTool.Services;

/// <summary>
/// 最小 PNG 编码器：只处理 8 位 RGBA（颜色类型 6）、无隔行、过滤方式全 0。
/// 不引入第三方库，配合 <see cref="PsdReader"/> 的解码结果直接落盘。
/// </summary>
public static class PngWriter
{
    #region 常量

    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private static readonly uint[] CrcTable = BuildCrcTable();

    #endregion

    #region 公开方法

    /// <summary>把 RGBA 像素写成 PNG 文件。</summary>
    /// <param name="path">输出文件绝对路径。</param>
    /// <param name="width">像素宽。</param>
    /// <param name="height">像素高。</param>
    /// <param name="rgba">RGBA 字节，长度须为 宽 × 高 × 4。</param>
    public static void WriteRgba(string path, int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "PNG 尺寸必须为正数");
        }

        int stride = width * 4;
        if (rgba.Length < stride * height)
        {
            throw new ArgumentException("像素数据长度不足", nameof(rgba));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteRgba(stream, width, height, rgba);
    }

    /// <summary>把 RGBA 像素编码成 PNG 字节（不落盘，用于导入时内存预览）。</summary>
    /// <param name="width">像素宽。</param>
    /// <param name="height">像素高。</param>
    /// <param name="rgba">RGBA 字节，长度须为 宽 × 高 × 4。</param>
    /// <returns>PNG 文件字节。</returns>
    public static byte[] EncodeRgba(int width, int height, byte[] rgba)
    {
        using var memory = new MemoryStream();
        WriteRgba(memory, width, height, rgba);
        return memory.ToArray();
    }

    /// <summary>把 RGBA 像素写成 PNG 流（调用方负责流的生命周期）。</summary>
    /// <param name="stream">目标流（从当前位置写起）。</param>
    /// <param name="width">像素宽。</param>
    /// <param name="height">像素高。</param>
    /// <param name="rgba">RGBA 字节，长度须为 宽 × 高 × 4。</param>
    public static void WriteRgba(Stream stream, int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "PNG 尺寸必须为正数");
        }

        int stride = width * 4;
        if (rgba.Length < stride * height)
        {
            throw new ArgumentException("像素数据长度不足", nameof(rgba));
        }

        stream.Write(Signature);

        var header = new byte[13];
        WriteUInt32Be(header, 0, (uint)width);
        WriteUInt32Be(header, 4, (uint)height);
        header[8] = 8;   // 位深
        header[9] = 6;   // 颜色类型：真彩 + alpha
        header[10] = 0;  // 压缩方式
        header[11] = 0;  // 过滤方式
        header[12] = 0;  // 隔行：无
        WriteChunk(stream, "IHDR", header);

        // 每行前置一个过滤字节 0（None），再交给 zlib
        var raw = new byte[(stride + 1) * height];
        for (int row = 0; row < height; row++)
        {
            int source = row * stride;
            int target = row * (stride + 1);
            raw[target] = 0;
            Buffer.BlockCopy(rgba, source, raw, target + 1, stride);
        }

        byte[] compressed;
        using (var memory = new MemoryStream())
        {
            using (var zlib = new ZLibStream(memory, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            compressed = memory.ToArray();
        }

        WriteChunk(stream, "IDAT", compressed);
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    #endregion

    #region 内部实现

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteUInt32Be(length, 0, (uint)data.Length);
        stream.Write(length);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        crc ^= 0xFFFFFFFF;

        var crcBytes = new byte[4];
        WriteUInt32Be(crcBytes, 0, crc);
        stream.Write(crcBytes);
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }

    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    #endregion
}
