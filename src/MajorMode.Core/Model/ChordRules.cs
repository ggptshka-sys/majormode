namespace MajorMode.Core.Model;

/// <summary>
/// Настраиваемые ограничения ввода и отображения аккорда.
/// </summary>
public sealed record ChordRules
{
    public const int StringCount = 6;
    public const int FingerCount = 4;
    public const int DefaultMaxFret = 24;

    /// <summary>Максимальный номер лада, который можно ввести.</summary>
    public int MaxFret { get; init; } = DefaultMaxFret;

    /// <summary>Минимальное число ладов на картинке.</summary>
    public int MinVisibleFrets { get; init; } = 5;

    /// <summary>Максимальное число ладов, при котором картинка остается читаемой.</summary>
    public int MaxVisibleFrets { get; init; } = 7;

    public static ChordRules Default { get; } = new();
}
