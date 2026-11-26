using System.Runtime.InteropServices;

namespace IAmNobita;

/// <summary>Small bottom-right popup. Never takes focus, never in Alt+Tab / Task View.</summary>
sealed class Toast : Form
{
    static Toast? current;

    readonly System.Windows.Forms.Timer life = new();
    readonly PictureBox pic;

    public static void Show(Icon icon, string text, int seconds)
    {
        current?.Close();
        current = new Toast(icon, text, seconds);
        current.Show();
    }

    Toast(Icon icon, string text, int seconds)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(32, 32, 32);
        Padding = new Padding(14, 10, 18, 10);

        pic = new PictureBox { Image = new Icon(icon, 32, 32).ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Margin = Padding.Empty };
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 0, 0, 0),
        };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange([pic, label]);
        Controls.Add(row);

        foreach (Control c in new Control[] { this, row, pic, label }) c.Click += (_, _) => Close();

        life.Interval = seconds * 1000;
        life.Tick += (_, _) => Close();
        life.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        // Win11 rounded corners; ignored on Win10.
        int round = 2;
        DwmSetWindowAttribute(Handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        life.Dispose();
        pic.Image?.Dispose();
        if (current == this) current = null;
        base.OnFormClosed(e);
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
