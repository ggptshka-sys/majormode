using System.IO;
using System.Windows;
using MajorMode.Core.Settings;
using Microsoft.Win32;

namespace MajorMode.App.Services;

/// <summary>Объявления для программ экранного доступа без перевода фокуса.</summary>
public interface IAnnouncer
{
    void Announce(string text, bool important);
}

public interface IDialogService
{
    string? PickFolder(string? initialFolder);

    bool ConfirmOverwrite(string fullPath);

    bool ConfirmUpdate(string currentVersion, string newVersion);
}

public sealed class WpfDialogService(Func<Window?> owner) : IDialogService
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выбор папки для сохранения картинок",
            Multiselect = false,
        };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog(owner()) == true ? dialog.FolderName : null;
    }

    public bool ConfirmOverwrite(string fullPath)
    {
        var text = $"Файл «{Path.GetFileName(fullPath)}» уже существует в папке {Path.GetDirectoryName(fullPath)}.\n\nПерезаписать его?";
        var w = owner();
        var result = w is null
            ? MessageBox.Show(text, "Подтверждение перезаписи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(w, text, "Подтверждение перезаписи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmUpdate(string currentVersion, string newVersion)
    {
        var text = $"Доступна новая версия {newVersion}. Установлена {currentVersion}.\n\n" +
                   "Скачать и установить сейчас? Программа перезапустится, введенный аккорд не сохранится. Уже сохраненные картинки и настройки не изменятся.";
        var w = owner();
        var result = w is null
            ? MessageBox.Show(text, "Обновление MajorMode", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes)
            : MessageBox.Show(w, text, "Обновление MajorMode", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        return result == MessageBoxResult.Yes;
    }
}

/// <summary>Настройки в %LOCALAPPDATA%\MajorMode\settings.json.</summary>
public sealed class SettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MajorMode", "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(_path) ? AppSettingsSerializer.Deserialize(File.ReadAllText(_path)) : new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, AppSettingsSerializer.Serialize(settings));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
