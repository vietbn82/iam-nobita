# I'm Nobita

Windows tray app that keeps your PC "active" while you're away.

- **Cống hiến Mode** — normal (default).
- **Nobita Mode** — after N seconds idle, nudges the mouse 1px and/or taps invisible `F15`; blocks sleep / screen-off.

## Install

PowerShell (no admin) — installs/updates to `%LOCALAPPDATA%\Programs\IAmNobita`, adds Start Menu shortcut, launches:

```powershell
irm https://raw.githubusercontent.com/vietbn82/iam-nobita/main/install.ps1 | iex
```

Or download the exe directly (no install needed, run from anywhere):
https://github.com/vietbn82/iam-nobita/releases/latest/download/IAmNobita.exe

> Unsigned exe: a browser download may show SmartScreen "Windows protected your PC" → `More info` → `Run anyway`. The PowerShell install avoids that prompt.

## Usage

| Action | How |
|---|---|
| Toggle mode | `Ctrl+F12` (configurable) or double-click tray icon |
| Switch once, later | Tray → `Hẹn giờ đổi mode` (15m / 30m / 1h / 2h / 4h / custom) |
| Weekly schedule | `Cài đặt…` → `Lịch`: time + target mode + weekdays, many rows |
| Start with Windows | Tray → `Khởi động cùng Windows` |
| Settings | Tray → `Cài đặt…` — saved to `%APPDATA%\IAmNobita\settings.json` |
| Update | Auto daily check of GitHub Releases, or Tray → `Kiểm tra cập nhật` |
| Exit | Tray → `Nghỉ khỏe!` |

Settings tabs: **Chung** (hotkey, confirm, startup, mouse/F15/both, idle seconds) · **Toast** (on/off, duration, font size, bold, icon, theme, corner, opacity, preview) · **Lịch** · **Cập nhật**.

## Build

Requires .NET 10 SDK.

```sh
dotnet test tests/IAmNobita.Tests
dotnet publish src -c Release -o publish
```

Output: `publish/IAmNobita.exe` — single self-contained file, runs on any Windows 10/11 x64 without installing .NET.

## Release

Self-update looks for asset `IAmNobita.exe` on the latest GitHub release (tag `vX.Y.Z`, must match `<Version>` in `src/IAmNobita.csproj`):

```sh
gh release create v2.1.0 publish/IAmNobita.exe
```
