using System.Drawing.Drawing2D;

namespace PlanReminder;

internal sealed class DailyTimelineView : UserControl
{
    private readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
    private readonly TimelineCanvas canvas;
    private readonly ComboBox chooser = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "时间表中的计划" };
    private DateOnly day;
    private readonly Button untimed = Theme.Button("未安排时间");
    private PlanItem[] untimedPlans = [];
    private readonly Label hint = Theme.Label("拖选空白时间添加 · 也可用“添加计划”精确输入时间", Theme.Small, Theme.Muted);
    internal DaySegment[] Segments { get; private set; } = [];
    internal event Action<int, int>? RangeSelected;
    internal event Action<PlanItem, DateOnly>? OpenRequested;
    internal event Action? ListRequested;

    internal DailyTimelineView()
    {
        Dock = DockStyle.Fill; Font = Theme.Body;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 8) };
        void Action(string text, System.Action action) {
            var b = Theme.Button(text); b.Font = Theme.Small; b.Height = 32; b.Width = text.Length <= 3 ? 68 : 88;
            b.Padding = new Padding(6, 0, 6, 0); b.Margin = new Padding(0, 0, 6, 6); b.Click += (_, _) => action(); toolbar.Controls.Add(b);
        }
        Action("当前时段", () => ScrollTo(day == DateOnly.FromDateTime(DateTime.Now) ? DateTime.Now.Hour * 60 : 480));
        Action("计划列表", () => ListRequested?.Invoke());
        untimed.Font = Theme.Small; untimed.Height = 32; untimed.Width = 128; untimed.Padding = new Padding(6, 0, 6, 0);
        untimed.Margin = new Padding(0, 0, 0, 6); toolbar.Controls.Add(untimed);
        untimed.Click += (_, _) => {
            var menu = new ContextMenuStrip { Font = Theme.Body };
            foreach (var p in untimedPlans) menu.Items.Add(p.Title, null, (_, _) => OpenRequested?.Invoke(p, day));
            menu.Closed += (_, _) => menu.Dispose(); menu.Show(untimed, new Point(0, untimed.Height));
        };
        var selection = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 0, 0, 12) };
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        hint.Dock = DockStyle.Top; hint.Margin = new Padding(0, 0, 0, 8);
        selection.Controls.Add(hint, 0, 0); selection.SetColumnSpan(hint, 2);
        chooser.Margin = new Padding(0, 0, 8, 0); selection.Controls.Add(chooser, 0, 1);
        var open = Theme.Button("查看计划"); open.Font = Theme.Small; open.Width = 88; open.Height = 32; open.Padding = new Padding(6, 0, 6, 0); open.Dock = DockStyle.Fill;
        open.Click += (_, _) => { if (chooser.SelectedItem is Choice c) OpenRequested?.Invoke(c.Segment.Plan, c.Segment.Occurrence); };
        selection.Controls.Add(open, 1, 1);
        canvas = new TimelineCanvas(this); scroll.Controls.Add(canvas);
        scroll.SizeChanged += (_, _) => ResizeCanvas();
        scroll.Layout += (_, _) => ResizeCanvas();
        root.Controls.Add(toolbar, 0, 0); root.Controls.Add(selection, 0, 1); root.Controls.Add(scroll, 0, 2); Controls.Add(root);
    }

    internal void SetData(IEnumerable<PlanItem> plans, DateOnly date)
    {
        var changed = day != date; var position = scroll.VerticalScroll.Value; day = date;
        var all = plans.ToArray(); Segments = DaySegment.Layout(all, date);
        untimedPlans = all.Where(p => p.Start is null && p.AppearsOn(date)).ToArray();
        untimed.Text = $"未安排时间 · {untimedPlans.Length}"; untimed.Enabled = untimedPlans.Length > 0;
        chooser.Items.Clear(); foreach (var s in Segments) chooser.Items.Add(new Choice(s));
        chooser.Enabled = Segments.Length > 0; if (chooser.Items.Count > 0) chooser.SelectedIndex = 0;
        canvas.Rebuild(); ResizeCanvas();
        if (changed) ScrollTo(day == DateOnly.FromDateTime(DateTime.Now) ? Math.Max(0, DateTime.Now.Hour * 60 - 60) : 480);
        else scroll.AutoScrollPosition = new Point(0, position);
    }
    private void ResizeCanvas() {
        canvas.Size = new Size(Math.Max(100, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 1), Theme.Px(this, 24 * 88 + 48));
        canvas.Arrange();
    }
    internal void ScrollTo(int minute) => scroll.AutoScrollPosition = new Point(0, (int)(minute * canvas.Hour / 60));
    internal void UpdateTime() => canvas.Invalidate();
    private sealed record Choice(DaySegment Segment) { public override string ToString() => Segment.Caption; }

    private sealed class TimelineCanvas : Panel
    {
        private readonly DailyTimelineView owner;
        private int? anchor, cursor;
        private readonly ToolTip tips = new();
        internal float Hour => Theme.Px(this, 88);
        private int LeftEdge => Theme.Px(this, 64);
        private int TopEdge => Theme.Px(this, 20);
        internal TimelineCanvas(DailyTimelineView owner) {
            this.owner = owner; DoubleBuffered = true; BackColor = Color.White;
            AccessibleName = "全天时间表，使用上方添加计划按钮或计划选择框也可操作";
        }
        internal void Rebuild() {
            foreach (Control child in Controls.Cast<Control>().ToArray()) { Controls.Remove(child); child.Dispose(); }
            tips.RemoveAll();
            foreach (var s in owner.Segments) {
                var color = ColorTranslator.FromHtml(s.Plan.Color);
                var b = new Button { FlatStyle = FlatStyle.Flat, Font = Theme.Small, Tag = s, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true,
                    Padding = new Padding(Theme.Px(this, 8), Theme.Px(this, 4), Theme.Px(this, 4), 0), UseMnemonic = false,
                    AccessibleName = s.Caption + (s.Plan.IsCompleteOn(s.Occurrence) ? "，已完成" : "，待完成"),
                    BackColor = Color.FromArgb((color.R + 7 * 255) / 8, (color.G + 7 * 255) / 8, (color.B + 7 * 255) / 8), ForeColor = Theme.Ink };
                b.FlatAppearance.BorderColor = color; b.FlatAppearance.BorderSize = 1;
                b.Click += (_, _) => owner.OpenRequested?.Invoke(s.Plan, s.Occurrence);
                b.Paint += (_, e) => { if (s.Plan.IsCompleteOn(s.Occurrence)) {
                    using var pen = new Pen(color, Theme.Px(this, 3));
                    var measured = TextRenderer.MeasureText(b.Text, b.Font, new Size(Math.Max(1, b.Width - b.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    var lines = Math.Max(1, (int)Math.Ceiling(measured.Height / (double)b.Font.Height));
                    for (var i = 0; i < lines; i++) {
                        var y = b.Padding.Top + b.Font.Height / 2 + i * b.Font.Height;
                        if (y >= b.Height) break;
                        e.Graphics.DrawLine(pen, b.Padding.Left, y, b.Width - b.Padding.Right - 4, y);
                    }
                }};
                tips.SetToolTip(b, s.Caption); Controls.Add(b);
            }
            Arrange(); Invalidate();
        }
        internal void Arrange() {
            var width = Math.Max(1, Width - LeftEdge - Theme.Px(this, 12));
            foreach (Button b in Controls) {
                var s = (DaySegment)b.Tag!; var columnWidth = width / s.Columns;
                var height = Math.Max(Theme.Px(this, 3), (int)((s.End - s.Start) * Hour / 60) - 2);
                b.SetBounds(LeftEdge + s.Column * columnWidth + 2, TopEdge + (int)(s.Start * Hour / 60), Math.Max(1, columnWidth - 4), height);
                b.Text = height < Theme.Px(this, 30) ? "" : height < Theme.Px(this, 62) ? s.Plan.Title.ReplaceLineEndings(" ") : $"{DaySegment.Clock(s.Start)}—{DaySegment.Clock(s.End)}\n{s.Plan.Title.ReplaceLineEndings(" ")}";
            }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var line = new Pen(Theme.Line); using var muted = new SolidBrush(Theme.Muted);
            for (var hour = 0; hour <= 24; hour++) {
                var y = TopEdge + hour * Hour; e.Graphics.DrawLine(line, LeftEdge, y, Width - 8, y);
                e.Graphics.DrawString($"{hour:00}:00", Theme.Small, muted, 3, y - Theme.Small.Height / 2f);
            }
            if (anchor is { } from && cursor is { } to) {
                using var fill = new SolidBrush(Color.FromArgb(55, Theme.Accent));
                e.Graphics.FillRectangle(fill, LeftEdge, TopEdge + Math.Min(from, to) * Hour / 60, Width - LeftEdge - 8, Math.Max(3, Math.Abs(to - from) * Hour / 60));
            }
            if (owner.day == DateOnly.FromDateTime(DateTime.Now)) {
                var minute = DateTime.Now.TimeOfDay.TotalMinutes; var y = TopEdge + (float)(minute * Hour / 60);
                using var pen = new Pen(Theme.Danger, Theme.Px(this, 2)); e.Graphics.DrawLine(pen, LeftEdge - 8, y, Width - 8, y);
                using var dot = new SolidBrush(Theme.Danger); e.Graphics.FillEllipse(dot, LeftEdge - 10, y - 4, 8, 8);
            }
        }
        private int Minute(int y) => Math.Clamp((int)Math.Round((y - TopEdge) * 60 / Hour), 0, 1440);
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left || e.X < LeftEdge) return;
            anchor = Math.Min(1439, Minute(e.Y)); cursor = anchor; Capture = true; Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (anchor is { } from) { cursor = Minute(e.Y); owner.hint.Text = $"已选 {DaySegment.Clock(Math.Min(from, cursor.Value))}—{DaySegment.Clock(Math.Max(from, cursor.Value))} · 松开填写计划"; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); if (anchor is not { } from || e.Button != MouseButtons.Left) return;
            var to = Minute(e.Y); anchor = cursor = null; Capture = false; Invalidate();
            owner.hint.Text = "拖选空白时间添加 · 也可用“添加计划”精确输入时间";
            if (from == to) to = Math.Min(1440, from + 60);
            owner.RangeSelected?.Invoke(Math.Min(from, to), Math.Max(from, to));
        }
        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}
