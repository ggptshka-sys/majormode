namespace MajorMode.Rendering.Export;

/// <summary>
/// Кодер настоящего статического GIF89a (один кадр, глобальная палитра 256 цветов, LZW).
/// </summary>
public static class GifEncoder
{
    /// <param name="bgra">Пиксели BGRA построчно (альфа игнорируется, картинка непрозрачная).</param>
    /// <param name="reservedRgb">Цвета 0xRRGGBB, которые должны попасть в палитру точно (маркеры, текст).</param>
    /// <param name="dither">Floyd–Steinberg для градиентов; на точных цветах палитры шум не появляется.</param>
    public static void Encode(ReadOnlySpan<byte> bgra, int width, int height, Stream output, IEnumerable<int>? reservedRgb = null, bool dither = true)
    {
        if (width is <= 0 or > ushort.MaxValue || height is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Размер GIF от 1 до 65535 пикселей.");
        }

        if (bgra.Length != width * height * 4)
        {
            throw new ArgumentException("Размер буфера не совпадает с размером картинки.", nameof(bgra));
        }

        var palette = PaletteQuantizer.BuildPalette(bgra, reservedRgb ?? [], out var exact);
        var indices = exact || !dither
            ? MapDirect(bgra, width, height, palette)
            : MapDithered(bgra, width, height, palette);

        WriteGif(output, width, height, palette, indices);
    }

    private static byte[] MapDirect(ReadOnlySpan<byte> bgra, int width, int height, int[] palette)
    {
        var mapper = new PaletteMapper(palette);
        var result = new byte[width * height];
        for (var i = 0; i < result.Length; i++)
        {
            var o = i * 4;
            result[i] = mapper.Map(bgra[o + 2], bgra[o + 1], bgra[o]);
        }

        return result;
    }

    private static byte[] MapDithered(ReadOnlySpan<byte> bgra, int width, int height, int[] palette)
    {
        var mapper = new PaletteMapper(palette);
        var result = new byte[width * height];
        var current = new float[(width + 2) * 3];
        var next = new float[(width + 2) * 3];

        for (var y = 0; y < height; y++)
        {
            var leftToRight = (y & 1) == 0;
            Array.Clear(next);
            for (var step = 0; step < width; step++)
            {
                var x = leftToRight ? step : width - 1 - step;
                var o = (y * width + x) * 4;
                var e = (x + 1) * 3;

                var r = Math.Clamp((int)MathF.Round(bgra[o + 2] + current[e]), 0, 255);
                var g = Math.Clamp((int)MathF.Round(bgra[o + 1] + current[e + 1]), 0, 255);
                var b = Math.Clamp((int)MathF.Round(bgra[o] + current[e + 2]), 0, 255);

                var idx = mapper.Map(r, g, b);
                result[y * width + x] = idx;

                var p = mapper.Color(idx);
                var er = r - ((p >> 16) & 0xFF);
                var eg = g - ((p >> 8) & 0xFF);
                var eb = b - (p & 0xFF);
                if (er == 0 && eg == 0 && eb == 0)
                {
                    continue;
                }

                var dir = leftToRight ? 1 : -1;
                Spread(current, e + dir * 3, er, eg, eb, 7f / 16);
                Spread(next, e - dir * 3, er, eg, eb, 3f / 16);
                Spread(next, e, er, eg, eb, 5f / 16);
                Spread(next, e + dir * 3, er, eg, eb, 1f / 16);
            }

            (current, next) = (next, current);
        }

        return result;
    }

    private static void Spread(float[] buffer, int at, int er, int eg, int eb, float k)
    {
        buffer[at] += er * k;
        buffer[at + 1] += eg * k;
        buffer[at + 2] += eb * k;
    }

    private static void WriteGif(Stream output, int width, int height, int[] palette, byte[] indices)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write("GIF89a"u8);

        // Logical Screen Descriptor: глобальная палитра 256 цветов (2^(7+1)).
        w.Write((ushort)width);
        w.Write((ushort)height);
        w.Write((byte)0xF7);
        w.Write((byte)0);
        w.Write((byte)0);

        for (var i = 0; i < 256; i++)
        {
            var c = i < palette.Length ? palette[i] : 0;
            w.Write((byte)((c >> 16) & 0xFF));
            w.Write((byte)((c >> 8) & 0xFF));
            w.Write((byte)(c & 0xFF));
        }

        // Image Descriptor.
        w.Write((byte)0x2C);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)width);
        w.Write((ushort)height);
        w.Write((byte)0);

        const int minCodeSize = 8;
        w.Write((byte)minCodeSize);
        w.Flush();
        LzwEncode(indices, minCodeSize, output);
        output.WriteByte(0x00);
        output.WriteByte(0x3B);
    }

    private static void LzwEncode(byte[] indices, int minCodeSize, Stream output)
    {
        var clearCode = 1 << minCodeSize;
        var endCode = clearCode + 1;
        const int maxCode = 4096;

        var writer = new SubBlockBitWriter(output);
        var dictionary = new Dictionary<int, int>(maxCode);
        var codeSize = minCodeSize + 1;
        var nextCode = endCode + 1;

        writer.Write(clearCode, codeSize);
        if (indices.Length == 0)
        {
            writer.Write(endCode, codeSize);
            writer.Finish();
            return;
        }

        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var k = indices[i];
            var key = (prefix << 8) | k;
            if (dictionary.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }

            WriteData(prefix);
            if (nextCode < maxCode)
            {
                dictionary[key] = nextCode++;
            }
            else
            {
                writer.Write(clearCode, codeSize);
                dictionary.Clear();
                codeSize = minCodeSize + 1;
                nextCode = endCode + 1;
            }

            prefix = k;
        }

        WriteData(prefix);
        writer.Write(endCode, codeSize);
        writer.Finish();

        void WriteData(int value)
        {
            writer.Write(value, codeSize);
            if (nextCode > (1 << codeSize) - 1 && codeSize < 12)
            {
                codeSize++;
            }
        }
    }

    /// <summary>Пишет коды LSB-first и режет поток на подблоки по 255 байт.</summary>
    private sealed class SubBlockBitWriter(Stream output)
    {
        private readonly byte[] _block = new byte[255];
        private int _blockLength;
        private int _bitBuffer;
        private int _bitCount;

        public void Write(int code, int size)
        {
            _bitBuffer |= code << _bitCount;
            _bitCount += size;
            while (_bitCount >= 8)
            {
                PutByte((byte)(_bitBuffer & 0xFF));
                _bitBuffer >>= 8;
                _bitCount -= 8;
            }
        }

        public void Finish()
        {
            if (_bitCount > 0)
            {
                PutByte((byte)(_bitBuffer & 0xFF));
                _bitBuffer = 0;
                _bitCount = 0;
            }

            FlushBlock();
        }

        private void PutByte(byte b)
        {
            _block[_blockLength++] = b;
            if (_blockLength == _block.Length)
            {
                FlushBlock();
            }
        }

        private void FlushBlock()
        {
            if (_blockLength == 0)
            {
                return;
            }

            output.WriteByte((byte)_blockLength);
            output.Write(_block, 0, _blockLength);
            _blockLength = 0;
        }
    }
}
