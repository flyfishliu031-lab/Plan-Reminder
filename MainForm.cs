using System.Diagnostics;
using System.Globalization;

namespace PlanReminder;

internal sealed class MainForm : Form
{
    private readonly PlanStore store;
    private readonly AppSettings settings;
    private readonly NotifyIcon tray = new() { Icon = Theme.AppIcon, Text = "计划表" };
    private readonly ContextMenuStrip trayMenu = new() { Font = Theme.Body };
    private bool exitRequested;
    private bool trayHintShown;
    private readonly CalendarView calendar = new();
    private readonly Label heading = Theme.Label("", Theme.Heading);
    private readonly Label summary = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label clock = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Button unscheduled = Theme.Button("待安排");
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
    private PlanItem? deleted;
    internal CalendarView Calendar => calendar;
    internal FlowLayoutPanel PlanList => list;
    internal NotifyIcon TrayIcon => tray;

    public MainForm(PlanStore data, AppSettings? preferences = null)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        store = data;
        settings = preferences ?? new AppSettings(Path.GetDirectoryName(data.FilePath)!);
        Text = "计划表"; Font = Theme.Body; BackColor = Theme.Canvas; ForeColor = Theme.Ink;
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
        unscheduled.Click += (_, _) => { selectedDay = null; RefreshViews(); };
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
            if (e.Control && e.KeyCode == Keys.Z && deleted is not null) { e.SuppressKeyPress = true; UndoDelete(); }
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
        Row(goToday, 10); Row(unscheduled, 28);
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
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(32, 24, 32, 20), Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize)); content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(noticeRow);
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        clock.Dock = DockStyle.Top; clock.Margin = new Padding(0, 0, 0, 18);
        var titleBar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 0, 0, 24) };
        titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        titleBar.RowStyles.Add(new RowStyle(SizeType.AutoSize)); titleBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Dock = DockStyle.Top; heading.Margin = new Padding(0, 0, 12, 10);
        titleBar.SizeChanged += (_, _) => heading.MaximumSize = new Size(Math.Max(1, titleBar.Width - Theme.Px(this, 156)), 0);
        summary.Dock = DockStyle.Top;
        var add = Theme.Button("＋ 添加计划", true); add.Dock = DockStyle.Top; add.Margin = new Padding(0, 5, 0, 0);
        add.Click += (_, _) => EditPlan(null);
        titleBar.Controls.Add(heading, 0, 0); titleBar.Controls.Add(add, 1, 0); titleBar.Controls.Add(summary, 0, 1); titleBar.SetColumnSpan(summary, 2);
        noticeText.AutoSize = false; noticeText.Dock = DockStyle.Fill; noticeText.TextAlign = ContentAlignment.MiddleLeft;
        noticeText.Padding = new Padding(12, 0, 0, 0); noticeText.AutoEllipsis = true;
        undo.Dock = DockStyle.Right; undo.Width = 80; undo.BackColor = notice.BackColor; undo.FlatAppearance.BorderSize = 0;
        notice.Controls.Add(noticeText); notice.Controls.Add(undo);
        var footer = Theme.Label("勾选划去 · Ctrl+N 添加 · Ctrl+Z 撤销删除", Theme.Small, Theme.Muted);
        footer.Dock = DockStyle.Top; footer.Margin = new Padding(0, 16, 0, 0);
        content.SizeChanged += (_, _) =>
        {
            var available = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal);
            footer.MaximumSize = clock.MaximumSize = summary.MaximumSize = new Size(available, 0);
        };
        content.Controls.Add(clock, 0, 0); content.Controls.Add(titleBar, 0, 1); content.Controls.Add(list, 0, 2);
        content.Controls.Add(notice, 0, 3); content.Controls.Add(footer, 0, 4);
        return content;
    }

    internal void SelectDate(DateOnly date)
    {
        selectedDay = date; RefreshViews(true);
    }

    internal void RefreshViews(bool navigate = false)
    {
        var scrollPosition = list.VerticalScroll.Value;
        var visible = store.Plans.Where(p => selectedDay is { } day ? p.AppearsOn(day) : p.Day is null)
            .OrderBy(p => p.StartsAt ?? DateTime.MaxValue).ThenBy(p => p.CreatedAt).ToArray();
        heading.Text = selectedDay is { } selected ? selected.ToString("M月d日 · dddd", CultureInfo.GetCultureInfo("zh-CN")) : "待安排";
        var completeCount = visible.Count(p => p.IsCompleted);
        summary.Text = visible.Length == 0 ? "添加一个计划，开始安排。" : $"{visible.Length} 项计划   ·   {visible.Length - completeCount} 项待完成   ·   {completeCount} 项已完成";
        unscheduled.Text = $"待安排  ·  {store.Plans.Count(p => p.Day is null)}";
        unscheduled.BackColor = selectedDay is null ? Theme.SoftAccent : Color.White;
        unscheduled.ForeColor = selectedDay is null ? Theme.Accent : Theme.Ink;
        calendar.SetData(store.Plans, selectedDay, navigate);
        // ponytail: redraw this day's cards; virtualize if a day routinely holds hundreds of plans.
        list.SuspendLayout();
        foreach (Control control in list.Controls.Cast<Control>().ToArray()) { list.Controls.Remove(control); control.Dispose(); }
        if (visible.Length == 0)
        {
            var empty = new SurfacePanel { Height = Theme.Px(this, 220), Margin = Padding.Empty };
            var message = Theme.Label("暂无计划\n\n点击“添加计划”开始安排", Theme.Body, Theme.Muted);
            message.AutoSize = false; message.Dock = DockStyle.Fill; message.TextAlign = ContentAlignment.MiddleCenter;
            empty.Controls.Add(message); list.Controls.Add(empty);
        }
        foreach (var item in visible)
        {
            var card = new PlanCard(item);
            card.CompletionRequested += ToggleCompletion;
            card.EditRequested += plan => EditPlan(plan);
            card.DeleteRequested += DeletePlan;
            list.Controls.Add(card);
        }
        ResizeCards(); list.ResumeLayout(true); ResizeCards();
        list.AutoScrollPosition = new Point(0, scrollPosition);
    }

    private void ResizeCards()
    {
        var width = Math.Max(100, list.ClientSize.Width - Theme.Px(this, 5) - (list.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        foreach (Control control in list.Controls) if (control.Width != width) control.Width = width;
    }

    private void EditPlan(PlanItem? existing)
    {
        var item = existing ?? new PlanItem { Day = selectedDay, Color = Palette.Next(store.Plans) };
        while (true)
        {
            using var editor = new PlanEditor(item, existing is null);
            if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is null) return;
            item = editor.Result;
            if (store.Plans.Any(p => item.Overlaps(p)) && MessageBox.Show(this,
                "此时间段与其他未完成计划重叠。仍要保存吗？", "时间重叠", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                continue;
            if (!Attempt(() => store.Save(item))) continue;
            RefreshViews(); return;
        }
    }

    internal void ToggleCompletion(PlanItem item)
    {
        Attempt(() => store.Save(item with { IsCompleted = !item.IsCompleted, UpdatedAt = DateTimeOffset.Now }));
        RefreshViews();
    }

    internal void DeletePlan(PlanItem item)
    {
        if (!Attempt(() => store.Delete(item.Id))) return;
        deleted = item; noticeText.Text = $"已删除：{item.Title}"; undo.Visible = true;
        ShowNotice(); RefreshViews();
    }

    internal void UndoDelete()
    {
        if (deleted is null || !Attempt(() => store.Save(deleted with { UpdatedAt = DateTimeOffset.Now }))) return;
        HideNotice(); RefreshViews();
    }

    private void ShowNotice()
    {
        notice.Visible = true; noticeRow.Height = Theme.Px(this, 44); undoTimer.Stop(); undoTimer.Start();
    }

    private void HideNotice()
    {
        undoTimer.Stop(); deleted = null; notice.Visible = false; noticeRow.Height = 0;
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
        if (date != today) { today = date; calendar.SetData(store.Plans, selectedDay); }
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
        if (disposing) { tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); systemTimer.Dispose(); undoTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
