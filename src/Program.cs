using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IAmNobita;

enum Command { None, Toggle, Nobita, Work }

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Updater.AfterUpdate(args); // before the mutex: old instance may still be exiting
        var command = args.Select(a => a.ToLowerInvariant()).FirstOrDefault(a => a is "--toggle" or "--nobita" or "--work") switch
        {
            "--toggle" => Command.Toggle,
            "--nobita" => Command.Nobita,
            "--work" => Command.Work,
            _ => Command.None,
        };
        using var mutex = new Mutex(true, @"Local\IAmNobita", out bool firstInstance);
        if (!firstInstance)
        {
            if (command != Command.None) HotkeyWindow.Send(command); // hand over to the running instance
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(command));
    }
}

/// <summary>Tray-only app: no window, nothing in Alt+Tab / Task View.</summary>
sealed class TrayApp : ApplicationContext
{
    const string Title = "I'm Nobita";
    static readonly TimeSpan UpdateCheckEvery = TimeSpan.FromHours(24);
    static readonly TimeSpan[] Presets = [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(4)];

    public static readonly Icon AppIcon = LoadIcon("nobita_nobi.ico");
    static readonly Icon WorkingIcon = LoadIcon("working.ico");

    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkey;
    ToolStripMenuItem modeItem = null!, timerMenu = null!;
    readonly System.Windows.Forms.Timer tick = new() { Interval = 1000 };
    Settings settings = Settings.Load();
    bool nobita;              // false = "Cống hiến Mode" (default), true = "Nobita Mode" (keep active)
    long? switchAt;           // Environment.TickCount64 deadline of the one-shot mode timer
    int activeTimer = -1;     // index of the checked item in timerMenu
    DateTime lastTick = DateTime.Now;
    long nextUpdateCheck = Environment.TickCount64 + 15_000;
    bool dialogOpen, checkingUpdate;

    string ModeText => L.Mode(nobita);
    Icon ModeIcon => nobita ? AppIcon : WorkingIcon;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public TrayApp(Command command)
    {
        L.Set(settings.Language);
        tray = new NotifyIcon { Visible = true };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle("Double-click"); };
        BuildMenu();

        hotkey = new HotkeyWindow(OnHotkey, Apply);
        if (!hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey))
            Toast.Show(WorkingIcon, L.T($"{settings.HotkeyText} bị app khác chiếm", $"{settings.HotkeyText} is taken by another app"), settings with { ShowToast = true, ToastSeconds = 4 });

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Log("App start");
        PurgeHistory();

        tick.Tick += (_, _) => OnTick();
        tick.Start();
        Refresh();
        Apply(command);
    }

    /// <summary>(Re)builds the tray menu in the current language.</summary>
    void BuildMenu()
    {
        var menu = new ContextMenuStrip();
        modeItem = new ToolStripMenuItem("", null, (_, _) => Toggle("Menu")) { Font = new Font(menu.Font, FontStyle.Bold) };

        timerMenu = new ToolStripMenuItem(L.T("Hẹn giờ đổi mode", "Mode timer"));
        for (int i = 0; i < Presets.Length; i++)
        {
            int index = i;
            timerMenu.DropDownItems.Add(new ToolStripMenuItem(FormatSpan(Presets[i]), null, (_, _) => StartTimer(Presets[index], index)));
        }
        timerMenu.DropDownItems.Add(new ToolStripMenuItem(L.T("Tùy chỉnh…", "Custom…"), null, (_, _) => CustomTimer(Presets.Length)));
        timerMenu.DropDownItems.Add(new ToolStripSeparator());
        timerMenu.DropDownItems.Add(new ToolStripMenuItem(L.T("Hủy hẹn giờ", "Cancel timer"), null, (_, _) => { switchAt = null; Refresh(); }));

        var skipToday = new ToolStripMenuItem(L.T("Tạm tắt lịch hôm nay", "Skip schedule today"), null, (_, _) => SkipToday());
        var startup = new ToolStripMenuItem(L.T("Khởi động cùng Windows", "Start with Windows"), null, (_, _) => Settings.StartWithWindows = !Settings.StartWithWindows);

        menu.Items.AddRange([
            modeItem,
            timerMenu,
            skipToday,
            startup,
            new ToolStripMenuItem(L.T("Cài đặt…", "Settings…"), null, (_, _) => OpenSettings()),
            new ToolStripMenuItem(L.T("Lịch sử đổi mode…", "Mode history…"), null, (_, _) => OpenHistory()),
            new ToolStripMenuItem(L.T("Kiểm tra cập nhật", "Check for updates"), null, (_, _) => _ = CheckUpdate(manual: true)),
            new ToolStripSeparator(),
            new ToolStripMenuItem(L.T("Nghỉ khỏe!", "Exit"), null, (_, _) => ExitThread()),
        ]);
        menu.Opening += (_, _) =>
        {
            startup.Checked = Settings.StartWithWindows;
            skipToday.Checked = settings.SkipDates.Contains(Today);
        };

        var old = tray.ContextMenuStrip;
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    /// <param name="source">History key (see <see cref="L.Source"/>).</param>
    /// <param name="inToast">Automatic switches (timer, schedule, command line) also name their source in the toast.</param>
    void Toggle(string source, bool inToast = false)
    {
        nobita = !nobita;
        Native.KeepAwake(nobita);
        Log(source);
        Refresh();
        Toast.Show(ModeIcon, inToast ? $"{L.Source(source)}: {ModeText}" : ModeText, settings);
    }

    void Apply(Command command)
    {
        bool target = command switch { Command.Toggle => !nobita, Command.Nobita => true, Command.Work => false, _ => nobita };
        if (target != nobita) Toggle("Command line", inToast: true);
    }

    void Log(string source)
    {
        try { History.Default.Add(DateTime.Now, nobita, source); }
        catch { } // ponytail: history is best-effort; a locked/corrupt DB must never break mode switching
    }

    void PurgeHistory()
    {
        if (settings.HistoryKeepDays <= 0) return;
        try { History.Default.Purge(DateTime.Today.AddDays(-settings.HistoryKeepDays)); }
        catch { } // best-effort, same as Log
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) Log("Lock");
        else if (e.Reason == SessionSwitchReason.SessionUnlock) Log("Unlock");
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) Log("Sleep");
        else if (e.Mode == PowerModes.Resume) Log("Resume");
    }

    void SkipToday()
    {
        var dates = settings.SkipDates.ToList();
        if (!dates.Remove(Today)) dates.Add(Today);
        settings = settings with { SkipDates = [.. dates.Order()] };
        settings.Save();
        Toast.Show(ModeIcon, dates.Contains(Today) ? L.T("Lịch tạm tắt hôm nay", "Schedule skipped today") : L.T("Lịch chạy lại hôm nay", "Schedule back on today"), settings);
    }

    void OnHotkey()
    {
        if (dialogOpen) return;
        if (settings.ConfirmOnHotkey)
        {
            dialogOpen = true;
            var answer = MessageBox.Show(L.T($"Đang xài {ModeText}\nCó chắc muốn chuyển không?", $"Currently in {ModeText}\nSwitch mode?"), Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            dialogOpen = false;
            if (answer != DialogResult.Yes) return;
        }
        Toggle("Hotkey");
    }

    void OnTick()
    {
        var now = DateTime.Now;
        var rule = Schedule.Due(settings.Schedules, lastTick, now, settings.SkipDates);
        bool newDay = now.Date != lastTick.Date;
        lastTick = now;
        if (newDay) PurgeHistory();
        if (rule != null && rule.Nobita != nobita)
            Toggle(rule.Name.Length > 0 ? $"Schedule: {rule.Name}" : "Schedule", inToast: true);

        if (switchAt is long due && Environment.TickCount64 >= due)
        {
            switchAt = null;
            Toggle("Timer", inToast: true);
        }

        if (nobita && Native.IdleTime() >= TimeSpan.FromSeconds(settings.IdleSeconds))
            Native.KeepActive(settings.ActiveMethod);

        if (settings.AutoCheckUpdate && Environment.TickCount64 >= nextUpdateCheck)
        {
            nextUpdateCheck = Environment.TickCount64 + (long)UpdateCheckEvery.TotalMilliseconds;
            _ = CheckUpdate(manual: false);
        }

        UpdateTooltip();
    }

    async Task CheckUpdate(bool manual)
    {
        if (checkingUpdate) return;
        checkingUpdate = true;
        try
        {
            var release = await Updater.GetLatestAsync();
            if (release == null || release.Version <= Updater.Current)
            {
                if (manual)
                    MessageBox.Show(release == null
                            ? L.T("Chưa có bản phát hành nào trên GitHub.", "No release on GitHub yet.")
                            : L.T($"Đang dùng bản mới nhất (v{Updater.Current}).", $"You have the latest version (v{Updater.Current})."),
                        Title, MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }
            if (!manual && dialogOpen) return; // don't stack dialogs; retried next day or via menu

            Toast.Show(AppIcon, L.T($"Có bản mới {release.Tag}", $"New version {release.Tag}"), settings);
            var answer = MessageBox.Show(L.T($"Có bản {release.Tag} (đang dùng v{Updater.Current}).\nCập nhật ngay?",
                    $"Version {release.Tag} is available (you have v{Updater.Current}).\nUpdate now?"), Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            if (answer != DialogResult.Yes) return;

            await Updater.InstallAsync(release);
            ExitThread();
        }
        catch (Exception e) // network, disk, permissions: never crash the tray app
        {
            if (manual && MessageBox.Show(L.T($"Không cập nhật được:\n{e.Message}\n\nMở trang phát hành để tải tay?",
                        $"Update failed:\n{e.Message}\n\nOpen the releases page to download manually?"), Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly) == DialogResult.Yes)
                Updater.OpenReleasesPage();
        }
        finally
        {
            checkingUpdate = false;
        }
    }

    void StartTimer(TimeSpan span, int index)
    {
        switchAt = Environment.TickCount64 + (long)span.TotalMilliseconds;
        activeTimer = index;
        Refresh();
        Toast.Show(ModeIcon, L.T($"Đổi mode sau {Format(span)}", $"Switching in {Format(span)}"), settings);
    }

    void CustomTimer(int index)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        using var form = new CustomTimerForm(settings.CustomTimerMinutes);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = settings with { CustomTimerMinutes = form.TotalMinutes };
            settings.Save();
            StartTimer(TimeSpan.FromMinutes(form.TotalMinutes), index);
        }
        dialogOpen = false;
    }

    void OpenSettings()
    {
        if (dialogOpen) return;
        dialogOpen = true;
        hotkey.Unregister(); // so the capture box can receive the current combo
        using var form = new SettingsForm(settings, hotkey.Register, () => _ = CheckUpdate(manual: true));
        if (form.ShowDialog() == DialogResult.OK)
        {
            var languageChanged = form.Result.Language != settings.Language;
            settings = form.Result; // hotkey already registered by the dialog's OK validation
            settings.Save();
            if (form.StartWithWindows != Settings.StartWithWindows) Settings.StartWithWindows = form.StartWithWindows;
            if (languageChanged)
            {
                L.Set(settings.Language);
                BuildMenu();
            }
            PurgeHistory();
        }
        else
        {
            hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey);
        }
        dialogOpen = false;
        Refresh();
    }

    void OpenHistory()
    {
        if (dialogOpen) return;
        dialogOpen = true;
        using (var form = new HistoryForm(History.Default)) form.ShowDialog();
        dialogOpen = false;
    }

    void Refresh()
    {
        modeItem.Text = ModeText;
        modeItem.ShortcutKeyDisplayString = settings.HotkeyKey == Keys.None ? null : settings.HotkeyText;
        tray.Icon = ModeIcon;
        for (int i = 0; i < timerMenu.DropDownItems.Count; i++)
            if (timerMenu.DropDownItems[i] is ToolStripMenuItem item) item.Checked = switchAt != null && i == activeTimer;
        UpdateTooltip();
    }

    void UpdateTooltip()
    {
        var text = switchAt is long due
            ? $"{ModeText} — {L.T("đổi sau", "switching in")} {Format(TimeSpan.FromMilliseconds(Math.Max(0, due - Environment.TickCount64)))}"
            : ModeText;
        if (tray.Text != text) tray.Text = text;
    }

    static string Format(TimeSpan t) => t.ToString(@"h\:mm\:ss");

    static string FormatSpan(TimeSpan t) =>
        t.TotalHours >= 1 ? L.T($"{t.TotalHours:0} giờ", $"{t.TotalHours:0} h") : L.T($"{t.TotalMinutes:0} phút", $"{t.TotalMinutes:0} min");

    static Icon LoadIcon(string name) =>
        new(typeof(TrayApp).Assembly.GetManifestResourceStream(name)!, SystemInformation.SmallIconSize);

    protected override void ExitThreadCore()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch; // static events: unhook or they keep us alive
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        tick.Dispose();
        hotkey.Dispose();
        nobita = false; // exiting ends Nobita Mode
        Log("App exit");
        Native.KeepAwake(false);
        tray.Visible = false;
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>
/// Message-only window (HWND_MESSAGE): receives WM_HOTKEY and command-line commands from a second instance.
/// Invisible everywhere.
/// </summary>
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312, WM_COMMAND_ARG = 0x8001 /* WM_APP + 1 */, Id = 1;
    const uint MOD_NOREPEAT = 0x4000;
    const string Caption = "IAmNobita.Command";
    static readonly IntPtr HWND_MESSAGE = new(-3);
    readonly Action onHotkey;
    readonly Action<Command> onCommand;
    bool registered;

    public HotkeyWindow(Action onHotkey, Action<Command> onCommand)
    {
        this.onHotkey = onHotkey;
        this.onCommand = onCommand;
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE, Caption = Caption });
    }

    /// <summary>From a second instance: forward <paramref name="command"/> to the running one.</summary>
    public static void Send(Command command)
    {
        var hwnd = Native.FindWindowEx(HWND_MESSAGE, IntPtr.Zero, null, Caption);
        if (hwnd != IntPtr.Zero) Native.PostMessage(hwnd, WM_COMMAND_ARG, (IntPtr)command, IntPtr.Zero);
    }

    /// <summary>Replaces any current hotkey. Keys.None = no hotkey (always succeeds).</summary>
    public bool Register(uint mods, Keys key)
    {
        Unregister();
        if (key == Keys.None) return true;
        return registered = Native.RegisterHotKey(Handle, Id, mods | MOD_NOREPEAT, (uint)key);
    }

    public void Unregister()
    {
        if (registered) Native.UnregisterHotKey(Handle, Id);
        registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) onHotkey();
        else if (m.Msg == WM_COMMAND_ARG && Enum.IsDefined((Command)(int)m.WParam)) onCommand((Command)(int)m.WParam);
        else base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}

static class Native
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern short GetKeyState(int vk);

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("kernel32.dll")]
    static extern uint SetThreadExecutionState(uint flags);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize, dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    public static bool IsWinKeyDown() => (GetKeyState(0x5B) | GetKeyState(0x5C)) < 0; // VK_LWIN | VK_RWIN

    public static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime))
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Resets the system idle timer. Mouse: 1px right then back (net zero). F15: key absent on real keyboards, types nothing.
    /// </summary>
    public static void KeepActive(ActiveMethod method)
    {
        const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, MOUSEEVENTF_MOVE = 1, KEYEVENTF_KEYUP = 2;
        const ushort VK_F15 = 0x7E;
        var inputs = new List<INPUT>();
        if (method != ActiveMethod.F15)
        {
            inputs.Add(new() { type = INPUT_MOUSE, u = new() { mi = new() { dx = 1, dwFlags = MOUSEEVENTF_MOVE } } });
            inputs.Add(new() { type = INPUT_MOUSE, u = new() { mi = new() { dx = -1, dwFlags = MOUSEEVENTF_MOVE } } });
        }
        if (method != ActiveMethod.Mouse)
        {
            inputs.Add(new() { type = INPUT_KEYBOARD, u = new() { ki = new() { wVk = VK_F15 } } });
            inputs.Add(new() { type = INPUT_KEYBOARD, u = new() { ki = new() { wVk = VK_F15, dwFlags = KEYEVENTF_KEYUP } } });
        }
        SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<INPUT>());
    }

    /// <summary>Block sleep + screen-off while on; restore normal power behaviour when off.</summary>
    public static void KeepAwake(bool on)
    {
        const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;
        SetThreadExecutionState(on ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED : ES_CONTINUOUS);
    }
}
