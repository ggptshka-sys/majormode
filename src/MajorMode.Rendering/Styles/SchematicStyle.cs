using SkiaSharp;

namespace MajorMode.Rendering.Styles;

/// <summary>Схематичный: черный фон, белые линии и текст.</summary>
internal sealed class SchematicStyle : ChordStyle
{
    public override SKColor TextColor => SKColors.White;

    public override SKColor MarkerOutline => SKColors.Black;

    public override void DrawBackground(SKCanvas canvas, BoardMapper map) => canvas.Clear(SKColors.Black);

    public override void DrawBoard(SKCanvas canvas, BoardMapper map)
    {
        var l = map.Layout;
        using var fret = Fill(SKColors.White);
        var lineExtra = l.StringThickness(6) / 2;

        for (var i = l.Range.ShowNut ? 1 : 0; i <= l.Range.Count; i++)
        {
            DrawFretBar(canvas, map, FretAlong(map, i), l.FretLineThickness, fret, lineExtra);
        }

        if (l.Range.ShowNut)
        {
            // Порожек утолщается наружу (в сторону X/O), не заходя в первую ячейку.
            var along = FretAlong(map, 0) - l.NutThickness / 2 + l.FretLineThickness / 2;
            DrawFretBar(canvas, map, along, l.NutThickness, fret, lineExtra);
        }

        for (var s = 1; s <= 6; s++)
        {
            using var paint = Stroke(SKColors.White, (float)l.StringThickness(s));
            DrawStringLine(canvas, map, s, map.AlongStart, map.AlongEnd, paint);
        }
    }
}
