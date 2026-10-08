namespace PlanReminder;

internal sealed class AlarmWindow : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
    private readonly bool sound;
    internal Guid PlanId { get; }
    internal DateOnly Day { get; }

    internal AlarmWindow(PlanItem plan, DateTimeOffset due)
    {
        PlanId = plan.Id; Day = DateOnly.FromDateTime(plan.Alarm!.Mode == "after" ? DateTime.Now : due.LocalDateTime); sound = plan.Alarm.Sound;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "计划表 · 闹钟"; Icon = Theme.AppIcon; Font = Theme.Body;
        BackColor = Theme.Canvas; ForeColor = Theme.Ink;
        ClientSize = new Size(480, 330); MinimumSize = new Size(380, 260);
        TopMost = true; StartPosition = FormStartPosition.CenterScreen;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = Theme.Label("时间到了", Theme.EditorHeading); heading.Margin = new Padding(0, 0, 0, 16);
        var title = Theme.Label(plan.Title, Theme.Title, ColorTranslator.FromHtml(plan.Color));
        title.AutoSize = false; title.Dock = DockStyle.Fill; title.AutoEllipsis = true;
        var hint = Theme.Label($"{due.ToLocalTime():MM月dd日 HH:mm} · 一分钟后自动停止响铃", Theme.Small, Theme.Muted);
        hint.Margin = new Padding(0, 12, 0, 16);
        var stop = Theme.Button("关闭闹钟", true); stop.Dock = DockStyle.Fill; stop.Click += (_, _) => Close();
        root.Controls.Add(heading, 0, 0); root.Controls.Add(title, 0, 1); root.Controls.Add(hint, 0, 2); root.Controls.Add(stop, 0, 3);
        Controls.Add(root); AcceptButton = stop; CancelButton = stop;
        timer.Tick += (_, _) => { if (System.Diagnostics.Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMinutes(1)) timer.Stop(); else Ring(); };
        Shown += (_, _) => { Theme.FitWindow(this); Ring(); timer.Start(); };
    }

    private void Ring() { if (sound) System.Media.SystemSounds.Exclamation.Play(); }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
