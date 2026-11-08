namespace MajorMode.Core.Model;

/// <summary>
/// Активный палец: лад и одна струна, малое баррэ на соседних струнах
/// или полное баррэ на все струны (только первый палец).
/// </summary>
public sealed record FingerPlacement
{
    private FingerPlacement(int finger, int fret, int highString, int lowString, bool isBarre)
    {
        Finger = finger;
        Fret = fret;
        HighString = highString;
        LowString = lowString;
        IsBarre = isBarre;
    }

    /// <summary>Номер пальца 1..4.</summary>
    public int Finger { get; }

    /// <summary>Лад, начиная с 1.</summary>
    public int Fret { get; }

    /// <summary>Струна с большим номером (со стороны 6 струны), которую прижимает палец.</summary>
    public int HighString { get; }

    /// <summary>Струна с меньшим номером; для одной струны равна <see cref="HighString"/>.</summary>
    public int LowString { get; }

    /// <summary>Номер струны 1..6; 0 для баррэ (полного или малого).</summary>
    public int GuitarString => HighString == LowString ? HighString : 0;

    /// <summary>Полное баррэ на все 6 струн.</summary>
    public bool IsBarre { get; }

    /// <summary>Малое баррэ: палец накрывает несколько соседних струн, но не все.</summary>
    public bool IsPartialBarre => !IsBarre && HighString != LowString;

    /// <summary>Полное или малое баррэ: рисуется полосой.</summary>
    public bool IsAnyBarre => HighString != LowString;

    /// <summary>Прижатые струны, от большего номера к меньшему.</summary>
    public IEnumerable<int> CoveredStrings => Enumerable.Range(LowString, HighString - LowString + 1).Reverse();

    public static FingerPlacement OnString(int finger, int fret, int guitarString)
    {
        ValidateFinger(finger, fret);
        ValidateString(guitarString);
        return new FingerPlacement(finger, fret, guitarString, guitarString, isBarre: false);
    }

    public static FingerPlacement Barre(int fret)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fret, 1);
        return new FingerPlacement(1, fret, ChordRules.StringCount, 1, isBarre: true);
    }

    /// <summary>Малое баррэ на струнах от <paramref name="highString"/> до <paramref name="lowString"/> включительно.</summary>
    public static FingerPlacement PartialBarre(int finger, int fret, int highString, int lowString)
    {
        ValidateFinger(finger, fret);
        ValidateString(highString);
        ValidateString(lowString);
        if (highString <= lowString)
        {
            throw new ArgumentException("Малое баррэ накрывает минимум две струны, первая — с большим номером.", nameof(highString));
        }

        if (highString == ChordRules.StringCount && lowString == 1 && finger == 1)
        {
            return Barre(fret);
        }

        return new FingerPlacement(finger, fret, highString, lowString, isBarre: false);
    }

    /// <summary>Прижимает ли палец указанную струну.</summary>
    public bool Covers(int guitarString) => guitarString >= LowString && guitarString <= HighString;

    /// <summary>Есть ли у двух пальцев общие струны.</summary>
    public bool Overlaps(FingerPlacement other) => LowString <= other.HighString && other.LowString <= HighString;

    private static void ValidateFinger(int finger, int fret)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(finger, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(finger, ChordRules.FingerCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(fret, 1);
    }

    private static void ValidateString(int guitarString)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(guitarString, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(guitarString, ChordRules.StringCount);
    }
}
