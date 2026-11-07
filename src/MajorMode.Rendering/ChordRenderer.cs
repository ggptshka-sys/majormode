using MajorMode.Core.Layout;
using MajorMode.Core.Model;
using MajorMode.Rendering.Styles;
using SkiaSharp;

namespace MajorMode.Rendering;

/// <summary>
/// Единственный renderer картинки аккорда: превью и экспорт используют один и тот же результат.
/// Одни и те же данные дают одинаковые пиксели (на одной машине и версии SkiaSharp).
/// </summary>
public static class ChordRenderer
{
    private static readonly Lazy<SKTypeface> BoldTypeface = new(() =>
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default);

    public static ChordImage Render(Chord chord, FretRange range, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(chord);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var f in chord.Fingers)
        {
            if (!range.Contains(f.Fret))
            {
                throw new ArgumentException($"Лад {f.Fret} пальца {f.Finger} вне диапазона {range}.", nameof(range));
            }
        }

        var size = options.Canvas.ToSize();
        var layout = ChordLayout.Create(size, options.Orientation, range);
        var style = ChordStyle.For(options.Style);
        var map = new BoardMapper(layout);

        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            style.DrawBackground(canvas, map);
            style.DrawBoard(canvas, map);
            DrawIndicators(canvas, chord, layout, style);
            DrawFingers(canvas, chord, layout, style);
            DrawFretNumber(canvas, layout, style);
            DrawTitle(canvas, chord.Name, layout, style);
            canvas.Flush();
        }

        return new ChordImage(bitmap);
    }

    /// <summary>Цвета, которые должны попасть в GIF-палитру без искажений.</summary>
    public static IReadOnlyList<SKColor> KeyColors(ImageStyle style)
    {
        var s = ChordStyle.For(style);
        var colors = new List<SKColor>
        {
            MarkerPalette.Finger1Red,
            MarkerPalette.Finger2Orange,
            MarkerPalette.Finger3Yellow,
            MarkerPalette.Finger4Green,
            MarkerPalette.MuteRed,
            MarkerPalette.DarkDigit,
            MarkerPalette.LightDigit,
        };
        colors.AddRange(s.KeyColors);
        return colors.Distinct().ToArray();
    }

    private static void DrawIndicators(SKCanvas canvas, Chord chord, ChordLayout layout, ChordStyle style)
    {
        var r = (float)layout.IndicatorRadius;
        for (var s = 1; s <= ChordRules.StringCount; s++)
        {
            var c = BoardMapper.ToSk(layout.IndicatorCenter(s));
            switch (chord.GetStringState(s))
            {
                case StringState.Muted:
                {
                    var d = r * 0.95f;
                    using var paint = new SKPaint
                    {
                        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round,
                        StrokeWidth = (float)(layout.Unit * 0.09), Color = MarkerPalette.MuteRed,
                    };
                    canvas.DrawLine(c.X - d, c.Y - d, c.X + d, c.Y + d, paint);
                    canvas.DrawLine(c.X - d, c.Y + d, c.X + d, c.Y - d, paint);
                    break;
                }

                case StringState.Open:
                {
                    using var ring = new SKPaint
                    {
                        IsAntialias = true, Style = SKPaintStyle.Stroke,
                        StrokeWidth = (float)(layout.Unit * 0.07), Color = style.OpenRingColor,
                    };
                    canvas.DrawCircle(c, r * 0.9f, ring);
                    break;
                }
            }
        }
    }

    private static void DrawFingers(SKCanvas canvas, Chord chord, ChordLayout layout, ChordStyle style)
    {
        var outlineWidth = (float)(layout.Unit * 0.06);
        using var font = new SKFont(BoldTypeface.Value, (float)layout.MarkerFontSize) { Subpixel = true, Edging = SKFontEdging.Antialias };

        var barre = chord.Barre;
        if (barre is not null)
        {
            var rect = ToSk(layout.BarreRect(barre.Fret));
            var radius = rect.Width < rect.Height ? rect.Width / 2 : rect.Height / 2;
            using (var outline = new SKPaint { IsAntialias = true, Color = style.MarkerOutline })
            {
                var o = rect;
                o.Inflate(outlineWidth, outlineWidth);
                canvas.DrawRoundRect(o, radius + outlineWidth, radius + outlineWidth, outline);
            }

            using (var fill = new SKPaint { IsAntialias = true, Color = MarkerPalette.Finger1Red })
            {
                canvas.DrawRoundRect(rect, radius, radius, fill);
            }

            var label = BoardMapper.ToSk(layout.BarreLabelCenter(barre.Fret));
            DrawCenteredText(canvas, "1", label, font, MarkerPalette.DigitColor(MarkerPalette.Finger1Red));
        }

        foreach (var f in chord.Fingers.Where(f => !f.IsBarre))
        {
            // Приоритет: заглушенная струна показывается X, маркер пальца на ней не рисуется.
            if (chord.GetStringState(f.GuitarString) != StringState.Fretted)
            {
                continue;
            }

            var c = BoardMapper.ToSk(layout.CellCenter(f.GuitarString, f.Fret));
            var r = (float)layout.MarkerRadius;
            var color = MarkerPalette.FingerColor(f.Finger);
            using (var outline = new SKPaint { IsAntialias = true, Color = style.MarkerOutline })
            {
                canvas.DrawCircle(c, r + outlineWidth, outline);
            }

            using (var fill = new SKPaint { IsAntialias = true, Color = color })
            {
                canvas.DrawCircle(c, r, fill);
            }

            DrawCenteredText(canvas, f.Finger.ToString(System.Globalization.CultureInfo.InvariantCulture), c, font, MarkerPalette.DigitColor(color));
        }
    }

    private static void DrawFretNumber(SKCanvas canvas, ChordLayout layout, ChordStyle style)
    {
        var text = layout.Range.Start.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var font = new SKFont(BoldTypeface.Value, (float)layout.FretNumberFontSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
        using var paint = new SKPaint { IsAntialias = true, Color = style.TextColor };
        font.MeasureText(text, out var bounds, paint);

        var (p, anchor) = layout.FretNumberAnchor();
        float x;
        float y;
        if (anchor == TextAnchor.RightMiddle)
        {
            x = (float)p.X - bounds.Right;
            y = (float)p.Y - bounds.MidY;
        }
        else
        {
            x = (float)p.X - bounds.MidX;
            y = (float)p.Y - bounds.Top;
        }

        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static void DrawTitle(SKCanvas canvas, string name, ChordLayout layout, ChordStyle style)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var rect = layout.TitleRect;
        var size = (float)layout.TitleFontSize;
        using var paint = new SKPaint { IsAntialias = true, Color = style.TextColor };
        using var font = new SKFont(BoldTypeface.Value, size) { Subpixel = true, Edging = SKFontEdging.Antialias };

        font.MeasureText(name, out var bounds, paint);
        var maxWidth = (float)rect.Width;
        var maxHeight = (float)rect.Height;
        if (bounds.Width > maxWidth || bounds.Height > maxHeight)
        {
            var scale = Math.Min(maxWidth / bounds.Width, maxHeight / bounds.Height);
            font.Size = size * scale * 0.98f;
            font.MeasureText(name, out bounds, paint);
        }

        var center = new SKPoint((float)rect.CenterX, (float)rect.CenterY);
        canvas.DrawText(name, center.X - bounds.MidX, center.Y - bounds.MidY, SKTextAlign.Left, font, paint);
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, SKPoint center, SKFont font, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        font.MeasureText(text, out var bounds, paint);
        canvas.DrawText(text, center.X - bounds.MidX, center.Y - bounds.MidY, SKTextAlign.Left, font, paint);
    }

    private static SKRect ToSk(RectD r) => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
}
