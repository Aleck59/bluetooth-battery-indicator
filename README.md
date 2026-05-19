# 🔋 Bluetooth Battery Indicator

A lightweight Windows system tray application that monitors Bluetooth device battery levels in real-time. Built with .NET 8, WPF, and Fluent Design.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows)
![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **System tray icon** with dynamic battery level display (color-coded)
- **Tooltip on hover** showing all connected devices and their battery percentage
- **Flyout panel** — click the tray icon for a quick-glance popup with device list
- **Settings window** with Fluent Design (Mica backdrop, Windows 11 style)
- **Device toggles** — choose which devices appear in the tooltip
- **Device nicknames** — rename devices for cleaner display
- **Device type icons** — auto-detected (🎧🖱⌨🎮) with manual override
- **Battery history** — 7-day sparkline graph showing drain over time
- **Low battery notifications** — Windows toast alert when a device drops below threshold
- **Configurable polling** — 30s / 1min / 5min refresh intervals
- **Auto-start with Windows** — via installer
- **Auto-update** — checks GitHub for new versions on startup
- **Persisted settings** — all preferences saved between sessions
- **Dark/Light theme** — follows your Windows system theme

## Installation

### Download

Grab the latest release from the [Releases page](../../releases):

- **`BluetoothBatteryIndicator_Setup.exe`** — Installer (recommended). Adds Start Menu shortcut and auto-start.

### Requirements

- Windows 10 (build 19041+) or Windows 11
- Bluetooth adapter

## Usage

1. Launch the app — it starts minimized to the system tray
2. **Hover** the tray icon to see device names and battery levels
3. **Left-click** the tray icon to open the flyout panel
4. **Right-click** → **Settings** to open the full settings window
5. **Right-click** → **Exit** to close the app

### Settings Window

- Toggle devices on/off (controls what appears in the tooltip)
- Set device nicknames
- Override device type icons
- Choose which device battery to display on the tray icon
- Configure polling interval
- Enable/disable auto-update
- View battery drain history per device

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)

### Build

```powershell
dotnet build BluetoothBatteryIndicator/BluetoothBatteryIndicator.csproj
```

### Run

```powershell
dotnet run --project BluetoothBatteryIndicator
```

### Publish (self-contained)

```powershell
dotnet publish BluetoothBatteryIndicator/BluetoothBatteryIndicator.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

### Build Installer

Requires [Inno Setup](https://jrsoftware.org/isinfo.php):

```powershell
iscc installer/BluetoothBatteryIndicator.iss
```

## How It Works

- Uses Windows `DeviceWatcher` API for real-time device connect/disconnect detection
- Reads battery via BLE GATT Battery Service + Windows device property fallback + registry
- Supports both Bluetooth Classic and Bluetooth Low Energy devices
- Device classification via Bluetooth Class of Device (CoD) and BLE Appearance

## Tech Stack

- **C# / .NET 8 / WPF**
- **[WPF UI](https://github.com/lepoco/wpfui)** — Fluent Design controls and theming
- **Windows.Devices.Bluetooth** — WinRT Bluetooth APIs
- **Inno Setup** — Installer packaging

## Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

## License

[MIT](LICENSE)
