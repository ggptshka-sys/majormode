namespace MajorMode.Core.Model;

/// <summary>
/// Активный палец: лад и струна, либо полное баррэ (только первый палец).
/// </summary>
public sealed record FingerPlacement
{
    private FingerPlacement(int finger, int fret, int guitarString, bool isBarre)
    {
        Finger = finger;
        Fret = fret;
        GuitarString = guitarString;
        IsBarre = isBarre;
    }

    /// <summary>Номер пальца 1..4.</summary>
    public int Finger { get; }

    /// <summary>Лад, начиная с 1.</summary>
    public int Fret { get; }

    /// <summary>Номер струны 1..6; 0 для баррэ.</summary>
    public int GuitarString { get; }

    /// <summary>Полное баррэ на все 6 струн.</summary>
    public bool IsBarre { get; }

    public static FingerPlacement OnString(int finger, int fret, int guitarString)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(finger, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(finger, ChordRules.FingerCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(fret, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(guitarString, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(guitarString, ChordRules.StringCount);
        return new FingerPlacement(finger, fret, guitarString, isBarre: false);
    }

    public static FingerPlacement Barre(int fret)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fret, 1);
        return new FingerPlacement(1, fret, 0, isBarre: true);
    }

    /// <summary>Прижимает ли палец указанную струну.</summary>
    public bool Covers(int guitarString) => IsBarre || GuitarString == guitarString;
}
