# I'm Nobita

Windows tray app that keeps your PC "active" while you're away.

| Mode | Behaviour |
|---|---|
| **Cống hiến Mode** ("Dedicated" — default) | Normal. App does nothing. |
| **Nobita Mode** ("Lazy") | After N seconds idle, nudges the mouse 1px and/or taps the invisible `F15` key; blocks sleep and screen-off. |

> The app UI is in Vietnamese. Menu labels below are quoted as shown in the app, with an English translation.

## Install

PowerShell (no admin). Installs or updates to `%LOCALAPPDATA%\Programs\IAmNobita`, adds a Start Menu shortcut and launches the app:

```powershell
irm https://raw.githubusercontent.com/vietbn82/iam-nobita/main/install.ps1 | iex
```

Or download the exe directly. No install needed, run it from anywhere:
https://github.com/vietbn82/iam-nobita/releases/latest/download/IAmNobita.exe

> The exe is unsigned. A browser download may show SmartScreen "Windows protected your PC" → `More info` → `Run anyway`. The PowerShell install does not trigger that prompt.

## Usage

| Action | How |
|---|---|
| Toggle mode | `Ctrl+F12` (configurable) or double-click the tray icon |
| Switch once, later | Tray → `Hẹn giờ đổi mode` (Mode timer): 15m / 30m / 1h / 2h / 4h / custom |
| Weekly schedule | Tray → `Cài đặt…` (Settings) → `Lịch` (Schedule): time + target mode + weekdays, any number of rows |
| Start with Windows | Tray → `Khởi động cùng Windows` (Start with Windows) |
| Settings | Tray → `Cài đặt…` (Settings), saved to `%APPDATA%\IAmNobita\settings.json` |
| Update | Checks GitHub Releases daily, or Tray → `Kiểm tra cập nhật` (Check for updates) |
| Exit | Tray → `Nghỉ khỏe!` (Exit) |

Settings tabs:

| Tab | Options |
|---|---|
| `Chung` (General) | Hotkey, confirm before toggling, start with Windows, keep-active method (mouse / F15 / both), idle seconds before acting |
| `Toast` | On/off, duration, font size, bold, icon, theme (auto / light / dark), screen corner, opacity, preview |
| `Lịch` (Schedule) | Weekly rules: at `HH:mm` on selected weekdays, switch to a mode |
| `Cập nhật` (Update) | Current version, daily auto-check, check now |

## Build

Requires the .NET 10 SDK.

```sh
dotnet test tests/IAmNobita.Tests
dotnet publish src -c Release -o publish
```

Output: `publish/IAmNobita.exe`, a single self-contained file that runs on any Windows 10/11 x64 machine without installing .NET.

## Release

Self-update looks for an `IAmNobita.exe` asset on the latest GitHub release. The tag (`vX.Y.Z`) must match `<Version>` in `src/IAmNobita.csproj`:

```sh
gh release create v2.1.0 publish/IAmNobita.exe
```
