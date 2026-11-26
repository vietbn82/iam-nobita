# I'm Nobita

Windows tray app that keeps your PC "active" while you're away.

- **Cống hiến Mode** — normal (default).
- **Nobita Mode** — nudges the mouse 1px after 60s idle and blocks sleep / screen-off.

## Usage

| Action | How |
|---|---|
| Toggle mode | `Ctrl+F12` (configurable) or double-click tray icon |
| Auto-switch mode later | Tray → `Hẹn giờ đổi mode` (15m / 30m / 1h / 2h / 4h / custom) |
| Start with Windows | Tray → `Khởi động cùng Windows` |
| Settings (hotkey, toast duration, confirm) | Tray → `Cài đặt…` — saved to `%APPDATA%\IAmNobita\settings.json` |
| Exit | Tray → `Nghỉ khỏe!` |

## Build

Requires .NET 10 SDK.

```sh
dotnet publish src -c Release -o publish
```

Output: `publish/IAmNobita.exe` — single self-contained file, runs on any Windows 10/11 x64 without installing .NET.
