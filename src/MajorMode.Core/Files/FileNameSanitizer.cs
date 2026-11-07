using System.Text;
using MajorMode.Core.Model;

namespace MajorMode.Core.Files;

/// <summary>
/// Приведение имени файла к правилам Windows независимо от ОС, на которой идут тесты.
/// Правила:
/// - символы \ / : * ? " &lt; &gt; | и управляющие заменяются на "_";
/// - пробелы и точки в конце удаляются, пробелы в начале тоже;
/// - зарезервированные имена (CON, PRN, AUX, NUL, COM1..9, LPT1..9) получают суффикс "_";
/// - расширение .gif/.png/.jpg/.jpeg, введенное пользователем, отбрасывается: его задает формат;
/// - # и буквы (C#, Bb) сохраняются как есть; Windows не различает регистр, поэтому "Bb" и "BB" — один файл;
/// - пустой результат заменяется на "chord".
/// </summary>
public static class FileNameSanitizer
{
    public const string FallbackName = "chord";
    public const int MaxBaseNameLength = 100;

    /// <summary>Лимит полного пути: классический MAX_PATH, чтобы файл открывали и старые программы (например, REAPER 5).</summary>
    public const int MaxFullPathLength = 259;

    private static readonly char[] InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
    private static readonly string[] KnownExtensions = [".gif", ".png", ".jpg", ".jpeg"];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Sanitize(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        text = StripKnownExtension(text);

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(char.IsControl(c) || Array.IndexOf(InvalidChars, c) >= 0 ? '_' : c);
        }

        var result = sb.ToString().TrimEnd(' ', '.').TrimStart(' ');
        if (result.Length > MaxBaseNameLength)
        {
            result = result[..MaxBaseNameLength].TrimEnd(' ', '.');
        }

        if (result.Length == 0)
        {
            return FallbackName;
        }

        var stem = result.Split('.')[0];
        if (ReservedNames.Contains(stem.TrimEnd(' ')))
        {
            result = stem.TrimEnd(' ') + "_" + result[stem.Length..];
        }

        return result;
    }

    /// <summary>Имя файла по умолчанию из названия аккорда.</summary>
    public static string DefaultFromChordName(string? chordName) => Sanitize(chordName);

    public static string BuildFileName(string? name, ExportFormat format) => Sanitize(name) + format.FileExtension();

    /// <summary>Полный путь; ошибка, если слишком длинный.</summary>
    public static (string? Path, string? Error) BuildFullPath(string? folder, string? name, ExportFormat format)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return (null, "Папка для сохранения не выбрана. Нажмите «Выбор пути».");
        }

        var full = System.IO.Path.Combine(folder.Trim(), BuildFileName(name, format));
        if (full.Length > MaxFullPathLength)
        {
            return (null, $"Путь к файлу слишком длинный ({full.Length} символов, максимум {MaxFullPathLength}). Выберите папку ближе к корню диска или сократите имя файла.");
        }

        return (full, null);
    }

    private static string StripKnownExtension(string text)
    {
        foreach (var ext in KnownExtensions)
        {
            if (text.Length > ext.Length && text.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return text[..^ext.Length];
            }
        }

        return text;
    }
}
