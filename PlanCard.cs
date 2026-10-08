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
        BackColor = Color.White;
        Margin = new Padding(0, 0, 0, 12);
        AccessibleName = item.Title;
        complete.AutoSize = false; complete.Checked = item.IsCompleted;
        complete.AccessibleName = item.IsCompleted ? $"恢复计划：{item.Title}" : $"划去计划：{item.Title}";
        title.Text = item.Title; title.Font = item.IsCompleted ? Theme.CompletedTitle : Theme.Title;
        title.ForeColor = ColorTranslator.FromHtml(item.Color); title.AutoSize = false; title.AutoEllipsis = true;
        timing.Text = item.TimeDescription() + (item.IsCompleted ? "   ·   已完成" : "");
        timing.AutoSize = false; timing.AutoEllipsis = true;
        notes.Text = item.Notes; notes.AutoSize = false; notes.AutoEllipsis = true;
        edit.FlatAppearance.BorderSize = delete.FlatAppearance.BorderSize = 0;
        edit.ForeColor = Theme.Muted; delete.ForeColor = Color.FromArgb(171, 86, 99);
        complete.CheckedChanged += (_, _) => CompletionRequested?.Invoke(item);
        edit.Click += (_, _) => EditRequested?.Invoke(item);
        delete.Click += (_, _) => DeleteRequested?.Invoke(item);
        Controls.AddRange([complete, title, timing, notes, edit, delete]);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int P(int value) => Theme.Px(this, value);
        var textWidth = Math.Max(P(120), Width - P(184));
        var titleHeight = Math.Clamp(TextRenderer.MeasureText(Item.Title, title.Font,
            new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height, P(26), P(76));
        complete.SetBounds(P(19), P(21), P(22), P(26));
        title.SetBounds(P(52), P(18), textWidth, titleHeight);
        timing.SetBounds(P(52), P(23) + titleHeight, textWidth, P(25));
        var noteHeight = string.IsNullOrWhiteSpace(Item.Notes) ? 0 : P(38);
        notes.SetBounds(P(52), P(52) + titleHeight, textWidth, noteHeight);
        edit.SetBounds(Width - P(124), P(17), P(54), P(32));
        delete.SetBounds(Width - P(65), P(17), P(54), P(32));
        var desired = P(68) + titleHeight + noteHeight;
        if (Height != desired) Height = desired;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Theme.Line);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(Item.Color));
        e.Graphics.FillRectangle(brush, 0, 0, Theme.Px(this, 4), Height);
    }
}
