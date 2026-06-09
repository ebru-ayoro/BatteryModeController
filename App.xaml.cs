using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BatteryModeController;

public partial class App : Application
{
    private const string ThemeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";

    private bool _isDarkMode;
    private DispatcherTimer? _themeTimer;

    public static bool IsDarkMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplySystemTheme();
        _themeTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, OnPollTheme, Dispatcher);
        _themeTimer.Start();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeTimer?.Stop();
        _themeTimer = null;
        base.OnExit(e);
    }

    private void OnPollTheme(object? sender, EventArgs e)
    {
        ApplySystemTheme();
    }

    public void ApplySystemTheme()
    {
        bool dark = IsWindowsDarkMode();
        if (dark == _isDarkMode) return;
        _isDarkMode = dark;
        IsDarkMode = dark;

        Resources["PageBgColor"] = dark ? Color.FromRgb(0x1e, 0x1e, 0x1e) : Color.FromRgb(0xf0, 0xf0, 0xf0);
        Resources["CardBgColor"] = dark ? Color.FromRgb(0x2d, 0x2d, 0x2d) : Color.FromRgb(0xff, 0xff, 0xff);
        Resources["CardBorderColor"] = dark ? Color.FromRgb(0x3d, 0x3d, 0x3d) : Color.FromRgb(0xe0, 0xe0, 0xe0);
        Resources["TextPrimaryColor"] = dark ? Color.FromRgb(0xe0, 0xe0, 0xe0) : Color.FromRgb(0x1a, 0x1a, 0x1a);
        Resources["TextSecondaryColor"] = dark ? Color.FromRgb(0x99, 0x99, 0x99) : Color.FromRgb(0x88, 0x88, 0x88);
        Resources["AccentBgColor"] = dark ? Color.FromRgb(0x60, 0xbd, 0xff) : Color.FromRgb(0x00, 0x5f, 0xb8);
        Resources["AccentFgColor"] = dark ? Color.FromRgb(0x1a, 0x1a, 0x1a) : Color.FromRgb(0xff, 0xff, 0xff);
        Resources["ActiveCardBorderColor"] = dark ? Color.FromRgb(0x60, 0xbd, 0xff) : Color.FromRgb(0x00, 0x5f, 0xb8);
        Resources["ToggleTrackColor"] = dark ? Color.FromRgb(0x66, 0x66, 0x66) : Color.FromRgb(0xcc, 0xcc, 0xcc);
        Resources["ToggleTrackCheckedColor"] = dark ? Color.FromRgb(0x60, 0xbd, 0xff) : Color.FromRgb(0x00, 0x5f, 0xb8);
        Resources["AcCardBgColor"] = dark ? Color.FromRgb(0x1a, 0x2a, 0x3d) : Color.FromRgb(0xf0, 0xf6, 0xff);
        Resources["AcCardBorderColor"] = dark ? Color.FromRgb(0x2a, 0x4a, 0x6a) : Color.FromRgb(0xc8, 0xda, 0xf5);
        Resources["BatCardBgColor"] = dark ? Color.FromRgb(0x3d, 0x2a, 0x1a) : Color.FromRgb(0xff, 0xf5, 0xf0);
        Resources["BatCardBorderColor"] = dark ? Color.FromRgb(0x6a, 0x4a, 0x2a) : Color.FromRgb(0xf0, 0xda, 0xc8);
        Resources["GreenColor"] = dark ? Color.FromRgb(0x2e, 0xcc, 0x71) : Color.FromRgb(0x2e, 0xcc, 0x71);
        Resources["GreenTextColor"] = dark ? Color.FromRgb(0x4e, 0xec, 0x91) : Color.FromRgb(0x1e, 0x8a, 0x3c);
        Resources["RedColor"] = dark ? Color.FromRgb(0xe7, 0x4c, 0x3c) : Color.FromRgb(0xe7, 0x4c, 0x3c);
        Resources["RedTextColor"] = dark ? Color.FromRgb(0xff, 0x7c, 0x5c) : Color.FromRgb(0xc0, 0x50, 0x28);

        foreach (Window window in Windows)
        {
            if (window is MainWindow mw)
                mw.ApplyTheme(dark);
        }
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeKeyPath);
            if (key?.GetValue(AppsUseLightTheme) is int value)
                return value == 0;
        }
        catch { }
        return false;
    }
}
