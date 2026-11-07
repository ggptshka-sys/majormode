using MajorMode.Core.Model;

namespace MajorMode.Core.Parsing;

public sealed record MutedStringsParseResult(IReadOnlySet<int>? Strings, string? Error)
{
    public bool IsValid => Error is null;
}

/// <summary>
/// Разбор поля заглушенных струн: "6", "6,5", "6; 5", "6 5".
/// Пусто или одни X (латинские или кириллические) означает "нет заглушенных" (MVP допущение).
/// </summary>
public static class MutedStringsParser
{
    public const string HelpText = "Номера заглушенных струн через запятую, например 6 или 6,5. Пусто или XX: нет заглушенных.";

    private static readonly char[] Separators = [',', ';', ' ', '\t'];

    public static MutedStringsParseResult Parse(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || IsNoneToken(trimmed))
        {
            return new MutedStringsParseResult(new SortedSet<int>(), null);
        }

        var result = new SortedSet<int>();
        var tokens = trimmed.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.Length == 1 && token[0] is >= '1' and <= '6')
            {
                result.Add(token[0] - '0');
                continue;
            }

            var reason = token.All(char.IsAsciiDigit)
                ? $"«{token}» — нет такой струны, номера струн от 1 до {ChordRules.StringCount}."
                : $"«{token}» — не номер струны.";
            return new MutedStringsParseResult(null, $"Заглушенные струны: {reason} {HelpText}");
        }

        return new MutedStringsParseResult(result, null);
    }

    private static bool IsNoneToken(string text) =>
        text.All(c => c is 'X' or 'x' or '\u0425' or '\u0445' or ' ');
}
