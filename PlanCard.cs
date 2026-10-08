using System.Drawing.Drawing2D;

namespace PlanReminder;

internal sealed class PlanCard : UserControl
{
    private readonly CheckBox complete = new();
    private readonly CompletedTitleLabel title = new();
    private readonly Label status = Theme.Label("", Theme.Small, Theme.Accent);
    private readonly Label timing = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label notes = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Button edit = Theme.Button("编辑");
    private readonly Button delete = Theme.Button("删除");
    public PlanItem Item { get; }
    internal Color SurfaceColor { get; }
    public event Action<PlanItem>? CompletionRequested;
    public event Action<PlanItem>? EditRequested;
    public event Action<PlanItem>? DeleteRequested;

    public PlanCard(PlanItem item, DateOnly? occurrence = null, bool overview = false)
    {
        Item = item;
        DoubleBuffered = true;
        BackColor = Theme.Canvas;
        Margin = new Padding(0, 0, 0, 14);
        AccessibleName = item.Title;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var day = occurrence ?? today;
        var finished = overview && item.SeriesFinished(today);
        var checkedOff = finished || item.IsCompleteOn(day);
        SurfaceColor = checkedOff ? Theme.SoftAccent : Color.White;
        title.Marked = checkedOff;
        status.Text = finished ? item.RepeatUntil is null ? "已完成" : "已结束" : item.IsRecurring ? "当天已完成" : "已完成";
        status.Visible = checkedOff;
        status.AutoSize = false;
        complete.AutoSize = false; complete.Checked = checkedOff;
        complete.Enabled = !item.IsRecurring || !finished && day <= today && item.AppearsOn(day);
        complete.AccessibleName = checkedOff ? $"恢复计划：{item.Title}" : $"划去计划：{item.Title}";
        if (item.IsRecurring) complete.AccessibleName = finished ? $"长期计划已结束：{item.Title}" :
            $"{(checkedOff ? "恢复" : "完成")} {day:yyyy年MM月dd日} 的计划：{item.Title}";
        title.Text = item.Title; title.Font = checkedOff ? Theme.CompletedTitle : Theme.Title;
        title.ForeColor = ColorTranslator.FromHtml(item.Color); title.AutoSize = false; title.AutoEllipsis = true;
        timing.Text = item.TimeDescription() + (item.IsRecurring ? "\n" + item.RepeatDescription(today) +
            (!overview ? $" · {(checkedOff ? "当天已完成" : day > today ? "未来日期当天可勾选" : "当天待完成")}" : !finished && checkedOff ? " · 今天已完成" : "") :
            "");
        timing.AutoSize = false;
        notes.Text = item.Notes; notes.AutoSize = false; notes.AutoEllipsis = true;
        complete.BackColor = title.BackColor = timing.BackColor = notes.BackColor = status.BackColor = SurfaceColor;
        edit.BackColor = delete.BackColor = SurfaceColor;
        edit.FlatAppearance.BorderSize = delete.FlatAppearance.BorderSize = 0;
        edit.ForeColor = Theme.Muted; delete.ForeColor = Theme.Danger;
        if (item.IsRecurring) delete.AccessibleName = "删除整个长期计划及完成记录";
        edit.Font = delete.Font = Theme.Small; edit.Padding = delete.Padding = Padding.Empty;
        complete.CheckedChanged += (_, _) => CompletionRequested?.Invoke(item);
        edit.Click += (_, _) => EditRequested?.Invoke(item);
        delete.Click += (_, _) => DeleteRequested?.Invoke(item);
        Controls.AddRange([complete, title, status, timing, notes, edit, delete]);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int P(int value) => Theme.Px(this, value);
        var textWidth = Math.Max(P(120), Width - P(190));
        var titleHeight = Math.Clamp(TextRenderer.MeasureText(Item.Title, title.Font,
            new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + P(2), P(26), P(84));
        complete.SetBounds(P(24), P(24), P(22), P(26));
        title.SetBounds(P(58), P(20), textWidth, titleHeight);
        var timingHeight = TextRenderer.MeasureText(timing.Text, timing.Font, new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + P(2);
        status.SetBounds(P(58), P(24) + titleHeight, textWidth, P(26));
        var timingTop = P(28) + titleHeight + (title.Marked ? P(30) : 0);
        timing.SetBounds(P(58), timingTop, textWidth, timingHeight);
        var noteHeight = string.IsNullOrWhiteSpace(Item.Notes) ? 0 : Math.Min(P(52), TextRenderer.MeasureText(Item.Notes, notes.Font,
            new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + P(2));
        notes.SetBounds(P(58), timingTop + timingHeight + P(8), textWidth, noteHeight);
        edit.SetBounds(Width - P(122), P(17), P(48), P(36));
        delete.SetBounds(Width - P(68), P(17), P(48), P(36));
        var desired = timingTop + timingHeight + (noteHeight == 0 ? 0 : P(8) + noteHeight) + P(22);
        if (Height != desired) Height = desired;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Theme.Px(this, 14));
        using var surface = new SolidBrush(SurfaceColor); e.Graphics.FillPath(surface, path);
        using var border = new Pen(title.Marked ? Theme.Accent : Theme.Line, title.Marked ? Theme.Px(this, 2) : 1); e.Graphics.DrawPath(border, path);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(Item.Color));
        using var stripe = Theme.Round(new Rectangle(Theme.Px(this, 10), Theme.Px(this, 20), Theme.Px(this, 4), Height - Theme.Px(this, 40)), Theme.Px(this, 2));
        e.Graphics.FillPath(brush, stripe);
    }
}

// WinForms' font strikeout is only a hairline. Paint each wrapped line with a DPI-scaled stroke.
internal sealed class CompletedTitleLabel : Label
{
    internal bool Marked;
    public CompletedTitleLabel() { DoubleBuffered = true; UseMnemonic = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (!Marked) { base.OnPaint(e); return; }
        using var font = new Font(Font, Font.Style & ~FontStyle.Strikeout);
        using var stroke = new Pen(ForeColor, Math.Max(2, Theme.Px(this, 3)));
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var lines = new List<string>();
        foreach (var paragraph in Text.Replace("\r", "").Split('\n'))
        {
            var remaining = paragraph;
            do
            {
                var length = remaining.Length;
                while (length > 1 && TextRenderer.MeasureText(e.Graphics, remaining[..length], font, Size.Empty, flags).Width > Width) length--;
                if (length > 0 && length < remaining.Length && char.IsHighSurrogate(remaining[length - 1])) length--;
                length = Math.Max(1, length);
                lines.Add(remaining.Length == 0 ? "" : remaining[..length]);
                remaining = remaining.Length == 0 ? "" : remaining[length..];
            } while (remaining.Length > 0);
        }
        var visible = Math.Max(1, Height / font.Height);
        for (var index = 0; index < Math.Min(visible, lines.Count); index++)
        {
            var text = lines[index];
            if (index == visible - 1 && lines.Count > visible)
            {
                while (text.Length > 0 && TextRenderer.MeasureText(e.Graphics, text + "…", font, Size.Empty, flags).Width > Width) text = text[..^1];
                text += "…";
            }
            var y = index * font.Height;
            TextRenderer.DrawText(e.Graphics, text, font, new Point(0, y), ForeColor, flags);
            var width = Math.Min(Width, TextRenderer.MeasureText(e.Graphics, text, font, Size.Empty, flags).Width);
            e.Graphics.DrawLine(stroke, 0, y + font.Height * .49f, width, y + font.Height * .49f);
        }
    }
}
