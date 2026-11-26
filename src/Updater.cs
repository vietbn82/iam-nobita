using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace IAmNobita;

/// <summary>Self-update from GitHub Releases: download asset IAmNobita.exe, verify SHA-256, swap exe, restart.</summary>
static class Updater
{
    const string Repo = "vietbn82/iam-nobita", AssetName = "IAmNobita.exe", AfterUpdateArg = "--after-update";
    public const string ReleasesPage = $"https://github.com/{Repo}/releases/latest";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5), DefaultRequestHeaders = { { "User-Agent", "IAmNobita" } } };

    public sealed record Release(Version Version, string Tag, string DownloadUrl, string? Sha256);

    public static Version Current { get; } = Normalize(typeof(Updater).Assembly.GetName().Version!);

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>Latest release, or null if none published / no exe asset.</summary>
    public static async Task<Release?> GetLatestAsync()
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            return new Release(Normalize(version), tag, asset.GetProperty("browser_download_url").GetString()!,
                digest?.StartsWith("sha256:") == true ? digest[7..] : null);
        }
        return null;
    }

    /// <summary>Downloads, swaps exe in place and starts the new one. Caller must exit right after.</summary>
    public static async Task InstallAsync(Release release)
    {
        var exe = Environment.ProcessPath!;
        string newPath = exe + ".new", oldPath = exe + ".old";

        await using (var src = await Http.GetStreamAsync(release.DownloadUrl))
        await using (var dst = File.Create(newPath))
            await src.CopyToAsync(dst);

        if (release.Sha256 != null)
        {
            string hash;
            await using (var f = File.OpenRead(newPath))
                hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(f));
            if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(newPath);
                throw new InvalidDataException(L.T("File tải về sai checksum — đã hủy cập nhật.", "Downloaded file failed the checksum — update cancelled."));
            }
        }

        File.Delete(oldPath);
        File.Move(exe, oldPath); // Windows allows renaming a running exe
        try { File.Move(newPath, exe); }
        catch { File.Move(oldPath, exe); throw; }

        Process.Start(exe, [AfterUpdateArg, Environment.ProcessId.ToString()]);
    }

    /// <summary>Run at startup: wait for the old process (so the single-instance mutex is free), drop leftover .old.</summary>
    public static void AfterUpdate(string[] args)
    {
        if (args is [AfterUpdateArg, var pidText] && int.TryParse(pidText, out var pid))
        {
            try { using var old = Process.GetProcessById(pid); old.WaitForExit(10_000); }
            catch (ArgumentException) { } // already gone
        }
        try { File.Delete(Environment.ProcessPath + ".old"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static void OpenReleasesPage() => Process.Start(new ProcessStartInfo(ReleasesPage) { UseShellExecute = true });
}
