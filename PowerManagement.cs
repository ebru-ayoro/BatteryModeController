using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace BatteryModeController;

public class PowerPlan
{
    private static readonly Dictionary<string, string> KnownDescriptions = new()
    {
        ["a1841308-3541-4fab-bc81-f71556f20b4a"] = "Saves power by reducing system performance and screen brightness to maximize battery life.",
        ["381b4222-f694-41f0-9685-ff5bb260df2e"] = "Automatically balances performance and energy consumption based on your current activity.",
        ["8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"] = "Prioritizes performance, which may increase energy consumption and heat generation.",
        ["e9a42b02-d5df-448d-aa00-03f14749eb61"] = "Removes micro-managed power savings for workloads that require maximum performance. Best for high-end PCs.",
        ["4dd17c4d-303f-43b8-842b-67f73ed2a6e0"] = "Extends battery life by reducing performance and screen brightness.",
        ["9f358a54-7529-46f8-96df-ea4efb847653"] = "Maximizes system performance for demanding tasks. Uses more power.",
        ["12ac29ce-a5dc-4bf6-a764-6cae687b3983"] = "Highest performance level with no power restrictions. Designed for workstation-class PCs.",
    };

    public Guid Guid { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public string Description => KnownDescriptions.TryGetValue(Guid.ToString("d"), out var desc) ? desc : "Controls system performance and power consumption.";
    public override string ToString() => Name;
}

public static partial class PowerManagement
{
    private const uint ERROR_SUCCESS = 0;

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(IntPtr UserPowerKey, ref Guid ActivePolicyGuid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [GeneratedRegex(@"^Power Scheme GUID: (\{?[0-9A-Fa-f\-]+\}?)\s+\((.+?)\)\s*\*?$")]
    private static partial Regex PowerCfgLineRegex();

    public static List<PowerPlan> GetPowerPlans()
    {
        var plans = new List<PowerPlan>();

        try
        {
            var psi = new ProcessStartInfo("powercfg", "/list")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return plans;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Guid activeGuid = GetActivePlanGuid();

            foreach (string line in output.Split('\n'))
            {
                var match = PowerCfgLineRegex().Match(line.Trim());
                if (!match.Success) continue;

                string guidStr = match.Groups[1].Value.Trim('{', '}');
                if (Guid.TryParse(guidStr, out Guid guid))
                {
                    plans.Add(new PowerPlan
                    {
                        Guid = guid,
                        Name = match.Groups[2].Value.Trim(),
                        IsActive = guid == activeGuid
                    });
                }
            }
        }
        catch { }

        return plans;
    }

    public static Guid GetActivePlanGuid()
    {
        IntPtr ptr = IntPtr.Zero;
        uint result = PowerGetActiveScheme(IntPtr.Zero, out ptr);
        if (result == ERROR_SUCCESS && ptr != IntPtr.Zero)
        {
            try
            {
                return Marshal.PtrToStructure<Guid>(ptr);
            }
            finally
            {
                LocalFree(ptr);
            }
        }
        return Guid.Empty;
    }

    public static bool SetActivePlan(Guid planGuid)
    {
        uint result = PowerSetActiveScheme(IntPtr.Zero, ref planGuid);
        return result == ERROR_SUCCESS;
    }

    public static bool IsPluggedIn()
    {
        if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            return status.ACLineStatus == 1;
        return false;
    }

    public static int? GetBatteryPercent()
    {
        if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status) && status.BatteryLifePercent <= 100)
            return status.BatteryLifePercent;
        return null;
    }
}

public class PlanStatusConverter : IValueConverter
{
    public static readonly PlanStatusConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
            return "Active now";
        return "Click to activate";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
