namespace PlanReminder;

internal sealed class PlanEditor : Form
{
    private readonly PlanItem original;
    private readonly TextBox title = new() { Multiline = true, MaxLength = 500, BorderStyle = BorderStyle.None, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox notes = new() { Multiline = true, MaxLength = 5000, BorderStyle = BorderStyle.None, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox hasDate = new() { Text = "安排日期", AutoSize = true };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日" };
    private readonly CheckBox hasSchedule = new() { Text = "设置时间段", AutoSize = true };
    private readonly DateTimePicker start = TimePicker();
    private readonly DateTimePicker end = TimePicker();
    private readonly DateTimePicker endDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日" };
    private readonly CheckBox hasDuration = new() { Text = "设置预期时长", AutoSize = true };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 525600, Value = 60 };
    private readonly ComboBox unit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox hasRepeat = new() { Text = "设为长期计划（每天重复）", AutoSize = true };
    private readonly ComboBox repeatMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "长期计划结束方式" };
    private readonly NumericUpDown repeatValue = new() { Minimum = 1, Maximum = 10000, Value = 30, AccessibleName = "长期计划目标数值" };
    private readonly Label repeatHint = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label error = Theme.Label("", Theme.Small, Theme.Danger);
    private readonly List<Button> swatches = [];
    private readonly Button customColor = Theme.Button("自定义颜色");
    private readonly TableLayoutPanel scheduleFields;
    private readonly TableLayoutPanel durationFields;
    private readonly TableLayoutPanel repeatFields;
    private readonly TableLayoutPanel endDateField;
    private readonly Panel dateField;
    private string color;
    private int previousUnit;
    public PlanItem? Result { get; private set; }

    public PlanEditor(PlanItem item, bool isNew)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        original = item; color = item.Color;
        Text = isNew ? "添加计划" : "编辑计划";
        Icon = Theme.AppIcon;
        Font = Theme.Body; ForeColor = Theme.Ink; BackColor = Theme.Canvas;
        ClientSize = new Size(720, 780); MinimumSize = new Size(580, 460);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = Stack();
        header.Padding = new Padding(28, 20, 28, 12);
        Add(header, Theme.Label(Text, Theme.EditorHeading), 4);
        Add(header, Theme.Label("把想做的事记下来，时间可以稍后安排。", Theme.Small, Theme.Muted), 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 0, 28, 0), Margin = Padding.Empty };
        var body = Stack();
        scroll.Layout += (_, _) => body.MaximumSize = new Size(Math.Max(1, scroll.ClientSize.Width - scroll.Padding.Horizontal -
            (scroll.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)), 0);
        var content = Section("计划内容");
        title.Text = item.Title; title.AccessibleName = "计划标题（必填）";
        title.PlaceholderText = "想做些什么？";
        Add(content.Layout, Input(title, 72, true), 12);
        Add(content.Layout, Theme.Label("备注 · 可选", Theme.Small, Theme.Muted), 8);
        notes.Text = item.Notes; notes.AccessibleName = "计划备注";
        notes.PlaceholderText = "补充说明、步骤或需要准备的东西";
        Add(content.Layout, Input(notes, 60), 0);
        Add(body, content.Panel, 16);

        var time = Section("时间安排");
        Add(time.Layout, Theme.Label("以下均为可选项，也可以只填写预期时长。", Theme.Small, Theme.Muted), 14);
        hasDate.Checked = item.Day is not null;
        date.Value = (item.Day ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(TimeOnly.MinValue);
        date.AccessibleName = "计划日期";
        Add(time.Layout, hasDate, 8);
        var dateLayout = Stack(); Add(dateLayout, date, 0); dateField = dateLayout;
        Add(time.Layout, dateField, 12);
        hasSchedule.Checked = item.Start is not null;
        start.Value = DateTime.Today.Add((item.Start ?? new TimeOnly(9, 0)).ToTimeSpan());
        end.Value = DateTime.Today.Add((item.End ?? new TimeOnly(10, 0)).ToTimeSpan());
        endDate.Value = (item.EndDay ?? item.Day ?? DateOnly.FromDateTime(DateTime.Now)).ToDateTime(TimeOnly.MinValue);
        start.AccessibleName = "开始时间"; end.AccessibleName = "结束时间"; endDate.AccessibleName = "结束日期";
        Add(time.Layout, hasSchedule, 8);
        scheduleFields = Stack();
        var times = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        times.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var startField = Field("开始时间", start); startField.Margin = new Padding(0, 0, 10, 0);
        var endField = Field("结束时间", end); endField.Margin = new Padding(10, 0, 0, 0);
        times.Controls.Add(startField, 0, 0); times.Controls.Add(endField, 1, 0);
        Add(scheduleFields, times, 12);
        endDateField = Field("结束日期 · 跨天时选择下一天", endDate);
        Add(scheduleFields, endDateField, 0);
        Add(time.Layout, scheduleFields, 16);

        unit.Items.AddRange(["分钟", "小时"]); unit.SelectedIndex = 0;
        duration.Value = item.DurationMinutes ?? 60; duration.AccessibleName = "预期时长数值"; unit.AccessibleName = "预期时长单位";
        hasDuration.Checked = item.DurationMinutes is not null;
        Add(time.Layout, hasDuration, 8);
        durationFields = Stack();
        var durationRow = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        durationRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        duration.Dock = unit.Dock = DockStyle.Top;
        duration.Margin = new Padding(0, 0, 10, 0); unit.Margin = Padding.Empty;
        durationRow.Controls.Add(duration, 0, 0); durationRow.Controls.Add(unit, 1, 0);
        Add(durationFields, durationRow, 6);
        Add(durationFields, Theme.Label("记录预计用时，可与时间段分别设置。", Theme.Small, Theme.Muted), 0);
        Add(time.Layout, durationFields, 0);
        Add(body, time.Panel, 16);

        var repeat = Section("长期计划");
        hasRepeat.Checked = item.IsRecurring;
        hasRepeat.Enabled = !item.IsRecurring || item.CompletionDates.Count == 0;
        hasRepeat.AccessibleName = "设为长期计划（每天重复）";
        Add(repeat.Layout, hasRepeat, 12);
        repeatFields = Stack();
        repeatMode.Items.AddRange(["持续天数", "完成次数"]);
        repeatMode.SelectedIndex = item.RepeatCount is null ? 0 : 1;
        repeatValue.Maximum = repeatMode.SelectedIndex == 0 ? 3650 : 10000;
        repeatValue.Value = item.RepeatUntil is { } until && item.Day is { } begin ? until.DayNumber - begin.DayNumber + 1 : item.RepeatCount ?? 30;
        Add(repeatFields, Field("结束方式", repeatMode), 12);
        Add(repeatFields, Field("目标数值", repeatValue), 10);
        Add(repeatFields, repeatHint, 0);
        Add(repeat.Layout, repeatFields, 0);
        Add(body, repeat.Panel, 16);

        var colorSection = Section("计划颜色");
        Add(colorSection.Layout, Theme.Label("用颜色区分计划，划去后仍保留原色。", Theme.Small, Theme.Muted), 12);
        var colors = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        foreach (var hex in Palette.Colors)
        {
            var swatch = Theme.Button(""); swatch.Size = new Size(36, 36); swatch.Padding = Padding.Empty;
            swatch.Margin = new Padding(0, 0, 8, 8);
            swatch.BackColor = ColorTranslator.FromHtml(hex); swatch.Tag = hex; swatch.AccessibleName = $"选择颜色 {hex}";
            swatch.Click += (_, _) => { color = hex; RefreshColor(); };
            swatches.Add(swatch); colors.Controls.Add(swatch);
        }
        customColor.Size = new Size(124, 36); customColor.Margin = new Padding(0, 0, 0, 8);
        customColor.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = ColorTranslator.FromHtml(color), FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            { color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; RefreshColor(); }
        };
        colors.Controls.Add(customColor); Add(colorSection.Layout, colors, 0);
        Add(body, colorSection.Panel, 0);
        scroll.Controls.Add(body);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(28, 12, 28, 16), BackColor = Color.White, Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        error.Dock = DockStyle.Fill; error.TextAlign = ContentAlignment.MiddleLeft;
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
        var cancel = Theme.Button("取消"); cancel.Width = 88; cancel.DialogResult = DialogResult.Cancel;
        var save = Theme.Button("保存计划", true); save.Width = 132;
        cancel.Margin = new Padding(0, 0, 12, 0); save.Margin = Padding.Empty;
        save.Click += (_, _) => SavePlan();
        actions.Controls.AddRange([cancel, save]);
        footer.Controls.Add(error, 0, 0); footer.Controls.Add(actions, 1, 0);
        AcceptButton = save; CancelButton = cancel;
        root.Controls.Add(header, 0, 0); root.Controls.Add(scroll, 0, 1); root.Controls.Add(footer, 0, 2); Controls.Add(root);
        hasDate.CheckedChanged += (_, _) => { if (!hasDate.Checked) hasSchedule.Checked = false; RefreshEnabled(); };
        hasSchedule.CheckedChanged += (_, _) => RefreshEnabled();
        hasDuration.CheckedChanged += (_, _) => RefreshEnabled();
        hasRepeat.CheckedChanged += (_, _) => { if (hasRepeat.Checked) hasDate.Checked = true; RefreshEnabled(); };
        repeatMode.SelectedIndexChanged += (_, _) =>
        { repeatValue.Maximum = repeatMode.SelectedIndex == 0 ? 3650 : 10000; RefreshRepeatHint(); };
        repeatValue.ValueChanged += (_, _) => RefreshRepeatHint();
        date.ValueChanged += (_, _) => { if (hasRepeat.Checked || endDate.Value.Date < date.Value.Date) endDate.Value = date.Value.Date; RefreshRepeatHint(); };
        unit.SelectedIndexChanged += (_, _) =>
        {
            var minutes = duration.Value * (previousUnit == 1 ? 60 : 1);
            previousUnit = unit.SelectedIndex;
            duration.DecimalPlaces = previousUnit == 1 ? 2 : 0;
            duration.Minimum = previousUnit == 1 ? .01m : 1m;
            duration.Maximum = previousUnit == 1 ? 8760m : 525600m;
            duration.Value = Math.Clamp(previousUnit == 1 ? Math.Round(minutes / 60, 2) : Math.Round(minutes), duration.Minimum, duration.Maximum);
        };
        RefreshColor(); RefreshEnabled(); ResumeLayout(true);
        Load += (_, _) => Theme.FitWindow(this);
        Shown += (_, _) => { title.Focus(); title.SelectionStart = title.TextLength; };
    }

    private static TableLayoutPanel Stack()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static void Add(TableLayoutPanel layout, Control control, int gap)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Dock = DockStyle.Top; control.Margin = new Padding(0, 0, 0, gap);
        layout.Controls.Add(control, 0, row);
        if (control is Label label)
            layout.SizeChanged += (_, _) => label.MaximumSize = new Size(Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal), 0);
    }

    private static (SurfacePanel Panel, TableLayoutPanel Layout) Section(string heading)
    {
        var panel = new SurfacePanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18) };
        var layout = Stack();
        Add(layout, Theme.Label(heading, Theme.Title), 12);
        panel.Controls.Add(layout); return (panel, layout);
    }

    private static SurfacePanel Input(TextBox box, int height, bool grow = false)
    {
        var panel = new SurfacePanel { Height = height, Padding = new Padding(12, 10, 12, 10), BackColor = Theme.Canvas };
        box.BackColor = Theme.Canvas; box.Dock = DockStyle.Fill; panel.Controls.Add(box);
        if (grow)
        {
            box.ScrollBars = ScrollBars.None;
            var caretQueued = false;
            var resizeQueued = false;
            void KeepCaretVisible()
            {
                if (!box.Focused || !box.IsHandleCreated || caretQueued) return;
                caretQueued = true;
                box.BeginInvoke(() =>
                {
                    caretQueued = false;
                    if (box.IsDisposed || !box.Focused) return;
                    var parent = panel.Parent;
                    while (parent is not null && parent is not Panel { AutoScroll: true }) parent = parent.Parent;
                    if (parent is not Panel scroll) return;
                    var caret = scroll.PointToClient(box.PointToScreen(box.GetPositionFromCharIndex(box.SelectionStart)));
                    var bottom = caret.Y + box.Font.Height + Theme.Px(box, 8);
                    if (bottom > scroll.ClientSize.Height)
                        scroll.AutoScrollPosition = new Point(0, scroll.VerticalScroll.Value + bottom - scroll.ClientSize.Height);
                    else if (caret.Y < 0)
                        scroll.AutoScrollPosition = new Point(0, Math.Max(0, scroll.VerticalScroll.Value + caret.Y));
                });
            }
            void ResizeInput()
            {
                if (box.IsDisposed || box.ClientSize.Width < 1) return;
                var lines = box.IsHandleCreated ? box.GetLineFromCharIndex(box.TextLength) + 1 :
                    Math.Max(1, TextRenderer.MeasureText(box.Text + " ", box.Font, new Size(box.ClientSize.Width, int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Height / box.Font.Height);
                var desired = Math.Max(Theme.Px(panel, height), lines * box.Font.Height + Theme.Px(box, 6) + panel.Padding.Vertical);
                if (panel.MinimumSize.Height != desired) panel.MinimumSize = new Size(0, desired);
                if (panel.Height != desired) panel.Height = desired;
                panel.PerformLayout();
                KeepCaretVisible();
            }
            void RefreshInput()
            {
                ResizeInput();
                if (!box.IsHandleCreated || resizeQueued) return;
                resizeQueued = true;
                box.BeginInvoke(() => { resizeQueued = false; if (!box.IsDisposed) ResizeInput(); });
            }
            box.TextChanged += (_, _) => RefreshInput();
            box.SizeChanged += (_, _) => RefreshInput();
            box.FontChanged += (_, _) => RefreshInput();
            box.HandleCreated += (_, _) => RefreshInput();
            box.KeyUp += (_, _) => KeepCaretVisible();
            box.MouseUp += (_, _) => KeepCaretVisible();
        }
        return panel;
    }

    private static TableLayoutPanel Field(string caption, Control input)
    {
        var field = Stack(); Add(field, Theme.Label(caption, Theme.Small, Theme.Muted), 8); Add(field, input, 0); return field;
    }

    private static DateTimePicker TimePicker() => new()
    { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };

    private void RefreshEnabled()
    {
        if (hasRepeat.Checked) hasDate.Checked = true;
        hasDate.Enabled = !hasRepeat.Checked;
        dateField.Visible = hasDate.Checked; hasSchedule.Enabled = hasDate.Checked;
        scheduleFields.Visible = hasDate.Checked && hasSchedule.Checked;
        start.Enabled = end.Enabled = endDate.Enabled = hasDate.Checked && hasSchedule.Checked;
        endDateField.Visible = !hasRepeat.Checked;
        durationFields.Visible = hasDuration.Checked;
        repeatFields.Visible = hasRepeat.Checked;
        RefreshRepeatHint();
    }

    private void RefreshRepeatHint()
    {
        if (repeatMode.SelectedIndex == 0)
        {
            var begin = DateOnly.FromDateTime(date.Value);
            var number = begin.DayNumber + (int)repeatValue.Value - 1;
            repeatHint.Text = number <= new DateOnly(9998, 12, 31).DayNumber ?
                $"从 {begin:yyyy/MM/dd} 开始，每天重复，至 {DateOnly.FromDayNumber(number):yyyy/MM/dd}（含当天）。到期自动结束，每天的完成记录会保留。" : "持续时间超出可用日期范围，请缩短天数。";
        }
        else repeatHint.Text = $"从安排日期开始，每天重复，累计完成 {repeatValue.Value:0} 次后结束。每天最多记录一次；未来日期到当天才可勾选。";
        repeatHint.Text += " 每天的时间段须在同一天内。";
        if (original.CompletionDates.Count > 0) repeatHint.Text += " 修改目标会保留已有记录；开始和结束日期须包含已完成的日期。";
    }

    private void RefreshColor()
    {
        foreach (var swatch in swatches)
        {
            swatch.FlatAppearance.BorderSize = string.Equals((string?)swatch.Tag, color, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            swatch.FlatAppearance.BorderColor = Theme.Ink; swatch.Invalidate();
        }
        customColor.ForeColor = Theme.Ink;
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
                EndDay = scheduled ? DateOnly.FromDateTime(hasRepeat.Checked ? date.Value : endDate.Value) : null,
                DurationMinutes = hasDuration.Checked ? (int)Math.Round(duration.Value * (unit.SelectedIndex == 1 ? 60 : 1), MidpointRounding.AwayFromZero) : null,
                RepeatUntil = hasRepeat.Checked && repeatMode.SelectedIndex == 0 ? DateOnly.FromDateTime(date.Value).AddDays((int)repeatValue.Value - 1) : null,
                RepeatCount = hasRepeat.Checked && repeatMode.SelectedIndex == 1 ? (int)repeatValue.Value : null,
                CompletedDates = hasRepeat.Checked ? original.CompletedDates ?? (original.IsCompleted && original.Day is { } completed ? [completed] : null) : null,
                IsCompleted = hasRepeat.Checked ? false : original.IsCompleted,
                UpdatedAt = DateTimeOffset.Now
            };
            result.Validate(); Result = result;
            DialogResult = DialogResult.OK; Close();
        }
        catch (ArgumentException ex) { error.Text = ex.Message; title.Focus(); }
    }
}
