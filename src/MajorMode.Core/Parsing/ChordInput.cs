namespace MajorMode.Core.Parsing;

/// <summary>Сырой текст полей пальца, как его ввел пользователь.</summary>
public sealed record FingerInput(string? Fret, string? GuitarString)
{
    public static FingerInput Unused { get; } = new("0", "0");
}

/// <summary>Сырой ввод формы аккорда.</summary>
public sealed record ChordInput(string? Name, IReadOnlyList<FingerInput> Fingers, string? MutedStrings)
{
    public static ChordInput Create(string? name, string? muted, params FingerInput[] fingers)
    {
        var all = new FingerInput[Model.ChordRules.FingerCount];
        for (var i = 0; i < all.Length; i++)
        {
            all[i] = i < fingers.Length ? fingers[i] : FingerInput.Unused;
        }

        return new ChordInput(name, all, muted);
    }
}
