using System.Drawing.Drawing2D;

namespace PlanReminder;

internal static class Theme
{
    public static readonly Color Canvas = ColorTranslator.FromHtml("#F3F6F5");
    public static readonly Color Ink = ColorTranslator.FromHtml("#20332F");
    public static readonly Color Muted = ColorTranslator.FromHtml("#63736E");
    public static readonly Color Accent = ColorTranslator.FromHtml("#146C60");
    public static readonly Color SoftAccent = ColorTranslator.FromHtml("#E7F2EF");
    public static readonly Color Line = ColorTranslator.FromHtml("#DEE7E3");
    public static readonly Color Danger = ColorTranslator.FromHtml("#AC414B");
    public static readonly Font Body = new("Microsoft YaHei UI", 10.5f);
    public static readonly Font Small = new("Microsoft YaHei UI", 9.25f);
    public static readonly Font Heading = new("Microsoft YaHei UI", 23, FontStyle.Bold);
    public static readonly Font EditorHeading = new("Microsoft YaHei UI", 19, FontStyle.Bold);
    public static readonly Font Title = new("Microsoft YaHei UI", 12, FontStyle.Bold);
    public static readonly Font CompletedTitle = new("Microsoft YaHei UI", 12, FontStyle.Bold | FontStyle.Strikeout);
    public static readonly Icon AppIcon = LoadIcon();

    private static Icon LoadIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("PlanReminder.assets.app.ico")!;
        using var icon = new Icon(stream); return (Icon)icon.Clone();
    }

    public static int Px(Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96d);

    public static Label Label(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text, Font = font ?? Body, ForeColor = color ?? Ink, AutoSize = true,
        Margin = new Padding(0), UseMnemonic = false
    };

    public static Button Button(string text, bool primary = false) => new RoundedButton
    {
        Text = text, Font = Body, FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Accent : Color.White,
        ForeColor = primary ? Color.White : Ink,
        FlatAppearance = { BorderSize = primary ? 0 : 1, BorderColor = Line },
        Cursor = Cursors.Hand, Height = 44, Width = 128,
        Padding = new Padding(12, 0, 12, 0), UseMnemonic = false
    };

    public static GraphicsPath Round(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }

    public static void FitWindow(Form form)
    {
        var area = Screen.FromControl(form.Owner ?? form).WorkingArea;
        form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
        form.Size = new Size(Math.Min(form.Width, area.Width - Px(form, 16)), Math.Min(form.Height, area.Height - Px(form, 16)));
        form.Location = new Point(Math.Clamp(form.Left, area.Left, Math.Max(area.Left, area.Right - form.Width)),
            Math.Clamp(form.Top, area.Top, Math.Max(area.Top, area.Bottom - form.Height)));
    }
}

internal sealed class RoundedButton : Button
{
    private bool hovered;
    private bool pressed;
    public RoundedButton() => DoubleBuffered = true;
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(Math.Max(Width, text.Width + Padding.Horizontal + Theme.Px(this, 12)),
            Math.Max(Height, text.Height + Theme.Px(this, 18)));
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var background = BackColor;
        if (!Enabled) background = Theme.Canvas;
        else if ((hovered || pressed) && Text.Length > 0)
            background = BackColor == Theme.Accent ? ColorTranslator.FromHtml(pressed ? "#0C5148" : "#105D53") : Theme.SoftAccent;
        e.Graphics.Clear(Parent is PlanCard card ? card.SurfaceColor : Parent?.BackColor ?? Theme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(1, 1, Width - 3, Height - 3), Theme.Px(this, Text.Length == 0 ? 8 : 10));
        using var fill = new SolidBrush(background); e.Graphics.FillPath(fill, path);
        if (FlatAppearance.BorderSize > 0)
        {
            using var pen = new Pen(FlatAppearance.BorderColor, Theme.Px(this, FlatAppearance.BorderSize));
            e.Graphics.DrawPath(pen, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -Theme.Px(this, 5), -Theme.Px(this, 5)), ForeColor, background);
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel() { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(20); }
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (!AutoSize || Controls.Count != 1) return base.GetPreferredSize(proposedSize);
        var width = Math.Max(1, (proposedSize.Width > 0 ? proposedSize.Width : Width) - Padding.Horizontal);
        var child = Controls[0].GetPreferredSize(new Size(width, 0));
        return new Size(child.Width + Padding.Horizontal, child.Height + Padding.Vertical);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Theme.Px(this, 14));
        using var brush = new SolidBrush(BackColor); e.Graphics.FillPath(brush, path);
        using var border = new Pen(Theme.Line); e.Graphics.DrawPath(border, path);
    }
}
