using System.Diagnostics;
using System.Globalization;

namespace PlanReminder;

internal sealed class MainForm : Form
{
    private readonly PlanStore store;
    private readonly AppSettings settings;
    private readonly NotifyIcon tray = new() { Icon = Theme.AppIcon, Text = $"计划表 v{Program.AppVersion}" };
    private readonly ContextMenuStrip trayMenu = new() { Font = Theme.Body };
    private bool exitRequested;
    private bool trayHintShown;
    private readonly List<AlarmWindow> alarms = [];
    private readonly Dictionary<Guid, (DateTimeOffset At, long Stamp, TimeSpan Remaining)> countdowns = [];
    private readonly CalendarView calendar = new();
    private readonly Label heading = Theme.Label("", Theme.Heading);
    private readonly Label summary = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label clock = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Button unscheduled = Theme.Button("待安排");
    private readonly Button longTerm = Theme.Button("长期计划");
    private readonly Button timetable = Theme.Button("时间表");
    private readonly DailyTimelineView timeline = new();
    private readonly Panel viewHost = new() { Dock = DockStyle.Fill };
    private bool showingTimeline;
    private readonly Label footer = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly FlowLayoutPanel list = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill };
    private readonly Panel notice = new() { Dock = DockStyle.Fill, BackColor = Theme.SoftAccent, Visible = false };
    private readonly Label noticeText = Theme.Label("", Theme.Small);
    private readonly Button undo = Theme.Button("撤销");
    private readonly RowStyle noticeRow = new(SizeType.Absolute, 0);
    private readonly System.Windows.Forms.Timer systemTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer undoTimer = new() { Interval = 10000 };
    private DateOnly? selectedDay = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly today = DateOnly.FromDateTime(DateTime.Now);
    private bool cardLayoutQueued;
    private bool showingSeries;
    private PlanItem? deleted;
    private (Guid Id, DateOnly Day, bool WasComplete)? completionUndo;
    private readonly TextBox search = new() { PlaceholderText = "搜索计划或备注", AccessibleName = "搜索计划或备注", Dock = DockStyle.Fill };
    private readonly ComboBox stateFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "完成状态筛选", Dock = DockStyle.Fill };
    private readonly TableLayoutPanel filters = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 10, 0, 0) };
    private string? feedback;
    private readonly FlowLayoutPanel dateNavigation = new() { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
    internal CalendarView Calendar => calendar;
    internal FlowLayoutPanel PlanList => list;
    internal NotifyIcon TrayIcon => tray;
    internal DailyTimelineView Timeline => timeline;

    public MainForm(PlanStore data, AppSettings? preferences = null)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        store = data;
        settings = preferences ?? new AppSettings(Path.GetDirectoryName(data.FilePath)!);
        showingTimeline = settings.View == "timetable"; showingSeries = settings.View == "series";
        if (settings.View is "series" or "unscheduled") selectedDay = null;
        Text = $"计划表 · v{Program.AppVersion}"; Font = Theme.Body; BackColor = Theme.Canvas; ForeColor = Theme.Ink;
        ClientSize = new Size(1180, 780); MinimumSize = new Size(840, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Theme.AppIcon;
        KeyPreview = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildContent(), 1, 0);
        Controls.Add(root);
        trayMenu.Items.Add("打开计划表", null, (_, _) => RestoreWindow());
        trayMenu.Items.Add("设置", null, (_, _) => OpenSettings());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出程序", null, (_, _) => RequestExit());
        tray.ContextMenuStrip = trayMenu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RestoreWindow(); };
        tray.DoubleClick += (_, _) => RestoreWindow();
        Shown += (_, _) => tray.Visible = true;
        calendar.DateSelected += day => SelectDate(day);
        unscheduled.Click += (_, _) => { showingTimeline = false; showingSeries = false; selectedDay = null; RememberView(); RefreshViews(); };
        longTerm.Click += (_, _) => ShowLongTerm();
        timetable.Click += (_, _) => ShowTimeline();
        timeline.ListRequested += () => { showingTimeline = false; RememberView(); RefreshViews(); };
        timeline.OpenRequested += ShowPlanDetails;
        timeline.RangeSelected += (from, to) => EditPlan(null, ScheduleSeed(from, to));
        list.SizeChanged += (_, _) => ResizeCards();
        list.Layout += (_, _) => ResizeCards();
        list.SizeChanged += (_, _) =>
        {
            if (cardLayoutQueued || !list.IsHandleCreated) return;
            cardLayoutQueued = true;
            list.BeginInvoke(() => { cardLayoutQueued = false; if (!list.IsDisposed) { ResizeCards(); list.PerformLayout(); } });
        };
        undo.Click += (_, _) => UndoDelete();
        undoTimer.Tick += (_, _) => HideNotice();
        systemTimer.Tick += (_, _) => UpdateClock();
        Activated += (_, _) => UpdateClock();
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { e.SuppressKeyPress = true; EditPlan(null); }
            if (e.Control && e.KeyCode == Keys.Z && (deleted is not null || completionUndo is not null)) { e.SuppressKeyPress = true; UndoDelete(); }
            if (e.Control && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; showingTimeline = false; RefreshViews(); search.Focus(); }
        };
        ResumeLayout(true);
        Load += (_, _) => Theme.FitWindow(this);
        RefreshViews(true); UpdateClock(); systemTimer.Start();
    }

    private Control BuildSidebar()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(24), Margin = Padding.Empty };
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, BackColor = Color.White, Margin = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Layout += (_, _) => sidebar.MaximumSize = new Size(Math.Max(1, scroll.ClientSize.Width - scroll.Padding.Horizontal -
            (scroll.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)), 0);
        void Row(Control control, int gap)
        {
            var row = sidebar.RowCount++; sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Top; control.Margin = new Padding(0, 0, 0, gap); sidebar.Controls.Add(control, 0, row);
        }
        var brand = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize)); brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var emblem = Theme.Label("计", Theme.Title, Color.White);
        emblem.AutoSize = false; emblem.BackColor = Theme.Accent; emblem.TextAlign = ContentAlignment.MiddleCenter; emblem.Size = new Size(44, 48);
        var name = Theme.Label("计划表", Theme.Title); name.Margin = new Padding(0, 0, 0, 5);
        var tagline = Theme.Label("让每一天从容有序", Theme.Small, Theme.Muted);
        brand.Controls.Add(emblem, 0, 0); brand.SetRowSpan(emblem, 2); brand.Controls.Add(name, 1, 0); brand.Controls.Add(tagline, 1, 1);
        Row(brand, 32);
        calendar.Height = 324;
        Row(calendar, 12);
        var legend = Theme.Label("彩色圆点表示当天已有计划", Theme.Small, Theme.Muted); Row(legend, 20);
        var goToday = Theme.Button("回到今天");
        goToday.Click += (_, _) => SelectDate(DateOnly.FromDateTime(DateTime.Now));
        Row(goToday, 10); Row(timetable, 10); Row(unscheduled, 10); Row(longTerm, 28);
        Row(Theme.Label("数据与设置", Theme.Small, Theme.Muted), 12);
        var backups = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        backups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); backups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        backups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        backups.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var export = Theme.Button("导出备份"); var import = Theme.Button("导入备份");
        var configure = Theme.Button("设置"); configure.Width = 56; configure.AccessibleName = "打开设置";
        export.Dock = import.Dock = configure.Dock = DockStyle.Top; export.Width = import.Width = 64;
        export.Font = import.Font = configure.Font = Theme.Small;
        export.Padding = import.Padding = configure.Padding = new Padding(4, 0, 4, 0);
        export.Margin = new Padding(0, 0, 4, 0); import.Margin = new Padding(4, 0, 4, 0); configure.Margin = new Padding(4, 0, 0, 0);
        export.Click += (_, _) => Export(); import.Click += (_, _) => Import();
        configure.Click += (_, _) => OpenSettings();
        backups.Controls.Add(export, 0, 0); backups.Controls.Add(import, 1, 0); backups.Controls.Add(configure, 2, 0);
        var dataHint = new LinkLabel
        {
            Text = "已自动保存 · 打开数据目录", Font = Theme.Small, AutoSize = true,
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent, UseMnemonic = false
        };
        dataHint.Links.Add(dataHint.Text.IndexOf("打开", StringComparison.Ordinal), 6);
        dataHint.LinkClicked += (_, _) => Attempt(() =>
        {
            var directory = Path.GetDirectoryName(store.FilePath)!; Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        });
        Row(backups, 14); Row(dataHint, 0);
        scroll.Controls.Add(sidebar); return scroll;
    }

    private Control BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(24, 20, 24, 16), Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize)); content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(noticeRow);
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        clock.Dock = DockStyle.Top; clock.Margin = new Padding(0, 0, 0, 10);
        var titleBar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 4, Margin = new Padding(0, 0, 0, 14) };
        titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        titleBar.RowStyles.Add(new RowStyle(SizeType.AutoSize)); titleBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Dock = DockStyle.Top; heading.Margin = new Padding(0, 0, 12, 10);
        titleBar.SizeChanged += (_, _) => heading.MaximumSize = new Size(Math.Max(1, titleBar.Width - Theme.Px(this, 156)), 0);
        summary.Dock = DockStyle.Top;
        var add = Theme.Button("＋ 添加计划", true); add.Dock = DockStyle.Top; add.Margin = new Padding(0, 5, 0, 0);
        add.Click += (_, _) => EditPlan(null);
        titleBar.Controls.Add(heading, 0, 0); titleBar.Controls.Add(add, 1, 0); titleBar.Controls.Add(summary, 0, 1); titleBar.SetColumnSpan(summary, 2);
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        search.Margin = new Padding(0, 0, 8, 0); stateFilter.Margin = new Padding(0, 0, 8, 0);
        stateFilter.Items.AddRange(["全部", "未完成", "已完成"]); stateFilter.SelectedIndex = 0;
        var clear = Theme.Button("清除"); clear.Width = 64; clear.Height = 30; clear.Font = Theme.Small; clear.Padding = new Padding(4, 0, 4, 0); clear.Dock = DockStyle.Fill;
        clear.Click += (_, _) => ClearFilters();
        clear.Enabled = false;
        search.TextChanged += (_, _) => { clear.Enabled = search.Text.Length > 0 || stateFilter.SelectedIndex != 0; RefreshViews(); };
        stateFilter.SelectedIndexChanged += (_, _) => { clear.Enabled = search.Text.Length > 0 || stateFilter.SelectedIndex != 0; RefreshViews(); };
        var searchLabel = Theme.Label("搜索", Theme.Small, Theme.Muted); searchLabel.AutoSize = false; searchLabel.Dock = DockStyle.Fill; searchLabel.TextAlign = ContentAlignment.MiddleLeft;
        filters.Controls.Add(searchLabel, 0, 0); filters.Controls.Add(search, 1, 0); filters.Controls.Add(stateFilter, 2, 0); filters.Controls.Add(clear, 3, 0);
        titleBar.Controls.Add(filters, 0, 2); titleBar.SetColumnSpan(filters, 2);
        foreach (var (text, offset) in new[] { ("前一天", -1), ("后一天", 1), ("今天", 0) }) {
            var button = Theme.Button(text); button.Font = Theme.Small; button.Height = 30; button.Width = 76; button.Padding = new Padding(6, 0, 6, 0); button.Margin = new Padding(0, 0, 8, 0);
            button.Click += (_, _) => { var date = selectedDay ?? today; var target = offset == 0 ? today : date.AddDays(offset); if (target.Year is >= 1753 and <= 9998) SelectDate(target); };
            dateNavigation.Controls.Add(button);
        }
        titleBar.Controls.Add(dateNavigation, 0, 3); titleBar.SetColumnSpan(dateNavigation, 2);
        noticeText.AutoSize = false; noticeText.Dock = DockStyle.Fill; noticeText.TextAlign = ContentAlignment.MiddleLeft;
        noticeText.Padding = new Padding(12, 0, 0, 0); noticeText.AutoEllipsis = true;
        undo.Dock = DockStyle.Right; undo.Width = 80; undo.BackColor = notice.BackColor; undo.FlatAppearance.BorderSize = 0;
        notice.Controls.Add(noticeText); notice.Controls.Add(undo);
        footer.Dock = DockStyle.Top; footer.Margin = new Padding(0, 16, 0, 0);
        content.SizeChanged += (_, _) =>
        {
            var available = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal);
            footer.MaximumSize = clock.MaximumSize = summary.MaximumSize = new Size(available, 0);
        };
        viewHost.Controls.Add(list); viewHost.Controls.Add(timeline);
        content.Controls.Add(clock, 0, 0); content.Controls.Add(titleBar, 0, 1); content.Controls.Add(viewHost, 0, 2);
        content.Controls.Add(notice, 0, 3); content.Controls.Add(footer, 0, 4);
        return content;
    }

    internal void SelectDate(DateOnly date)
    {
        showingSeries = false; selectedDay = date; RememberView(); RefreshViews(true);
    }

    internal void ShowLongTerm() { showingTimeline = false; showingSeries = true; selectedDay = null; RememberView(); RefreshViews(); }
    internal void ShowTimeline() { showingTimeline = true; showingSeries = false; selectedDay ??= today; RememberView(); RefreshViews(); }
    private void RememberView()
    {
        var view = showingTimeline ? "timetable" : showingSeries ? "series" : selectedDay is null ? "unscheduled" : "plans";
        if (settings.View == view) return;
        try { settings.Save(settings.ExitOnClose, view); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { feedback = "视图已切换，但偏好未保存。"; }
    }
    internal void ClearFilters() { search.Text = ""; stateFilter.SelectedIndex = 0; }
    internal void FilterPlans(string query, int state) { search.Text = query; stateFilter.SelectedIndex = state; }

    internal void RefreshViews(bool navigate = false)
    {
        var scrollPosition = list.VerticalScroll.Value;
        var visible = store.Plans.Where(p => showingSeries ? p.IsRecurring : selectedDay is { } day ? p.AppearsOn(day) : p.Day is null)
            .OrderBy(p => p.IsRecurring && selectedDay is { } date && p.Start is { } time ? date.ToDateTime(time) :
                p.StartsAt ?? DateTime.MaxValue).ThenBy(p => p.CreatedAt).ToArray();
        heading.Text = showingSeries ? "长期计划" : selectedDay is { } selected ? selected.ToString("M月d日 · dddd", CultureInfo.GetCultureInfo("zh-CN")) : "待安排";
        var completeCount = visible.Count(p => showingSeries ? p.SeriesFinished(today) : p.IsCompleteOn(selectedDay ?? today));
        summary.Text = visible.Length == 0 ? "添加一个计划，开始安排。" : $"{visible.Length} 项计划   ·   {visible.Length - completeCount} 项待完成   ·   {completeCount} 项已完成";
        unscheduled.Text = $"待安排  ·  {store.Plans.Count(p => p.Day is null)}";
        unscheduled.BackColor = selectedDay is null && !showingSeries ? Theme.SoftAccent : Color.White;
        unscheduled.ForeColor = selectedDay is null && !showingSeries ? Theme.Accent : Theme.Ink;
        longTerm.Text = $"长期计划  ·  {store.Plans.Count(p => p.IsRecurring)}";
        longTerm.BackColor = showingSeries ? Theme.SoftAccent : Color.White;
        longTerm.ForeColor = showingSeries ? Theme.Accent : Theme.Ink;
        timetable.BackColor = showingTimeline ? Theme.SoftAccent : Color.White;
        timetable.ForeColor = showingTimeline ? Theme.Accent : Theme.Ink;
        filters.Visible = !showingTimeline;
        dateNavigation.Visible = selectedDay is not null;
        timeline.Visible = showingTimeline; list.Visible = !showingTimeline;
        if (showingTimeline) { timeline.BringToFront(); timeline.SetData(visible, selectedDay!.Value); }
        footer.Text = showingSeries ? "勾选记录今天完成 · 日历可恢复历史记录 · 删除会移除整个长期计划，可撤销" :
            "勾选划去 · Ctrl+N 添加 · Ctrl+F 搜索 · Ctrl+Z 撤销";
        if (feedback is not null) footer.Text = feedback;
        calendar.SetData(store.Plans, selectedDay, navigate);
        // ponytail: redraw this day's cards; virtualize if a day routinely holds hundreds of plans.
        list.SuspendLayout();
        foreach (Control control in list.Controls.Cast<Control>().ToArray()) { list.Controls.Remove(control); control.Dispose(); }
        var shown = 0;
        var query = search.Text.Trim();
        foreach (var item in visible)
        {
            if (query.Length > 0 && !item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) && !item.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var occurrences = !showingSeries && item.Start is not null && selectedDay is { } date ? item.Segments(date).Select(s => s.Occurrence) : new[] { selectedDay ?? today };
            foreach (var occurrence in occurrences) {
                var done = showingSeries ? item.SeriesFinished(today) : item.IsCompleteOn(occurrence);
                if (stateFilter.SelectedIndex == 1 && done || stateFilter.SelectedIndex == 2 && !done) continue;
                var card = new PlanCard(item, occurrence, showingSeries);
                card.CompletionRequested += plan => CompleteOn(plan, occurrence);
                card.EditRequested += plan => EditPlan(plan);
                card.DeleteRequested += DeletePlan;
                list.Controls.Add(card); shown++;
            }
        }
        if (shown == 0)
        {
            var empty = new SurfacePanel { Height = Theme.Px(this, 220), Margin = Padding.Empty };
            var message = Theme.Label(visible.Length == 0 ? "暂无计划\n\n点击“添加计划”开始安排" : "没有匹配的计划\n\n点击“清除”恢复全部计划", Theme.Body, Theme.Muted);
            message.AutoSize = false; message.Dock = DockStyle.Fill; message.TextAlign = ContentAlignment.MiddleCenter;
            empty.Controls.Add(message); list.Controls.Add(empty);
        }
        ResizeCards(); list.ResumeLayout(true); ResizeCards();
        if (!showingTimeline && (query.Length > 0 || stateFilter.SelectedIndex != 0)) summary.Text = $"{shown} 项匹配 · 共 {visible.Length} 项计划";
        list.AutoScrollPosition = new Point(0, scrollPosition);
    }

    private void ResizeCards()
    {
        var width = Math.Max(100, list.ClientSize.Width - Theme.Px(this, 5) - (list.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        foreach (Control control in list.Controls) if (control.Width != width) control.Width = width;
    }

    internal PlanItem ScheduleSeed(int from, int to)
    {
        var day = selectedDay ?? today;
        return new PlanItem { Day = day, Start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(from)),
            EndDay = day.AddDays(to == 1440 ? 1 : 0), End = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(to % 1440)), Color = Palette.Next(store.Plans) };
    }
    private void EditPlan(PlanItem? existing, PlanItem? seed = null)
    {
        var item = existing ?? seed ?? (showingTimeline ? ScheduleSeed(540, 600) : new PlanItem { Day = showingSeries ? today : selectedDay,
            RepeatCount = showingSeries ? 30 : null, Color = Palette.Next(store.Plans) });
        while (true)
        {
            using var editor = new PlanEditor(item, existing is null);
            if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is null) return;
            item = editor.Result;
            var conflicts = store.Plans.Where(p => item.Overlaps(p)).Take(3).ToArray();
            if (conflicts.Length > 0 && !Confirm("时间重叠", "与以下计划重叠：\n" + string.Join("\n", conflicts.Select(p => p.Title.Length > 80 ? p.Title[..80] + "…" : p.Title)), "仍然保存", "修改时间")) continue;
            if (!Attempt(() => store.Save(item))) continue;
            if (item.Day is null) { selectedDay = null; showingTimeline = showingSeries = false; }
            else if (!(showingSeries && item.IsRecurring) && (selectedDay is null || !item.AppearsOn(selectedDay.Value))) { selectedDay = item.Day; showingSeries = false; }
            if (item.Start is null) showingTimeline = false;
            ClearFilters(); RememberView(); feedback = "已保存" + (item.Alarm is { } savedAlarm ? " · " + savedAlarm.Status(item, DateTimeOffset.Now) : " · 自动保存在本机");
            RefreshViews(true); return;
        }
    }

    internal void ToggleCompletion(PlanItem item)
        => CompleteOn(item, selectedDay ?? today);
    private void CompleteOn(PlanItem item, DateOnly day)
    {
        if (Attempt(() =>
        {
            if (item.IsRecurring && day > today) throw new ArgumentException("未来日期的长期计划到当天才可记录完成。");
            store.Save(item.ToggleOn(day));
        })) {
            deleted = null; completionUndo = (item.Id, day, item.IsCompleteOn(day));
            noticeText.Text = item.IsCompleteOn(day) ? "已恢复计划 · 10 秒内可撤销" : "已划去计划 · 10 秒内可撤销";
            undo.Visible = true; ShowNotice();
        }
        RefreshViews();
    }

    private void ShowPlanDetails(PlanItem item, DateOnly occurrence)
    {
        using var dialog = new Form { Text = "时间表 · 计划详情", Icon = Theme.AppIcon, Font = Theme.Body, BackColor = Theme.Canvas,
            AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96), ClientSize = new Size(620, 480), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false };
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16) };
        var card = new PlanCard(item, occurrence) { Dock = DockStyle.Top };
        card.CompletionRequested += p => { dialog.Close(); CompleteOn(p, occurrence); };
        card.EditRequested += p => { dialog.Close(); EditPlan(p); };
        card.DeleteRequested += p => { dialog.Close(); DeletePlan(p); };
        scroll.Controls.Add(card); dialog.Controls.Add(scroll); dialog.Shown += (_, _) => Theme.FitWindow(dialog); dialog.ShowDialog(this);
    }

    internal void DeletePlan(PlanItem item)
    {
        if (item.IsRecurring && Application.MessageLoop && !Confirm("删除整个长期计划？", $"将删除这个计划及 {item.CompletionDates.Count} 条完成记录。删除后 10 秒内可撤销。", "删除整个计划", "取消")) return;
        if (!Attempt(() => store.Delete(item.Id))) return;
        completionUndo = null; deleted = item; noticeText.Text = $"已删除：{item.Title}"; undo.Visible = true;
        ShowNotice(); RefreshViews();
    }

    internal void UndoDelete()
    {
        if (completionUndo is { } action) {
            var current = store.Plans.FirstOrDefault(p => p.Id == action.Id);
            if (current is not null && current.IsCompleteOn(action.Day) != action.WasComplete && !Attempt(() => store.Save(current.ToggleOn(action.Day)))) return;
            HideNotice(); RefreshViews(); return;
        }
        if (deleted is null || !Attempt(() => store.Save(deleted with { UpdatedAt = DateTimeOffset.Now }))) return;
        HideNotice(); RefreshViews();
    }

    private void ShowNotice()
    {
        notice.Visible = true; noticeRow.Height = Theme.Px(this, 44); undoTimer.Stop(); undoTimer.Start();
    }

    private void HideNotice()
    {
        undoTimer.Stop(); deleted = null; completionUndo = null; notice.Visible = false; noticeRow.Height = 0;
    }

    private bool Confirm(string heading, string text, string accept, string cancel)
    {
        var yes = new TaskDialogButton(accept); var no = new TaskDialogButton(cancel);
        return TaskDialog.ShowDialog(this, new TaskDialogPage { Caption = "计划表", Heading = heading, Text = text, AllowCancel = true, DefaultButton = no, Buttons = { no, yes } }) == yes;
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "计划备份 (*.json)|*.json", FileName = $"计划备份-{DateTime.Now:yyyyMMdd}.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (Attempt(() => store.Export(dialog.FileName))) MessageBox.Show(this, "备份已导出。", "导出备份", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Import()
    {
        using var dialog = new OpenFileDialog { Filter = "计划备份 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var count = 0;
        if (!Attempt(() => count = store.Import(dialog.FileName))) return;
        RefreshViews();
        MessageBox.Show(this, $"已导入或更新 {count} 项计划。\n相同编号保留更新时间较新的版本。", "导入备份", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool Attempt(Action action)
    {
        try { action(); return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, $"操作未完成，当前计划未被更改。\n\n{ex.Message}", "无法完成操作", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void UpdateClock()
    {
        TimeZoneInfo.ClearCachedData(); // Read the device timezone again rather than keeping a stale cached offset.
        var now = DateTime.Now;
        var offset = TimeZoneInfo.Local.GetUtcOffset(now);
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        clock.Text = $"系统时间  ·  {now:yyyy/MM/dd  HH:mm:ss}   (UTC{sign}{offset.Duration():hh\\:mm})";
        var date = DateOnly.FromDateTime(now);
        if (showingTimeline) timeline.UpdateTime();
        if (date != today) { today = date; RefreshViews(); }
        if (Application.MessageLoop) CheckAlarms();
    }

    internal void RestoreWindow()
    {
        if (IsDisposed) return;
        Show(); ShowInTaskbar = true;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Theme.FitWindow(this); Activate();
        OwnedForms.FirstOrDefault(form => form.Visible)?.Activate();
    }

    private void OpenSettings()
    {
        RestoreWindow();
        if (OwnedForms.Any(form => form.Visible)) return;
        using var dialog = new SettingsForm(settings); dialog.ShowDialog(this);
    }

    internal void RequestExit() { exitRequested = true; Close(); }

    private void CheckAlarms()
    {
        var changed = false;
        foreach (var window in alarms.ToArray()) {
            var current = store.Plans.FirstOrDefault(p => p.Id == window.PlanId);
            if (current?.Alarm is null || current.IsCompleteOn(window.Day) || current.GoalReached || current.Alarm.Mode == "after" && current.SeriesFinished(today)) window.Close();
        }
        foreach (var plan in store.Plans.ToArray()) {
            var now = DateTimeOffset.Now;
            DateTimeOffset? candidate;
            if (plan.Alarm?.Mode == "after") {
                var alarm = plan.Alarm;
                var at = alarm.At!.Value;
                if (plan.IsCompleted || plan.SeriesFinished(today) || plan.IsRecurring && plan.IsCompleteOn(today) || alarm.FiredAt is { } fired && at <= fired) continue;
                if (!countdowns.TryGetValue(plan.Id, out var anchor) || anchor.At != at) {
                    anchor = (at, System.Diagnostics.Stopwatch.GetTimestamp(), at - now); countdowns[plan.Id] = anchor;
                }
                var remaining = anchor.Remaining - System.Diagnostics.Stopwatch.GetElapsedTime(anchor.Stamp);
                candidate = remaining <= TimeSpan.Zero && remaining >= TimeSpan.FromMinutes(-5) ? at : null;
            } else candidate = plan.Alarm?.Next(plan, now);
            if (candidate is not { } due || plan.Alarm?.Mode != "after" && due > now) continue;
            try { store.Save(plan with { Alarm = plan.Alarm! with { FiredAt = due } }); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            var window = new AlarmWindow(plan, due); alarms.Add(window);
            window.FormClosed += (_, _) => { alarms.Remove(window); window.Dispose(); };
            window.Show();
            changed = true;
        }
        foreach (var id in countdowns.Keys.Where(id => !store.Plans.Any(p => p.Id == id && p.Alarm?.Mode == "after")).ToArray()) countdowns.Remove(id);
        if (changed) RefreshViews();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !exitRequested && !settings.ExitOnClose)
        {
            e.Cancel = true; Hide();
            if (!trayHintShown && tray.Visible && Application.MessageLoop)
            {
                trayHintShown = true;
                tray.ShowBalloonTip(3000, "计划表已在后台运行", "点击托盘图标重新打开，右键菜单可退出程序。", ToolTipIcon.Info);
            }
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (var window in alarms.ToArray()) window.Close(); tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); systemTimer.Dispose(); undoTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
