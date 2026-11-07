using System.Windows;
using System.Windows.Threading;
using MajorMode.App.Services;
using MajorMode.App.Themes;

namespace MajorMode.App;

public partial class App : Application
{
    /// <summary>Версия, с которой программа только что обновилась (аргумент --updated-from), иначе null.</summary>
    public static string? UpdatedFromVersion { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        var index = Array.IndexOf(e.Args, UpdateService.UpdatedFromArgument);
        if (index >= 0 && index + 1 < e.Args.Length)
        {
            UpdatedFromVersion = e.Args[index + 1];
        }

        _ = UpdateService.CleanupAsync();
        ThemeManager.Initialize(this);
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Непредвиденная ошибка: {e.Exception.Message}\n\nПриложение продолжит работу. Если ошибка повторяется, перезапустите его.",
            "MajorMode: ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
