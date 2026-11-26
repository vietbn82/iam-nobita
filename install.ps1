# I'm Nobita installer — per-user, no admin.
#   irm https://raw.githubusercontent.com/vietbn82/iam-nobita/main/install.ps1 | iex
# Downloads the latest GitHub release, verifies SHA-256, installs to %LOCALAPPDATA%\Programs\IAmNobita,
# adds a Start Menu shortcut and launches it. Re-run to update.
param(
    [string]$Dir = (Join-Path $env:LOCALAPPDATA 'Programs\IAmNobita'),
    [switch]$NoLaunch,
    [switch]$NoShortcut
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue' # Invoke-WebRequest is very slow with the progress bar
$repo = 'vietbn82/iam-nobita'
$exe = Join-Path $Dir 'IAmNobita.exe'

$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'IAmNobita-installer' }
$asset = $release.assets | Where-Object name -eq 'IAmNobita.exe' | Select-Object -First 1
if (-not $asset) { throw "Release $($release.tag_name) has no IAmNobita.exe asset." }
Write-Host "I'm Nobita $($release.tag_name) -> $exe"

New-Item -ItemType Directory -Force $Dir | Out-Null
$tmp = "$exe.download"
Invoke-WebRequest $asset.browser_download_url -OutFile $tmp -UseBasicParsing

if ($asset.digest -like 'sha256:*') {
    $hash = (Get-FileHash $tmp -Algorithm SHA256).Hash
    if ($hash -ne $asset.digest.Substring(7)) {
        Remove-Item $tmp
        throw "Checksum mismatch - download discarded."
    }
}

# Replace a running copy at the same path.
Get-Process IAmNobita -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process -Force
Start-Sleep -Milliseconds 500
Move-Item $tmp $exe -Force

if (-not $NoShortcut) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) "I'm Nobita.lnk"
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $Dir
    $shortcut.Save()
}

if (-not $NoLaunch) { Start-Process $exe }
Write-Host "Done. Start Menu: I'm Nobita. To auto-start: tray menu -> 'Khoi dong cung Windows' (Start with Windows)."
