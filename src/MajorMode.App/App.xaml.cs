using System.Windows;
using System.Windows.Threading;

namespace MajorMode.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
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
