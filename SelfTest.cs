namespace PlanReminder;

// One executable check; no test framework or runtime dependencies.
internal static class SelfTest
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlanReminder-check-" + Guid.NewGuid().ToString("N"));
        var checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            Console.WriteLine("PASS: " + description); checks++;
        }
        void Reject(Action action, string description)
        {
            try { action(); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            { Check(true, description); return; }
            throw new InvalidOperationException("FAIL: " + description);
        }
        var today = DateOnly.FromDateTime(DateTime.Now);
        var first = new PlanItem { Title = "中文计划 & delete <文字>", Day = today, Color = "#237DAB" };
        var store = new PlanStore(Path.Combine(root, "data")); store.Load();
        Check(store.Plans.Count == 0, "empty first launch");
        store.Save(first);
        var reopened = new PlanStore(Path.Combine(root, "data")); reopened.Load();
        Check(reopened.Plans.Single() == first, "Unicode and optional fields survive restart");
        var crossDay = new PlanItem { Title = "跨天计划", Day = today, EndDay = today.AddDays(1), Start = new TimeOnly(23, 0), End = new TimeOnly(1, 0) };
        crossDay.Validate();
        Check(crossDay.AppearsOn(today) && crossDay.AppearsOn(today.AddDays(1)) && !crossDay.AppearsOn(today.AddDays(2)), "cross-day calendar membership");
        Check(!(crossDay with { End = TimeOnly.MinValue }).AppearsOn(today.AddDays(1)), "midnight endpoint is exclusive");
        Check(crossDay.Overlaps(crossDay with { Id = Guid.NewGuid() }), "overlap detection");
        Check(!crossDay.Overlaps(crossDay with { Id = Guid.NewGuid(), IsCompleted = true }), "completed plans do not block schedule");
        Check(!crossDay.Overlaps(crossDay with { Id = Guid.NewGuid(), Day = today.AddDays(1), EndDay = today.AddDays(1), Start = new TimeOnly(1, 0), End = new TimeOnly(2, 0) }), "touching intervals do not overlap");
        Reject(() => (first with { Title = " " }).Validate(), "blank title rejected");
        Reject(() => (crossDay with { EndDay = today }).Validate(), "backwards interval rejected");
        Reject(() => (first with { DurationMinutes = 0 }).Validate(), "zero duration rejected");
        Reject(() => (first with { Day = null, Start = new TimeOnly(9, 0) }).Validate(), "incomplete schedule rejected");
        var palettePlans = new List<PlanItem>();
        for (var index = 0; index < 100; index++) palettePlans.Add(first with { Id = Guid.NewGuid(), Color = Palette.Next(palettePlans) });
        Check(palettePlans.Select(p => p.Color).Distinct().Count() == 100, "automatic colors remain unique after palette is exhausted");
        var completed = first with { IsCompleted = true, UpdatedAt = first.UpdatedAt.AddSeconds(1) };
        store.Save(completed); reopened.Load();
        Check(reopened.Plans.Single().IsCompleted && File.Exists(store.FilePath + ".bak"), "completion and automatic backup persist");
        var backup = Path.Combine(root, "backup.json"); store.Export(backup);
        var imported = new PlanStore(Path.Combine(root, "import")); imported.Load();
        Check(imported.Import(backup) == 1 && imported.Import(backup) == 0, "import merges without duplicating plans");
        imported.Save(first with { Title = "较新的标题", UpdatedAt = completed.UpdatedAt.AddSeconds(1) });
        Check(imported.Import(backup) == 0 && imported.Plans.Single().Title == "较新的标题", "older backup cannot overwrite newer plan");
        var malformed = Path.Combine(root, "malformed.json"); File.WriteAllText(malformed, "{ broken");
        var before = File.ReadAllText(imported.FilePath);
        Reject(() => imported.Import(malformed), "malformed backup rejected");
        Check(File.ReadAllText(imported.FilePath) == before, "failed import leaves stored data unchanged");
        File.WriteAllText(malformed, "{\"schemaVersion\":2,\"plans\":[]}");
        Reject(() => imported.Import(malformed), "unknown schema rejected");
        File.WriteAllText(malformed, "{\"schemaVersion\":1,\"plans\":[null]}");
        Reject(() => imported.Import(malformed), "null plan rejected");
        Reject(() => store.Export(store.FilePath), "export cannot overwrite active data");
        var broken = new PlanStore(Path.Combine(root, "blocked")); Directory.CreateDirectory(broken.FilePath);
        Reject(() => broken.Save(first), "disk write failure surfaced");
        Check(broken.Plans.Count == 0, "failed disk write does not change in-memory state");
        File.WriteAllText(store.FilePath, "damaged");
        Reject(store.Load, "damaged primary data rejected");
        store.RestoreBackup();
        Check(store.Plans.Single() == first && Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.corrupt-*").Length == 1, "backup recovery preserves damaged original");

        var uiStore = new PlanStore(Path.Combine(root, "ui")); uiStore.Save(first);
        using var form = new MainForm(uiStore);
        _ = form.Handle; form.PerformLayout();
        form.ToggleCompletion(first);
        var card = form.PlanList.Controls.OfType<PlanCard>().Single();
        var title = card.Controls.OfType<Label>().Single(label => label.Text == first.Title);
        Check(title.Font.Strikeout && title.ForeColor == ColorTranslator.FromHtml(first.Color), "completed title uses same color and strikethrough");
        form.ToggleCompletion(uiStore.Plans.Single());
        Check(!uiStore.Plans.Single().IsCompleted, "completion can be restored");
        form.DeletePlan(uiStore.Plans.Single()); Check(uiStore.Plans.Count == 0, "delete persists immediately");
        form.UndoDelete(); Check(uiStore.Plans.Count == 1, "delete undo restores plan");
        uiStore.Save(crossDay); form.SelectDate(today.AddDays(1));
        Check(form.PlanList.Controls.OfType<PlanCard>().Single().Item.Id == crossDay.Id, "continuing cross-day plan appears on next day");
        IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
        using var editor = new PlanEditor(first, false);
        editor.SavePlan();
        Check(editor.Result is { } editorPlan && editorPlan.Title == first.Title && editorPlan.Day == today && editorPlan.Start is null, "editor preserves date with optional schedule empty");
        using var pendingEditor = new PlanEditor(new PlanItem { Title = "待安排内容", DurationMinutes = 90 }, true);
        pendingEditor.SavePlan();
        Check(pendingEditor.Result?.Day is null && pendingEditor.Result?.DurationMinutes == 90, "unscheduled editor preserves optional duration");
        using var scheduledEditor = new PlanEditor(crossDay, false);
        var scheduledTitle = Descendants(scheduledEditor).OfType<TextBox>().Single(box => box.AccessibleName == "计划标题（必填）");
        scheduledTitle.Text = "修改后的跨天计划";
        scheduledEditor.SavePlan();
        Check(scheduledEditor.Result is { } scheduledPlan && scheduledPlan.Title == "修改后的跨天计划" && scheduledPlan.StartsAt == crossDay.StartsAt && scheduledPlan.EndsAt == crossDay.EndsAt,
            "editor saves edited title and retains cross-day schedule");

        var settings = new AppSettings(Path.Combine(root, "settings")); settings.Load();
        Check(!settings.ExitOnClose && !File.Exists(settings.FilePath), "first launch defaults to close-to-tray without writing settings");
        settings.Save(true);
        var savedSettings = new AppSettings(Path.Combine(root, "settings")); savedSettings.Load();
        Check(savedSettings.ExitOnClose, "close-exit preference survives restart");
        settings.Save(false); savedSettings.Load();
        Check(!savedSettings.ExitOnClose, "close-exit preference can be disabled");
        using (var cancelledSettings = new SettingsForm(settings))
        {
            Descendants(cancelledSettings).OfType<CheckBox>().Single().Checked = true;
            cancelledSettings.Close();
        }
        Check(!settings.ExitOnClose, "cancelling settings leaves current preference unchanged");
        using (var settingsDialog = new SettingsForm(settings))
        {
            Descendants(settingsDialog).OfType<CheckBox>().Single().Checked = true;
            settingsDialog.SaveSettings();
        }
        savedSettings.Load();
        Check(settings.ExitOnClose && savedSettings.ExitOnClose, "settings dialog saves and immediately applies preference");
        var storedSettings = File.ReadAllText(settings.FilePath);
        File.WriteAllText(settings.FilePath, "{\"exitOnClose\":\"invalid\"}");
        Reject(settings.Load, "invalid settings rejected");
        Check(settings.ExitOnClose, "failed settings load does not change current preference");
        File.WriteAllText(settings.FilePath, storedSettings);
        var blockedSettings = new AppSettings(Path.Combine(root, "blocked-settings")); Directory.CreateDirectory(blockedSettings.FilePath);
        Reject(() => blockedSettings.Save(true), "settings write failure surfaced");
        Check(!blockedSettings.ExitOnClose, "failed settings save does not apply unpersisted preference");

        settings.Save(false);
        var plansBeforeClosing = File.ReadAllText(uiStore.FilePath);
        using var trayForm = new MainForm(uiStore, settings);
        trayForm.Show(); Application.DoEvents();
        Check(trayForm.TrayIcon.Visible && trayForm.TrayIcon.Icon is not null && trayForm.TrayIcon.ContextMenuStrip!.Items.Cast<ToolStripItem>().Any(item => item.Text == "设置"),
            "running window registers tray icon with settings menu");
        trayForm.Close(); Application.DoEvents();
        Check(!trayForm.IsDisposed && !trayForm.Visible && trayForm.TrayIcon.Visible, "default close hides window and retains tray icon");
        trayForm.TrayIcon.ContextMenuStrip!.Items[0].PerformClick(); Application.DoEvents();
        Check(trayForm.Visible, "tray open action restores hidden window");
        trayForm.WindowState = FormWindowState.Minimized; trayForm.RestoreWindow();
        Check(trayForm.WindowState == FormWindowState.Normal, "restoring from tray also restores a minimized window");
        trayForm.Close();
        trayForm.TrayIcon.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(item => item.Text == "退出程序").PerformClick();
        Check(trayForm.IsDisposed && !trayForm.TrayIcon.Visible, "tray exit closes hidden window and removes icon");
        settings.Save(true);
        using var exitForm = new MainForm(uiStore, settings); exitForm.Show(); exitForm.Close();
        Check(exitForm.IsDisposed && !exitForm.TrayIcon.Visible, "enabled close-exit preference closes program and removes icon");
        Check(File.ReadAllText(uiStore.FilePath) == plansBeforeClosing, "close and exit paths preserve stored plans");
        Console.WriteLine($"All {checks} checks passed. Test data: {root}");
    }
}
