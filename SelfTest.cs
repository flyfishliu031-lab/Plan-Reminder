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
        File.WriteAllText(malformed, "{\"schemaVersion\":99,\"plans\":[]}");
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

        var begin = today.AddDays(-4);
        var daily = new PlanItem { Title = "每天学习", Day = begin, RepeatUntil = today, Color = Palette.Colors[2] };
        daily.Validate();
        Check(daily.AppearsOn(begin) && daily.AppearsOn(today) && !daily.AppearsOn(begin.AddDays(-1)) && !daily.AppearsOn(today.AddDays(1)),
            "duration recurrence includes start and final day only");
        Check(!daily.SeriesFinished(today) && daily.SeriesFinished(today.AddDays(1)), "duration recurrence ends after final day even if days were missed");
        var recorded = daily.ToggleOn(begin).ToggleOn(today);
        Check(recorded.CompletionDates.Count == 2 && recorded.IsCompleteOn(begin) && !recorded.IsCompleteOn(begin.AddDays(1)), "daily completion is recorded independently");
        var restoredDay = recorded.ToggleOn(begin);
        Check(restoredDay.CompletionDates.Count == 1 && restoredDay.IsCompleteOn(today), "restoring one day preserves other daily records");
        Check(restoredDay.SeriesFinished(today.AddDays(1)), "restoring history does not extend a duration goal");
        Reject(() => daily.ToggleOn(today.AddDays(1)), "completion outside recurrence range rejected");
        var countGoal = daily with { RepeatUntil = null, RepeatCount = 2 };
        var countDone = countGoal.ToggleOn(begin).ToggleOn(today);
        Check(countDone.SeriesFinished(today) && !countDone.AppearsOn(today.AddDays(1)) && countDone.AppearsOn(begin.AddDays(1)),
            "count goal ends after target and retains historical calendar dates");
        var countRestored = countDone.ToggleOn(begin);
        Check(!countRestored.SeriesFinished(today) && countRestored.AppearsOn(today.AddDays(30)), "restoring history reopens count goal");
        Check(countGoal.ToggleOn(begin).ToggleOn(begin).CompletionDates.Count == 0, "same day cannot accumulate duplicate completions");
        Reject(() => (daily with { Day = null }).Validate(), "recurrence requires start date");
        Reject(() => (daily with { RepeatCount = 3 }).Validate(), "recurrence cannot combine duration and count goals");
        Reject(() => (daily with { RepeatUntil = begin.AddDays(-1) }).Validate(), "backwards recurrence range rejected");
        Reject(() => (countGoal with { RepeatCount = 0 }).Validate(), "zero recurrence target rejected");
        Reject(() => (daily with { CompletedDates = [begin, begin] }).Validate(), "duplicate completion records rejected");
        Reject(() => (daily with { CompletedDates = [begin.AddDays(-1)] }).Validate(), "completion before start rejected");
        Reject(() => (daily with { RepeatUntil = begin.AddDays(3650) }).Validate(), "oversized duration goal rejected");
        Reject(() => (daily with { Start = new TimeOnly(23, 0), End = new TimeOnly(1, 0), EndDay = begin.AddDays(1) }).Validate(),
            "cross-day daily template rejected without changing ordinary cross-day plans");
        var timedDaily = daily with { Start = new TimeOnly(9, 0), End = new TimeOnly(10, 0), EndDay = begin };
        var timedSingle = new PlanItem { Title = "另一项计划", Day = today, EndDay = today, Start = new TimeOnly(9, 30), End = new TimeOnly(10, 30) };
        Check(timedDaily.Overlaps(timedSingle) && timedSingle.Overlaps(timedDaily), "daily and ordinary overlap detected symmetrically");
        Check(!(timedDaily with { CompletedDates = [today] }).Overlaps(timedSingle), "completed daily occurrence does not block ordinary schedule");
        var longSingle = timedSingle with { Day = begin, Start = new TimeOnly(23, 0), EndDay = begin.AddDays(1), End = new TimeOnly(12, 0) };
        Check(timedDaily.Overlaps(longSingle), "overlap detects following day after ordinary partial first day");
        var anotherDaily = timedDaily with { Id = Guid.NewGuid(), Day = today, EndDay = today, RepeatUntil = today.AddDays(5) };
        Check(timedDaily.Overlaps(anotherDaily) && !(timedDaily with { CompletedDates = [today] }).Overlaps(anotherDaily),
            "daily series overlap respects dates and recorded exceptions");
        var sortedStore = new PlanStore(Path.Combine(root, "sort-daily"));
        sortedStore.Save(timedDaily with { Start = new TimeOnly(15, 0), End = new TimeOnly(16, 0) });
        sortedStore.Save(timedSingle with { Start = new TimeOnly(8, 0), End = new TimeOnly(9, 0) });
        using (var sortedForm = new MainForm(sortedStore))
        {
            sortedForm.SelectDate(today);
            Check(sortedForm.PlanList.Controls.OfType<PlanCard>().First().Item.Id == timedSingle.Id,
                "daily series sorts by occurrence time alongside ordinary plans");
        }

        var recurringStore = new PlanStore(Path.Combine(root, "recurring")); recurringStore.Save(countDone);
        var recurringReopened = new PlanStore(Path.Combine(root, "recurring")); recurringReopened.Load();
        Check(recurringReopened.Plans.Single().RepeatCount == 2 && recurringReopened.Plans.Single().CompletionDates.SequenceEqual(countDone.CompletionDates),
            "recurrence goal and daily records survive restart");
        var recurrenceBackup = Path.Combine(root, "recurrence-backup.json"); recurringStore.Export(recurrenceBackup);
        var recurringImport = new PlanStore(Path.Combine(root, "recurring-import")); recurringImport.Import(recurrenceBackup);
        Check(recurringImport.Plans.Single().CompletionDates.SequenceEqual(countDone.CompletionDates), "backup import preserves recurrence progress");
        var legacy = Path.Combine(root, "legacy.json");
        File.WriteAllText(legacy, "{\"schemaVersion\":1,\"plans\":[{\"id\":\"" + first.Id + "\",\"title\":\"旧版计划\",\"day\":\"" + today.ToString("yyyy-MM-dd") + "\"}]}");
        Check(recurringImport.Import(legacy) == 1 && recurringImport.Plans.Any(p => p.Title == "旧版计划" && !p.IsRecurring), "version one backups remain readable");
        using (var recurringForm = new MainForm(recurringStore))
        {
            recurringForm.ShowLongTerm();
            Check(recurringForm.PlanList.Controls.OfType<PlanCard>().Single().Item.Id == countDone.Id, "long-term overview retains finished series");
            recurringForm.SelectDate(begin); recurringForm.ToggleCompletion(recurringStore.Plans.Single());
            Check(recurringStore.Plans.Single().CompletionDates.Count == 1 && !recurringStore.Plans.Single().SeriesFinished(today),
                "calendar completion updates stored series progress");
            recurringForm.DeletePlan(recurringStore.Plans.Single()); recurringForm.UndoDelete();
            Check(recurringStore.Plans.Single().CompletionDates.SequenceEqual(countRestored.CompletionDates), "deleting and undoing series preserves daily records");
        }
        using (var futureCard = new PlanCard(countGoal, today.AddDays(1)))
            Check(!futureCard.Controls.OfType<CheckBox>().Single().Enabled, "future daily completion is disabled");
        using (var doneCard = new PlanCard(countDone, today, true))
            Check(doneCard.Controls.OfType<CheckBox>().Single() is { Checked: true, Enabled: false } && doneCard.Controls.OfType<Label>().First().Font.Strikeout,
                "finished overview uses strikethrough and preserves progress");
        using (var recurrenceEditor = new PlanEditor(recorded, false))
        {
            recurrenceEditor.SavePlan();
            Check(recurrenceEditor.Result is { RepeatUntil: { } last } editedDaily && last == today && editedDaily.CompletionDates.SequenceEqual(recorded.CompletionDates),
                "editor retains duration goal and all completed days");
        }
        using (var goalEditor = new PlanEditor(countDone, false))
        {
            goalEditor.SavePlan();
            Check(goalEditor.Result?.RepeatCount == 2 && goalEditor.Result.CompletionDates.SequenceEqual(countDone.CompletionDates), "editor retains count goal and progress");
        }
        using (var createEditor = new PlanEditor(first with { Day = null }, true))
        {
            Descendants(createEditor).OfType<CheckBox>().Single(box => box.Text == "设为长期计划（每天重复）").Checked = true;
            createEditor.SavePlan();
            Check(createEditor.Result is { IsRecurring: true, Day: not null }, "enabling long-term plan supplies required start date");
        }

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
        var alarmNow = DateTimeOffset.Now;
        var reminder = new PlanItem { Title = "十分钟后休息", Alarm = new PlanAlarm { AfterMinutes = 10, At = alarmNow.AddMinutes(10) } };
        reminder.Validate();
        Check(reminder.Alarm!.Next(reminder, alarmNow) == alarmNow.AddMinutes(10), "countdown is independent of dates and expected duration");
        Check(reminder.Alarm.Next(reminder with { IsCompleted = true }, alarmNow) is null, "completed plan cancels countdown");
        Check((reminder.Alarm with { FiredAt = reminder.Alarm.At }).Next(reminder, alarmNow) is null, "acknowledged alarm does not ring twice");
        Check((reminder.Alarm with { At = alarmNow.AddMinutes(-6) }).Next(reminder, alarmNow) is null, "old missed countdown does not ring after restart or import");
        Check((reminder.Alarm with { At = alarmNow.AddMinutes(-2) }).Next(reminder, alarmNow) is not null, "recent missed alarm can catch up");
        Reject(() => (reminder with { Alarm = new PlanAlarm { AfterMinutes = 0, At = alarmNow } }).Validate(), "zero alarm delay rejected");
        Reject(() => (reminder with { Alarm = new PlanAlarm { Mode = "start" } }).Validate(), "schedule alarm requires full time range");
        Reject(() => (reminder with { Alarm = new PlanAlarm { Mode = "unknown" } }).Validate(), "unknown alarm mode rejected");
        Reject(() => (reminder with { Alarm = reminder.Alarm with { Ringtone = "file:///private" } }).Validate(), "unsupported ringtone URI rejected");
        var dailyAlarm = new PlanItem { Title = "每天学习提醒", Day = today, EndDay = today, Start = new TimeOnly(23, 0), End = new TimeOnly(23, 30), RepeatCount = 3, Alarm = new PlanAlarm { Mode = "start" } };
        dailyAlarm.Validate();
        var morning = new DateTimeOffset(today.ToDateTime(new TimeOnly(8, 0)), TimeZoneInfo.Local.GetUtcOffset(today.ToDateTime(new TimeOnly(8, 0))));
        Check(dailyAlarm.Alarm!.Next(dailyAlarm, morning)?.LocalDateTime == today.ToDateTime(new TimeOnly(23, 0)), "daily alarm uses system local start time");
        Check(dailyAlarm.Alarm.Next(dailyAlarm.ToggleOn(today), morning)?.LocalDateTime == today.AddDays(1).ToDateTime(new TimeOnly(23, 0)), "daily completion cancels today but retains next day alarm");
        var crossAlarm = crossDay with { Alarm = new PlanAlarm { Mode = "end" } };
        Check(crossAlarm.Alarm!.Next(crossAlarm, morning)?.LocalDateTime == today.AddDays(1).ToDateTime(new TimeOnly(1, 0)), "end alarm respects cross-day end date");
        store.Save(reminder); reopened.Load();
        Check(reopened.Plans.Single(p => p.Id == reminder.Id).Alarm == reminder.Alarm, "alarm deadline and settings survive restart");
        store.Save(reminder with { Alarm = reminder.Alarm with { FiredAt = reminder.Alarm.At } }); reopened.Load();
        Check(reopened.Plans.Single(p => p.Id == reminder.Id).Alarm!.Next(reminder, alarmNow) is null, "fired state survives process restart");
        store.Save(reminder.ToggleOn(today)); store.Save(reminder);
        Check(store.Plans.Single(p => p.Id == reminder.Id).Alarm!.FiredAt == reminder.Alarm.At, "stale cards and completion undo cannot overwrite fired state");
        store.Export(backup); imported.Import(backup);
        Check(imported.Plans.Any(p => p.Id == reminder.Id && p.Alarm is not null), "schema 3 backup retains alarm settings");
        using var alarmEditor = new PlanEditor(reminder, false);
        alarmEditor.SavePlan();
        Check(alarmEditor.Result?.Alarm?.At == reminder.Alarm.At, "editing a countdown preserves its original deadline");
        Console.WriteLine($"All {checks} checks passed. Test data: {root}");
    }
}
