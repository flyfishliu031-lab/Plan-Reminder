using System.Diagnostics;
using System.Globalization;

namespace PlanReminder;

internal sealed class MainForm : Form
{
    private readonly PlanStore store;
    private readonly CalendarView calendar = new();
    private readonly Label heading = Theme.Label("", Theme.Heading);
    private readonly Label summary = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Label clock = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly Button unscheduled = Theme.Button("待安排");
    private readonly FlowLayoutPanel list = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill };
    private readonly Panel notice = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(238, 235, 253), Visible = false };
    private readonly Label noticeText = Theme.Label("", Theme.Small);
    private readonly Button undo = Theme.Button("撤销");
    private readonly RowStyle noticeRow = new(SizeType.Absolute, 0);
    private readonly System.Windows.Forms.Timer systemTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer undoTimer = new() { Interval = 10000 };
    private DateOnly? selectedDay = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly today = DateOnly.FromDateTime(DateTime.Now);
    private PlanItem? deleted;
    internal CalendarView Calendar => calendar;
    internal FlowLayoutPanel PlanList => list;

    public MainForm(PlanStore data)
    {
        store = data;
        Text = "计划表"; Font = Theme.Body; BackColor = Theme.Canvas; ForeColor = Theme.Ink;
        ClientSize = new Size(1120, 750); MinimumSize = new Size(970, 640);
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildContent(), 1, 0);
        Controls.Add(root);
        calendar.DateSelected += day => SelectDate(day);
        unscheduled.Click += (_, _) => { selectedDay = null; RefreshViews(); };
        list.SizeChanged += (_, _) => ResizeCards();
        undo.Click += (_, _) => UndoDelete();
        undoTimer.Tick += (_, _) => HideNotice();
        systemTimer.Tick += (_, _) => UpdateClock();
        Activated += (_, _) => UpdateClock();
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { e.SuppressKeyPress = true; EditPlan(null); }
            if (e.Control && e.KeyCode == Keys.Z && deleted is not null) { e.SuppressKeyPress = true; UndoDelete(); }
        };
        RefreshViews(true); UpdateClock(); systemTimer.Start();
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.White,
            Padding = new Padding(20, 22, 20, 18), Margin = Padding.Empty
        };
        foreach (var height in new[] { 90, 348, 48, 48 }) sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var brand = new Panel { Dock = DockStyle.Fill };
        var emblem = Theme.Label("计", Theme.Title, Color.White);
        emblem.AutoSize = false; emblem.BackColor = Theme.Accent; emblem.TextAlign = ContentAlignment.MiddleCenter;
        emblem.SetBounds(0, 3, 42, 42);
        var name = Theme.Label("计划表", Theme.Title); name.Location = new Point(54, 0);
        var tagline = Theme.Label("你的每日安排", Theme.Small, Theme.Muted); tagline.Location = new Point(54, 28);
        brand.Controls.AddRange([emblem, name, tagline]);
        calendar.Dock = DockStyle.Fill; calendar.Margin = new Padding(0, 0, 0, 14);
        var goToday = Theme.Button("回到今天"); goToday.Dock = DockStyle.Fill; goToday.Margin = new Padding(0, 6, 0, 4);
        goToday.Click += (_, _) => SelectDate(DateOnly.FromDateTime(DateTime.Now));
        unscheduled.Dock = DockStyle.Fill; unscheduled.Margin = new Padding(0, 6, 0, 4);
        var backups = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        backups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); backups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var export = Theme.Button("导出备份"); var import = Theme.Button("导入备份");
        export.Dock = import.Dock = DockStyle.Fill;
        export.Margin = new Padding(0, 0, 5, 0); import.Margin = new Padding(5, 0, 0, 0);
        export.Click += (_, _) => Export(); import.Click += (_, _) => Import();
        backups.Controls.Add(export, 0, 0); backups.Controls.Add(import, 1, 0);
        var dataHint = new LinkLabel
        {
            Text = "数据自动保存在本机\n打开数据目录", Font = Theme.Small, Dock = DockStyle.Fill,
            LinkColor = Theme.Muted, ActiveLinkColor = Theme.Accent, UseMnemonic = false, Padding = new Padding(0, 12, 0, 0)
        };
        dataHint.Links.Add(dataHint.Text.IndexOf("打开", StringComparison.Ordinal), 6);
        dataHint.LinkClicked += (_, _) => Attempt(() =>
        {
            var directory = Path.GetDirectoryName(store.FilePath)!; Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        });
        sidebar.Controls.Add(brand, 0, 0); sidebar.Controls.Add(calendar, 0, 1);
        sidebar.Controls.Add(goToday, 0, 2); sidebar.Controls.Add(unscheduled, 0, 3);
        sidebar.Controls.Add(backups, 0, 5); sidebar.Controls.Add(dataHint, 0, 6);
        return sidebar;
    }

    private Control BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(28, 22, 28, 12), Margin = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(noticeRow);
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        clock.Dock = DockStyle.Fill; clock.TextAlign = ContentAlignment.MiddleLeft;
        var titleBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        titleBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); titleBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Dock = DockStyle.Fill; heading.AutoSize = false; heading.TextAlign = ContentAlignment.MiddleLeft;
        summary.Dock = DockStyle.Fill; summary.AutoSize = false;
        var add = Theme.Button("＋ 添加计划", true); add.Dock = DockStyle.Fill; add.Margin = new Padding(0, 8, 0, 8);
        add.Click += (_, _) => EditPlan(null);
        titleBar.Controls.Add(heading, 0, 0); titleBar.Controls.Add(add, 1, 0); titleBar.Controls.Add(summary, 0, 1); titleBar.SetColumnSpan(summary, 2);
        noticeText.AutoSize = false; noticeText.Dock = DockStyle.Fill; noticeText.TextAlign = ContentAlignment.MiddleLeft;
        noticeText.Padding = new Padding(12, 0, 0, 0); noticeText.AutoEllipsis = true;
        undo.Dock = DockStyle.Right; undo.Width = 80; undo.BackColor = notice.BackColor; undo.FlatAppearance.BorderSize = 0;
        notice.Controls.Add(noticeText); notice.Controls.Add(undo);
        var footer = Theme.Label("勾选划去完成 · Ctrl+N 添加 · Ctrl+Z 撤销删除                         v1.0.0", Theme.Small, Theme.Muted);
        footer.Dock = DockStyle.Fill; footer.AutoSize = false; footer.TextAlign = ContentAlignment.MiddleLeft;
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
        heading.Text = selectedDay is { } selected ? selected.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")) : "待安排";
        var completeCount = visible.Count(p => p.IsCompleted);
        summary.Text = visible.Length == 0 ? "添加一个计划，开始安排。" : $"{visible.Length} 项计划   ·   {visible.Length - completeCount} 项待完成   ·   {completeCount} 项已完成";
        unscheduled.Text = $"待安排  ·  {store.Plans.Count(p => p.Day is null)}";
        unscheduled.BackColor = selectedDay is null ? Color.FromArgb(238, 235, 253) : Color.White;
        unscheduled.ForeColor = selectedDay is null ? Theme.Accent : Theme.Ink;
        calendar.SetData(store.Plans, selectedDay, navigate);
        // ponytail: redraw this day's cards; virtualize if a day routinely holds hundreds of plans.
        list.SuspendLayout();
        foreach (Control control in list.Controls.Cast<Control>().ToArray()) { list.Controls.Remove(control); control.Dispose(); }
        if (visible.Length == 0)
        {
            var empty = new Panel { Height = Theme.Px(this, 200), BackColor = Color.White, Margin = Padding.Empty };
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
        ResizeCards(); list.ResumeLayout(true);
        list.AutoScrollPosition = new Point(0, scrollPosition);
    }

    private void ResizeCards()
    {
        var width = Math.Max(100, list.ClientSize.Width - Theme.Px(this, 5) - (list.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        foreach (Control control in list.Controls) control.Width = width;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) { systemTimer.Dispose(); undoTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
