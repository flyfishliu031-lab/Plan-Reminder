namespace PlanReminder;

internal sealed class CalendarView : UserControl
{
    private readonly Label heading = Theme.Label("", Theme.Title);
    private readonly DayButton[] days = new DayButton[42];
    private DateOnly month = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly? selection;
    private IReadOnlyList<PlanItem> plans = [];
    public event Action<DateOnly>? DateSelected;

    public CalendarView()
    {
        BackColor = Color.White;
        AccessibleName = "月历";
        Height = 390;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        var previous = Theme.Button("‹");
        var next = Theme.Button("›");
        previous.AccessibleName = "上个月"; next.AccessibleName = "下个月";
        previous.Dock = next.Dock = DockStyle.Fill;
        previous.FlatAppearance.BorderSize = next.FlatAppearance.BorderSize = 0;
        previous.Click += (_, _) => MoveMonth(-1);
        next.Click += (_, _) => MoveMonth(1);
        heading.Dock = DockStyle.Fill; heading.TextAlign = ContentAlignment.MiddleCenter;
        navigation.Controls.Add(previous, 0, 0); navigation.Controls.Add(heading, 1, 0); navigation.Controls.Add(next, 2, 0);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 7, Margin = Padding.Empty };
        for (var column = 0; column < 7; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
            var weekday = Theme.Label(new[] { "一", "二", "三", "四", "五", "六", "日" }[column], Theme.Small, Theme.Muted);
            weekday.Dock = DockStyle.Fill; weekday.TextAlign = ContentAlignment.MiddleCenter;
            grid.Controls.Add(weekday, column, 0);
        }
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        for (var row = 1; row <= 6; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        for (var index = 0; index < days.Length; index++)
        {
            var button = new DayButton { Dock = DockStyle.Fill, Margin = new Padding(2) };
            button.Click += (_, _) => { if (button.Day is { } day) DateSelected?.Invoke(day); };
            button.KeyDown += (_, e) =>
            {
                var offset = e.KeyCode switch { Keys.Left => -1, Keys.Right => 1, Keys.Up => -7, Keys.Down => 7, _ => 0 };
                if (offset == 0 || button.Day is not { } day) return;
                var target = day.AddDays(offset);
                if (target.Year is < 1753 or > 9998) return;
                e.Handled = true;
                DateSelected?.Invoke(target);
                days.FirstOrDefault(d => d.Day == target)?.Focus();
            };
            days[index] = button;
            grid.Controls.Add(button, index % 7, index / 7 + 1);
        }
        layout.Controls.Add(navigation, 0, 0); layout.Controls.Add(grid, 0, 1);
        Controls.Add(layout);
    }

    public void SetData(IReadOnlyList<PlanItem> items, DateOnly? selected, bool navigate = false)
    {
        plans = items; selection = selected;
        if (navigate && selected is { } day) month = day;
        RefreshDays();
    }

    private void MoveMonth(int direction)
    {
        var target = new DateOnly(month.Year, month.Month, 1).AddMonths(direction);
        if (target.Year is < 1753 or > 9998) return;
        month = target;
        RefreshDays();
    }

    private void RefreshDays()
    {
        heading.Text = $"{month.Year} 年 {month.Month} 月";
        var first = new DateOnly(month.Year, month.Month, 1);
        var beginning = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var index = 0; index < days.Length; index++)
        {
            var day = beginning.AddDays(index);
            var button = days[index];
            button.Day = day;
            button.Enabled = day.Year is >= 1753 and <= 9998;
            button.IsCurrentMonth = day.Month == month.Month;
            button.IsToday = day == today;
            button.IsSelected = day == selection;
            var matches = plans.Where(p => p.AppearsOn(day)).ToArray();
            button.Markers = matches.Take(3).Select(p => ColorTranslator.FromHtml(p.Color)).ToArray();
            button.Text = day.Day.ToString();
            button.AccessibleName = $"{day:yyyy年MM月dd日}，{matches.Length} 项计划";
            button.Invalidate();
        }
    }

    private sealed class DayButton : Button
    {
        internal DateOnly? Day;
        internal bool IsCurrentMonth;
        internal bool IsToday;
        internal bool IsSelected;
        internal Color[] Markers = [];
        public DayButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; DoubleBuffered = true; }

        protected override bool IsInputKey(Keys keyData) =>
            (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnPaint(PaintEventArgs e)
        {
            var background = IsSelected ? Theme.Accent : IsToday ? Color.FromArgb(238, 235, 253) : Color.White;
            e.Graphics.Clear(background);
            var color = IsSelected ? Color.White : IsCurrentMonth ? Theme.Ink : Color.FromArgb(170, 175, 188);
            var area = new Rectangle(0, 0, Width, Height - Theme.Px(this, 12));
            TextRenderer.DrawText(e.Graphics, Text, Theme.Body, area, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            var diameter = Theme.Px(this, 5); var gap = Theme.Px(this, 3);
            var start = (Width - Markers.Length * (diameter + gap) + gap) / 2;
            for (var index = 0; index < Markers.Length; index++)
            {
                using var brush = new SolidBrush(IsSelected ? Color.White : Markers[index]);
                e.Graphics.FillEllipse(brush, start + index * (diameter + gap), Height - Theme.Px(this, 10), diameter, diameter);
            }
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5), color, background);
        }
    }
}
