namespace MajorMode.Rendering.Export;

/// <summary>
/// Палитра до 256 цветов: зарезервированные цвета (маркеры, текст) попадают точно,
/// остальное — median cut по минимальной сумме квадратов ошибок с учетом числа пикселей.
/// Цвета хранятся как 0xRRGGBB.
/// </summary>
internal static class PaletteQuantizer
{
    public const int MaxColors = 256;

    public static int[] BuildPalette(ReadOnlySpan<byte> bgra, IEnumerable<int> reserved, out bool isExact)
    {
        var histogram = BuildHistogram(bgra);
        var reservedSet = new List<int>();
        foreach (var c in reserved)
        {
            if (!reservedSet.Contains(c) && reservedSet.Count < MaxColors / 4)
            {
                reservedSet.Add(c);
            }
        }

        var extra = histogram.Keys.Where(c => !reservedSet.Contains(c)).ToList();
        if (extra.Count + reservedSet.Count <= MaxColors)
        {
            isExact = true;
            return reservedSet.Concat(extra).ToArray();
        }

        isExact = false;
        var entries = histogram
            .Where(kv => !reservedSet.Contains(kv.Key))
            .Select(kv => new Entry(kv.Key, kv.Value))
            .ToArray();
        var boxes = MedianCut(entries, MaxColors - reservedSet.Count);
        var palette = new List<int>(reservedSet);
        foreach (var color in boxes)
        {
            if (!palette.Contains(color))
            {
                palette.Add(color);
            }
        }

        return palette.ToArray();
    }

    private static Dictionary<int, int> BuildHistogram(ReadOnlySpan<byte> bgra)
    {
        var histogram = new Dictionary<int, int>();
        var count = bgra.Length / 4;
        var prev = -1;
        var run = 0;
        for (var i = 0; i < count; i++)
        {
            var o = i * 4;
            var rgb = (bgra[o + 2] << 16) | (bgra[o + 1] << 8) | bgra[o];
            if (rgb == prev)
            {
                run++;
                continue;
            }

            if (run > 0)
            {
                CollectionsMarshalAdd(histogram, prev, run);
            }

            prev = rgb;
            run = 1;
        }

        if (run > 0)
        {
            CollectionsMarshalAdd(histogram, prev, run);
        }

        return histogram;
    }

    private static void CollectionsMarshalAdd(Dictionary<int, int> histogram, int key, int add)
    {
        ref var slot = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(histogram, key, out _);
        slot += add;
    }

    private static List<int> MedianCut(Entry[] entries, int target)
    {
        var boxes = new List<Box> { new(entries, 0, entries.Length) };
        while (boxes.Count < target)
        {
            Box? worst = null;
            foreach (var b in boxes)
            {
                if (b.Length > 1 && (worst is null || b.Error > worst.Error))
                {
                    worst = b;
                }
            }

            if (worst is null || worst.Error <= 0)
            {
                break;
            }

            boxes.Remove(worst);
            var (a, b2) = worst.Split();
            boxes.Add(a);
            boxes.Add(b2);
        }

        return boxes.Select(b => b.Mean).ToList();
    }

    private readonly record struct Entry(int Rgb, int Count)
    {
        public int R => (Rgb >> 16) & 0xFF;

        public int G => (Rgb >> 8) & 0xFF;

        public int B => Rgb & 0xFF;

        public int Channel(int c) => c switch { 0 => R, 1 => G, _ => B };
    }

    private sealed class Box
    {
        private readonly Entry[] _entries;

        public Box(Entry[] entries, int start, int end)
        {
            _entries = entries;
            Start = start;
            End = end;

            double w = 0, sr = 0, sg = 0, sb = 0, sq = 0;
            for (var i = start; i < end; i++)
            {
                var e = entries[i];
                w += e.Count;
                sr += (double)e.R * e.Count;
                sg += (double)e.G * e.Count;
                sb += (double)e.B * e.Count;
                sq += ((double)e.R * e.R + (double)e.G * e.G + (double)e.B * e.B) * e.Count;
            }

            Weight = w;
            Error = w > 0 ? sq - (sr * sr + sg * sg + sb * sb) / w : 0;
            Mean = w > 0
                ? (Clamp(sr / w) << 16) | (Clamp(sg / w) << 8) | Clamp(sb / w)
                : 0;
        }

        public int Start { get; }

        public int End { get; }

        public int Length => End - Start;

        public double Weight { get; }

        public double Error { get; }

        public int Mean { get; }

        public (Box, Box) Split()
        {
            var channel = WidestChannel();
            Array.Sort(_entries, Start, Length, Comparer<Entry>.Create((x, y) =>
            {
                var d = x.Channel(channel).CompareTo(y.Channel(channel));
                return d != 0 ? d : x.Rgb.CompareTo(y.Rgb);
            }));

            var half = Weight / 2;
            double acc = 0;
            var cut = Start + 1;
            for (var i = Start; i < End - 1; i++)
            {
                acc += _entries[i].Count;
                cut = i + 1;
                if (acc >= half)
                {
                    break;
                }
            }

            return (new Box(_entries, Start, cut), new Box(_entries, cut, End));
        }

        private int WidestChannel()
        {
            var best = 0;
            var bestRange = -1;
            for (var c = 0; c < 3; c++)
            {
                int min = 255, max = 0;
                for (var i = Start; i < End; i++)
                {
                    var v = _entries[i].Channel(c);
                    if (v < min)
                    {
                        min = v;
                    }

                    if (v > max)
                    {
                        max = v;
                    }
                }

                if (max - min > bestRange)
                {
                    bestRange = max - min;
                    best = c;
                }
            }

            return best;
        }

        private static int Clamp(double v) => (int)Math.Clamp(Math.Round(v), 0, 255);
    }
}

/// <summary>Поиск ближайшего цвета палитры: точное совпадение или кэш по 6 битам на канал.</summary>
internal sealed class PaletteMapper
{
    private readonly int[] _palette;
    private readonly Dictionary<int, byte> _exact = new();
    private readonly short[] _cache = new short[1 << 18];

    public PaletteMapper(int[] palette)
    {
        if (palette.Length is 0 or > PaletteQuantizer.MaxColors)
        {
            throw new ArgumentOutOfRangeException(nameof(palette));
        }

        _palette = palette;
        for (var i = 0; i < palette.Length; i++)
        {
            _exact.TryAdd(palette[i], (byte)i);
        }

        Array.Fill(_cache, (short)-1);
    }

    public int Color(int index) => _palette[index];

    public byte Map(int r, int g, int b)
    {
        var rgb = (r << 16) | (g << 8) | b;
        if (_exact.TryGetValue(rgb, out var idx))
        {
            return idx;
        }

        var key = ((r >> 2) << 12) | ((g >> 2) << 6) | (b >> 2);
        var cached = _cache[key];
        if (cached >= 0)
        {
            return (byte)cached;
        }

        var cr = (r & ~3) | 2;
        var cg = (g & ~3) | 2;
        var cb = (b & ~3) | 2;
        var best = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < _palette.Length; i++)
        {
            var p = _palette[i];
            var dr = ((p >> 16) & 0xFF) - cr;
            var dg = ((p >> 8) & 0xFF) - cg;
            var db = (p & 0xFF) - cb;
            var dist = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        _cache[key] = (short)best;
        return (byte)best;
    }
}
