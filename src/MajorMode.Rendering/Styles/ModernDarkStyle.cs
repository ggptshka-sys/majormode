using SkiaSharp;

namespace MajorMode.Rendering.Styles;

/// <summary>Современный темный, в духе аудиоплагина: панель, приглушенные лады, тонированные струны.</summary>
internal sealed class ModernDarkStyle : ChordStyle
{
    private static readonly SKColor BackgroundTop = new(0x1B, 0x1E, 0x24);
    private static readonly SKColor BackgroundBottom = new(0x0E, 0x10, 0x14);
    private static readonly SKColor Panel = new(0x23, 0x28, 0x31);
    private static readonly SKColor PanelBorder = new(0x3A, 0x42, 0x50);
    private static readonly SKColor FretColor = new(0x6B, 0x76, 0x88);
    private static readonly SKColor NutColor = new(0xE6, 0xEA, 0xF0);

    private static readonly SKColor[] StringColors =
    [
        new(0xEE, 0xF3, 0xF8), // 1
        new(0xDD, 0xE7, 0xF0), // 2
        new(0xC9, 0xD9, 0xE8), // 3
        new(0xE2, 0xC9, 0x8A), // 4
        new(0xE6, 0xB8, 0x72), // 5
        new(0xE8, 0xA6, 0x5D), // 6
    ];

    public override SKColor TextColor => new(0xF2, 0xF4, 0xF8);

    public override SKColor MarkerOutline => BackgroundBottom;

    public override void DrawBackground(SKCanvas canvas, BoardMapper map)
    {
        var size = map.Layout.Canvas;
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, size.Height),
                [BackgroundTop, BackgroundBottom], null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);
    }

    public override void DrawBoard(SKCanvas canvas, BoardMapper map)
    {
        var l = map.Layout;
        var surface = map.BoardSurface(0.55, 0.3, l.Range.ShowNut ? 0.25 : 0.12);
        var corner = (float)(l.Unit * 0.22);
        using (var panel = Fill(Panel))
        {
            canvas.DrawRoundRect(surface, corner, corner, panel);
        }

        using (var border = Stroke(PanelBorder, (float)(l.Unit * 0.03)))
        {
            canvas.DrawRoundRect(surface, corner, corner, border);
        }

        using var fret = Fill(FretColor);
        for (var i = l.Range.ShowNut ? 1 : 0; i <= l.Range.Count; i++)
        {
            DrawFretBar(canvas, map, FretAlong(map, i), l.FretLineThickness * 1.2, fret, l.Unit * 0.12);
        }

        if (l.Range.ShowNut)
        {
            using var nut = Fill(NutColor);
            var along = FretAlong(map, 0) - l.NutThickness / 2 + l.FretLineThickness / 2;
            DrawFretBar(canvas, map, along, l.NutThickness, nut, l.Unit * 0.12);
        }

        var tail = map.AlongEnd + l.FretSpacing * 0.3;
        for (var s = 1; s <= 6; s++)
        {
            using var paint = Stroke(StringColors[s - 1], (float)l.StringThickness(s), SKStrokeCap.Round);
            DrawStringLine(canvas, map, s, map.AlongStart, tail, paint);
        }
    }
}
