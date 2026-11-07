using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MajorMode.App.Services;
using MajorMode.App.Themes;
using MajorMode.App.ViewModels;

namespace MajorMode.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new UiaAnnouncer(AnnouncementText), new WpfDialogService(() => this), new SettingsStore());
        DataContext = _viewModel;
        Loaded += (_, _) => ChordNameBox.Focus();
        Closing += (_, _) => _viewModel.PersistSettings();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ThemeManager.ApplyTitleBar(this);

    /// <summary>Стрелки в группе переключателей не только переносят фокус, но и выбирают вариант (как в Win32).</summary>
    private void RadioButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is RadioButton radio && IsArrowKeyDown())
        {
            radio.IsChecked = true;
        }
    }

    private static bool IsArrowKeyDown() =>
        Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) ||
        Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
}
