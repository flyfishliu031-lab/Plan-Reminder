namespace PlanReminder;

internal static class Theme
{
    public static readonly Color Canvas = ColorTranslator.FromHtml("#F5F6FA");
    public static readonly Color Ink = ColorTranslator.FromHtml("#252838");
    public static readonly Color Muted = ColorTranslator.FromHtml("#717788");
    public static readonly Color Accent = ColorTranslator.FromHtml("#6654D9");
    public static readonly Color Line = ColorTranslator.FromHtml("#E6E8EF");
    public static readonly Font Body = new("Microsoft YaHei UI", 10);
    public static readonly Font Small = new("Microsoft YaHei UI", 9);
    public static readonly Font Heading = new("Microsoft YaHei UI", 22, FontStyle.Bold);
    public static readonly Font Title = new("Microsoft YaHei UI", 12, FontStyle.Bold);
    public static readonly Font CompletedTitle = new("Microsoft YaHei UI", 12, FontStyle.Bold | FontStyle.Strikeout);

    public static int Px(Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96d);

    public static Label Label(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text, Font = font ?? Body, ForeColor = color ?? Ink, AutoSize = true,
        Margin = new Padding(0), UseMnemonic = false
    };

    public static Button Button(string text, bool primary = false) => new()
    {
        Text = text, Font = Body, FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Accent : Color.White,
        ForeColor = primary ? Color.White : Ink,
        FlatAppearance = { BorderSize = primary ? 0 : 1, BorderColor = Line },
        Cursor = Cursors.Hand, Height = 38, AutoSize = false,
        Padding = new Padding(6, 0, 6, 0), UseMnemonic = false
    };
}
