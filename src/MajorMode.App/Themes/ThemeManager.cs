using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace MajorMode.App.Themes;

/// <summary>
/// Темная тема по умолчанию; в режиме высокой контрастности Windows — системное оформление.
/// Переключается на лету, без перезапуска.
/// </summary>
public static class ThemeManager
{
    private const int DwmUseImmersiveDarkMode = 20;

    private static ResourceDictionary? _current;

    public static bool IsDark { get; private set; }

    public static event EventHandler? ThemeChanged;

    public static void Initialize(Application app)
    {
        Apply(app);
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                app.Dispatcher.BeginInvoke(() => Apply(app));
            }
        };
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility)
            {
                app.Dispatcher.BeginInvoke(() => Apply(app));
            }
        };
    }

    private static void Apply(Application app)
    {
        var dark = !SystemParameters.HighContrast;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                dark ? "/MajorMode;component/Themes/Dark.xaml" : "/MajorMode;component/Themes/HighContrast.xaml",
                UriKind.Relative),
        };
        if (!dark)
        {
            AddSystemBrushes(dictionary);
        }

        var merged = app.Resources.MergedDictionaries;
        if (_current is not null)
        {
            merged.Remove(_current);
        }

        merged.Add(dictionary);
        _current = dictionary;

        if (IsDark != dark)
        {
            IsDark = dark;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    private static void AddSystemBrushes(ResourceDictionary d)
    {
        Brush window = SystemColors.WindowBrush;
        Brush text = SystemColors.WindowTextBrush;
        d["Brush.Window"] = window;
        d["Brush.Header"] = window;
        d["Brush.Card"] = window;
        d["Brush.Field"] = window;
        d["Brush.Inset"] = window;
        d["Brush.Text"] = text;
        d["Brush.Label"] = text;
        d["Brush.TextMuted"] = text;
        d["Brush.Error"] = text;
        d["Brush.CardBorder"] = text;
        d["Brush.Accent"] = SystemColors.HighlightBrush;
        d["Brush.Focus"] = SystemColors.HighlightBrush;
    }

    /// <summary>Темный заголовок окна Windows 10 (20H1+) и 11; на старых системах вызов просто игнорируется.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = IsDark ? 1 : 0;
        try
        {
            _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
