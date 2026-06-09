using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace BatteryModeController;

public class AppSettings
{
    public string? ACPlanGuid { get; set; }
    public string? BatteryPlanGuid { get; set; }
}

public partial class MainWindow : Window
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BatteryModeController", "settings.json");

    private List<PowerPlan> _plans = [];
    private Guid _selectedManualGuid;
    private bool _applying;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(App.IsDarkMode);
        RefreshPlans();
        UpdateStatus();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    public void ApplyTheme(bool dark)
    {
        if (PresentationSource.FromVisual(this)?.CompositionTarget != null)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int useDarkMode = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            }
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStatus();
            if (AutoModeToggle.IsChecked == true)
                ApplyAutoPlan();
        });
    }

    private void RefreshPlans()
    {
        _plans = PowerManagement.GetPowerPlans();
        PlansList.ItemsSource = null;
        PlansList.ItemsSource = _plans;

        AcPlanCombo.ItemsSource = null;
        AcPlanCombo.ItemsSource = _plans;
        BatteryPlanCombo.ItemsSource = null;
        BatteryPlanCombo.ItemsSource = _plans;

        var active = _plans.FirstOrDefault(p => p.IsActive);
        if (active != null)
        {
            if (_selectedManualGuid == Guid.Empty)
                _selectedManualGuid = active.Guid;

            AcPlanCombo.SelectedItem = active;
            BatteryPlanCombo.SelectedItem = active;

            var acPref = LoadPreference("AC");
            var batPref = LoadPreference("Battery");
            if (acPref != Guid.Empty)
                AcPlanCombo.SelectedItem = _plans.FirstOrDefault(p => p.Guid == acPref) ?? active;
            if (batPref != Guid.Empty)
                BatteryPlanCombo.SelectedItem = _plans.FirstOrDefault(p => p.Guid == batPref) ?? active;
        }

        RestoreManualSelection();
        UpdateAutoPlanStatus();
    }

    private void RestoreManualSelection()
    {
        foreach (var p in _plans)
            p.IsActive = p.Guid == _selectedManualGuid;
        PlansList.ItemsSource = null;
        PlansList.ItemsSource = _plans;
    }

    private void UpdateStatus()
    {
        bool pluggedIn = PowerManagement.IsPluggedIn();
        var batteryPct = PowerManagement.GetBatteryPercent();

        if (pluggedIn)
        {
            StatusIcon.Text = "\u26A1";
            StatusText.Text = "Plugged In";
        }
        else
        {
            StatusIcon.Text = "\U0001F50B";
            StatusText.Text = "On Battery";
        }

        StatusText.Foreground = TryFindResource(pluggedIn ? "GreenTextBrush" : "RedTextBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(30, 130, 60));

        if (batteryPct.HasValue)
            BatteryText.Text = $"Battery: {batteryPct.Value}%";
        else
            BatteryText.Text = "Battery: N/A";

        var active = _plans.FirstOrDefault(p => p.IsActive);
        CurrentPlanText.Text = active != null ? $"Current plan: {active.Name}" : "Current plan: Unknown";

        UpdateAutoPlanStatus();
        UpdateApplyButtonState();
    }

    private void UpdateAutoPlanStatus()
    {
        var activeGuid = PowerManagement.GetActivePlanGuid();
        var activeBrush = TryFindResource("GreenTextBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(30, 130, 60));

        var acPlan = AcPlanCombo.SelectedItem as PowerPlan;
        if (acPlan != null)
        {
            bool isAcActive = acPlan.Guid == activeGuid;
            AcPlanStatus.Text = isAcActive ? "Active now" : "";
            AcPlanStatus.Foreground = isAcActive ? activeBrush : Brushes.Transparent;
        }

        var batPlan = BatteryPlanCombo.SelectedItem as PowerPlan;
        if (batPlan != null)
        {
            bool isBatActive = batPlan.Guid == activeGuid;
            BatteryPlanStatus.Text = isBatActive ? "Active now" : "";
            BatteryPlanStatus.Foreground = isBatActive ? activeBrush : Brushes.Transparent;
        }
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        bool isAuto = AutoModeToggle.IsChecked == true;
        ManualPanel.Visibility = isAuto ? Visibility.Collapsed : Visibility.Visible;
        AutoPanel.Visibility = isAuto ? Visibility.Visible : Visibility.Collapsed;

        if (isAuto)
        {
            ApplyBtn.Content = "Save Auto Settings";
            ApplyBtn.ToolTip = "Save which plans to use for AC and battery, then apply the matching one now";
        }
        else
        {
            ApplyBtn.Content = "Apply";
            ApplyBtn.ToolTip = "Switch to the selected power plan";
        }

        UpdateApplyButtonState();

        if (!isAuto)
            RestoreManualSelection();
        UpdateAutoPlanStatus();
    }

    private void OnPlanSelected(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is PowerPlan plan)
        {
            _selectedManualGuid = plan.Guid;
            foreach (var p in _plans)
                p.IsActive = p.Guid == plan.Guid;
            UpdateApplyButtonState();
        }
    }

    private void OnAutoPlanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAutoPlanStatus();
        UpdateApplyButtonState();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (_applying) return;
        _applying = true;
        ApplyBtn.IsEnabled = false;

        try
        {
            if (AutoModeToggle.IsChecked == true)
            {
                var acPlan = AcPlanCombo.SelectedItem as PowerPlan;
                var batPlan = BatteryPlanCombo.SelectedItem as PowerPlan;

                if (acPlan == null || batPlan == null)
                {
                    InfoText.Text = "Please select both AC and Battery plans.";
                    return;
                }

                SavePreference("AC", acPlan.Guid);
                SavePreference("Battery", batPlan.Guid);

                bool pluggedIn = PowerManagement.IsPluggedIn();
                var target = pluggedIn ? acPlan : batPlan;

                if (PowerManagement.SetActivePlan(target.Guid))
                {
                    InfoText.Text = $"Switched to \"{target.Name}\" ({(pluggedIn ? "AC" : "Battery")} mode)";
                    RefreshPlans();
                    UpdateStatus();
                }
                else
                {
                    InfoText.Text = "Failed to switch power plan.";
                }
            }
            else
            {
                if (_selectedManualGuid == Guid.Empty)
                {
                    InfoText.Text = "Please select a power plan.";
                    return;
                }

                if (PowerManagement.SetActivePlan(_selectedManualGuid))
                {
                    var selected = _plans.FirstOrDefault(p => p.Guid == _selectedManualGuid);
                    InfoText.Text = selected != null ? $"Switched to \"{selected.Name}\"" : "Switched.";
                    RefreshPlans();
                    UpdateStatus();
                }
                else
                {
                    InfoText.Text = "Failed to switch power plan.";
                }
            }
        }
        finally
        {
            _applying = false;
            ApplyBtn.IsEnabled = true;
        }
    }

    private void UpdateApplyButtonState()
    {
        if (AutoModeToggle.IsChecked == true)
            ApplyBtn.IsEnabled = AcPlanCombo.SelectedItem != null && BatteryPlanCombo.SelectedItem != null;
        else
            ApplyBtn.IsEnabled = _selectedManualGuid != Guid.Empty;
    }

    private void ApplyAutoPlan()
    {
        bool pluggedIn = PowerManagement.IsPluggedIn();
        var acPlan = AcPlanCombo.SelectedItem as PowerPlan;
        var batPlan = BatteryPlanCombo.SelectedItem as PowerPlan;

        var target = pluggedIn ? acPlan : batPlan;
        if (target == null) return;

        var currentGuid = PowerManagement.GetActivePlanGuid();
        if (currentGuid == target.Guid) return;

        if (PowerManagement.SetActivePlan(target.Guid))
        {
            RefreshPlans();
            UpdateStatus();
            InfoText.Text = $"Auto-switched to \"{target.Name}\" ({(pluggedIn ? "AC" : "Battery")} mode)";
        }
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    private static void SaveSettings(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static Guid LoadPreference(string key)
    {
        var settings = LoadSettings();
        string? val = key == "AC" ? settings.ACPlanGuid : settings.BatteryPlanGuid;
        if (string.IsNullOrEmpty(val)) return Guid.Empty;
        if (Guid.TryParse(val, out var guid)) return guid;
        return Guid.Empty;
    }

    private static void SavePreference(string key, Guid guid)
    {
        var settings = LoadSettings();
        if (key == "AC")
            settings.ACPlanGuid = guid.ToString();
        else
            settings.BatteryPlanGuid = guid.ToString();
        SaveSettings(settings);
    }
}
