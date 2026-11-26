using System.Runtime.InteropServices;

namespace IAmNobita;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\IAmNobita", out bool firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}

/// <summary>Tray-only app: no window, nothing in Alt+Tab / Task View.</summary>
sealed class TrayApp : ApplicationContext
{
    const string Title = "I'm Nobita";
    static readonly TimeSpan IdleBeforeJiggle = TimeSpan.FromSeconds(60);
    static readonly (string Text, TimeSpan Span)[] Presets =
    [
        ("15 phút", TimeSpan.FromMinutes(15)),
        ("30 phút", TimeSpan.FromMinutes(30)),
        ("1 giờ", TimeSpan.FromHours(1)),
        ("2 giờ", TimeSpan.FromHours(2)),
        ("4 giờ", TimeSpan.FromHours(4)),
    ];

    public static readonly Icon AppIcon = LoadIcon("nobita_nobi.ico");
    static readonly Icon WorkingIcon = LoadIcon("working.ico");

    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkey;
    readonly ToolStripMenuItem modeItem, timerMenu, startupItem;
    readonly System.Windows.Forms.Timer tick = new() { Interval = 1000 };
    Settings settings = Settings.Load();
    bool nobita;              // false = "Cống hiến Mode" (default), true = "Nobita Mode" (jiggle)
    long? switchAt;           // Environment.TickCount64 deadline of the mode timer
    ToolStripMenuItem? activeTimerItem;
    bool dialogOpen;

    string ModeText => nobita ? "Nobita Mode" : "Cống hiến Mode";
    Icon ModeIcon => nobita ? AppIcon : WorkingIcon;

    public TrayApp()
    {
        var menu = new ContextMenuStrip();
        modeItem = new ToolStripMenuItem("", null, (_, _) => Toggle()) { Font = new Font(menu.Font, FontStyle.Bold) };

        timerMenu = new ToolStripMenuItem("Hẹn giờ đổi mode");
        foreach (var (text, span) in Presets)
            timerMenu.DropDownItems.Add(new ToolStripMenuItem(text, null, (s, _) => StartTimer(span, (ToolStripMenuItem)s!)));
        timerMenu.DropDownItems.Add(new ToolStripMenuItem("Tùy chỉnh…", null, (s, _) => CustomTimer((ToolStripMenuItem)s!)));
        timerMenu.DropDownItems.Add(new ToolStripSeparator());
        timerMenu.DropDownItems.Add(new ToolStripMenuItem("Hủy hẹn giờ", null, (_, _) => { switchAt = null; Refresh(); }));

        startupItem = new ToolStripMenuItem("Khởi động cùng Windows", null, (_, _) => Settings.StartWithWindows = !Settings.StartWithWindows);

        menu.Items.AddRange([
            modeItem,
            timerMenu,
            startupItem,
            new ToolStripMenuItem("Cài đặt…", null, (_, _) => OpenSettings()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Nghỉ khỏe!", null, (_, _) => ExitThread()),
        ]);
        menu.Opening += (_, _) => startupItem.Checked = Settings.StartWithWindows;

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };

        hotkey = new HotkeyWindow(OnHotkey);
        if (!hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey))
            Toast.Show(WorkingIcon, $"{settings.HotkeyText} bị app khác chiếm", settings.ToastSeconds + 2);

        tick.Tick += (_, _) => OnTick();
        tick.Start();
        Refresh();
    }

    void Toggle()
    {
        nobita = !nobita;
        Native.KeepAwake(nobita);
        Refresh();
        Toast.Show(ModeIcon, ModeText, settings.ToastSeconds);
    }

    void OnHotkey()
    {
        if (dialogOpen) return;
        if (settings.ConfirmOnHotkey)
        {
            dialogOpen = true;
            var answer = MessageBox.Show($"Đang xài {ModeText}\nCó chắc muốn chuyển không?", Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            dialogOpen = false;
            if (answer != DialogResult.Yes) return;
        }
        Toggle();
    }

    void OnTick()
    {
        if (switchAt is long due && Environment.TickCount64 >= due)
        {
            switchAt = null;
            Toggle();
        }
        if (nobita && Native.IdleTime() >= IdleBeforeJiggle) Native.Jiggle();
        UpdateTooltip();
    }

    void StartTimer(TimeSpan span, ToolStripMenuItem item)
    {
        switchAt = Environment.TickCount64 + (long)span.TotalMilliseconds;
        activeTimerItem = item;
        Refresh();
        Toast.Show(ModeIcon, $"Đổi mode sau {Format(span)}", settings.ToastSeconds);
    }

    void CustomTimer(ToolStripMenuItem item)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        using var form = new CustomTimerForm(settings.CustomTimerMinutes);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = settings with { CustomTimerMinutes = form.TotalMinutes };
            settings.Save();
            StartTimer(TimeSpan.FromMinutes(form.TotalMinutes), item);
        }
        dialogOpen = false;
    }

    void OpenSettings()
    {
        if (dialogOpen) return;
        dialogOpen = true;
        hotkey.Unregister(); // so the capture box can receive the current combo
        using var form = new SettingsForm(settings, hotkey.Register);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = form.Result; // hotkey already registered by the dialog's OK validation
            settings.Save();
            if (form.StartWithWindows != Settings.StartWithWindows) Settings.StartWithWindows = form.StartWithWindows;
        }
        else
        {
            hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey);
        }
        dialogOpen = false;
    }

    void Refresh()
    {
        modeItem.Text = ModeText;
        tray.Icon = ModeIcon;
        foreach (var item in timerMenu.DropDownItems.OfType<ToolStripMenuItem>())
            item.Checked = switchAt != null && item == activeTimerItem;
        UpdateTooltip();
    }

    void UpdateTooltip()
    {
        var text = switchAt is long due
            ? $"{ModeText} — đổi sau {Format(TimeSpan.FromMilliseconds(Math.Max(0, due - Environment.TickCount64)))}"
            : ModeText;
        if (tray.Text != text) tray.Text = text;
    }

    static string Format(TimeSpan t) => t.ToString(@"h\:mm\:ss");

    static Icon LoadIcon(string name) =>
        new(typeof(TrayApp).Assembly.GetManifestResourceStream(name)!, SystemInformation.SmallIconSize);

    protected override void ExitThreadCore()
    {
        tick.Dispose();
        hotkey.Dispose();
        Native.KeepAwake(false);
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>Message-only window (HWND_MESSAGE): receives WM_HOTKEY, invisible everywhere.</summary>
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312, Id = 1;
    const uint MOD_NOREPEAT = 0x4000;
    readonly Action onHotkey;
    bool registered;

    public HotkeyWindow(Action onHotkey)
    {
        this.onHotkey = onHotkey;
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
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

    // MOUSEINPUT is the largest union member, so this matches sizeof(INPUT) on x86 and x64.
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public MOUSEINPUT mi; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    public static bool IsWinKeyDown() => (GetKeyState(0x5B) | GetKeyState(0x5C)) < 0; // VK_LWIN | VK_RWIN

    public static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime))
            : TimeSpan.Zero;
    }

    /// <summary>Move cursor 1px right then back — resets the system idle timer, net zero movement.</summary>
    public static void Jiggle()
    {
        const uint INPUT_MOUSE = 0, MOUSEEVENTF_MOVE = 1;
        INPUT[] inputs =
        [
            new() { type = INPUT_MOUSE, mi = new() { dx = 1, dwFlags = MOUSEEVENTF_MOVE } },
            new() { type = INPUT_MOUSE, mi = new() { dx = -1, dwFlags = MOUSEEVENTF_MOVE } },
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Block sleep + screen-off while on; restore normal power behaviour when off.</summary>
    public static void KeepAwake(bool on)
    {
        const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;
        SetThreadExecutionState(on ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED : ES_CONTINUOUS);
    }
}
