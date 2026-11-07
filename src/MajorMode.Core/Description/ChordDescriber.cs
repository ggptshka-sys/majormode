using System.Text;
using MajorMode.Core.Layout;
using MajorMode.Core.Model;

namespace MajorMode.Core.Description;

/// <summary>
/// Полное текстовое описание аккорда для программы экранного доступа.
/// </summary>
public static class ChordDescriber
{
    private static readonly string[] Ordinals = ["Первый", "Второй", "Третий", "Четвертый"];

    public static string Describe(Chord chord, FretRange range)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var sb = new StringBuilder();

        sb.Append(chord.Name.Length > 0 ? $"Аккорд {chord.Name}." : "Аккорд без названия.");

        if (chord.Fingers.Count == 0)
        {
            sb.Append(" Пальцы не заданы.");
        }

        foreach (var f in chord.Fingers)
        {
            sb.Append(' ');
            sb.Append(f.IsBarre
                ? $"Баррэ первым пальцем на {f.Fret} ладу, все струны."
                : $"{Ordinals[f.Finger - 1]} палец: {f.Fret} лад, {f.GuitarString} струна.");
        }

        var muted = chord.MutedStrings.OrderByDescending(s => s).ToArray();
        sb.Append(' ');
        sb.Append(muted.Length switch
        {
            0 => "Заглушенных нет.",
            1 => $"Заглушена {muted[0]} струна.",
            _ => $"Заглушены струны: {string.Join(", ", muted)}.",
        });

        var open = chord.OpenStrings;
        sb.Append(' ');
        sb.Append(open.Count switch
        {
            0 => "Открытых нет.",
            1 => $"Открыта {open[0]} струна.",
            _ => $"Открытые струны: {string.Join(", ", open)}.",
        });

        foreach (var s in chord.MutedButFrettedStrings)
        {
            sb.Append($" Внимание: на {s} струне есть палец, но она заглушена и показана с X.");
        }

        sb.Append($" Отображаются лады {range.Start}-{range.End}.");
        return sb.ToString();
    }
}
