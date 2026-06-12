using System;
using System.Collections.Generic;
using UnityEngine;

public class GifAnimationData
{
    public int width;
    public int height;
    public List<GifFrameData> frames = new List<GifFrameData>();
}

public class GifFrameData
{
    public Color32[] pixels;
    public float delaySeconds;
}

public static class GifAnimationDecoder
{
    private struct GraphicControl
    {
        public int disposalMethod;
        public bool hasTransparency;
        public int transparentIndex;
        public float delaySeconds;
    }

    public static GifAnimationData Decode(byte[] bytes, float defaultFrameDuration = 0.1f)
    {
        if (bytes == null || bytes.Length < 13)
        {
            throw new ArgumentException("GIF data is empty or too short.");
        }

        defaultFrameDuration = defaultFrameDuration > 0f ? defaultFrameDuration : 0.1f;
        int index = 0;
        string header = ReadString(bytes, ref index, 6);
        if (!header.StartsWith("GIF", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid GIF header.");
        }

        int width = ReadUInt16(bytes, ref index);
        int height = ReadUInt16(bytes, ref index);
        byte packed = ReadByte(bytes, ref index);
        bool hasGlobalColorTable = (packed & 0x80) != 0;
        int globalColorTableSize = 1 << ((packed & 0x07) + 1);
        ReadByte(bytes, ref index);
        ReadByte(bytes, ref index);

        Color32[] globalColorTable = hasGlobalColorTable
            ? ReadColorTable(bytes, ref index, globalColorTableSize)
            : null;

        GifAnimationData animation = new GifAnimationData
        {
            width = width,
            height = height
        };

        Color32[] canvas = new Color32[width * height];
        GraphicControl graphicControl = CreateDefaultGraphicControl(defaultFrameDuration);

        while (index < bytes.Length)
        {
            byte blockType = ReadByte(bytes, ref index);
            if (blockType == 0x3B)
            {
                break;
            }

            if (blockType == 0x21)
            {
                ReadExtension(bytes, ref index, ref graphicControl, defaultFrameDuration);
                continue;
            }

            if (blockType != 0x2C)
            {
                throw new ArgumentException($"Unexpected GIF block type: 0x{blockType:X2}");
            }

            ReadImageBlock(bytes, ref index, globalColorTable, animation, ref canvas, graphicControl);
            graphicControl = CreateDefaultGraphicControl(defaultFrameDuration);
        }

        return animation;
    }

    private static GraphicControl CreateDefaultGraphicControl(float defaultFrameDuration)
    {
        return new GraphicControl
        {
            disposalMethod = 0,
            hasTransparency = false,
            transparentIndex = -1,
            delaySeconds = defaultFrameDuration
        };
    }

    private static void ReadExtension(byte[] bytes, ref int index, ref GraphicControl graphicControl, float defaultFrameDuration)
    {
        byte label = ReadByte(bytes, ref index);
        if (label != 0xF9)
        {
            SkipSubBlocks(bytes, ref index);
            return;
        }

        int blockSize = ReadByte(bytes, ref index);
        if (blockSize != 4)
        {
            index += blockSize;
            ReadByte(bytes, ref index);
            return;
        }

        byte packed = ReadByte(bytes, ref index);
        int delayHundredths = ReadUInt16(bytes, ref index);
        int transparentIndex = ReadByte(bytes, ref index);
        ReadByte(bytes, ref index);

        graphicControl = new GraphicControl
        {
            disposalMethod = (packed >> 2) & 0x07,
            hasTransparency = (packed & 0x01) != 0,
            transparentIndex = transparentIndex,
            delaySeconds = delayHundredths > 0 ? delayHundredths * 0.01f : defaultFrameDuration
        };
    }

    private static void ReadImageBlock(
        byte[] bytes,
        ref int index,
        Color32[] globalColorTable,
        GifAnimationData animation,
        ref Color32[] canvas,
        GraphicControl graphicControl)
    {
        int left = ReadUInt16(bytes, ref index);
        int top = ReadUInt16(bytes, ref index);
        int width = ReadUInt16(bytes, ref index);
        int height = ReadUInt16(bytes, ref index);
        byte packed = ReadByte(bytes, ref index);
        bool hasLocalColorTable = (packed & 0x80) != 0;
        bool isInterlaced = (packed & 0x40) != 0;
        int localColorTableSize = 1 << ((packed & 0x07) + 1);
        Color32[] colorTable = hasLocalColorTable
            ? ReadColorTable(bytes, ref index, localColorTableSize)
            : globalColorTable;

        if (colorTable == null)
        {
            throw new ArgumentException("GIF image has no color table.");
        }

        int lzwMinimumCodeSize = ReadByte(bytes, ref index);
        byte[] imageData = ReadSubBlocks(bytes, ref index);
        byte[] indices = DecodeLzw(imageData, lzwMinimumCodeSize, width * height);
        if (isInterlaced)
        {
            indices = Deinterlace(indices, width, height);
        }

        Color32[] restoreCanvas = graphicControl.disposalMethod == 3
            ? CopyCanvas(canvas)
            : null;

        DrawFrame(canvas, animation.width, animation.height, left, top, width, height, indices, colorTable, graphicControl);
        animation.frames.Add(new GifFrameData
        {
            pixels = CopyCanvas(canvas),
            delaySeconds = graphicControl.delaySeconds
        });

        if (graphicControl.disposalMethod == 2)
        {
            ClearRect(canvas, animation.width, animation.height, left, top, width, height);
        }
        else if (graphicControl.disposalMethod == 3 && restoreCanvas != null)
        {
            canvas = restoreCanvas;
        }
    }

    private static void DrawFrame(
        Color32[] canvas,
        int canvasWidth,
        int canvasHeight,
        int left,
        int top,
        int width,
        int height,
        byte[] indices,
        Color32[] colorTable,
        GraphicControl graphicControl)
    {
        int sourceIndex = 0;
        for (int y = 0; y < height; y++)
        {
            int destY = top + y;
            for (int x = 0; x < width; x++)
            {
                int destX = left + x;
                if (sourceIndex >= indices.Length)
                {
                    return;
                }

                int colorIndex = indices[sourceIndex++];
                if (graphicControl.hasTransparency && colorIndex == graphicControl.transparentIndex)
                {
                    continue;
                }

                if (colorIndex < 0 || colorIndex >= colorTable.Length)
                {
                    continue;
                }

                if (destX < 0 || destX >= canvasWidth || destY < 0 || destY >= canvasHeight)
                {
                    continue;
                }

                canvas[destY * canvasWidth + destX] = colorTable[colorIndex];
            }
        }
    }

    private static void ClearRect(Color32[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height)
    {
        Color32 transparent = new Color32(0, 0, 0, 0);
        for (int y = 0; y < height; y++)
        {
            int destY = top + y;
            if (destY < 0 || destY >= canvasHeight)
            {
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                int destX = left + x;
                if (destX < 0 || destX >= canvasWidth)
                {
                    continue;
                }

                canvas[destY * canvasWidth + destX] = transparent;
            }
        }
    }

    private static byte[] Deinterlace(byte[] indices, int width, int height)
    {
        byte[] result = new byte[width * height];
        int source = 0;
        int[] starts = { 0, 4, 2, 1 };
        int[] steps = { 8, 8, 4, 2 };

        for (int pass = 0; pass < starts.Length; pass++)
        {
            for (int y = starts[pass]; y < height; y += steps[pass])
            {
                int dest = y * width;
                for (int x = 0; x < width && source < indices.Length; x++)
                {
                    result[dest + x] = indices[source++];
                }
            }
        }

        return result;
    }

    private static byte[] DecodeLzw(byte[] data, int minimumCodeSize, int expectedSize)
    {
        List<byte> output = new List<byte>(expectedSize);
        int clearCode = 1 << minimumCodeSize;
        int endCode = clearCode + 1;
        int nextCode = endCode + 1;
        int codeSize = minimumCodeSize + 1;
        int bitIndex = 0;
        int previousCode = -1;
        Dictionary<int, byte[]> dictionary = CreateInitialDictionary(clearCode);

        while (bitIndex < data.Length * 8)
        {
            int code = ReadCode(data, ref bitIndex, codeSize);
            if (code < 0)
            {
                break;
            }

            if (code == clearCode)
            {
                dictionary = CreateInitialDictionary(clearCode);
                codeSize = minimumCodeSize + 1;
                nextCode = endCode + 1;
                previousCode = -1;
                continue;
            }

            if (code == endCode)
            {
                break;
            }

            byte[] entry;
            if (!dictionary.TryGetValue(code, out entry))
            {
                if (code == nextCode && previousCode >= 0 && dictionary.TryGetValue(previousCode, out byte[] previousEntryForSpecialCase))
                {
                    entry = AppendByte(previousEntryForSpecialCase, previousEntryForSpecialCase[0]);
                }
                else
                {
                    break;
                }
            }

            output.AddRange(entry);

            if (previousCode >= 0 && dictionary.TryGetValue(previousCode, out byte[] previousEntry) && nextCode < 4096)
            {
                dictionary[nextCode] = AppendByte(previousEntry, entry[0]);
                nextCode++;
                if (nextCode == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previousCode = code;
            if (output.Count >= expectedSize)
            {
                break;
            }
        }

        if (output.Count < expectedSize)
        {
            output.AddRange(new byte[expectedSize - output.Count]);
        }

        if (output.Count > expectedSize)
        {
            output.RemoveRange(expectedSize, output.Count - expectedSize);
        }

        return output.ToArray();
    }

    private static Dictionary<int, byte[]> CreateInitialDictionary(int clearCode)
    {
        Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
        for (int i = 0; i < clearCode; i++)
        {
            dictionary[i] = new[] { (byte)i };
        }

        return dictionary;
    }

    private static byte[] AppendByte(byte[] source, byte value)
    {
        byte[] result = new byte[source.Length + 1];
        Buffer.BlockCopy(source, 0, result, 0, source.Length);
        result[result.Length - 1] = value;
        return result;
    }

    private static int ReadCode(byte[] data, ref int bitIndex, int codeSize)
    {
        if (bitIndex + codeSize > data.Length * 8)
        {
            return -1;
        }

        int code = 0;
        for (int i = 0; i < codeSize; i++)
        {
            int byteIndex = (bitIndex + i) / 8;
            int bitOffset = (bitIndex + i) % 8;
            if ((data[byteIndex] & (1 << bitOffset)) != 0)
            {
                code |= 1 << i;
            }
        }

        bitIndex += codeSize;
        return code;
    }

    private static byte[] ReadSubBlocks(byte[] bytes, ref int index)
    {
        List<byte> data = new List<byte>();
        while (index < bytes.Length)
        {
            int blockSize = ReadByte(bytes, ref index);
            if (blockSize == 0)
            {
                break;
            }

            for (int i = 0; i < blockSize; i++)
            {
                data.Add(ReadByte(bytes, ref index));
            }
        }

        return data.ToArray();
    }

    private static void SkipSubBlocks(byte[] bytes, ref int index)
    {
        while (index < bytes.Length)
        {
            int blockSize = ReadByte(bytes, ref index);
            if (blockSize == 0)
            {
                break;
            }

            index += blockSize;
        }
    }

    private static Color32[] ReadColorTable(byte[] bytes, ref int index, int size)
    {
        Color32[] table = new Color32[size];
        for (int i = 0; i < size; i++)
        {
            byte r = ReadByte(bytes, ref index);
            byte g = ReadByte(bytes, ref index);
            byte b = ReadByte(bytes, ref index);
            table[i] = new Color32(r, g, b, 255);
        }

        return table;
    }

    private static Color32[] CopyCanvas(Color32[] canvas)
    {
        Color32[] copy = new Color32[canvas.Length];
        Array.Copy(canvas, copy, canvas.Length);
        return copy;
    }

    private static string ReadString(byte[] bytes, ref int index, int length)
    {
        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)ReadByte(bytes, ref index);
        }

        return new string(chars);
    }

    private static int ReadUInt16(byte[] bytes, ref int index)
    {
        int value = bytes[index] | (bytes[index + 1] << 8);
        index += 2;
        return value;
    }

    private static byte ReadByte(byte[] bytes, ref int index)
    {
        if (index >= bytes.Length)
        {
            throw new ArgumentException("Unexpected end of GIF data.");
        }

        return bytes[index++];
    }
}
