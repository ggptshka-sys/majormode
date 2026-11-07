namespace MajorMode.Tests.Rendering;

/// <summary>Минимальный независимый декодер GIF (один кадр, глобальная палитра) для проверки кодера.</summary>
internal static class TestGifDecoder
{
    public sealed record Decoded(string Signature, int Width, int Height, int[] Rgb, int FrameCount);

    public static Decoded Decode(byte[] data)
    {
        var pos = 0;
        var signature = System.Text.Encoding.ASCII.GetString(data, 0, 6);
        pos = 6;
        var width = data[pos] | (data[pos + 1] << 8);
        var height = data[pos + 2] | (data[pos + 3] << 8);
        var packed = data[pos + 4];
        pos += 7;

        int[] palette = [];
        if ((packed & 0x80) != 0)
        {
            var size = 1 << ((packed & 7) + 1);
            palette = new int[size];
            for (var i = 0; i < size; i++)
            {
                palette[i] = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
                pos += 3;
            }
        }

        int[]? pixels = null;
        var frames = 0;
        while (pos < data.Length)
        {
            var b = data[pos++];
            if (b == 0x3B)
            {
                break;
            }

            if (b == 0x21)
            {
                pos++;
                while (data[pos] != 0)
                {
                    pos += data[pos] + 1;
                }

                pos++;
                continue;
            }

            if (b != 0x2C)
            {
                throw new InvalidDataException($"Неожиданный блок 0x{b:X2}");
            }

            frames++;
            pos += 8;
            var imagePacked = data[pos++];
            if ((imagePacked & 0x80) != 0)
            {
                throw new InvalidDataException("Локальная палитра не ожидается.");
            }

            var minCodeSize = data[pos++];
            using var ms = new MemoryStream();
            while (data[pos] != 0)
            {
                var len = data[pos++];
                ms.Write(data, pos, len);
                pos += len;
            }

            pos++;
            var indices = Lzw(ms.ToArray(), minCodeSize, width * height);
            pixels = indices.Select(i => palette[i]).ToArray();
        }

        return new Decoded(signature, width, height, pixels ?? [], frames);
    }

    private static byte[] Lzw(byte[] data, int minCodeSize, int pixelCount)
    {
        var clear = 1 << minCodeSize;
        var end = clear + 1;
        var output = new List<byte>(pixelCount);
        var prefix = new int[4096];
        var suffix = new byte[4096];
        var length = new int[4096];
        for (var i = 0; i < clear; i++)
        {
            suffix[i] = (byte)i;
            length[i] = 1;
            prefix[i] = -1;
        }

        var codeSize = minCodeSize + 1;
        var next = end + 1;
        var old = -1;
        var bitPos = 0;
        var stack = new byte[4097];

        while (true)
        {
            if (bitPos + codeSize > data.Length * 8)
            {
                throw new InvalidDataException("Поток LZW оборван без кода конца.");
            }

            var code = 0;
            for (var i = 0; i < codeSize; i++, bitPos++)
            {
                if ((data[bitPos >> 3] & (1 << (bitPos & 7))) != 0)
                {
                    code |= 1 << i;
                }
            }

            if (code == clear)
            {
                codeSize = minCodeSize + 1;
                next = end + 1;
                old = -1;
                continue;
            }

            if (code == end)
            {
                break;
            }

            int firstChar;
            if (old == -1)
            {
                output.Add(suffix[code]);
                old = code;
                continue;
            }

            int cur;
            if (code < next)
            {
                cur = code;
            }
            else if (code == next)
            {
                cur = old;
            }
            else
            {
                throw new InvalidDataException($"Код {code} больше ожидаемого {next}.");
            }

            var sp = 0;
            var c = cur;
            while (c >= 0)
            {
                stack[sp++] = suffix[c];
                c = prefix[c];
            }

            firstChar = stack[sp - 1];
            for (var i = sp - 1; i >= 0; i--)
            {
                output.Add(stack[i]);
            }

            if (code == next)
            {
                output.Add((byte)firstChar);
            }

            if (next < 4096)
            {
                prefix[next] = old;
                suffix[next] = (byte)firstChar;
                length[next] = length[old] + 1;
                next++;
                if (next == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }

            old = code;
        }

        if (output.Count != pixelCount)
        {
            throw new InvalidDataException($"Пикселей {output.Count}, ожидалось {pixelCount}.");
        }

        return output.ToArray();
    }
}
