namespace PlanReminder;

internal sealed class PlanEditor : Form
{
    private readonly PlanItem original;
    private readonly TextBox title = new() { Multiline = true, MaxLength = 500 };
    private readonly TextBox notes = new() { Multiline = true, MaxLength = 5000, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox hasDate = new() { Text = "安排日期", AutoSize = true };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日" };
    private readonly CheckBox hasSchedule = new() { Text = "设置时间段（可选）", AutoSize = true };
    private readonly DateTimePicker start = TimePicker();
    private readonly DateTimePicker end = TimePicker();
    private readonly DateTimePicker endDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd" };
    private readonly CheckBox hasDuration = new() { Text = "设置预期时长（可选）", AutoSize = true };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 525600, Value = 60 };
    private readonly ComboBox unit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label error = Theme.Label("", Theme.Small, Color.FromArgb(184, 58, 78));
    private readonly List<Button> swatches = [];
    private readonly Button customColor = Theme.Button("自定义颜色");
    private string color;
    private int previousUnit;
    public PlanItem? Result { get; private set; }

    public PlanEditor(PlanItem item, bool isNew)
    {
        original = item; color = item.Color;
        Text = isNew ? "添加计划" : "编辑计划";
        Font = Theme.Body; ForeColor = Theme.Ink; BackColor = Color.White;
        ClientSize = new Size(640, 700); MinimumSize = new Size(600, 440);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 68, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 12, 20, 12), BackColor = Theme.Canvas
        };
        var save = Theme.Button("保存计划", true); save.Width = 120;
        var cancel = Theme.Button("取消"); cancel.Width = 90; cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => SavePlan();
        footer.Controls.AddRange([save, cancel]);
        AcceptButton = save; CancelButton = cancel;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 14, 24, 12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(Control control, int height)
        {
            var index = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 2, 0, 6);
            layout.Controls.Add(control, 0, index);
        }
        Row(Theme.Label("计划内容", Theme.Title), 32);
        title.Text = item.Title; title.AccessibleName = "计划标题（必填）"; title.AcceptsReturn = true;
        Row(title, 58);
        Row(Theme.Label("备注（可选）", Theme.Small, Theme.Muted), 27);
        notes.Text = item.Notes; notes.AccessibleName = "计划备注"; notes.AcceptsReturn = true;
        Row(notes, 70);

        hasDate.Checked = item.Day is not null;
        date.Value = (item.Day ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(TimeOnly.MinValue);
        date.AccessibleName = "计划日期";
        Row(hasDate, 30); Row(date, 38);
        hasSchedule.Checked = item.Start is not null;
        start.Value = DateTime.Today.Add((item.Start ?? new TimeOnly(9, 0)).ToTimeSpan());
        end.Value = DateTime.Today.Add((item.End ?? new TimeOnly(10, 0)).ToTimeSpan());
        endDate.Value = (item.EndDay ?? item.Day ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(TimeOnly.MinValue);
        start.AccessibleName = "开始时间"; end.AccessibleName = "结束时间"; endDate.AccessibleName = "结束日期";
        Row(hasSchedule, 32);
        var schedule = new TableLayoutPanel { ColumnCount = 4, RowCount = 2, Margin = Padding.Empty };
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        schedule.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        schedule.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); schedule.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        schedule.Controls.Add(Theme.Label("开始时间"), 0, 0); schedule.Controls.Add(start, 1, 0);
        schedule.Controls.Add(Theme.Label("结束时间"), 2, 0); schedule.Controls.Add(end, 3, 0);
        schedule.Controls.Add(Theme.Label("结束日期"), 0, 1); schedule.Controls.Add(endDate, 1, 1);
        var crossDayHint = Theme.Label("跨天时调整结束日期", Theme.Small, Theme.Muted);
        schedule.Controls.Add(crossDayHint, 2, 1); schedule.SetColumnSpan(crossDayHint, 2);
        foreach (Control control in schedule.Controls) { control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 3, 8, 5); }
        Row(schedule, 78);

        unit.Items.AddRange(["分钟", "小时"]); unit.SelectedIndex = 0;
        duration.Value = item.DurationMinutes ?? 60; duration.AccessibleName = "预期时长数值"; unit.AccessibleName = "预期时长单位";
        hasDuration.Checked = item.DurationMinutes is not null;
        Row(hasDuration, 30);
        var durationRow = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        duration.Dock = unit.Dock = DockStyle.Fill;
        durationRow.Controls.Add(duration, 0, 0); durationRow.Controls.Add(unit, 1, 0);
        var hint = Theme.Label("仅作为预计用时，可与时间段分别设置", Theme.Small, Theme.Muted);
        hint.Dock = DockStyle.Fill; durationRow.Controls.Add(hint, 2, 0);
        Row(durationRow, 38);
        Row(Theme.Label("计划颜色", Theme.Small, Theme.Muted), 28);
        var colors = new FlowLayoutPanel { WrapContents = false, Margin = Padding.Empty };
        foreach (var hex in Palette.Colors)
        {
            var swatch = Theme.Button(""); swatch.Size = new Size(32, 32); swatch.Margin = new Padding(0, 0, 6, 0);
            swatch.BackColor = ColorTranslator.FromHtml(hex); swatch.Tag = hex; swatch.AccessibleName = $"选择颜色 {hex}";
            swatch.Click += (_, _) => { color = hex; RefreshColor(); };
            swatches.Add(swatch); colors.Controls.Add(swatch);
        }
        customColor.Width = 118; customColor.Height = 32;
        customColor.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = ColorTranslator.FromHtml(color), FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            { color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; RefreshColor(); }
        };
        colors.Controls.Add(customColor); Row(colors, 40);
        error.AutoSize = false; Row(error, 44);
        scroll.Controls.Add(layout); Controls.Add(scroll); Controls.Add(footer);
        hasDate.CheckedChanged += (_, _) => { if (!hasDate.Checked) hasSchedule.Checked = false; RefreshEnabled(); };
        hasSchedule.CheckedChanged += (_, _) => RefreshEnabled();
        hasDuration.CheckedChanged += (_, _) => RefreshEnabled();
        date.ValueChanged += (_, _) => { if (endDate.Value.Date < date.Value.Date) endDate.Value = date.Value.Date; };
        unit.SelectedIndexChanged += (_, _) =>
        {
            var minutes = duration.Value * (previousUnit == 1 ? 60 : 1);
            previousUnit = unit.SelectedIndex;
            duration.DecimalPlaces = previousUnit == 1 ? 2 : 0;
            duration.Minimum = previousUnit == 1 ? .01m : 1m;
            duration.Maximum = previousUnit == 1 ? 8760m : 525600m;
            duration.Value = Math.Clamp(previousUnit == 1 ? Math.Round(minutes / 60, 2) : Math.Round(minutes), duration.Minimum, duration.Maximum);
        };
        RefreshColor(); RefreshEnabled();
        Shown += (_, _) => { title.Focus(); title.SelectionStart = title.TextLength; };
    }

    private static DateTimePicker TimePicker() => new()
    { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };

    private void RefreshEnabled()
    {
        date.Enabled = hasDate.Checked; hasSchedule.Enabled = hasDate.Checked;
        start.Enabled = end.Enabled = endDate.Enabled = hasDate.Checked && hasSchedule.Checked;
        duration.Enabled = unit.Enabled = hasDuration.Checked;
    }

    private void RefreshColor()
    {
        foreach (var swatch in swatches)
        {
            swatch.FlatAppearance.BorderSize = string.Equals((string?)swatch.Tag, color, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            swatch.FlatAppearance.BorderColor = Theme.Ink;
        }
        customColor.ForeColor = ColorTranslator.FromHtml(color);
    }

    internal void SavePlan()
    {
        try
        {
            var scheduled = hasDate.Checked && hasSchedule.Checked;
            var result = original with
            {
                Title = title.Text.Trim(), Notes = notes.Text.Trim(), Color = color,
                Day = hasDate.Checked ? DateOnly.FromDateTime(date.Value) : null,
                Start = scheduled ? TimeOnly.FromDateTime(start.Value) : null,
                End = scheduled ? TimeOnly.FromDateTime(end.Value) : null,
                EndDay = scheduled ? DateOnly.FromDateTime(endDate.Value) : null,
                DurationMinutes = hasDuration.Checked ? (int)Math.Round(duration.Value * (unit.SelectedIndex == 1 ? 60 : 1), MidpointRounding.AwayFromZero) : null,
                UpdatedAt = DateTimeOffset.Now
            };
            result.Validate(); Result = result;
            DialogResult = DialogResult.OK; Close();
        }
        catch (ArgumentException ex) { error.Text = ex.Message; }
    }
}
