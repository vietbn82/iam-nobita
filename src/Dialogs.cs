namespace IAmNobita;

/// <summary>Base for the small dialogs: fixed, centered, DPI-scaled, error label + OK/Hủy at bottom.</summary>
abstract class DialogBase : Form
{
    protected readonly TableLayoutPanel Grid = NewGrid();
    protected readonly Label Error = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(640, 0) };

    protected DialogBase(string title)
    {
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = title;
        Icon = TrayApp.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        TopMost = true;
        Grid.Padding = new Padding(12);
        Controls.Add(Grid);
    }

    protected static TableLayoutPanel NewGrid()
    {
        var grid = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return grid;
    }

    protected static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) });
        control.Anchor = AnchorStyles.Left;
        grid.Controls.Add(control);
    }

    protected static void AddFull(TableLayoutPanel grid, Control control)
    {
        grid.Controls.Add(control);
        grid.SetColumnSpan(control, 2);
    }

    /// <summary><paramref name="validate"/> returns error text, or null to close with OK.</summary>
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
        AddFull(Grid, Error);
        AddFull(Grid, buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected static ComboBox Combo(string[] items, int selected)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        c.Items.AddRange(items);
        c.SelectedIndex = Math.Clamp(selected, 0, items.Length - 1);
        return c;
    }

    protected static NumericUpDown Number(int min, int max, int value, int step = 1) =>
        new() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Increment = step, Width = 70 };
}

sealed class SettingsForm : DialogBase
{
    // Chung
    readonly TextBox hotkeyBox = new() { ReadOnly = true, Width = 170, BackColor = SystemColors.Window, ShortcutsEnabled = false };
    readonly CheckBox confirm = new() { Text = "Hỏi xác nhận khi bấm hotkey", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Khởi động cùng Windows", AutoSize = true };
    readonly ComboBox activeMethod;
    readonly NumericUpDown idleSeconds;
    // Toast
    readonly CheckBox showToast = new() { Text = "Hiện toast khi đổi mode", AutoSize = true };
    readonly NumericUpDown toastSeconds, toastFontSize, toastOpacity;
    readonly CheckBox toastBold = new() { Text = "Chữ đậm", AutoSize = true };
    readonly CheckBox toastIcon = new() { Text = "Hiện icon", AutoSize = true };
    readonly ComboBox toastTheme, toastCorner;
    // Lịch
    readonly DataGridView schedule = new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        RowHeadersWidth = 24,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    // Cập nhật
    readonly CheckBox autoUpdate = new() { Text = "Tự kiểm tra bản mới mỗi ngày", AutoSize = true };

    readonly Settings original;
    uint mods;
    Keys key;
    List<ScheduleRule> rules = [];

    // Monday-first, matching the T2..CN columns.
    static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    static readonly string[] WeekNames = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
    static readonly string[] ModeNames = ["Nobita", "Cống hiến"];

    public Settings Result => original with
    {
        HotkeyModifiers = mods,
        HotkeyKey = key,
        ConfirmOnHotkey = confirm.Checked,
        ActiveMethod = (ActiveMethod)activeMethod.SelectedIndex,
        IdleSeconds = (int)idleSeconds.Value,
        ShowToast = showToast.Checked,
        ToastSeconds = (int)toastSeconds.Value,
        ToastFontSize = (int)toastFontSize.Value,
        ToastBold = toastBold.Checked,
        ToastShowIcon = toastIcon.Checked,
        ToastTheme = (ToastTheme)toastTheme.SelectedIndex,
        ToastCorner = (ToastCorner)toastCorner.SelectedIndex,
        ToastOpacity = (int)toastOpacity.Value,
        Schedules = rules,
        AutoCheckUpdate = autoUpdate.Checked,
    };

    public bool StartWithWindows => startup.Checked;

    /// <param name="tryRegister">Registers the hotkey; false if another app owns it.</param>
    /// <param name="checkUpdate">Runs a manual update check.</param>
    public SettingsForm(Settings s, Func<uint, Keys, bool> tryRegister, Action checkUpdate) : base("Cài đặt — I'm Nobita")
    {
        original = s;
        (mods, key) = (s.HotkeyModifiers, s.HotkeyKey);

        // --- Chung
        hotkeyBox.Text = s.HotkeyText;
        hotkeyBox.PreviewKeyDown += (_, e) => e.IsInputKey = true; // capture Tab/arrows too
        hotkeyBox.KeyDown += CaptureHotkey;
        var clear = new Button { Text = "Xóa", AutoSize = true };
        clear.Click += (_, _) => { (mods, key) = (0, Keys.None); hotkeyBox.Text = Settings.FormatHotkey(0, Keys.None); };
        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        hotkeyRow.Controls.AddRange([hotkeyBox, clear]);
        confirm.Checked = s.ConfirmOnHotkey;
        startup.Checked = Settings.StartWithWindows;
        activeMethod = Combo(["Lắc chuột", "Phím F15", "Cả hai"], (int)s.ActiveMethod);
        idleSeconds = Number(30, 600, s.IdleSeconds, 10);

        var general = NewGrid();
        AddRow(general, "Hotkey đổi mode", hotkeyRow);
        AddFull(general, new Label { Text = "Bấm vào ô rồi nhấn tổ hợp phím mới.", AutoSize = true, ForeColor = SystemColors.GrayText });
        AddFull(general, confirm);
        AddFull(general, startup);
        AddRow(general, "Kiểu giữ active", activeMethod);
        AddRow(general, "Rảnh bao lâu thì giữ active (giây)", idleSeconds);

        // --- Toast
        showToast.Checked = s.ShowToast;
        toastSeconds = Number(1, 10, s.ToastSeconds);
        toastFontSize = Number(8, 20, s.ToastFontSize);
        toastBold.Checked = s.ToastBold;
        toastIcon.Checked = s.ToastShowIcon;
        toastTheme = Combo(["Tự động (theo Windows)", "Sáng", "Tối"], (int)s.ToastTheme);
        toastCorner = Combo(["Dưới phải", "Dưới trái", "Trên phải", "Trên trái"], (int)s.ToastCorner);
        toastOpacity = Number(50, 100, s.ToastOpacity, 5);
        var preview = new Button { Text = "Xem thử", AutoSize = true };
        preview.Click += (_, _) => Toast.Show(TrayApp.AppIcon, "Nobita Mode", Result with { ShowToast = true });

        var toast = NewGrid();
        AddFull(toast, showToast);
        AddRow(toast, "Thời gian hiện (giây)", toastSeconds);
        AddRow(toast, "Cỡ chữ", toastFontSize);
        AddFull(toast, toastBold);
        AddFull(toast, toastIcon);
        AddRow(toast, "Theme", toastTheme);
        AddRow(toast, "Góc màn hình", toastCorner);
        AddRow(toast, "Độ hiển thị (%, 100 = rõ hẳn)", toastOpacity);
        AddFull(toast, preview);

        // --- Lịch
        BuildScheduleGrid(s.Schedules);
        var removeRow = new Button { Text = "Xóa dòng đang chọn", AutoSize = true };
        removeRow.Click += (_, _) =>
        {
            if (schedule.CurrentRow is { IsNewRow: false } row) schedule.Rows.Remove(row);
        };
        var scheduleHelp = new Label
        {
            Text = "Đến giờ, đúng ngày → tự chuyển sang mode đã chọn. Thêm dòng ở dòng trống cuối bảng.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Top,
        };
        var schedulePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        schedulePanel.Controls.Add(scheduleHelp);
        schedulePanel.Controls.Add(schedule);
        schedulePanel.Controls.Add(removeRow);

        // --- Cập nhật
        autoUpdate.Checked = s.AutoCheckUpdate;
        var checkNow = new Button { Text = "Kiểm tra ngay", AutoSize = true };
        checkNow.Click += (_, _) => checkUpdate();
        var releases = new LinkLabel { Text = "Trang phát hành trên GitHub", AutoSize = true };
        releases.LinkClicked += (_, _) => Updater.OpenReleasesPage();
        var update = NewGrid();
        AddFull(update, new Label { Text = $"Phiên bản hiện tại: v{Updater.Current}", AutoSize = true });
        AddFull(update, autoUpdate);
        AddFull(update, checkNow);
        AddFull(update, releases);

        var tabs = new TabControl { Size = new Size(660, 380) };
        foreach (var (title, content) in new (string, Control)[] { ("Chung", general), ("Toast", toast), ("Lịch", schedulePanel), ("Cập nhật", update) })
        {
            var page = new TabPage(title) { UseVisualStyleBackColor = true, AutoScroll = true };
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
        }
        AddFull(Grid, tabs);

        AddButtons(() =>
        {
            var err = ReadSchedule();
            if (err != null) { tabs.SelectedIndex = 2; return err; }
            if (!tryRegister(mods, key)) { tabs.SelectedIndex = 0; return $"{Settings.FormatHotkey(mods, key)} đang bị app khác chiếm. Chọn tổ hợp khác."; }
            return null;
        });
    }

    void BuildScheduleGrid(List<ScheduleRule> existing)
    {
        schedule.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Bật", Width = 40 });
        schedule.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên", Width = 150 });
        schedule.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Giờ", Width = 60 });
        var mode = new DataGridViewComboBoxColumn { HeaderText = "Chuyển sang", Width = 95, FlatStyle = FlatStyle.Flat };
        mode.Items.AddRange(ModeNames);
        schedule.Columns.Add(mode);
        foreach (var day in WeekNames)
            schedule.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = day, Width = 34 });

        foreach (var r in existing)
            schedule.Rows.Add([r.Enabled, r.Name, r.Time.ToString("HH:mm"), ModeNames[r.Nobita ? 0 : 1], .. Week.Select(d => (object)r.Days.Contains(d))]);

        schedule.DefaultValuesNeeded += (_, e) =>
        {
            e.Row.Cells[0].Value = true;
            e.Row.Cells[2].Value = "08:00";
            e.Row.Cells[3].Value = ModeNames[0];
            for (int i = 0; i < 7; i++) e.Row.Cells[4 + i].Value = i < 5; // T2–T6
        };
    }

    /// <summary>Grid → <see cref="rules"/>. Returns error text for the first bad row.</summary>
    string? ReadSchedule()
    {
        schedule.EndEdit();
        var list = new List<ScheduleRule>();
        foreach (DataGridViewRow row in schedule.Rows)
        {
            if (row.IsNewRow) continue;
            int n = row.Index + 1;
            var timeText = (row.Cells[2].Value as string ?? "").Trim();
            if (!TimeOnly.TryParseExact(timeText, ["H:mm", "HH:mm"], out var time))
                return $"Lịch dòng {n}: giờ \"{timeText}\" không hợp lệ (vd. 08:30).";
            var days = Week.Where((_, i) => row.Cells[4 + i].Value is true).ToArray();
            if (days.Length == 0) return $"Lịch dòng {n}: chọn ít nhất 1 ngày.";
            list.Add(new ScheduleRule
            {
                Enabled = row.Cells[0].Value is true,
                Name = (row.Cells[1].Value as string ?? "").Trim(),
                Time = time,
                Nobita = row.Cells[3].Value as string != ModeNames[1],
                Days = days,
            });
        }
        rules = list;
        return null;
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
    readonly NumericUpDown hours, minutes;

    public int TotalMinutes => (int)(hours.Value * 60 + minutes.Value);

    public CustomTimerForm(int initialMinutes) : base("Hẹn giờ đổi mode")
    {
        hours = Number(0, 23, initialMinutes / 60);
        minutes = Number(0, 59, initialMinutes % 60);
        AddRow(Grid, "Giờ", hours);
        AddRow(Grid, "Phút", minutes);
        AddButtons(() => TotalMinutes >= 1 ? null : "Tối thiểu 1 phút.");
    }
}
