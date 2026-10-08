using System.Drawing.Drawing2D;

namespace PlanReminder;

internal sealed class PlanCard : UserControl
{
    private readonly CheckBox complete = new();
    private readonly Label title = Theme.Label("");
    private readonly Label timing = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label notes = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Button edit = Theme.Button("编辑");
    private readonly Button delete = Theme.Button("删除");
    public PlanItem Item { get; }
    public event Action<PlanItem>? CompletionRequested;
    public event Action<PlanItem>? EditRequested;
    public event Action<PlanItem>? DeleteRequested;

    public PlanCard(PlanItem item)
    {
        Item = item;
        DoubleBuffered = true;
        BackColor = Theme.Canvas;
        Margin = new Padding(0, 0, 0, 14);
        AccessibleName = item.Title;
        complete.AutoSize = false; complete.Checked = item.IsCompleted;
        complete.AccessibleName = item.IsCompleted ? $"恢复计划：{item.Title}" : $"划去计划：{item.Title}";
        title.Text = item.Title; title.Font = item.IsCompleted ? Theme.CompletedTitle : Theme.Title;
        title.ForeColor = ColorTranslator.FromHtml(item.Color); title.AutoSize = false; title.AutoEllipsis = true;
        timing.Text = item.TimeDescription() + (item.IsCompleted ? "   ·   已完成" : "");
        timing.AutoSize = false;
        notes.Text = item.Notes; notes.AutoSize = false; notes.AutoEllipsis = true;
        complete.BackColor = title.BackColor = timing.BackColor = notes.BackColor = Color.White;
        edit.FlatAppearance.BorderSize = delete.FlatAppearance.BorderSize = 0;
        edit.ForeColor = Theme.Muted; delete.ForeColor = Theme.Danger;
        edit.Font = delete.Font = Theme.Small; edit.Padding = delete.Padding = Padding.Empty;
        complete.CheckedChanged += (_, _) => CompletionRequested?.Invoke(item);
        edit.Click += (_, _) => EditRequested?.Invoke(item);
        delete.Click += (_, _) => DeleteRequested?.Invoke(item);
        Controls.AddRange([complete, title, timing, notes, edit, delete]);
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
        var timingTop = P(28) + titleHeight;
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
        using var surface = new SolidBrush(Color.White); e.Graphics.FillPath(surface, path);
        using var border = new Pen(Theme.Line); e.Graphics.DrawPath(border, path);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(Item.Color));
        using var stripe = Theme.Round(new Rectangle(Theme.Px(this, 10), Theme.Px(this, 20), Theme.Px(this, 4), Height - Theme.Px(this, 40)), Theme.Px(this, 2));
        e.Graphics.FillPath(brush, stripe);
    }
}
