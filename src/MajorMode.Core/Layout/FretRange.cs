using MajorMode.Core.Model;

namespace MajorMode.Core.Layout;

/// <summary>Видимый диапазон ладов на картинке.</summary>
public readonly record struct FretRange(int Start, int Count)
{
    public int End => Start + Count - 1;

    /// <summary>Порожек рисуется, когда диапазон начинается с 1 лада.</summary>
    public bool ShowNut => Start == 1;

    public bool Contains(int fret) => fret >= Start && fret <= End;

    public override string ToString() => $"{Start}-{End}";
}

public sealed record FretRangeResult(FretRange? Range, string? Error)
{
    public bool IsValid => Range is not null;
}

/// <summary>
/// Автоматический выбор диапазона ладов.
/// 1) Баррэ и нет пальцев ниже него: старт с лада баррэ.
/// 2) Иначе все позиции в 1..MinVisible: старт с 1.
/// 3) Иначе старт с минимального используемого лада.
/// Без пальцев: лады 1..MinVisible.
/// </summary>
public static class FretRangeCalculator
{
    public static FretRangeResult Calculate(Chord chord, ChordRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(chord);
        rules ??= ChordRules.Default;

        if (chord.Fingers.Count == 0)
        {
            return new FretRangeResult(new FretRange(1, rules.MinVisibleFrets), null);
        }

        var minFret = chord.Fingers.Min(f => f.Fret);
        var maxFret = chord.Fingers.Max(f => f.Fret);
        var barre = chord.Barre;

        int start;
        if (barre is not null && !chord.Fingers.Any(f => !f.IsBarre && f.Fret < barre.Fret))
        {
            start = barre.Fret;
        }
        else if (maxFret <= rules.MinVisibleFrets)
        {
            start = 1;
        }
        else
        {
            start = minFret;
        }

        var end = Math.Max(start + rules.MinVisibleFrets - 1, maxFret);
        var count = end - start + 1;
        if (count > rules.MaxVisibleFrets)
        {
            var span = maxFret - minFret + 1;
            return new FretRangeResult(null,
                $"Пальцы стоят на ладах с {minFret} по {maxFret} ({span} ладов). " +
                $"На картинке читаемо помещается не больше {rules.MaxVisibleFrets} ладов. Проверьте лады пальцев.");
        }

        if (end > rules.MaxFret)
        {
            start = Math.Max(1, rules.MaxFret - count + 1);
        }

        return new FretRangeResult(new FretRange(start, count), null);
    }
}
