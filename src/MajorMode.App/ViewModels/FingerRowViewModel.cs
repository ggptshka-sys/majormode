using MajorMode.Core.Parsing;

namespace MajorMode.App.ViewModels;

/// <summary>Поля одного пальца: лад и струна, с короткими именами и текстом ошибок для UIA.</summary>
public sealed class FingerRowViewModel(int number, Action onChanged) : ObservableObject
{
    private string _fret = "0";
    private string _guitarString = "0";
    private string _fretError = string.Empty;
    private string _stringError = string.Empty;

    public int Number { get; } = number;

    public string FretLabel => $"{Number} палец лад";

    public string StringLabel => $"{Number} палец струна";

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
        private set => Set(ref _fretError, value);
    }

    public string StringError
    {
        get => _stringError;
        private set => Set(ref _stringError, value);
    }

    public FingerInput ToInput() => new(Fret, GuitarString);

    public void SetErrors(string fretError, string stringError)
    {
        FretError = fretError;
        StringError = stringError;
    }
}
