using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IAmNobita;

/// <summary>
/// Small corner popup sized exactly to its text (+ optional icon as tall as the text).
/// Self-painted: no child controls, so no hidden margins. Never takes focus, never in Alt+Tab / Task View.
/// </summary>
sealed class Toast : Form
{
    const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    static Toast? current;

    readonly System.Windows.Forms.Timer life = new();
    readonly ToastCorner corner;
    readonly string text;
    readonly Font font;
    readonly Icon? icon;
    readonly Color fore;
    readonly int padX, padY, gap, lineHeight;

    public static void Show(Icon icon, string text, Settings s)
    {
        if (!s.ShowToast) return;
        current?.Close();
        current = new Toast(icon, text, s);
        current.Show();
    }

    Toast(Icon sourceIcon, string text, Settings s)
    {
        this.text = text;
        corner = s.ToastCorner;
        bool light = s.ToastTheme switch
        {
            ToastTheme.Light => true,
            ToastTheme.Dark => false,
            _ => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not 0,
        };
        fore = light ? Color.Black : Color.White;
        BackColor = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = s.ToastOpacity / 100.0;
        DoubleBuffered = true;

        font = new Font("Segoe UI", s.ToastFontSize, s.ToastBold ? FontStyle.Bold : FontStyle.Regular);
        var textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFlags);
        lineHeight = textSize.Height;
        float scale = DeviceDpi / 96f;
        (padX, padY, gap) = ((int)(10 * scale), (int)(5 * scale), (int)(6 * scale));
        if (s.ToastShowIcon) icon = new Icon(sourceIcon, lineHeight, lineHeight);

        ClientSize = new Size(
            padX * 2 + (icon != null ? lineHeight + gap : 0) + textSize.Width,
            padY * 2 + lineHeight);

        Click += (_, _) => Close();
        life.Interval = s.ToastSeconds * 1000;
        life.Tick += (_, _) => Close();
        life.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int x = padX;
        if (icon != null)
        {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawIcon(icon, new Rectangle(x, padY, lineHeight, lineHeight));
            x += lineHeight + gap;
        }
        TextRenderer.DrawText(e.Graphics, text, font, new Point(x, padY), fore, TextFlags);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.Style |= WS_POPUP; // popups have no minimum track size → window can be as small as the text
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        const int m = 12;
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = corner switch
        {
            ToastCorner.BottomLeft => new Point(wa.Left + m, wa.Bottom - Height - m),
            ToastCorner.TopRight => new Point(wa.Right - Width - m, wa.Top + m),
            ToastCorner.TopLeft => new Point(wa.Left + m, wa.Top + m),
            _ => new Point(wa.Right - Width - m, wa.Bottom - Height - m),
        };
        // Win11 small rounded corners (DWMWCP_ROUNDSMALL); ignored on Win10.
        int round = 3;
        DwmSetWindowAttribute(Handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        life.Dispose();
        font.Dispose();
        icon?.Dispose();
        if (current == this) current = null;
        base.OnFormClosed(e);
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
