using System.Text.Json;

namespace IAmNobita;

/// <summary>Base for the small dialogs: fixed, centered, DPI-scaled, error label + OK/Cancel at bottom.</summary>
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

    protected static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        foreach (var c in controls)
        {
            if (c is Label l) { l.AutoSize = true; l.Margin = new Padding(3, 7, 3, 3); }
            row.Controls.Add(c);
        }
        return row;
    }

    protected static Button Btn(string text, Action onClick)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary><paramref name="validate"/> returns error text, or null to close with OK.</summary>
    protected void AddButtons(Func<string?> validate)
    {
        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = L.T("Hủy", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
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

    protected static ComboBox Combo(string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        c.Items.AddRange(items);
        return c;
    }

    protected static void Pick(ComboBox c, int index) => c.SelectedIndex = Math.Clamp(index, 0, c.Items.Count - 1);

    protected static NumericUpDown Number(int min, int max, int step = 1) =>
        new() { Minimum = min, Maximum = max, Increment = step, Width = 70 };

    protected static void Set(NumericUpDown n, int value) => n.Value = Math.Clamp(value, (int)n.Minimum, (int)n.Maximum);
}

sealed class SettingsForm : DialogBase
{
    // Chung
    readonly ComboBox language = Combo([L.T("Tự động (theo Windows)", "Auto (Windows)"), "Tiếng Việt", "English"]);
    readonly TextBox hotkeyBox = new() { ReadOnly = true, Width = 170, BackColor = SystemColors.Window, ShortcutsEnabled = false };
    readonly CheckBox confirm = new() { Text = L.T("Hỏi xác nhận khi bấm hotkey", "Confirm before switching by hotkey"), AutoSize = true };
    readonly CheckBox startup = new() { Text = L.T("Khởi động cùng Windows", "Start with Windows"), AutoSize = true };
    readonly ComboBox activeMethod = Combo([L.T("Lắc chuột", "Mouse nudge"), L.T("Phím F15", "F15 key"), L.T("Cả hai", "Both")]);
    readonly NumericUpDown idleSeconds = Number(30, 600, 10);
    readonly NumericUpDown keepDays = Number(0, 3650, 30);
    // Toast
    readonly CheckBox showToast = new() { Text = L.T("Hiện toast khi đổi mode", "Show a toast when the mode changes"), AutoSize = true };
    readonly NumericUpDown toastSeconds = Number(1, 10), toastFontSize = Number(8, 20), toastOpacity = Number(50, 100, 5);
    readonly CheckBox toastBold = new() { Text = L.T("Chữ đậm", "Bold"), AutoSize = true };
    readonly CheckBox toastIcon = new() { Text = L.T("Hiện icon", "Show icon"), AutoSize = true };
    readonly ComboBox toastTheme = Combo([L.T("Tự động (theo Windows)", "Auto (Windows)"), L.T("Sáng", "Light"), L.T("Tối", "Dark")]);
    readonly ComboBox toastCorner = Combo([L.T("Dưới phải", "Bottom right"), L.T("Dưới trái", "Bottom left"), L.T("Trên phải", "Top right"), L.T("Trên trái", "Top left")]);
    // Lịch
    readonly DataGridView schedule = new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        RowHeadersWidth = 24,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    readonly DateTimePicker skipPicker = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly ListBox skipList = new() { Width = 130, Height = 64 };
    // Cập nhật
    readonly CheckBox autoUpdate = new() { Text = L.T("Tự kiểm tra bản mới mỗi ngày", "Check for updates daily"), AutoSize = true };

    Settings original;
    uint mods;
    Keys key;
    List<ScheduleRule> rules = [];

    // Monday-first, matching the day columns.
    static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    static string[] WeekNames => L.En ? ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] : ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
    static string[] ModeNames => ["Nobita", L.T("Cống hiến", "Work")];

    public Settings Result => original with
    {
        Language = (Language)language.SelectedIndex,
        HotkeyModifiers = mods,
        HotkeyKey = key,
        ConfirmOnHotkey = confirm.Checked,
        ActiveMethod = (ActiveMethod)activeMethod.SelectedIndex,
        IdleSeconds = (int)idleSeconds.Value,
        HistoryKeepDays = (int)keepDays.Value,
        ShowToast = showToast.Checked,
        ToastSeconds = (int)toastSeconds.Value,
        ToastFontSize = (int)toastFontSize.Value,
        ToastBold = toastBold.Checked,
        ToastShowIcon = toastIcon.Checked,
        ToastTheme = (ToastTheme)toastTheme.SelectedIndex,
        ToastCorner = (ToastCorner)toastCorner.SelectedIndex,
        ToastOpacity = (int)toastOpacity.Value,
        Schedules = rules,
        SkipDates = [.. skipList.Items.Cast<DateOnly>().Order()],
        AutoCheckUpdate = autoUpdate.Checked,
    };

    public bool StartWithWindows => startup.Checked;

    /// <param name="tryRegister">Registers the hotkey; false if another app owns it.</param>
    /// <param name="checkUpdate">Runs a manual update check.</param>
    public SettingsForm(Settings s, Func<uint, Keys, bool> tryRegister, Action checkUpdate) : base(L.T("Cài đặt", "Settings") + " — I'm Nobita")
    {
        original = s;

        // --- Chung
        hotkeyBox.PreviewKeyDown += (_, e) => e.IsInputKey = true; // capture Tab/arrows too
        hotkeyBox.KeyDown += CaptureHotkey;
        var clear = Btn(L.T("Xóa", "Clear"), () => { (mods, key) = (0, Keys.None); hotkeyBox.Text = Settings.FormatHotkey(0, Keys.None); });
        startup.Checked = Settings.StartWithWindows;

        var general = NewGrid();
        AddRow(general, L.T("Ngôn ngữ", "Language"), language);
        AddRow(general, L.T("Hotkey đổi mode", "Toggle hotkey"), Row(hotkeyBox, clear));
        AddFull(general, new Label { Text = L.T("Bấm vào ô rồi nhấn tổ hợp phím mới.", "Click the box, then press the new key combo."), AutoSize = true, ForeColor = SystemColors.GrayText });
        AddFull(general, confirm);
        AddFull(general, startup);
        AddRow(general, L.T("Kiểu giữ active", "Keep-active method"), activeMethod);
        AddRow(general, L.T("Rảnh bao lâu thì giữ active (giây)", "Idle seconds before acting"), idleSeconds);
        AddRow(general, L.T("Giữ lịch sử (ngày, 0 = mãi mãi)", "Keep history (days, 0 = forever)"), keepDays);
        AddRow(general, L.T("Sao lưu cài đặt", "Backup settings"), Row(Btn(L.T("Xuất…", "Export…"), Export), Btn(L.T("Nhập…", "Import…"), Import)));

        // --- Toast
        var preview = Btn(L.T("Xem thử", "Preview"), () => Toast.Show(TrayApp.AppIcon, "Nobita Mode", Result with { ShowToast = true }));
        var toast = NewGrid();
        AddFull(toast, showToast);
        AddRow(toast, L.T("Thời gian hiện (giây)", "Duration (seconds)"), toastSeconds);
        AddRow(toast, L.T("Cỡ chữ", "Font size"), toastFontSize);
        AddFull(toast, toastBold);
        AddFull(toast, toastIcon);
        AddRow(toast, "Theme", toastTheme);
        AddRow(toast, L.T("Góc màn hình", "Screen corner"), toastCorner);
        AddRow(toast, L.T("Độ hiển thị (%, 100 = rõ hẳn)", "Opacity (%, 100 = solid)"), toastOpacity);
        AddFull(toast, preview);

        // --- Lịch
        BuildScheduleColumns();
        var removeRow = Btn(L.T("Xóa dòng đang chọn", "Delete selected row"), () =>
        {
            if (schedule.CurrentRow is { IsNewRow: false } row) schedule.Rows.Remove(row);
        });
        var scheduleHelp = new Label
        {
            Text = L.T("Đến giờ, đúng ngày → tự chuyển sang mode đã chọn. Thêm dòng ở dòng trống cuối bảng.",
                "At the time, on the ticked days → switch to the chosen mode. Add rules in the empty last row."),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Top,
        };
        var addSkip = Btn(L.T("Thêm", "Add"), () =>
        {
            var d = DateOnly.FromDateTime(skipPicker.Value);
            if (!skipList.Items.Contains(d)) skipList.Items.Add(d);
        });
        var removeSkip = Btn(L.T("Xóa", "Remove"), () =>
        {
            if (skipList.SelectedItem != null) skipList.Items.Remove(skipList.SelectedItem);
        });
        var holidays = Row(new Label { Text = L.T("Ngày nghỉ (lịch không chạy):", "Days off (schedule paused):") }, skipPicker, addSkip, skipList, removeSkip);

        var schedulePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        schedulePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        schedulePanel.Controls.Add(scheduleHelp);
        schedulePanel.Controls.Add(schedule);
        schedulePanel.Controls.Add(removeRow);
        schedulePanel.Controls.Add(holidays);

        // --- Cập nhật
        var releases = new LinkLabel { Text = L.T("Trang phát hành trên GitHub", "Releases on GitHub"), AutoSize = true };
        releases.LinkClicked += (_, _) => Updater.OpenReleasesPage();
        var update = NewGrid();
        AddFull(update, new Label { Text = $"{L.T("Phiên bản hiện tại", "Current version")}: v{Updater.Current}", AutoSize = true });
        AddFull(update, autoUpdate);
        AddFull(update, Btn(L.T("Kiểm tra ngay", "Check now"), checkUpdate));
        AddFull(update, releases);

        var tabs = new TabControl { Size = new Size(680, 440) };
        foreach (var (title, content) in new (string, Control)[]
                 {
                     (L.T("Chung", "General"), general), ("Toast", toast), (L.T("Lịch", "Schedule"), schedulePanel), (L.T("Cập nhật", "Update"), update),
                 })
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
            if (!tryRegister(mods, key))
            {
                tabs.SelectedIndex = 0;
                return L.T($"{Settings.FormatHotkey(mods, key)} đang bị app khác chiếm. Chọn tổ hợp khác.",
                    $"{Settings.FormatHotkey(mods, key)} is taken by another app. Pick another combo.");
            }
            return null;
        });

        Fill(s);
    }

    /// <summary>Settings → controls. Used on open and on import.</summary>
    void Fill(Settings s)
    {
        original = s;
        (mods, key) = (s.HotkeyModifiers, s.HotkeyKey);
        Pick(language, (int)s.Language);
        hotkeyBox.Text = s.HotkeyText;
        confirm.Checked = s.ConfirmOnHotkey;
        Pick(activeMethod, (int)s.ActiveMethod);
        Set(idleSeconds, s.IdleSeconds);
        Set(keepDays, s.HistoryKeepDays);

        showToast.Checked = s.ShowToast;
        Set(toastSeconds, s.ToastSeconds);
        Set(toastFontSize, s.ToastFontSize);
        toastBold.Checked = s.ToastBold;
        toastIcon.Checked = s.ToastShowIcon;
        Pick(toastTheme, (int)s.ToastTheme);
        Pick(toastCorner, (int)s.ToastCorner);
        Set(toastOpacity, s.ToastOpacity);

        schedule.Rows.Clear();
        foreach (var r in s.Schedules)
            schedule.Rows.Add([r.Enabled, r.Name, r.Time.ToString("HH:mm"), ModeNames[r.Nobita ? 0 : 1], .. Week.Select(d => (object)r.Days.Contains(d))]);
        skipList.Items.Clear();
        foreach (var d in s.SkipDates) skipList.Items.Add(d);

        autoUpdate.Checked = s.AutoCheckUpdate;
    }

    void Export()
    {
        var err = ReadSchedule();
        if (err != null) { Error.Text = err; return; }
        using var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "IAmNobita-settings.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, Result.ToJson());
            Error.Text = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error.Text = $"{L.T("Không xuất được", "Export failed")}: {e.Message}";
        }
    }

    void Import()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Fill(Settings.FromJson(File.ReadAllText(dialog.FileName)));
            Error.Text = L.T("Đã nhập. Bấm OK để áp dụng.", "Imported. Press OK to apply.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Error.Text = $"{L.T("Không nhập được", "Import failed")}: {e.Message}";
        }
    }

    void BuildScheduleColumns()
    {
        schedule.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = L.T("Bật", "On"), Width = 40 });
        schedule.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Tên", "Name"), Width = 150 });
        schedule.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Giờ", "Time"), Width = 60 });
        var mode = new DataGridViewComboBoxColumn { HeaderText = L.T("Chuyển sang", "Switch to"), Width = 95, FlatStyle = FlatStyle.Flat };
        mode.Items.AddRange(ModeNames);
        schedule.Columns.Add(mode);
        foreach (var day in WeekNames)
            schedule.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = day, Width = 38 });

        schedule.DefaultValuesNeeded += (_, e) =>
        {
            e.Row.Cells[0].Value = true;
            e.Row.Cells[2].Value = "08:00";
            e.Row.Cells[3].Value = ModeNames[0];
            for (int i = 0; i < 7; i++) e.Row.Cells[4 + i].Value = i < 5; // Mon–Fri
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
                return L.T($"Lịch dòng {n}: giờ \"{timeText}\" không hợp lệ (vd. 08:30).", $"Schedule row {n}: invalid time \"{timeText}\" (e.g. 08:30).");
            var days = Week.Where((_, i) => row.Cells[4 + i].Value is true).ToArray();
            if (days.Length == 0) return L.T($"Lịch dòng {n}: chọn ít nhất 1 ngày.", $"Schedule row {n}: tick at least one day.");
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
            Error.Text = L.T("Cần ít nhất 1 phím Ctrl / Alt / Shift / Win.", "Needs at least one of Ctrl / Alt / Shift / Win.");
            return;
        }
        (mods, key) = (m, e.KeyCode);
        hotkeyBox.Text = Settings.FormatHotkey(mods, key);
        Error.Text = "";
    }
}

sealed class HistoryForm : DialogBase
{
    readonly History history;
    readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly Label summary = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly DataGridView list = new()
    {
        Size = new Size(520, 360),
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };

    public HistoryForm(History history) : base(L.T("Lịch sử đổi mode", "Mode history") + " — I'm Nobita")
    {
        this.history = history;
        to.Value = DateTime.Today;
        from.Value = DateTime.Today.AddDays(-30);

        list.Columns.Add("at", L.T("Thời gian", "Time"));
        list.Columns.Add("mode", "Mode");
        list.Columns.Add("source", L.T("Nguồn", "Source"));

        var close = new Button { Text = L.T("Đóng", "Close"), AutoSize = true, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right };
        CancelButton = close;
        AddFull(Grid, Row(new Label { Text = L.T("Từ", "From") }, from, new Label { Text = L.T("Đến", "To") }, to, Btn(L.T("Xem", "Show"), Reload)));
        AddFull(Grid, list);
        AddFull(Grid, summary);
        AddFull(Grid, Error);
        AddFull(Grid, close);
        Reload();
    }

    void Reload()
    {
        list.Rows.Clear();
        try
        {
            var entries = history.Query(from.Value.Date, to.Value.Date.AddDays(1)); // both days inclusive
            var format = L.T("dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss");
            foreach (var e in entries)
                list.Rows.Add(e.At.ToString(format), L.Mode(e.Nobita), L.Source(e.Source));
            summary.Text = L.T($"{entries.Count} dòng", $"{entries.Count} rows");
            Error.Text = "";
        }
        catch (Exception e)
        {
            summary.Text = "";
            Error.Text = $"{L.T("Không đọc được lịch sử", "Could not read history")}: {e.Message}";
        }
    }
}

sealed class CustomTimerForm : DialogBase
{
    readonly NumericUpDown hours = Number(0, 23), minutes = Number(0, 59);

    public int TotalMinutes => (int)(hours.Value * 60 + minutes.Value);

    public CustomTimerForm(int initialMinutes) : base(L.T("Hẹn giờ đổi mode", "Mode timer"))
    {
        Set(hours, initialMinutes / 60);
        Set(minutes, initialMinutes % 60);
        AddRow(Grid, L.T("Giờ", "Hours"), hours);
        AddRow(Grid, L.T("Phút", "Minutes"), minutes);
        AddButtons(() => TotalMinutes >= 1 ? null : L.T("Tối thiểu 1 phút.", "At least 1 minute."));
    }
}
