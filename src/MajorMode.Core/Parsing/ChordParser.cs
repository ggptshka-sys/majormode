using System.Globalization;
using MajorMode.Core.Model;

namespace MajorMode.Core.Parsing;

public sealed record ChordParseResult(Chord? Chord, IReadOnlyList<ValidationMessage> Messages)
{
    public bool IsValid => Chord is not null;

    public IEnumerable<ValidationMessage> Errors => Messages.Where(m => m.IsError);

    public IEnumerable<ValidationMessage> Warnings => Messages.Where(m => !m.IsError);
}

/// <summary>
/// Разбор и проверка формы аккорда. Гармонию не проверяет.
/// </summary>
public static class ChordParser
{
    public const string SkipFingerHint = "Чтобы пропустить палец, введите 0 в оба поля.";
    public const string BarreHint = "Для баррэ первым пальцем введите в поле струны латинскую B.";
    public const string PartialBarreHint = "Для малого баррэ перечислите соседние струны через запятую, например 4,3,2.";

    private const char CyrillicCapitalVe = '\u0412';
    private const char CyrillicSmallVe = '\u0432';
    private static readonly char[] StringListSeparators = [',', ';', ' '];

    public static ChordParseResult Parse(ChordInput input, ChordRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        rules ??= ChordRules.Default;
        var messages = new List<ValidationMessage>();
        var fingers = new List<FingerPlacement>();

        for (var finger = 1; finger <= ChordRules.FingerCount; finger++)
        {
            var raw = finger <= input.Fingers.Count ? input.Fingers[finger - 1] : FingerInput.Unused;
            var placement = ParseFinger(finger, raw, rules, messages);
            if (placement is not null)
            {
                fingers.Add(placement);
            }
        }

        var muted = MutedStringsParser.Parse(input.MutedStrings);
        if (!muted.IsValid)
        {
            messages.Add(new ValidationMessage(FieldId.MutedStrings, Severity.Error, muted.Error!));
        }

        CheckFingerConflicts(fingers, messages);

        if (muted.IsValid)
        {
            CheckMutedConflicts(fingers, muted.Strings!, messages);
        }

        if (messages.Any(m => m.IsError))
        {
            return new ChordParseResult(null, messages);
        }

        var chord = new Chord(input.Name ?? string.Empty, fingers, muted.Strings!);
        return new ChordParseResult(chord, messages);
    }

    private static FingerPlacement? ParseFinger(int finger, FingerInput raw, ChordRules rules, List<ValidationMessage> messages)
    {
        var fretField = FieldIds.FingerFret(finger);
        var stringField = FieldIds.FingerString(finger);
        var fretText = (raw.Fret ?? string.Empty).Trim();
        var stringText = (raw.GuitarString ?? string.Empty).Trim();

        var fretOk = TryParseNumber(fretText, out var fret);
        if (!fretOk)
        {
            messages.Add(new(fretField, Severity.Error,
                $"Палец {finger}, лад: «{fretText}» — не число. Введите лад от 1 до {rules.MaxFret}. {SkipFingerHint}"));
        }

        var isBarre = false;
        var guitarString = 0;
        var partialBarre = default((int High, int Low)?);
        var stringOk = true;
        if (stringText is "B" or "b")
        {
            isBarre = true;
        }
        else if (stringText.IndexOfAny(StringListSeparators) >= 0)
        {
            stringOk = TryParseStringList(finger, stringText, messages, out guitarString, out partialBarre);
        }
        else if (stringText.Length == 1 && stringText[0] is CyrillicCapitalVe or CyrillicSmallVe)
        {
            stringOk = false;
            messages.Add(new(stringField, Severity.Error,
                $"Палец {finger}, струна: введена русская буква «В». Для баррэ нужна латинская B."));
        }
        else if (!TryParseNumber(stringText, out guitarString))
        {
            stringOk = false;
            messages.Add(new(stringField, Severity.Error,
                $"Палец {finger}, струна: «{stringText}» — введите номер струны от 1 до {ChordRules.StringCount}" +
                (finger == 1 ? " или B для баррэ." : ".") + $" {SkipFingerHint}"));
        }

        if (!fretOk || !stringOk)
        {
            return null;
        }

        var stringIsZero = !isBarre && partialBarre is null && guitarString == 0;
        if (fret == 0 && stringIsZero)
        {
            return null;
        }

        if (fret == 0)
        {
            messages.Add(new(fretField, Severity.Error,
                $"Палец {finger}: указана струна, но лад равен 0. Введите лад от 1 до {rules.MaxFret}. {SkipFingerHint}"));
            return null;
        }

        if (stringIsZero)
        {
            messages.Add(new(stringField, Severity.Error,
                $"Палец {finger}: указан лад, но струна равна 0. Введите струну от 1 до {ChordRules.StringCount}" +
                (finger == 1 ? " или B для баррэ." : ".") + $" {SkipFingerHint}"));
            return null;
        }

        var valid = true;
        if (fret > rules.MaxFret)
        {
            valid = false;
            messages.Add(new(fretField, Severity.Error,
                $"Палец {finger}, лад {fret}: допустимы лады от 1 до {rules.MaxFret}. {SkipFingerHint}"));
        }

        if (isBarre && finger != 1)
        {
            valid = false;
            messages.Add(new(stringField, Severity.Error,
                $"Палец {finger}, струна B: баррэ на все струны можно указать только для первого пальца. {PartialBarreHint}"));
        }
        else if (!isBarre && partialBarre is null && guitarString > ChordRules.StringCount)
        {
            valid = false;
            messages.Add(new(stringField, Severity.Error,
                $"Палец {finger}, струна {guitarString}: у гитары струны от 1 до {ChordRules.StringCount}" +
                (finger == 1 ? ", для баррэ введите B." : ".")));
        }

        if (!valid)
        {
            return null;
        }

        if (isBarre)
        {
            return FingerPlacement.Barre(fret);
        }

        return partialBarre is { } span
            ? FingerPlacement.PartialBarre(finger, fret, span.High, span.Low)
            : FingerPlacement.OnString(finger, fret, guitarString);
    }

    /// <summary>Список струн через запятую: одна струна или малое баррэ на соседних струнах.</summary>
    private static bool TryParseStringList(int finger, string text, List<ValidationMessage> messages,
        out int singleString, out (int High, int Low)? partialBarre)
    {
        singleString = 0;
        partialBarre = null;
        var field = FieldIds.FingerString(finger);
        var strings = new SortedSet<int>();
        foreach (var token in text.Split(StringListSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var s) || s is < 1 or > ChordRules.StringCount)
            {
                messages.Add(new(field, Severity.Error,
                    $"Палец {finger}, струны «{text}»: номера струн от 1 до {ChordRules.StringCount} через запятую, например 4,3,2."));
                return false;
            }

            strings.Add(s);
        }

        if (strings.Count == 1)
        {
            singleString = strings.Min;
            return true;
        }

        if (strings.Max - strings.Min + 1 != strings.Count)
        {
            messages.Add(new(field, Severity.Error,
                $"Палец {finger}, струны {string.Join(",", strings.Reverse())}: малое баррэ накрывает только соседние струны подряд, например 4,3,2."));
            return false;
        }

        partialBarre = (strings.Max, strings.Min);
        return true;
    }

    private static bool TryParseNumber(string text, out int value)
    {
        if (text.Length == 0)
        {
            value = 0;
            return true;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static void CheckFingerConflicts(List<FingerPlacement> fingers, List<ValidationMessage> messages)
    {
        for (var i = 0; i < fingers.Count; i++)
        {
            for (var j = i + 1; j < fingers.Count; j++)
            {
                var a = fingers[i];
                var b = fingers[j];
                if (!a.Overlaps(b))
                {
                    continue;
                }

                if (a.IsAnyBarre || b.IsAnyBarre)
                {
                    // Пальцы на ладах выше баррэ — обычная аппликатура; странно только на том же ладу.
                    if (a.Fret == b.Fret)
                    {
                        var barre = a.IsAnyBarre ? a : b;
                        var other = ReferenceEquals(barre, a) ? b : a;
                        var whose = barre.IsBarre ? string.Empty : $" пальца {barre.Finger}";
                        messages.Add(new(FieldIds.FingerFret(other.Finger), Severity.Warning,
                            $"Палец {other.Finger} стоит на том же {other.Fret} ладу, что и баррэ{whose}."));
                    }

                    continue;
                }

                if (a.Fret == b.Fret)
                {
                    messages.Add(new(FieldIds.FingerString(b.Finger), Severity.Error,
                        $"Пальцы {a.Finger} и {b.Finger} стоят в одной точке: {a.Fret} лад, {a.GuitarString} струна."));
                }
                else
                {
                    messages.Add(new(FieldIds.FingerString(b.Finger), Severity.Warning,
                        $"На {a.GuitarString} струне два пальца ({a.Finger} и {b.Finger}). Звучит больший лад."));
                }
            }
        }
    }

    private static void CheckMutedConflicts(List<FingerPlacement> fingers, IReadOnlySet<int> muted, List<ValidationMessage> messages)
    {
        foreach (var s in muted.OrderByDescending(s => s))
        {
            foreach (var f in fingers.Where(f => f.Covers(s)))
            {
                var who = f.IsBarre ? "её накрывает баррэ первого пальца"
                    : f.IsPartialBarre ? $"её накрывает малое баррэ пальца {f.Finger}"
                    : $"на ней стоит палец {f.Finger}";
                messages.Add(new(FieldId.MutedStrings, Severity.Warning,
                    $"Струна {s} заглушена, но {who}. На картинке она показана заглушенной (X). Данные не изменены."));
            }
        }
    }
}
