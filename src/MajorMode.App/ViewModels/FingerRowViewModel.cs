using MajorMode.Core.Model;
using MajorMode.Core.Parsing;

namespace MajorMode.App.ViewModels;

/// <summary>Поля одного пальца: лад и струна, с текстом ошибок и подсказками для UIA.</summary>
public sealed class FingerRowViewModel(int number, Action onChanged) : ObservableObject
{
    private string _fret = "0";
    private string _guitarString = "0";
    private string _fretError = string.Empty;
    private string _stringError = string.Empty;

    public int Number { get; } = number;

    public string FretLabel => $"Палец {Number}: лад";

    public string StringLabel => $"Палец {Number}: струна";

    public string Fret
    {
        get => _fret;
        set
        {
            if (Set(ref _fret, value ?? string.Empty))
            {
                onChanged();
            }
        }
    }

    public string GuitarString
    {
        get => _guitarString;
        set
        {
            if (Set(ref _guitarString, value ?? string.Empty))
            {
                onChanged();
            }
        }
    }

    public string FretError
    {
        get => _fretError;
        private set
        {
            if (Set(ref _fretError, value))
            {
                OnPropertyChanged(nameof(FretHelp));
            }
        }
    }

    public string StringError
    {
        get => _stringError;
        private set
        {
            if (Set(ref _stringError, value))
            {
                OnPropertyChanged(nameof(StringHelp));
            }
        }
    }

    public string FretHelp => Compose(
        $"Лад от 1 до {ChordRules.DefaultMaxFret}. {ChordParser.SkipFingerHint}", FretError);

    public string StringHelp => Compose(
        $"Струна от 1 до 6" + (Number == 1 ? ", или латинская B для баррэ на все струны." : ".") + $" {ChordParser.SkipFingerHint}",
        StringError);

    public FingerInput ToInput() => new(Fret, GuitarString);

    public void SetErrors(string fretError, string stringError)
    {
        FretError = fretError;
        StringError = stringError;
    }

    internal static string Compose(string help, string error) =>
        string.IsNullOrEmpty(error) ? help : $"Ошибка: {error} Подсказка: {help}";
}
