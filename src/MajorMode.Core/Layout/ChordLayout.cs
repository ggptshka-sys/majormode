using MajorMode.Core.Model;

namespace MajorMode.Core.Layout;

/// <summary>
/// Геометрия картинки: маппинг (струна, лад) в пиксели для обеих ориентаций и любого холста.
/// Все размеры элементов выражены через шаг между струнами <see cref="Unit"/>.
/// </summary>
public sealed class ChordLayout
{
    private const double SafeMarginRatio = 0.06;
    private const double TitleFontRatio = 0.12;
    private const double TitleLineFactor = 1.35;
    private const double TitleGapRatio = 0.03;

    private const double MarkerPad = 0.6;
    private const double IndicatorBand = 1.2;
    private const double FarPad = 0.5;
    private const double VerticalLabelBand = 1.3;
    private const double HorizontalLabelBand = 1.3;
    private const double VerticalFretFactor = 1.3;
    private const double HorizontalFretFactor = 1.6;

    private ChordLayout(CanvasSize canvas, FretboardOrientation orientation, FretRange range)
    {
        Canvas = canvas;
        Orientation = orientation;
        Range = range;

        var minSide = Math.Min(canvas.Width, canvas.Height);
        var margin = Math.Round(minSide * SafeMarginRatio);
        SafeArea = new RectD(margin, margin, canvas.Width - 2 * margin, canvas.Height - 2 * margin);

        TitleFontSize = minSide * TitleFontRatio;
        var titleHeight = TitleFontSize * TitleLineFactor;
        var gap = minSide * TitleGapRatio;
        var area = RectD.FromEdges(SafeArea.X, SafeArea.Y + titleHeight + gap, SafeArea.Right, SafeArea.Bottom);

        double widthUnits;
        double heightUnits;
        if (orientation == FretboardOrientation.Vertical)
        {
            FretFactor = VerticalFretFactor;
            widthUnits = 2 * VerticalLabelBand + 2 * MarkerPad + (ChordRules.StringCount - 1);
            heightUnits = IndicatorBand + range.Count * FretFactor + FarPad;
        }
        else
        {
            FretFactor = HorizontalFretFactor;
            widthUnits = IndicatorBand + range.Count * FretFactor + FarPad;
            heightUnits = 2 * MarkerPad + (ChordRules.StringCount - 1) + HorizontalLabelBand;
        }

        Unit = Math.Min(area.Width / widthUnits, area.Height / heightUnits);
        var boxW = widthUnits * Unit;
        var boxH = heightUnits * Unit;

        // Название и схема центрируются по вертикали одним блоком, чтобы на 9:16 не было пустоты между ними.
        var groupHeight = titleHeight + gap + boxH;
        var groupTop = SafeArea.Y + (SafeArea.Height - groupHeight) / 2;
        TitleRect = new RectD(SafeArea.X, groupTop, SafeArea.Width, titleHeight);
        DiagramBounds = new RectD(area.X + (area.Width - boxW) / 2, groupTop + titleHeight + gap, boxW, boxH);

        var s = Unit;
        if (orientation == FretboardOrientation.Vertical)
        {
            var left = DiagramBounds.X + (VerticalLabelBand + MarkerPad) * s;
            var top = DiagramBounds.Y + IndicatorBand * s;
            BoardRect = new RectD(left, top, (ChordRules.StringCount - 1) * s, range.Count * FretSpacing);
        }
        else
        {
            var left = DiagramBounds.X + IndicatorBand * s;
            var top = DiagramBounds.Y + MarkerPad * s;
            BoardRect = new RectD(left, top, range.Count * FretSpacing, (ChordRules.StringCount - 1) * s);
        }
    }

    public CanvasSize Canvas { get; }

    public FretboardOrientation Orientation { get; }

    public FretRange Range { get; }

    /// <summary>Поля по краям холста, внутри которых рисуется все.</summary>
    public RectD SafeArea { get; }

    public RectD TitleRect { get; }

    public double TitleFontSize { get; }

    /// <summary>Прямоугольник всей схемы: гриф, маркеры, X/O, номер лада.</summary>
    public RectD DiagramBounds { get; }

    /// <summary>Область между крайними струнами и от порожка (верхнего видимого лада) до последнего лада.</summary>
    public RectD BoardRect { get; }

    /// <summary>Шаг между струнами в пикселях.</summary>
    public double Unit { get; }

    public double FretFactor { get; }

    public double FretSpacing => Unit * FretFactor;

    public double MarkerRadius => Unit * 0.38;

    public double MarkerFontSize => Unit * 0.5;

    public double IndicatorRadius => Unit * 0.24;

    public double FretNumberFontSize => Unit * 0.55;

    public double BarreThickness => Unit * 0.76;

    public double FretLineThickness => Unit * 0.045;

    public double NutThickness => Unit * 0.16;

    public static ChordLayout Create(CanvasSize canvas, FretboardOrientation orientation, FretRange range)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvas));
        }

        if (range.Start < 1 || range.Count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(range));
        }

        return new ChordLayout(canvas, orientation, range);
    }

    /// <summary>Толщина струны: убывает от 6 к 1.</summary>
    public double StringThickness(int guitarString)
    {
        ValidateString(guitarString);
        return Unit * (0.025 + 0.009 * (guitarString - 1));
    }

    /// <summary>Координата струны поперек грифа (X для вертикали, Y для горизонтали).</summary>
    public double StringOffset(int guitarString)
    {
        ValidateString(guitarString);
        return Orientation == FretboardOrientation.Vertical
            ? BoardRect.X + (ChordRules.StringCount - guitarString) * Unit
            : BoardRect.Y + (guitarString - 1) * Unit;
    }

    /// <summary>Координата линии лада вдоль грифа; index 0 — верхняя граница (порожек), Count — последняя.</summary>
    public double FretLineOffset(int index)
    {
        if (index < 0 || index > Range.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Orientation == FretboardOrientation.Vertical
            ? BoardRect.Y + index * FretSpacing
            : BoardRect.X + index * FretSpacing;
    }

    public LineD StringLine(int guitarString)
    {
        var o = StringOffset(guitarString);
        return Orientation == FretboardOrientation.Vertical
            ? new LineD(new PointD(o, BoardRect.Y), new PointD(o, BoardRect.Bottom))
            : new LineD(new PointD(BoardRect.X, o), new PointD(BoardRect.Right, o));
    }

    public LineD FretLine(int index)
    {
        var o = FretLineOffset(index);
        return Orientation == FretboardOrientation.Vertical
            ? new LineD(new PointD(BoardRect.X, o), new PointD(BoardRect.Right, o))
            : new LineD(new PointD(o, BoardRect.Y), new PointD(o, BoardRect.Bottom));
    }

    /// <summary>Центр ячейки между линиями ладов, где ставится палец.</summary>
    public PointD CellCenter(int guitarString, int fret)
    {
        if (!Range.Contains(fret))
        {
            throw new ArgumentOutOfRangeException(nameof(fret), $"Лад {fret} вне видимого диапазона {Range}.");
        }

        var along = FretLineOffset(0) + (fret - Range.Start + 0.5) * FretSpacing;
        var across = StringOffset(guitarString);
        return Orientation == FretboardOrientation.Vertical
            ? new PointD(across, along)
            : new PointD(along, across);
    }

    /// <summary>Центр X/O со стороны порожка.</summary>
    public PointD IndicatorCenter(int guitarString)
    {
        var across = StringOffset(guitarString);
        var along = FretLineOffset(0) - IndicatorBand / 2 * Unit;
        return Orientation == FretboardOrientation.Vertical
            ? new PointD(across, along)
            : new PointD(along, across);
    }

    /// <summary>Полоса баррэ через все 6 струн на указанном ладу.</summary>
    public RectD BarreRect(int fret) => BarreRect(fret, ChordRules.StringCount, 1);

    /// <summary>Полоса баррэ на струнах от <paramref name="highString"/> до <paramref name="lowString"/>; не заходит на соседние струны.</summary>
    public RectD BarreRect(int fret, int highString, int lowString)
    {
        var center = CellCenter(lowString, fret);
        var halfT = BarreThickness / 2;
        var overhang = MarkerRadius * 1.1;
        if (Orientation == FretboardOrientation.Vertical)
        {
            return RectD.FromEdges(StringOffset(highString) - overhang, center.Y - halfT, StringOffset(lowString) + overhang, center.Y + halfT);
        }

        return RectD.FromEdges(center.X - halfT, StringOffset(lowString) - overhang, center.X + halfT, StringOffset(highString) + overhang);
    }

    /// <summary>Точка привязки цифры баррэ — середина полосы.</summary>
    public PointD BarreLabelCenter(int fret) => BarreLabelCenter(fret, ChordRules.StringCount, 1);

    public PointD BarreLabelCenter(int fret, int highString, int lowString)
    {
        var r = BarreRect(fret, highString, lowString);
        return new PointD(r.CenterX, r.CenterY);
    }

    /// <summary>
    /// Номер начального лада: слева от верхнего видимого лада (вертикаль)
    /// или под левым видимым ладом (горизонталь).
    /// </summary>
    public (PointD Point, TextAnchor Anchor) FretNumberAnchor()
    {
        var firstCell = FretLineOffset(0) + FretSpacing / 2;
        if (Orientation == FretboardOrientation.Vertical)
        {
            var x = StringOffset(6) - (MarkerPad + 0.15) * Unit;
            return (new PointD(x, firstCell), TextAnchor.RightMiddle);
        }

        var y = StringOffset(6) + (MarkerPad + 0.1) * Unit;
        return (new PointD(firstCell, y), TextAnchor.TopCenter);
    }

    /// <summary>Приблизительная область номера лада (для проверки, что ничего не обрезано).</summary>
    public RectD FretNumberBounds(int digits)
    {
        var (p, anchor) = FretNumberAnchor();
        var w = FretNumberFontSize * 0.62 * Math.Max(1, digits);
        var h = FretNumberFontSize;
        return anchor == TextAnchor.RightMiddle
            ? new RectD(p.X - w, p.Y - h / 2, w, h)
            : new RectD(p.X - w / 2, p.Y, w, h);
    }

    private static void ValidateString(int guitarString)
    {
        if (guitarString is < 1 or > ChordRules.StringCount)
        {
            throw new ArgumentOutOfRangeException(nameof(guitarString));
        }
    }
}
