# I'm Nobita

Windows tray app that keeps your PC "active" while you're away.

| Mode | Behaviour |
|---|---|
| **Work Mode** (default; "Cống hiến Mode" in Vietnamese) | Normal. App does nothing. |
| **Nobita Mode** | After N seconds idle, nudges the mouse 1px and/or taps the invisible `F15` key; blocks sleep and screen-off. |

UI in English or Vietnamese: follows the Windows language by default, change it in Settings → General → Language.

## Install

PowerShell (no admin). Installs or updates to `%LOCALAPPDATA%\Programs\IAmNobita`, adds a Start Menu shortcut and launches the app:

```powershell
irm https://raw.githubusercontent.com/vietbn82/iam-nobita/main/install.ps1 | iex
```

winget (pending review in [winget-pkgs](https://github.com/microsoft/winget-pkgs)):

```powershell
winget install vietbn82.IAmNobita
```

Or download the exe directly. No install needed, run it from anywhere:
https://github.com/vietbn82/iam-nobita/releases/latest/download/IAmNobita.exe

> The exe is unsigned. A browser download may show SmartScreen "Windows protected your PC" → `More info` → `Run anyway`. The PowerShell install does not trigger that prompt.

## Usage

| Action | How |
|---|---|
| Toggle mode | `Ctrl+F12` (configurable) or double-click the tray icon |
| Switch once, later | Tray → `Mode timer`: 15m / 30m / 1h / 2h / 4h / custom |
| Weekly schedule | Tray → `Settings…` → `Schedule`: time + target mode + weekdays, any number of rows |
| Days off | Settings → `Schedule` → days off list, or Tray → `Skip schedule today` |
| History | Tray → `Mode history…`: mode switches plus app start/exit, lock/unlock, sleep/resume. Last 30 days by default, any date range on demand. SQLite at `%APPDATA%\IAmNobita\history.db`, kept 365 days (configurable, 0 = forever) |
| Backup settings | Settings → `General` → `Export…` / `Import…` (JSON) |
| Start with Windows | Tray → `Start with Windows` |
| Update | Checks GitHub Releases daily, or Tray → `Check for updates` |
| Exit | Tray → `Exit` |

Settings are saved to `%APPDATA%\IAmNobita\settings.json`.

### Command line

```powershell
IAmNobita.exe --nobita   # switch to Nobita Mode
IAmNobita.exe --work     # switch to Work Mode
IAmNobita.exe --toggle   # switch to the other mode
```

If the app is already running, the command is sent to it; otherwise the app starts and applies it. Installed via winget, the command is `iamnobita`. Handy for scripts and Task Scheduler.

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
gh release create v2.3.0 publish/IAmNobita.exe
```

Then copy `winget/manifests/...` to a new version folder, update `PackageVersion`, `InstallerUrl` and `InstallerSha256`, run `winget validate`, and open a PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).

## License

[MIT](LICENSE)
