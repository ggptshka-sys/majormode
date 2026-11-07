using MajorMode.Core.Description;
using MajorMode.Core.Layout;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;

namespace MajorMode.Core;

public sealed record ChordAnalysis(
    Chord? Chord,
    FretRange? Range,
    IReadOnlyList<ValidationMessage> Messages,
    string Description)
{
    /// <summary>Аккорд можно рисовать и сохранять.</summary>
    public bool CanRender => Chord is not null && Range is not null;

    public IEnumerable<ValidationMessage> Errors => Messages.Where(m => m.IsError);

    public IEnumerable<ValidationMessage> Warnings => Messages.Where(m => !m.IsError);

    public IEnumerable<ValidationMessage> For(FieldId field) => Messages.Where(m => m.Field == field);
}

/// <summary>Разбор ввода, выбор диапазона ладов и текстовое описание в одном месте.</summary>
public static class ChordAnalyzer
{
    public static ChordAnalysis Analyze(ChordInput input, ChordRules? rules = null)
    {
        rules ??= ChordRules.Default;
        var parsed = ChordParser.Parse(input, rules);
        var messages = parsed.Messages.ToList();

        if (parsed.Chord is null)
        {
            return new ChordAnalysis(null, null, messages, BuildErrorDescription(messages));
        }

        var range = FretRangeCalculator.Calculate(parsed.Chord, rules);
        if (!range.IsValid)
        {
            messages.Add(new ValidationMessage(FieldId.General, Severity.Error, range.Error!));
            return new ChordAnalysis(null, null, messages, BuildErrorDescription(messages));
        }

        var description = ChordDescriber.Describe(parsed.Chord, range.Range!.Value);
        return new ChordAnalysis(parsed.Chord, range.Range, messages, description);
    }

    /// <summary>Сводка ошибок: "Ошибок: 2. Палец 2, струна: ...".</summary>
    public static string BuildErrorSummary(IEnumerable<ValidationMessage> messages)
    {
        var errors = messages.Where(m => m.IsError).ToList();
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        return $"Ошибок: {errors.Count}. " + string.Join(" ", errors.Select(e => e.Text));
    }

    public static string BuildWarningSummary(IEnumerable<ValidationMessage> messages)
    {
        var warnings = messages.Where(m => !m.IsError).ToList();
        if (warnings.Count == 0)
        {
            return string.Empty;
        }

        return $"Предупреждений: {warnings.Count}. " + string.Join(" ", warnings.Select(w => w.Text));
    }

    private static string BuildErrorDescription(IEnumerable<ValidationMessage> messages) =>
        "Картинка не построена: в аккорде есть ошибки. " + BuildErrorSummary(messages);
}
