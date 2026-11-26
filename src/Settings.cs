using System.Text.Json;
using Microsoft.Win32;

namespace IAmNobita;

sealed record Settings
{
    // RegisterHotKey modifier flags
    public const uint ModAlt = 1, ModCtrl = 2, ModShift = 4, ModWin = 8;

    public uint HotkeyModifiers { get; set; } = ModCtrl;
    public Keys HotkeyKey { get; set; } = Keys.F12;
    public bool ConfirmOnHotkey { get; set; }
    public int ToastSeconds { get; set; } = 2;
    public int CustomTimerMinutes { get; set; } = 60;

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IAmNobita", "settings.json");

    public static Settings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
            if (s != null)
                return s with
                {
                    ToastSeconds = Math.Clamp(s.ToastSeconds, 1, 10),
                    CustomTimerMinutes = Math.Clamp(s.CustomTimerMinutes, 1, 24 * 60 - 1),
                };
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Không lưu được cài đặt:\n{e.Message}", "I'm Nobita", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string HotkeyText => FormatHotkey(HotkeyModifiers, HotkeyKey);

    public static string FormatHotkey(uint mods, Keys key)
    {
        if (key == Keys.None) return "(không có)";
        var parts = new List<string>();
        if ((mods & ModCtrl) != 0) parts.Add("Ctrl");
        if ((mods & ModAlt) != 0) parts.Add("Alt");
        if ((mods & ModShift) != 0) parts.Add("Shift");
        if ((mods & ModWin) != 0) parts.Add("Win");
        parts.Add(new KeysConverter().ConvertToString(key) ?? key.ToString());
        return string.Join("+", parts);
    }

    // Registry is the source of truth, so a moved exe gets fixed by toggling off/on.
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunValue = "IAmNobita";

    public static bool StartWithWindows
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunValue) is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }
}
