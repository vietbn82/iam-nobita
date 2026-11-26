using System.Globalization;

namespace IAmNobita;

enum Language { Auto, Vi, En }

/// <summary>UI language. Strings live inline as L.T("vi", "en") pairs — no resx for ~100 short strings.</summary>
static class L
{
    public static bool En { get; private set; }

    public static void Set(Language lang) =>
        En = lang switch
        {
            Language.Vi => false,
            Language.En => true,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "vi",
        };

    public static string T(string vi, string en) => En ? en : vi;

    public static string Mode(bool nobita) => nobita ? "Nobita Mode" : T("Cống hiến Mode", "Work Mode");

    /// <summary>History sources are stored as fixed English keys; v2.2 rows (Vietnamese text) pass through.</summary>
    public static string Source(string key) => key switch
    {
        "Timer" => T("Hẹn giờ", "Timer"),
        "Command line" => T("Dòng lệnh", "Command line"),
        "App start" => T("Mở app", "App start"),
        "App exit" => T("Thoát app", "App exit"),
        "Lock" => T("Khóa máy", "Lock"),
        "Unlock" => T("Mở khóa", "Unlock"),
        "Sleep" => T("Sleep", "Sleep"),
        "Resume" => T("Thức dậy", "Resume"),
        _ when key.StartsWith("Schedule: ") => T("Lịch: ", "Schedule: ") + key[10..],
        "Schedule" => T("Lịch", "Schedule"),
        _ => key, // Hotkey, Menu, Double-click, legacy rows
    };
}
