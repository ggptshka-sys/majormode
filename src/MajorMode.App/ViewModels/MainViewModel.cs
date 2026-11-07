using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MajorMode.App.Services;
using MajorMode.Core;
using MajorMode.Core.Files;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;
using MajorMode.Core.Settings;
using MajorMode.Rendering;
using MajorMode.Rendering.Export;
using RenderOptions = MajorMode.Core.Model.RenderOptions;

namespace MajorMode.App.ViewModels;

public enum ImageState
{
    Ready,
    Invalid,
    Saving,
    Saved,
    SaveFailed,
}

public sealed class MainViewModel : ObservableObject
{
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan DescriptionDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan AnnounceDelay = TimeSpan.FromMilliseconds(1500);

    private readonly IAnnouncer _announcer;
    private readonly IDialogService _dialogs;
    private readonly SettingsStore _settingsStore;
    private readonly UpdateService _updates;
    private readonly Action _shutdown;
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _descriptionTimer;
    private readonly DispatcherTimer _announceTimer;

    private string _chordName = string.Empty;
    private string _mutedStrings = string.Empty;
    private string _mutedError = string.Empty;
    private string _generalError = string.Empty;
    private FretboardOrientation _orientation;
    private CanvasFormat _canvas;
    private ImageStyle _style;
    private ExportFormat _format;
    private string _fileName = string.Empty;
    private bool _fileNameEdited;
    private string _outputFolder = string.Empty;
    private ImageSource? _preview;
    private string _description = string.Empty;
    private string _errorSummary = string.Empty;
    private string _warningSummary = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isSaving;
    private ChordAnalysis _analysis;
    private string _lastAnnouncedProblems = string.Empty;
    private int _previewVersion;
    private ImageState? _saveOutcome;
    private string _savedFileName = string.Empty;
    private bool _everSaved;
    private ImageState _state;
    private string _stateText = string.Empty;

    public MainViewModel(IAnnouncer announcer, IDialogService dialogs, SettingsStore settingsStore, UpdateService updates, Action shutdown)
    {
        _announcer = announcer;
        _dialogs = dialogs;
        _settingsStore = settingsStore;
        _updates = updates;
        _shutdown = shutdown;

        Fingers = Enumerable.Range(1, ChordRules.FingerCount).Select(n => new FingerRowViewModel(n, OnInputChanged)).ToArray();
        CanvasOptions = Enum.GetValues<CanvasFormat>().Select(c => new Option<CanvasFormat>(c, c.DisplayName())).ToArray();
        StyleOptions = Enum.GetValues<ImageStyle>().Select(s => new Option<ImageStyle>(s, s.DisplayName())).ToArray();
        FormatOptions = Enum.GetValues<ExportFormat>().Select(f => new Option<ExportFormat>(f, f.DisplayName())).ToArray();

        var settings = settingsStore.Load();
        _orientation = settings.Orientation;
        _canvas = settings.Canvas;
        _style = settings.Style;
        _format = settings.Format;
        _outputFolder = !string.IsNullOrWhiteSpace(settings.OutputFolder) && Directory.Exists(settings.OutputFolder)
            ? settings.OutputFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        _analysis = ChordAnalyzer.Analyze(BuildInput());
        _previewTimer = new DispatcherTimer { Interval = PreviewDelay };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RenderPreview(); };
        _descriptionTimer = new DispatcherTimer { Interval = DescriptionDelay };
        _descriptionTimer.Tick += (_, _) => { _descriptionTimer.Stop(); Description = _analysis.Description; };
        _announceTimer = new DispatcherTimer { Interval = AnnounceDelay };
        _announceTimer.Tick += (_, _) => { _announceTimer.Stop(); AnnounceProblemsIfChanged(); };

        _fileName = AutoFileName;
        ChooseFolderCommand = new RelayCommand(ChooseFolder, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy);

        ApplyAnalysis();
        Description = _analysis.Description;
        RenderPreview();
        UpdateState();
    }

    public IReadOnlyList<FingerRowViewModel> Fingers { get; }

    public IReadOnlyList<Option<CanvasFormat>> CanvasOptions { get; }

    public IReadOnlyList<Option<ImageStyle>> StyleOptions { get; }

    public IReadOnlyList<Option<ExportFormat>> FormatOptions { get; }

    public ICommand ChooseFolderCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CheckUpdatesCommand { get; }

    public string AppVersionText => $"Версия {UpdateService.CurrentVersionText}";

    public string ChordName
    {
        get => _chordName;
        set
        {
            if (!Set(ref _chordName, value ?? string.Empty))
            {
                return;
            }

            if (!_fileNameEdited)
            {
                _fileName = AutoFileName;
                OnPropertyChanged(nameof(FileName));
            }

            OnPropertyChanged(nameof(EffectiveFileName));
            OnPropertyChanged(nameof(FileNameHelp));
            OnInputChanged();
        }
    }

    public string ChordNameHelp => "Свободный текст, например C, Dm, F#m7, Bb. Пишется крупно над грифом и используется как имя файла по умолчанию.";

    public string MutedStrings
    {
        get => _mutedStrings;
        set
        {
            if (Set(ref _mutedStrings, value ?? string.Empty))
            {
                OnInputChanged();
            }
        }
    }

    public string MutedError
    {
        get => _mutedError;
        private set
        {
            if (Set(ref _mutedError, value))
            {
                OnPropertyChanged(nameof(MutedHelp));
            }
        }
    }

    public string MutedHelp => FingerRowViewModel.Compose(MutedStringsParser.HelpText, MutedError);

    public string GeneralError
    {
        get => _generalError;
        private set => Set(ref _generalError, value);
    }

    public bool IsVertical
    {
        get => _orientation == FretboardOrientation.Vertical;
        set
        {
            if (value)
            {
                SetOrientation(FretboardOrientation.Vertical);
            }
        }
    }

    public bool IsHorizontal
    {
        get => _orientation == FretboardOrientation.Horizontal;
        set
        {
            if (value)
            {
                SetOrientation(FretboardOrientation.Horizontal);
            }
        }
    }

    public CanvasFormat Canvas
    {
        get => _canvas;
        set
        {
            if (Set(ref _canvas, value))
            {
                OnPropertyChanged(nameof(PreviewCaption));
                SchedulePreview();
            }
        }
    }

    public ImageStyle Style
    {
        get => _style;
        set
        {
            if (Set(ref _style, value))
            {
                SchedulePreview();
            }
        }
    }

    public ExportFormat Format
    {
        get => _format;
        set
        {
            if (Set(ref _format, value))
            {
                OnPropertyChanged(nameof(EffectiveFileName));
                OnPropertyChanged(nameof(FileNameHelp));
                OnPropertyChanged(nameof(PreviewCaption));
                MarkChanged();
            }
        }
    }

    public string PreviewCaption
    {
        get
        {
            var size = Canvas.ToSize();
            return $"{size.Width} × {size.Height}  ·  {Format.FileExtension().TrimStart('.').ToUpperInvariant()}";
        }
    }

    /// <summary>Заполняется из названия аккорда, пока пользователь не ввел свое имя. Пусто — тоже по названию.</summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            value ??= string.Empty;
            if (!Set(ref _fileName, value))
            {
                return;
            }

            _fileNameEdited = value.Trim().Length > 0 && value != AutoFileName;
            OnPropertyChanged(nameof(EffectiveFileName));
            OnPropertyChanged(nameof(FileNameHelp));
            MarkChanged();
        }
    }

    private string AutoFileName => FileNameSanitizer.DefaultFromChordName(ChordName);

    private string FileNameSource => string.IsNullOrWhiteSpace(FileName) ? ChordName : FileName;

    public string EffectiveFileName => FileNameSanitizer.BuildFileName(FileNameSource, Format);

    public string FileNameHelp =>
        $"Пусто: имя по названию аккорда. Недопустимые символы заменяются на подчеркивание. Будет сохранено как {EffectiveFileName}.";

    public string OutputFolder
    {
        get => _outputFolder;
        private set => Set(ref _outputFolder, value);
    }

    public ImageSource? Preview
    {
        get => _preview;
        private set
        {
            if (Set(ref _preview, value))
            {
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public bool HasPreview => Preview is not null;

    public string Description
    {
        get => _description;
        private set => Set(ref _description, value);
    }

    public string ErrorSummary
    {
        get => _errorSummary;
        private set
        {
            if (Set(ref _errorSummary, value))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    public bool HasErrors => ErrorSummary.Length > 0;

    public string WarningSummary
    {
        get => _warningSummary;
        private set
        {
            if (Set(ref _warningSummary, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public bool HasWarnings => WarningSummary.Length > 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (Set(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => StatusMessage.Length > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    /// <summary>Состояние картинки для индикатора под превью и в строке состояния.</summary>
    public ImageState State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value))
            {
                OnPropertyChanged(nameof(StateKey));
            }
        }
    }

    public string StateKey => State.ToString();

    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value);
    }

    public void PersistSettings() => _settingsStore.Save(new AppSettings
    {
        OutputFolder = OutputFolder,
        Orientation = _orientation,
        Canvas = Canvas,
        Style = Style,
        Format = Format,
    });

    private void SetOrientation(FretboardOrientation orientation)
    {
        if (_orientation == orientation)
        {
            return;
        }

        _orientation = orientation;
        OnPropertyChanged(nameof(IsVertical));
        OnPropertyChanged(nameof(IsHorizontal));
        SchedulePreview();
    }

    private ChordInput BuildInput() =>
        new(ChordName, Fingers.Select(f => f.ToInput()).ToArray(), MutedStrings);

    private RenderOptions BuildRenderOptions() => new(_orientation, Canvas, Style);

    private void OnInputChanged()
    {
        _analysis = ChordAnalyzer.Analyze(BuildInput());
        ApplyAnalysis();

        Restart(_descriptionTimer);
        Restart(_announceTimer);
        SchedulePreview();
    }

    private void ApplyAnalysis()
    {
        foreach (var row in Fingers)
        {
            row.SetErrors(
                JoinMessages(_analysis.For(FieldIds.FingerFret(row.Number))),
                JoinMessages(_analysis.For(FieldIds.FingerString(row.Number))));
        }

        MutedError = JoinMessages(_analysis.For(FieldId.MutedStrings).Where(m => m.IsError));
        GeneralError = JoinMessages(_analysis.For(FieldId.General));
        ErrorSummary = ChordAnalyzer.BuildErrorSummary(_analysis.Messages);
        WarningSummary = ChordAnalyzer.BuildWarningSummary(_analysis.Messages);
    }

    private static string JoinMessages(IEnumerable<ValidationMessage> messages) =>
        string.Join(" ", messages.Select(m => m.IsError ? m.Text : "Предупреждение: " + m.Text));

    private void AnnounceProblemsIfChanged()
    {
        var current = ErrorSummary + WarningSummary;
        if (current == _lastAnnouncedProblems)
        {
            return;
        }

        var hadProblems = _lastAnnouncedProblems.Length > 0;
        _lastAnnouncedProblems = current;
        if (current.Length > 0)
        {
            _announcer.Announce(string.Join(" ", new[] { ErrorSummary, WarningSummary }.Where(s => s.Length > 0)), important: false);
        }
        else if (hadProblems)
        {
            _announcer.Announce("Ошибок нет.", important: false);
        }
    }

    private void SchedulePreview()
    {
        Restart(_previewTimer);
        MarkChanged();
    }

    /// <summary>Любое изменение того, что попадет в файл, сбрасывает "Сохранено".</summary>
    private void MarkChanged()
    {
        _saveOutcome = null;
        UpdateState();
    }

    private void UpdateState()
    {
        (State, StateText) = (_isSaving, _analysis.CanRender, _saveOutcome) switch
        {
            (true, _, _) => (ImageState.Saving, "Сохранение…"),
            (_, false, _) => (ImageState.Invalid, "Есть ошибки: картинка не построена"),
            (_, _, ImageState.Saved) => (ImageState.Saved, $"Сохранено: {_savedFileName}"),
            (_, _, ImageState.SaveFailed) => (ImageState.SaveFailed, "Не сохранено: причина в сообщении ниже"),
            _ when _everSaved => (ImageState.Ready, "Изменено, не сохранено"),
            _ => (ImageState.Ready, "Картинка готова, не сохранена"),
        };
    }

    private static void Restart(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private void RenderPreview()
    {
        var version = ++_previewVersion;
        if (!_analysis.CanRender)
        {
            Preview = null;
            return;
        }

        var chord = _analysis.Chord!;
        var range = _analysis.Range!.Value;
        var options = BuildRenderOptions();
        Task.Run(() =>
        {
            using var image = ChordRenderer.Render(chord, range, options);
            var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.GetBgraPixels(), image.Width * 4);
            bitmap.Freeze();
            return bitmap;
        }).ContinueWith(t =>
        {
            if (version == _previewVersion && t.IsCompletedSuccessfully)
            {
                Preview = t.Result;
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ChooseFolder()
    {
        var folder = _dialogs.PickFolder(OutputFolder);
        if (folder is null)
        {
            return;
        }

        OutputFolder = folder;
        MarkChanged();
        PersistSettings();
        StatusMessage = $"Папка для сохранения: {folder}";
        _announcer.Announce(StatusMessage, important: false);
    }

    private async Task SaveAsync()
    {
        _announceTimer.Stop();
        _analysis = ChordAnalyzer.Analyze(BuildInput());
        ApplyAnalysis();

        if (!_analysis.CanRender)
        {
            Report($"Не сохранено. {ErrorSummary}");
            return;
        }

        var (path, pathError) = FileNameSanitizer.BuildFullPath(OutputFolder, FileNameSource, Format);
        if (path is null)
        {
            _saveOutcome = ImageState.SaveFailed;
            UpdateState();
            Report($"Не сохранено. {pathError}");
            return;
        }

        if (File.Exists(path) && !_dialogs.ConfirmOverwrite(path))
        {
            Report("Сохранение отменено, существующий файл не изменен.");
            return;
        }

        var chord = _analysis.Chord!;
        var range = _analysis.Range!.Value;
        var options = BuildRenderOptions();
        var format = Format;

        IsBusy = true;
        _isSaving = true;
        StatusMessage = "Сохранение…";
        UpdateState();
        try
        {
            var result = await Task.Run(() =>
            {
                using var image = ChordRenderer.Render(chord, range, options);
                return ImageExporter.Save(image, format, options.Style, path, overwrite: true);
            });
            SetSaveOutcome(result.Success, path);
            Report(result.Message);
            if (result.Success)
            {
                PersistSettings();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SetSaveOutcome(false, path);
            Report($"Ошибка сохранения. {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _isSaving = false;
            UpdateState();
        }
    }

    private async Task CheckUpdatesAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Проверка обновлений…";
            _announcer.Announce(StatusMessage, important: false);
            var check = await _updates.CheckAsync();
            if (check.Status != UpdateCheckStatus.Available || check.Release is null)
            {
                Report(check.Message);
                return;
            }

            var newVersion = check.Release.Version.ToString(3);
            if (!_dialogs.ConfirmUpdate(UpdateService.CurrentVersionText, newVersion))
            {
                Report($"Обновление до версии {newVersion} отложено.");
                return;
            }

            _announcer.Announce($"Загрузка версии {newVersion}. Программа перезапустится сама.", important: true);
            var error = await _updates.InstallAsync(check.Release, new Progress<string>(s => StatusMessage = s));
            if (error is not null)
            {
                Report(error);
                return;
            }

            PersistSettings();
            _shutdown();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Вызывается после перезапуска новой версией.</summary>
    public void AnnounceUpdated(string fromVersion) =>
        Report($"Программа обновлена: была версия {fromVersion}, теперь {UpdateService.CurrentVersionText}.");

    private void SetSaveOutcome(bool success, string path)
    {
        _saveOutcome = success ? ImageState.Saved : ImageState.SaveFailed;
        if (success)
        {
            _savedFileName = Path.GetFileName(path);
            _everSaved = true;
        }
    }

    private void Report(string message)
    {
        StatusMessage = message;
        _announcer.Announce(message, important: true);
    }
}
