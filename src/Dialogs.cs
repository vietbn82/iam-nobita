namespace IAmNobita;

/// <summary>Base for the two small dialogs: fixed, centered, OK/Hủy row at bottom.</summary>
abstract class DialogBase : Form
{
    protected readonly TableLayoutPanel Grid = new() { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(12) };
    protected readonly Label Error = new() { AutoSize = true, ForeColor = Color.Firebrick };

    protected DialogBase(string title)
    {
        Text = title;
        Icon = TrayApp.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        TopMost = true;
        Controls.Add(Grid);
    }

    protected void AddRow(string label, Control control)
    {
        Grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
        Grid.Controls.Add(control);
    }

    protected void AddFull(Control control)
    {
        Grid.Controls.Add(control);
        Grid.SetColumnSpan(control, 2);
    }

    /// <summary>Adds error label + OK/Hủy. <paramref name="validate"/> returns error text, or null to close with OK.</summary>
    protected void AddButtons(Func<string?> validate)
    {
        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            var err = validate();
            Error.Text = err ?? "";
            if (err == null) DialogResult = DialogResult.OK;
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        AddFull(Error);
        AddFull(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

sealed class SettingsForm : DialogBase
{
    readonly TextBox hotkeyBox = new() { ReadOnly = true, Width = 170, BackColor = SystemColors.Window, ShortcutsEnabled = false };
    readonly CheckBox confirm = new() { Text = "Hỏi xác nhận khi bấm hotkey", AutoSize = true };
    readonly NumericUpDown toastSeconds = new() { Minimum = 1, Maximum = 10, Width = 60 };
    readonly CheckBox startup = new() { Text = "Khởi động cùng Windows", AutoSize = true };
    readonly Settings original;
    uint mods;
    Keys key;

    public Settings Result => original with
    {
        HotkeyModifiers = mods,
        HotkeyKey = key,
        ConfirmOnHotkey = confirm.Checked,
        ToastSeconds = (int)toastSeconds.Value,
    };

    public bool StartWithWindows => startup.Checked;

    /// <param name="tryRegister">Registers the hotkey; false if another app owns it.</param>
    public SettingsForm(Settings settings, Func<uint, Keys, bool> tryRegister) : base("Cài đặt — I'm Nobita")
    {
        original = settings;
        (mods, key) = (settings.HotkeyModifiers, settings.HotkeyKey);
        hotkeyBox.Text = settings.HotkeyText;
        confirm.Checked = settings.ConfirmOnHotkey;
        toastSeconds.Value = settings.ToastSeconds;
        startup.Checked = Settings.StartWithWindows;

        hotkeyBox.PreviewKeyDown += (_, e) => e.IsInputKey = true; // capture Tab/arrows too
        hotkeyBox.KeyDown += CaptureHotkey;
        var clear = new Button { Text = "Xóa", AutoSize = true };
        clear.Click += (_, _) => { key = Keys.None; mods = 0; hotkeyBox.Text = Settings.FormatHotkey(0, Keys.None); };
        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        hotkeyRow.Controls.AddRange([hotkeyBox, clear]);

        AddRow("Hotkey đổi mode", hotkeyRow);
        AddFull(new Label { Text = "Bấm vào ô rồi nhấn tổ hợp phím mới.", AutoSize = true, ForeColor = SystemColors.GrayText });
        AddRow("Thời gian hiện toast (giây)", toastSeconds);
        AddFull(confirm);
        AddFull(startup);
        AddButtons(() => tryRegister(mods, key) ? null : $"{Settings.FormatHotkey(mods, key)} đang bị app khác chiếm. Chọn tổ hợp khác.");
    }

    void CaptureHotkey(object? sender, KeyEventArgs e)
    {
        e.Handled = e.SuppressKeyPress = true;
        if (e.KeyCode is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin) return;

        uint m = 0;
        if (e.Control) m |= Settings.ModCtrl;
        if (e.Alt) m |= Settings.ModAlt;
        if (e.Shift) m |= Settings.ModShift;
        if (Native.IsWinKeyDown()) m |= Settings.ModWin;
        if (m == 0)
        {
            Error.Text = "Cần ít nhất 1 phím Ctrl / Alt / Shift / Win.";
            return;
        }
        (mods, key) = (m, e.KeyCode);
        hotkeyBox.Text = Settings.FormatHotkey(mods, key);
        Error.Text = "";
    }
}

sealed class CustomTimerForm : DialogBase
{
    readonly NumericUpDown hours = new() { Minimum = 0, Maximum = 23, Width = 60 };
    readonly NumericUpDown minutes = new() { Minimum = 0, Maximum = 59, Width = 60 };

    public int TotalMinutes => (int)(hours.Value * 60 + minutes.Value);

    public CustomTimerForm(int initialMinutes) : base("Hẹn giờ đổi mode")
    {
        hours.Value = initialMinutes / 60;
        minutes.Value = initialMinutes % 60;
        AddRow("Giờ", hours);
        AddRow("Phút", minutes);
        AddButtons(() => TotalMinutes >= 1 ? null : "Tối thiểu 1 phút.");
    }
}
