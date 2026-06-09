# Battery Mode Controller

A lightweight Windows WPF app that lets you manually or automatically switch Windows power plans when you plug or unplug your laptop.

## Features

- **Manual mode** — pick any power plan and apply it with one click
- **Auto mode** — assign one plan for AC power and another for battery; the app switches them automatically when you plug/unplug
- **Auto-detects** your current AC/battery status and shows battery percentage
- **Follows** Windows light/dark theme in real time
- **Active plan indicator** — see which plan is currently active in both manual and auto views
- **Preferences saved** — your AC and battery plan choices persist between sessions
- **Tooltips** on every control explain what it does

## Requirements

- Windows 10 or Windows 11
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

## How to Use

### Quick start

1. Download the latest release from [Releases](https://github.com/ebru-ayoro/BatteryModeController/releases)
2. Extract the zip and run `BatteryModeController.exe`
3. The app appears in the system tray area as a small window

### Manual mode (default)

1. Select a power plan from the list by clicking its radio button
2. Click **Apply** to switch to that plan
3. The active plan shows the label *Active now* next to it

### Auto mode

1. Toggle the **Auto Mode** switch at the top
2. Choose an **AC plan** (used when plugged in) and a **Battery plan** (used on battery)
3. Click **Save Auto Settings** — the app immediately switches to the matching plan and will keep switching as you plug/unplug

### Exiting

Close the window normally. The app does not run in the background.

## Building from Source

```powershell
git clone https://github.com/ebru-ayoro/BatteryModeController.git
cd BatteryModeController
dotnet build
dotnet run
```

Requires the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).

## How It Works

- Enumerates power plans via `powercfg /list`
- Reads/writes the active scheme using `PowerGetActiveScheme` / `PowerSetActiveScheme` (P/Invoke from `powrprof.dll`)
- Detects AC status and battery percentage via `GetSystemPowerStatus` (kernel32)
- Listens to `SystemEvents.PowerModeChanged` for plug/unplug events
- Polls the Windows registry (`AppsUseLightTheme`) every 1.5 s to follow dark/light theme
- Saves AC/Battery plan preferences as JSON to `%LOCALAPPDATA%\BatteryModeController\settings.json`

## Known Plan GUIDs

| GUID | Name |
|------|------|
| `a1841308-3541-4fab-bc81-f71556f20b4a` | Power Saver |
| `381b4222-f694-41f0-9685-ff5bb260df2e` | Balanced |
| `8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c` | High Performance |
| `e9a42b02-d5df-448d-aa00-03f14749eb61` | Ultimate Performance |
| `4dd17c4d-303f-43b8-842b-67f73ed2a6e0` | Power Saver (extended) |
| `9f358a54-7529-46f8-96df-ea4efb847653` | High Performance (alternate) |
| `12ac29ce-a5dc-4bf6-a764-6cae687b3983` | Ultra Performance |

## License

MIT — see [LICENSE](LICENSE) for details.
