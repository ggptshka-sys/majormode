using MajorMode.Core.Layout;
using MajorMode.Core.Model;
using SkiaSharp;

namespace MajorMode.Rendering;

/// <summary>
/// Переводит координаты "вдоль грифа / поперек грифа" в пиксели с учетом ориентации,
/// чтобы стили рисовались одним кодом для вертикали и горизонтали.
/// </summary>
internal sealed class BoardMapper(ChordLayout layout)
{
    public ChordLayout Layout { get; } = layout;

    public bool IsVertical => Layout.Orientation == FretboardOrientation.Vertical;

    /// <summary>Координата порожка (верхнего видимого лада) вдоль грифа.</summary>
    public float AlongStart => (float)Layout.FretLineOffset(0);

    public float AlongEnd => (float)Layout.FretLineOffset(Layout.Range.Count);

    /// <summary>Поперек: от 6 струны к 1 (в пикселях может убывать, поэтому min/max).</summary>
    public float AcrossMin => (float)Math.Min(Layout.StringOffset(6), Layout.StringOffset(1));

    public float AcrossMax => (float)Math.Max(Layout.StringOffset(6), Layout.StringOffset(1));

    public SKPoint Point(double along, double across) =>
        IsVertical ? new SKPoint((float)across, (float)along) : new SKPoint((float)along, (float)across);

    public SKRect Rect(double alongFrom, double alongTo, double acrossFrom, double acrossTo)
    {
        var a0 = (float)Math.Min(alongFrom, alongTo);
        var a1 = (float)Math.Max(alongFrom, alongTo);
        var c0 = (float)Math.Min(acrossFrom, acrossTo);
        var c1 = (float)Math.Max(acrossFrom, acrossTo);
        return IsVertical ? new SKRect(c0, a0, c1, a1) : new SKRect(a0, c0, a1, c1);
    }

    /// <summary>Поверхность грифа с небольшим запасом за крайние струны и за последний лад.</summary>
    public SKRect BoardSurface(double acrossPadUnits = 0.5, double alongTailUnits = 0.25, double alongHeadUnits = 0)
    {
        var pad = Layout.Unit * acrossPadUnits;
        return Rect(AlongStart - Layout.Unit * alongHeadUnits, AlongEnd + Layout.FretSpacing * alongTailUnits, AcrossMin - pad, AcrossMax + pad);
    }

    /// <summary>Вектор вдоль грифа и поперек для градиентов.</summary>
    public (SKPoint From, SKPoint To) AcrossGradient(SKRect r) =>
        IsVertical ? (new SKPoint(r.Left, r.MidY), new SKPoint(r.Right, r.MidY)) : (new SKPoint(r.MidX, r.Top), new SKPoint(r.MidX, r.Bottom));

    public (SKPoint From, SKPoint To) AlongGradient(SKRect r) =>
        IsVertical ? (new SKPoint(r.MidX, r.Top), new SKPoint(r.MidX, r.Bottom)) : (new SKPoint(r.Left, r.MidY), new SKPoint(r.Right, r.MidY));

    public static SKPoint ToSk(PointD p) => new((float)p.X, (float)p.Y);
}
