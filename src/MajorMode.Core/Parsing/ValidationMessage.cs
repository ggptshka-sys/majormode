namespace MajorMode.Core.Parsing;

public enum FieldId
{
    General,
    ChordName,
    Finger1Fret,
    Finger1String,
    Finger2Fret,
    Finger2String,
    Finger3Fret,
    Finger3String,
    Finger4Fret,
    Finger4String,
    MutedStrings,
    FileName,
    OutputFolder,
}

public enum Severity
{
    Warning,
    Error,
}

public sealed record ValidationMessage(FieldId Field, Severity Severity, string Text)
{
    public bool IsError => Severity == Severity.Error;
}

public static class FieldIds
{
    public static FieldId FingerFret(int finger) => finger switch
    {
        1 => FieldId.Finger1Fret,
        2 => FieldId.Finger2Fret,
        3 => FieldId.Finger3Fret,
        4 => FieldId.Finger4Fret,
        _ => throw new ArgumentOutOfRangeException(nameof(finger)),
    };

    public static FieldId FingerString(int finger) => finger switch
    {
        1 => FieldId.Finger1String,
        2 => FieldId.Finger2String,
        3 => FieldId.Finger3String,
        4 => FieldId.Finger4String,
        _ => throw new ArgumentOutOfRangeException(nameof(finger)),
    };

    /// <summary>Короткое название поля для сводки ошибок.</summary>
    public static string DisplayName(FieldId field) => field switch
    {
        FieldId.General => "Аккорд",
        FieldId.ChordName => "Название аккорда",
        FieldId.Finger1Fret => "Палец 1, лад",
        FieldId.Finger1String => "Палец 1, струна",
        FieldId.Finger2Fret => "Палец 2, лад",
        FieldId.Finger2String => "Палец 2, струна",
        FieldId.Finger3Fret => "Палец 3, лад",
        FieldId.Finger3String => "Палец 3, струна",
        FieldId.Finger4Fret => "Палец 4, лад",
        FieldId.Finger4String => "Палец 4, струна",
        FieldId.MutedStrings => "Заглушенные струны",
        FieldId.FileName => "Имя файла",
        FieldId.OutputFolder => "Путь",
        _ => field.ToString(),
    };
}
