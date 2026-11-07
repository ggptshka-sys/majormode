using MajorMode.Core.Model;
using SkiaSharp;

namespace MajorMode.Rendering.Styles;

/// <summary>
/// Оформление картинки. Стиль рисует только фон, гриф, лады, порожек и струны;
/// маркеры пальцев, X/O, название и номер лада рисует общий renderer одинаково для всех стилей.
/// </summary>
internal abstract class ChordStyle
{
    public abstract SKColor TextColor { get; }

    public virtual SKColor OpenRingColor => TextColor;

    /// <summary>Обводка маркеров, отделяющая их от струн и фона.</summary>
    public abstract SKColor MarkerOutline { get; }

    /// <summary>Цвета, которые GIF-палитра обязана содержать точно.</summary>
    public virtual IEnumerable<SKColor> KeyColors => [TextColor, MarkerOutline, OpenRingColor];

    public abstract void DrawBackground(SKCanvas canvas, BoardMapper map);

    public abstract void DrawBoard(SKCanvas canvas, BoardMapper map);

    public static ChordStyle For(ImageStyle style) => style switch
    {
        ImageStyle.Schematic => new SchematicStyle(),
        ImageStyle.ModernDark => new ModernDarkStyle(),
        ImageStyle.RealisticWood => new WoodStyle(),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

    protected static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

    protected static SKPaint Stroke(SKColor color, float width, SKStrokeCap cap = SKStrokeCap.Butt) =>
        new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width, StrokeCap = cap };

    /// <summary>Линия лада/порожка поперек грифа, от 6 до 1 струны.</summary>
    protected static void DrawFretBar(SKCanvas canvas, BoardMapper map, double along, double thickness, SKPaint paint, double acrossExtra = 0)
    {
        var half = thickness / 2;
        var rect = map.Rect(along - half, along + half, map.AcrossMin - acrossExtra, map.AcrossMax + acrossExtra);
        canvas.DrawRect(rect, paint);
    }

    /// <summary>Положение лада index (0..Count) вдоль грифа; линия 0 сдвигается внутрь при толстом порожке.</summary>
    protected static double FretAlong(BoardMapper map, int index) => map.Layout.FretLineOffset(index);

    protected static void DrawStringLine(SKCanvas canvas, BoardMapper map, int guitarString, double alongFrom, double alongTo, SKPaint paint)
    {
        var across = map.Layout.StringOffset(guitarString);
        canvas.DrawLine(map.Point(alongFrom, across), map.Point(alongTo, across), paint);
    }
}
