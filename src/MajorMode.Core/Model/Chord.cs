namespace MajorMode.Core.Model;

public enum StringState
{
    Open,
    Fretted,
    Muted,
}

/// <summary>
/// Разобранный и проверенный аккорд. Гармонически не валидируется.
/// </summary>
public sealed class Chord
{
    public Chord(string name, IEnumerable<FingerPlacement> fingers, IEnumerable<int> mutedStrings)
    {
        Name = name?.Trim() ?? string.Empty;
        Fingers = fingers.OrderBy(f => f.Finger).ToArray();
        if (Fingers.Select(f => f.Finger).Distinct().Count() != Fingers.Count)
        {
            throw new ArgumentException("Номера пальцев не должны повторяться.", nameof(fingers));
        }

        if (Fingers.Any(f => f.IsBarre && f.Finger != 1))
        {
            throw new ArgumentException("Баррэ допустимо только для первого пальца.", nameof(fingers));
        }

        var muted = new SortedSet<int>(mutedStrings);
        if (muted.Any(s => s is < 1 or > ChordRules.StringCount))
        {
            throw new ArgumentOutOfRangeException(nameof(mutedStrings));
        }

        MutedStrings = muted;
    }

    public string Name { get; }

    /// <summary>Только используемые пальцы, по возрастанию номера.</summary>
    public IReadOnlyList<FingerPlacement> Fingers { get; }

    public IReadOnlySet<int> MutedStrings { get; }

    public FingerPlacement? Barre => Fingers.FirstOrDefault(f => f.IsBarre);

    public bool IsFretted(int guitarString) => Fingers.Any(f => f.Covers(guitarString));

    /// <summary>Приоритет отображения: заглушена &gt; прижата (включая баррэ) &gt; открыта.</summary>
    public StringState GetStringState(int guitarString)
    {
        if (MutedStrings.Contains(guitarString))
        {
            return StringState.Muted;
        }

        return IsFretted(guitarString) ? StringState.Fretted : StringState.Open;
    }

    /// <summary>Открытые струны, от 6 к 1.</summary>
    public IReadOnlyList<int> OpenStrings =>
        AllStringsDescending().Where(s => GetStringState(s) == StringState.Open).ToArray();

    /// <summary>Струны, где одновременно есть палец и заглушка (повод для предупреждения).</summary>
    public IReadOnlyList<int> MutedButFrettedStrings =>
        AllStringsDescending().Where(s => MutedStrings.Contains(s) && IsFretted(s)).ToArray();

    public static IEnumerable<int> AllStringsDescending()
    {
        for (var s = ChordRules.StringCount; s >= 1; s--)
        {
            yield return s;
        }
    }
}
