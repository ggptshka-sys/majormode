using SkiaSharp;

namespace MajorMode.Rendering.Styles;

/// <summary>
/// Реалистичный деревянный гриф: программная текстура палисандра, металлические лады и струны, костяной порожек.
/// Текстура детерминирована (фиксированный генератор), декоративных точек-инкрустаций нет, чтобы не путать их с пальцами.
/// </summary>
internal sealed class WoodStyle : ChordStyle
{
    private static readonly SKColor BackgroundCenter = new(0x2A, 0x20, 0x18);
    private static readonly SKColor BackgroundEdge = new(0x0D, 0x0A, 0x08);
    private static readonly SKColor WoodLight = new(0x5A, 0x36, 0x20);
    private static readonly SKColor WoodMid = new(0x47, 0x29, 0x18);
    private static readonly SKColor WoodDark = new(0x33, 0x1D, 0x10);

    public override SKColor TextColor => new(0xF5, 0xEF, 0xE6);

    public override SKColor MarkerOutline => new(0xF5, 0xEF, 0xE6);

    public override void DrawBackground(SKCanvas canvas, BoardMapper map)
    {
        var size = map.Layout.Canvas;
        var center = new SKPoint(size.Width / 2f, size.Height / 2f);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(center, Math.Max(size.Width, size.Height) * 0.7f,
                [BackgroundCenter, BackgroundEdge], null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);
    }

    public override void DrawBoard(SKCanvas canvas, BoardMapper map)
    {
        var l = map.Layout;
        var surface = map.BoardSurface(0.55, 0.25);
        DrawWood(canvas, map, surface);

        DrawFrets(canvas, map);
        DrawStrings(canvas, map, surface);
    }

    private static void DrawWood(SKCanvas canvas, BoardMapper map, SKRect surface)
    {
        var (from, to) = map.AcrossGradient(surface);
        using (var basePaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(from, to,
                [WoodDark, WoodLight, WoodMid, WoodLight, WoodDark], [0f, 0.2f, 0.5f, 0.8f, 1f], SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRect(surface, basePaint);
        }

        canvas.Save();
        canvas.ClipRect(surface, SKClipOperation.Intersect, true);

        var rng = new DeterministicRandom(20261107);
        var unit = (float)map.Layout.Unit;
        var acrossFrom = map.IsVertical ? surface.Left : surface.Top;
        var acrossTo = map.IsVertical ? surface.Right : surface.Bottom;
        var alongFrom = map.IsVertical ? surface.Top : surface.Left;
        var alongTo = map.IsVertical ? surface.Bottom : surface.Right;
        var grainCount = 70;

        for (var i = 0; i < grainCount; i++)
        {
            var across = acrossFrom + (acrossTo - acrossFrom) * rng.NextFloat();
            var amplitude = unit * (0.02f + 0.08f * rng.NextFloat());
            var wavelength = unit * (2f + 4f * rng.NextFloat());
            var phase = rng.NextFloat() * MathF.PI * 2;
            var dark = rng.NextFloat() < 0.65f;
            var alpha = (byte)(18 + rng.Next(40));
            var color = dark ? new SKColor(0x1E, 0x0F, 0x06, alpha) : new SKColor(0x8A, 0x5A, 0x38, alpha);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = unit * (0.01f + 0.03f * rng.NextFloat()),
                Color = color,
            };

            var step = unit * 0.25f;
            var points = new List<SKPoint>();
            for (var a = alongFrom; a <= alongTo + step; a += step)
            {
                var c = across + amplitude * MathF.Sin(phase + (a - alongFrom) / wavelength * MathF.PI * 2);
                points.Add(map.Point(a, c));
            }

            canvas.DrawPoints(SKPointMode.Polygon, points.ToArray(), paint);
        }

        // Затемнение к краям грифа для объема.
        var edge = unit * 0.35f;
        var (gFrom, gTo) = map.AcrossGradient(surface);
        using (var shade = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(gFrom, gTo,
                [new SKColor(0, 0, 0, 120), SKColors.Transparent, SKColors.Transparent, new SKColor(0, 0, 0, 120)],
                [0f, edge / (acrossTo - acrossFrom), 1f - edge / (acrossTo - acrossFrom), 1f], SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRect(surface, shade);
        }

        canvas.Restore();
    }

    private static void DrawFrets(SKCanvas canvas, BoardMapper map)
    {
        var l = map.Layout;
        var thickness = l.Unit * 0.09;
        var extra = l.Unit * 0.55;

        for (var i = l.Range.ShowNut ? 1 : 0; i <= l.Range.Count; i++)
        {
            var along = FretAlong(map, i);
            var rect = map.Rect(along - thickness / 2, along + thickness / 2, map.AcrossMin - extra, map.AcrossMax + extra);

            var shadowRect = map.Rect(along + thickness / 2, along + thickness * 1.2, map.AcrossMin - extra, map.AcrossMax + extra);
            using (var shadow = Fill(new SKColor(0, 0, 0, 90)))
            {
                canvas.DrawRect(shadowRect, shadow);
            }

            var (from, to) = map.AlongGradient(rect);
            using var metal = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(from, to,
                    [new SKColor(0x7E, 0x80, 0x84), new SKColor(0xF4, 0xF5, 0xF7), new SKColor(0x9A, 0x9C, 0xA0), new SKColor(0x5E, 0x60, 0x64)],
                    [0f, 0.35f, 0.7f, 1f], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(rect, metal);
        }

        if (l.Range.ShowNut)
        {
            var nut = l.NutThickness * 1.3;
            var start = FretAlong(map, 0);
            var rect = map.Rect(start - nut, start + thickness / 2, map.AcrossMin - extra, map.AcrossMax + extra);
            var (from, to) = map.AlongGradient(rect);
            using var bone = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(from, to,
                    [new SKColor(0xD9, 0xCF, 0xB4), new SKColor(0xF4, 0xEE, 0xDC), new SKColor(0xC8, 0xBC, 0x9C)],
                    [0f, 0.5f, 1f], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(rect, bone);
        }
    }

    private static void DrawStrings(SKCanvas canvas, BoardMapper map, SKRect surface)
    {
        var l = map.Layout;
        var alongEnd = map.IsVertical ? surface.Bottom : surface.Right;
        var alongStart = l.Range.ShowNut ? map.AlongStart - l.NutThickness * 1.3 : map.AlongStart - l.FretSpacing * 0.15;

        for (var s = 6; s >= 1; s--)
        {
            var t = l.StringThickness(s) * 1.15;
            var across = l.StringOffset(s);
            var wound = s >= 4;

            var shadowOffset = t * 0.8;
            using (var shadow = Stroke(new SKColor(0, 0, 0, 110), (float)t))
            {
                canvas.DrawLine(map.Point(alongStart, across + shadowOffset), map.Point(alongEnd, across + shadowOffset), shadow);
            }

            var rect = map.Rect(alongStart, alongEnd, across - t / 2, across + t / 2);
            var (from, to) = map.AcrossGradient(rect);
            SKColor[] colors = wound
                ? [new SKColor(0x7A, 0x5C, 0x30), new SKColor(0xF0, 0xD6, 0x9C), new SKColor(0x9C, 0x76, 0x40)]
                : [new SKColor(0x8C, 0x92, 0x9A), new SKColor(0xFA, 0xFB, 0xFC), new SKColor(0xA4, 0xAA, 0xB2)];
            using var metal = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(from, to, colors, [0f, 0.45f, 1f], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(rect, metal);
        }
    }

    /// <summary>Простой LCG: одинаковая текстура на любой машине и версии .NET.</summary>
    private sealed class DeterministicRandom(uint seed)
    {
        private uint _state = seed;

        public int Next(int maxExclusive) => (int)(NextUInt() % (uint)maxExclusive);

        public float NextFloat() => (NextUInt() >> 8) / (float)(1 << 24);

        private uint NextUInt()
        {
            _state = unchecked(_state * 1664525u + 1013904223u);
            return _state;
        }
    }
}
