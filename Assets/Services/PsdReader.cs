/***************************************************************
 * (c) copyright 2026 - 2030, Honor.Runtime
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PsdReader.cs
 * author:    云毅
 * created:   2026
 * descrip:   零依赖 PSD/PSB 图层信息解析
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MapEditorTool.Services;

/// <summary>PSD 图层内的单个通道记录：通道 id、数据长度与在文件中的偏移。</summary>
public sealed class PsdChannelInfo
{
    /// <summary>通道 id：-1 透明蒙版、0 红、1 绿、2 蓝、-2 用户蒙版、-3 真实蒙版。</summary>
    public short Id { get; init; }

    /// <summary>通道数据长度（字节，含 2 字节压缩标记）。</summary>
    public long Length { get; init; }

    /// <summary>通道数据在文件中的起始偏移；图层记录解析完成后统一回填。</summary>
    public long DataOffset { get; set; }
}

/// <summary>解码后的一张图层位图（RGBA8888，行优先）。</summary>
public sealed class PsdLayerBitmap
{
    /// <summary>像素宽。</summary>
    public int Width { get; init; }

    /// <summary>像素高。</summary>
    public int Height { get; init; }

    /// <summary>RGBA 字节，长度 = Width × Height × 4。</summary>
    public byte[] Rgba { get; init; } = Array.Empty<byte>();
}

/// <summary>PSD 单个图层（或图层组）的元信息。</summary>
public sealed class PsdLayerInfo
{
    /// <summary>图层名，优先取 luni 的 Unicode 名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>图层左边界，画布绝对坐标。</summary>
    public int Left { get; set; }

    /// <summary>图层上边界。</summary>
    public int Top { get; set; }

    /// <summary>图层右边界。</summary>
    public int Right { get; set; }

    /// <summary>图层下边界。</summary>
    public int Bottom { get; set; }

    /// <summary>像素宽。</summary>
    public int Width => Math.Max(0, Right - Left);

    /// <summary>像素高。</summary>
    public int Height => Math.Max(0, Bottom - Top);

    /// <summary>是否可见。</summary>
    public bool Visible { get; set; } = true;

    /// <summary>不透明度，0–1。</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>混合模式键。</summary>
    public string BlendMode { get; set; } = string.Empty;

    /// <summary>是否为图层组。</summary>
    public bool IsGroup { get; set; }

    /// <summary>是否为分组结束的边界标记（lsct=3），该记录不计入图层列表。</summary>
    public bool IsGroupEnd { get; set; }

    /// <summary>所属组路径，根层为空串。</summary>
    public string GroupPath { get; set; } = string.Empty;

    /// <summary>在 PSD 中的原始序号，自底向上。</summary>
    public int Index { get; set; }

    /// <summary>层级深度，根层为 0。</summary>
    public int Depth { get; set; }

    /// <summary>是否具备有效像素区域。</summary>
    public bool HasBounds => Width > 0 && Height > 0;

    /// <summary>图层的通道记录（顺序与文件内通道数据一致）。</summary>
    public List<PsdChannelInfo> Channels { get; } = new();

    /// <summary>图层完整路径，组路径加自身名。</summary>
    public string FullPath => string.IsNullOrEmpty(GroupPath) ? Name : $"{GroupPath}/{Name}";
}

/// <summary>PSD 文档元信息。</summary>
public sealed class PsdDocument
{
    /// <summary>画布宽。</summary>
    public int Width { get; set; }

    /// <summary>画布高。</summary>
    public int Height { get; set; }

    /// <summary>通道数。</summary>
    public int Channels { get; set; }

    /// <summary>位深。</summary>
    public int Depth { get; set; }

    /// <summary>颜色模式。</summary>
    public int ColorMode { get; set; }

    /// <summary>是否为 PSB（大文档）格式；通道行计数为 4 字节。</summary>
    public bool IsPsb { get; set; }

    /// <summary>解析得到的图层集合，自底向上。</summary>
    public List<PsdLayerInfo> Layers { get; } = new();
}

/// <summary>
/// PSD/PSB 图层信息读取器。只解析图层记录（名称、边界、可见性、不透明度、组结构），
/// 不读取像素通道，因此无需任何第三方库，解析速度快且内存占用恒定。
/// </summary>
public static class PsdReader
{
    #region 常量

    private const string PsdSignature = "8BPS";

    private const int SectionDividerType = 0x6C736374; // 'lsct'
    private const int UnicodeNameType = 0x6C756E69;   // 'luni'

    #endregion

    #region 公开方法

    /// <summary>
    /// 读取 PSD/PSB 文件的图层元信息。
    /// </summary>
    /// <param name="filePath">PSD 文件绝对路径。</param>
    /// <returns>解析结果。</returns>
    /// <exception cref="InvalidDataException">文件签名或结构不符合 PSD 规范时抛出。</exception>
    public static PsdDocument Read(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var signature = Encoding.ASCII.GetString(ReadBytes(stream, 4));
        if (signature != PsdSignature)
        {
            throw new InvalidDataException($"不是有效的 PSD 文件，签名为 {signature}");
        }

        int version = ReadUInt16BE(stream);
        stream.Seek(6, SeekOrigin.Current);

        var document = new PsdDocument
        {
            Channels = ReadUInt16BE(stream),
            Height = ReadInt32BE(stream),
            Width = ReadInt32BE(stream),
            Depth = ReadUInt16BE(stream),
            ColorMode = ReadUInt16BE(stream),
        };

        bool isPsb = version == 2;
        long lengthFieldSize = isPsb ? 8 : 4;
        document.IsPsb = isPsb;

        // 颜色模式数据段：长度字段固定 4 字节（PSB 也是 4 字节）
        SkipSection(stream, 4);

        // 图像资源段：长度字段固定 4 字节
        SkipSection(stream, 4);

        // 图层与蒙版信息段：PSB 使用 8 字节长度
        if (!TryReadSectionLength(stream, lengthFieldSize, out long layerAndMaskLength) || layerAndMaskLength <= 0)
        {
            return document;
        }

        if (!TryReadSectionLength(stream, lengthFieldSize, out long layerInfoLength) || layerInfoLength <= 0)
        {
            return document;
        }

        var records = new List<PsdLayerInfo>();
        var layers = ReadLayerRecords(stream, isPsb, layerInfoLength, records);
        LocateChannelData(stream, records);
        document.Layers.AddRange(layers);
        return document;
    }

    /// <summary>
    /// 解码单个图层的像素：按通道 id 取 R/G/B 与透明蒙版，合成 RGBA 位图。
    /// 仅支持 8 位/通道；仅支持 RAW(0) 与 RLE(1) 两种通道压缩。
    /// </summary>
    /// <param name="filePath">PSD / PSB 文件绝对路径。</param>
    /// <param name="layer">目标图层。</param>
    /// <param name="isPsb">是否为 PSB（行计数 4 字节）。</param>
    /// <returns>解码结果；图层无像素或压缩方式不支持时返回 null。</returns>
    public static PsdLayerBitmap? ReadLayerPixels(string filePath, PsdLayerInfo layer, bool isPsb)
    {
        if (!layer.HasBounds || layer.Channels.Count == 0)
        {
            return null;
        }

        int width = layer.Width;
        int height = layer.Height;
        var red = new byte[width * height];
        var green = new byte[width * height];
        var blue = new byte[width * height];
        byte[]? alpha = null;
        bool hasColor = false;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        foreach (var channel in layer.Channels)
        {
            if (channel.Id is < -1 or > 2)
            {
                // 用户蒙版 / 真实蒙版：与外观无关，跳过
                continue;
            }

            var plane = DecodeChannel(stream, channel, width, height, isPsb);
            if (plane is null)
            {
                continue;
            }

            switch (channel.Id)
            {
                case 0:
                    red = plane;
                    hasColor = true;
                    break;
                case 1:
                    green = plane;
                    hasColor = true;
                    break;
                case 2:
                    blue = plane;
                    hasColor = true;
                    break;
                case -1:
                    alpha = plane;
                    break;
            }
        }

        if (!hasColor)
        {
            return null;
        }

        var rgba = new byte[width * height * 4];
        for (int i = 0, j = 0; i < width * height; i++, j += 4)
        {
            rgba[j] = red[i];
            rgba[j + 1] = green[i];
            rgba[j + 2] = blue[i];
            rgba[j + 3] = alpha?[i] ?? (byte)255;
        }

        return new PsdLayerBitmap { Width = width, Height = height, Rgba = rgba };
    }

    #endregion

    #region 像素解码

    /// <summary>解码单个通道为 8 位灰阶平面（长度 = 宽 × 高）。</summary>
    private static byte[]? DecodeChannel(Stream stream, PsdChannelInfo channel, int width, int height, bool isPsb)
    {
        if (channel.Length <= 2)
        {
            return null;
        }

        stream.Position = channel.DataOffset;
        int compression = ReadUInt16BE(stream);

        switch (compression)
        {
            case 0:
                {
                    var raw = ReadBytes(stream, width * height);
                    return raw;
                }

            case 1:
                {
                    int rowFieldSize = isPsb ? 4 : 2;
                    var rowCounts = new int[height];
                    for (int row = 0; row < height; row++)
                    {
                        rowCounts[row] = rowFieldSize == 4
                            ? (int)ReadUInt32BE(stream)
                            : ReadUInt16BE(stream);
                    }

                    var plane = new byte[width * height];
                    for (int row = 0; row < height; row++)
                    {
                        int count = rowCounts[row];
                        if (count <= 0)
                        {
                            continue;
                        }

                        var packed = ReadBytes(stream, count);
                        UnpackBitsRow(packed, plane, row * width, width);
                    }

                    return plane;
                }

            default:
                // ZIP / ZIP prediction 暂不支持：交给上层回退到清单切片
                return null;
        }
    }

    /// <summary>PackBits 解包一行到目标平面的指定偏移。</summary>
    private static void UnpackBitsRow(byte[] packed, byte[] plane, int offset, int expected)
    {
        int source = 0;
        int written = 0;
        while (written < expected && source < packed.Length)
        {
            int header = packed[source++];
            if (header >= 128)
            {
                int repeat = 257 - header;
                if (repeat <= 0 || source >= packed.Length)
                {
                    break;
                }

                byte value = packed[source++];
                for (int i = 0; i < repeat && written < expected; i++)
                {
                    plane[offset + written++] = value;
                }
            }
            else
            {
                int literal = header + 1;
                for (int i = 0; i < literal && written < expected && source < packed.Length; i++)
                {
                    plane[offset + written++] = packed[source++];
                }
            }
        }
    }

    #endregion

    #region 图层记录解析

    private static List<PsdLayerInfo> ReadLayerRecords(
        Stream stream,
        bool isPsb,
        long layerInfoLength,
        List<PsdLayerInfo> records)
    {
        var layers = new List<PsdLayerInfo>();
        long sectionEnd = stream.Position + layerInfoLength;

        int rawLayerCount = ReadInt16BE(stream);
        int layerCount = Math.Abs(rawLayerCount);
        if (layerCount == 0)
        {
            return layers;
        }

        var groupStack = new Stack<PsdLayerInfo>();

        for (int index = 0; index < layerCount; index++)
        {
            if (stream.Position >= sectionEnd)
            {
                break;
            }

            var layer = ReadOneLayer(stream, index, isPsb);
            records.Add(layer);
            ApplyGroupStructure(layer, groupStack, layers, index);
        }

        return layers;
    }

    /// <summary>
    /// 图层记录之后紧接通道图像数据：按记录顺序（含图层组结束标记）逐通道回填数据偏移。
    /// 这里只记偏移不读像素，像素由 <see cref="ReadLayerPixels"/> 按需解码。
    /// </summary>
    private static void LocateChannelData(Stream stream, List<PsdLayerInfo> records)
    {
        foreach (var record in records)
        {
            foreach (var channel in record.Channels)
            {
                if (stream.Position >= stream.Length)
                {
                    return;
                }

                channel.DataOffset = stream.Position;
                stream.Seek(channel.Length, SeekOrigin.Current);
            }
        }
    }

    private static PsdLayerInfo ReadOneLayer(Stream stream, int index, bool isPsb)
    {
        int top = ReadInt32BE(stream);
        int left = ReadInt32BE(stream);
        int bottom = ReadInt32BE(stream);
        int right = ReadInt32BE(stream);

        // 先建记录，再采集通道元信息（通道数据在本段之后，这里只记长度）
        var layer = new PsdLayerInfo
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom,
            Index = index,
        };

        int channelCount = ReadUInt16BE(stream);
        for (int channel = 0; channel < channelCount; channel++)
        {
            short channelId = (short)ReadUInt16BE(stream);

            // PSB 的通道数据长度是 8 字节
            long channelLength = isPsb ? (long)ReadUInt64BE(stream) : ReadUInt32BE(stream);
            layer.Channels.Add(new PsdChannelInfo { Id = channelId, Length = channelLength });
        }

        // 签名 '8BIM' + 混合模式键
        ReadBytes(stream, 4);
        string blendMode = Encoding.ASCII.GetString(ReadBytes(stream, 4));

        byte opacity = ReadByte(stream);
        ReadByte(stream); // clipping
        byte flags = ReadByte(stream);
        ReadByte(stream); // filler

        layer.BlendMode = blendMode;
        layer.Opacity = opacity / 255.0;
        layer.Visible = (flags & 0x02) == 0;

        long extraLength = ReadUInt32BE(stream);
        if (extraLength <= 0)
        {
            layer.Name = $"图层 {index}";
            return layer;
        }

        long extraEnd = stream.Position + extraLength;
        ParseExtraData(stream, extraEnd, layer, isPsb);
        stream.Position = extraEnd;
        return layer;
    }

    /// <summary>解析图层附加数据，提取名称、Unicode 名与分组标记。</summary>
    private static void ParseExtraData(Stream stream, long extraEnd, PsdLayerInfo layer, bool isPsb)
    {
        // 蒙版数据长度：PSD 与 PSB 都是 4 字节
        long maskLength = ReadUInt32BE(stream);
        if (maskLength > 0)
        {
            stream.Seek(maskLength, SeekOrigin.Current);
        }

        // 混合范围
        long blendRangesLength = ReadUInt32BE(stream);
        if (blendRangesLength > 0)
        {
            stream.Seek(blendRangesLength, SeekOrigin.Current);
        }

        // 旧式 Pascal 名称，长度补齐到 4 字节
        int nameLength = ReadByte(stream);
        string legacyName = Encoding.ASCII.GetString(ReadBytes(stream, nameLength));
        long consumed = 1 + nameLength;
        long padded = (consumed + 3) / 4 * 4;
        stream.Seek(padded - consumed, SeekOrigin.Current);
        layer.Name = legacyName;

        int? sectionType = null;

        while (stream.Position + 12 <= extraEnd)
        {
            string signature = Encoding.ASCII.GetString(ReadBytes(stream, 4));
            if (signature != "8BIM" && signature != "8B64")
            {
                break;
            }

            string key = Encoding.ASCII.GetString(ReadBytes(stream, 4));
            int dataLength = ReadInt32BE(stream);
            long dataEnd = stream.Position + dataLength;

            switch (key)
            {
                case "luni":
                    layer.Name = ReadUnicodeName(stream, dataLength);
                    break;

                case "lsct":
                case "lsdk":
                    sectionType = ReadInt32BE(stream);
                    break;
            }

            stream.Position = dataEnd;
            if ((dataLength & 1) == 1)
            {
                stream.Seek(1, SeekOrigin.Current);
            }
        }

        // 部分旧版 PSD 用名称标记分组，做一次兜底识别
        if (layer.Name == "</Layer group>")
        {
            sectionType ??= 3;
        }

        if (sectionType is 1 or 2)
        {
            layer.IsGroup = true;
        }
        else if (sectionType == 3)
        {
            layer.IsGroup = false;
            layer.IsGroupEnd = true;
        }

        layer.Name = string.IsNullOrEmpty(layer.Name) ? $"图层 {layer.Index}" : layer.Name;

        if (sectionType == 3)
        {
            layer.GroupPath = string.Empty;
            layer.Depth = 0;
        }
    }

    /// <summary>根据 lsct 标记维护组栈，为每层生成组路径。</summary>
    private static void ApplyGroupStructure(
        PsdLayerInfo layer,
        Stack<PsdLayerInfo> groupStack,
        List<PsdLayerInfo> layers,
        int index)
    {
        // 组结束标记：出栈，自身不作为图层记录
        if (layer.IsGroupEnd || (layer.Name.Length == 0 && !layer.IsGroup))
        {
            if (groupStack.Count > 0)
            {
                groupStack.Pop();
            }

            return;
        }

        layer.Depth = groupStack.Count;
        layer.GroupPath = groupStack.Count == 0
            ? string.Empty
            : BuildGroupPath(groupStack);

        layers.Add(layer);

        if (layer.IsGroup)
        {
            groupStack.Push(layer);
        }
    }

    private static string BuildGroupPath(Stack<PsdLayerInfo> groupStack)
    {
        var names = new List<string>(groupStack.Count);
        foreach (var group in groupStack)
        {
            names.Add(group.Name);
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string ReadUnicodeName(Stream stream, int dataLength)
    {
        if (dataLength < 4)
        {
            return string.Empty;
        }

        int charCount = ReadInt32BE(stream);
        int available = dataLength - 4;
        if (charCount <= 0 || available <= 0)
        {
            return string.Empty;
        }

        // 规范里 luni 的字符数是 UTF-16 字符个数（占 2 字节）；
        // 个别导出器写成字节数，这里按可用长度做一次兜底。
        int byteCount = charCount * 2 <= available ? charCount * 2 : Math.Min(charCount, available);
        var buffer = ReadBytes(stream, byteCount);
        return Encoding.BigEndianUnicode.GetString(buffer).TrimEnd('\0');
    }

    #endregion

    #region 读取辅助

    private static void SkipSection(Stream stream, long lengthFieldSize)
    {
        if (!TryReadSectionLength(stream, lengthFieldSize, out long length) || length <= 0)
        {
            return;
        }

        stream.Seek(length, SeekOrigin.Current);
    }

    private static bool TryReadSectionLength(Stream stream, long lengthFieldSize, out long length)
    {
        length = 0;
        if (stream.Position >= stream.Length)
        {
            return false;
        }

        length = lengthFieldSize == 8 ? (long)ReadUInt64BE(stream) : ReadUInt32BE(stream);
        if (stream.Position + length > stream.Length)
        {
            return false;
        }

        return true;
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        var buffer = new byte[count];
        int read = 0;
        while (read < count)
        {
            int chunk = stream.Read(buffer, read, count - read);
            if (chunk <= 0)
            {
                throw new EndOfStreamException("PSD 数据在读取过程中提前结束");
            }

            read += chunk;
        }

        return buffer;
    }

    private static byte ReadByte(Stream stream)
    {
        int value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException("PSD 数据在读取过程中提前结束");
        }

        return (byte)value;
    }

    private static int ReadUInt16BE(Stream stream)
    {
        var buffer = ReadBytes(stream, 2);
        return (buffer[0] << 8) | buffer[1];
    }

    private static int ReadInt16BE(Stream stream)
    {
        var buffer = ReadBytes(stream, 2);
        short value = (short)((buffer[0] << 8) | buffer[1]);
        return value;
    }

    private static int ReadInt32BE(Stream stream)
    {
        var buffer = ReadBytes(stream, 4);
        return (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
    }

    private static uint ReadUInt32BE(Stream stream)
    {
        var buffer = ReadBytes(stream, 4);
        return (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
    }

    private static ulong ReadUInt64BE(Stream stream)
    {
        var buffer = ReadBytes(stream, 8);
        ulong value = 0;
        foreach (var item in buffer)
        {
            value = (value << 8) | item;
        }

        return value;
    }

    #endregion
}
