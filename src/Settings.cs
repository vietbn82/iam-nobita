using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace IAmNobita;

enum ActiveMethod { Mouse, F15, Both }
enum ToastTheme { Auto, Light, Dark }
enum ToastCorner { BottomRight, BottomLeft, TopRight, TopLeft }

sealed record Settings
{
    // RegisterHotKey modifier flags
    public const uint ModAlt = 1, ModCtrl = 2, ModShift = 4, ModWin = 8;

    // General
    public uint HotkeyModifiers { get; set; } = ModCtrl;
    public Keys HotkeyKey { get; set; } = Keys.F12;
    public bool ConfirmOnHotkey { get; set; }
    public ActiveMethod ActiveMethod { get; set; } = ActiveMethod.Mouse;
    public int IdleSeconds { get; set; } = 60;
    public int CustomTimerMinutes { get; set; } = 60;

    // Toast
    public bool ShowToast { get; set; } = true;
    public int ToastSeconds { get; set; } = 2;
    public int ToastFontSize { get; set; } = 10;
    public bool ToastBold { get; set; }
    public bool ToastShowIcon { get; set; } = true;
    public ToastTheme ToastTheme { get; set; } = ToastTheme.Auto;
    public ToastCorner ToastCorner { get; set; } = ToastCorner.BottomRight;
    public int ToastOpacity { get; set; } = 100;

    // Schedule + update
    public List<ScheduleRule> Schedules { get; set; } = [];
    public bool AutoCheckUpdate { get; set; } = true;

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IAmNobita", "settings.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Settings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json);
            if (s != null)
                return s with
                {
                    IdleSeconds = Math.Clamp(s.IdleSeconds, 30, 600),
                    CustomTimerMinutes = Math.Clamp(s.CustomTimerMinutes, 1, 24 * 60 - 1),
                    ToastSeconds = Math.Clamp(s.ToastSeconds, 1, 10),
                    ToastFontSize = Math.Clamp(s.ToastFontSize, 8, 20),
                    ToastOpacity = Math.Clamp(s.ToastOpacity, 50, 100),
                    Schedules = s.Schedules ?? [],
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Không lưu được cài đặt:\n{e.Message}", "I'm Nobita", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    [JsonIgnore]
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
