namespace PlanReminder;

// Exercise real controls in an isolated store; screenshots contain sample plans only.
internal static class UiCheck
{
    internal static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var store = new PlanStore(Path.Combine(Path.GetTempPath(), "PlanReminder-ui-" + Guid.NewGuid().ToString("N")));
        Program.AddDemoPlans(store);
        using var main = new MainForm(store);
        main.Show(); Application.DoEvents();
        Capture(main, Path.Combine(directory, "main.png"));
        CheckText(main);
        var day = DateOnly.FromDateTime(DateTime.Now);
        var sample = new PlanItem { Title = "为明天准备一份清晰的计划", Notes = "列出重要事项，给专注和休息都留一点时间。", Day = day,
            Color = Palette.Colors[2], Start = new TimeOnly(9, 0), End = new TimeOnly(10, 0), EndDay = day, DurationMinutes = 60 };
        using var editor = new PlanEditor(sample, true);
        editor.Show(main); Application.DoEvents();
        foreach (var input in Descendants(editor).OfType<TextBox>().Where(box => box.Multiline))
            if (input.ClientSize.Height < input.Font.Height * 2)
                throw new InvalidOperationException($"Text input cannot display two complete lines: {input.AccessibleName}");
        Capture(editor, Path.Combine(directory, "editor.png"));
        CheckText(editor);
        var scroller = Descendants(editor).OfType<Panel>().Single(panel => panel.AutoScroll);
        scroller.AutoScrollPosition = new Point(0, scroller.VerticalScroll.Maximum);
        Application.DoEvents(); CheckText(editor);
        Capture(editor, Path.Combine(directory, "editor-bottom.png"));
        editor.ClientSize = new Size(Theme.Px(editor, 580), Theme.Px(editor, 460));
        Application.DoEvents(); CheckText(editor);
        var save = Descendants(editor).OfType<Button>().Single(button => button.Text == "保存计划");
        if (!editor.ClientRectangle.Contains(editor.RectangleToClient(save.RectangleToScreen(save.ClientRectangle))))
            throw new InvalidOperationException("Save action is outside the resized editor.");
        Capture(editor, Path.Combine(directory, "editor-small.png"));
        editor.ClientSize = new Size(Theme.Px(editor, 720), Theme.Px(editor, 780));
        foreach (var checkbox in Descendants(editor).OfType<CheckBox>().Where(box => box.Text != "安排日期")) checkbox.Checked = false;
        scroller.AutoScrollPosition = Point.Empty; Application.DoEvents(); CheckText(editor);
        Capture(editor, Path.Combine(directory, "editor-simple.png"));
        var title = Descendants(editor).OfType<TextBox>().Single(box => box.AccessibleName == "计划标题（必填）");
        var compactHeight = title.Parent!.Height;
        title.Text = string.Join("\r\n", Enumerable.Range(1, 14).Select(index => $"第 {index} 行：保留前面填写的内容")) + "\r\n";
        title.SelectionStart = title.TextLength; title.Focus(); Application.DoEvents();
        if (title.Parent.Height <= compactHeight || title.ClientSize.Height < (title.GetLineFromCharIndex(title.TextLength) + 1) * title.Font.Height)
            throw new InvalidOperationException("Multiline title did not grow to fit its lines.");
        var caret = scroller.PointToClient(title.PointToScreen(title.GetPositionFromCharIndex(title.SelectionStart)));
        if (caret.Y < 0 || caret.Y + title.Font.Height > scroller.ClientSize.Height)
            throw new InvalidOperationException("Growing title hides the active line outside the scroll viewport.");
        CheckText(editor); Capture(editor, Path.Combine(directory, "editor-multiline.png"));
        title.Text = new string('字', 490); Application.DoEvents();
        var wideHeight = title.Parent.Height;
        editor.ClientSize = new Size(Theme.Px(editor, 580), Theme.Px(editor, 460)); Application.DoEvents();
        if (title.Parent.Height < wideHeight || title.ClientSize.Height < (title.GetLineFromCharIndex(title.TextLength) + 1) * title.Font.Height)
            throw new InvalidOperationException($"Wrapped title does not fit after narrowing the editor: oldHeight={wideHeight}, height={title.Parent.Height}, input={title.ClientSize}, lines={title.GetLineFromCharIndex(title.TextLength) + 1}, fontHeight={title.Font.Height}.");
        title.Text = sample.Title; Application.DoEvents();
        if (title.Parent.Height > compactHeight + Theme.Px(title, 2)) throw new InvalidOperationException("Title field does not shrink after removing lines.");
        editor.ClientSize = new Size(Theme.Px(editor, 720), Theme.Px(editor, 780));
        foreach (var checkbox in Descendants(editor).OfType<CheckBox>()) checkbox.Checked = true;
        Application.DoEvents(); CheckText(editor);
        title.Text = " "; editor.SavePlan(); Application.DoEvents(); CheckText(editor);
        if (editor.Result is not null) throw new InvalidOperationException("Blank editor title was accepted.");
        editor.Close();
        main.ClientSize = new Size(Theme.Px(main, 840), Theme.Px(main, 500));
        Application.DoEvents(); CheckText(main);
        if (main.PlanList.HorizontalScroll.Visible) throw new InvalidOperationException("Plan list has an unnecessary horizontal scrollbar.");
        if (main.Calendar.Width > main.Calendar.Parent!.ClientSize.Width) throw new InvalidOperationException("Calendar exceeds its sidebar.");
        var sidebarScroll = main.Calendar.Parent!.Parent!;
        if (main.Calendar.Parent.Width > sidebarScroll.ClientSize.Width - sidebarScroll.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)
            throw new InvalidOperationException("Sidebar exceeds the scroll viewport.");
        Capture(main, Path.Combine(directory, "main-small.png"));
        store.Save(sample with { Title = new string('长', 500), Notes = new string('备', 5000), EndDay = day.AddDays(1), End = new TimeOnly(10, 30), DurationMinutes = 525600 });
        main.RefreshViews(); Application.DoEvents(); CheckText(main);
        if (main.PlanList.HorizontalScroll.Visible) throw new InvalidOperationException("Long content creates horizontal scrolling.");
        var recurring = new PlanItem { Title = "每天学习，积累一点进步", Day = day.AddDays(-4), RepeatCount = 10,
            CompletedDates = [day.AddDays(-4), day.AddDays(-2)], Color = Palette.Colors[2], DurationMinutes = 30 };
        store.Save(recurring);
        main.ClientSize = new Size(Theme.Px(main, 1180), Theme.Px(main, 780)); main.ShowLongTerm(); Application.DoEvents(); CheckText(main);
        Capture(main, Path.Combine(directory, "long-term.png"));
        using var repeatEditor = new PlanEditor(recurring, false); repeatEditor.Show(main); Application.DoEvents(); CheckText(repeatEditor);
        var repeatScroll = Descendants(repeatEditor).OfType<Panel>().Single(panel => panel.AutoScroll);
        repeatScroll.AutoScrollPosition = new Point(0, repeatScroll.VerticalScroll.Maximum); Application.DoEvents(); CheckText(repeatEditor);
        Capture(repeatEditor, Path.Combine(directory, "editor-long-term.png")); repeatEditor.Close();
        var settings = new AppSettings(Path.GetDirectoryName(store.FilePath)!);
        using var settingsForm = new SettingsForm(settings); settingsForm.Show(main); Application.DoEvents(); CheckText(settingsForm);
        Capture(settingsForm, Path.Combine(directory, "settings.png"));
        settingsForm.ClientSize = new Size(Theme.Px(settingsForm, 420), Theme.Px(settingsForm, 300));
        Application.DoEvents(); CheckText(settingsForm);
        settingsForm.Close();
        using var alarmEditor = new PlanEditor(sample with { Alarm = new PlanAlarm { AfterMinutes = 10, At = DateTimeOffset.Now.AddMinutes(10) } }, true);
        alarmEditor.Show(main); Application.DoEvents();
        var alarmScroll = Descendants(alarmEditor).OfType<Panel>().Single(panel => panel.AutoScroll);
        var alarmBox = Descendants(alarmEditor).OfType<CheckBox>().Single(box => box.Text == "添加闹钟");
        alarmScroll.AutoScrollPosition = new Point(0, alarmScroll.VerticalScroll.Maximum); Application.DoEvents(); CheckText(alarmEditor);
        Capture(alarmEditor, Path.Combine(directory, "editor-alarm.png")); alarmEditor.Close();
        using var alarmWindow = new AlarmWindow(sample with { Alarm = new PlanAlarm { Mode = "start", Sound = false } }, DateTimeOffset.Now);
        alarmWindow.Show(); Application.DoEvents(); CheckText(alarmWindow);
        Capture(alarmWindow, Path.Combine(directory, "alarm.png")); alarmWindow.Close();
        var blockedSettings = new AppSettings(Path.Combine(Path.GetDirectoryName(store.FilePath)!, "blocked"));
        Directory.CreateDirectory(blockedSettings.FilePath);
        using var errorForm = new SettingsForm(blockedSettings); errorForm.Show(main); errorForm.SaveSettings();
        Application.DoEvents(); CheckText(errorForm); errorForm.Close();
        var due = DateTimeOffset.Now.AddSeconds(2);
        var timed = new PlanItem { Title = "后台闹钟触发测试", Alarm = new PlanAlarm { AfterMinutes = 1, At = due, Sound = false } };
        store.Save(timed); main.Hide();
        var rang = false;
        var deadline = DateTime.UtcNow.AddSeconds(8);
        using var observer = new System.Windows.Forms.Timer { Interval = 100 };
        observer.Tick += (_, _) => {
            var window = Application.OpenForms.OfType<AlarmWindow>().FirstOrDefault(form => form.PlanId == timed.Id);
            if (window is not null) {
                rang = store.Plans.Single(p => p.Id == timed.Id).Alarm?.FiredAt == due;
                Capture(window, Path.Combine(directory, "alarm-background.png"));
                Descendants(window).OfType<Button>().Single(button => button.Text == "关闭闹钟").PerformClick();
                Application.ExitThread();
            } else if (DateTime.UtcNow >= deadline) Application.ExitThread();
        };
        observer.Start(); Application.Run(); observer.Stop();
        if (!rang) throw new InvalidOperationException("Background alarm did not ring and persist its fired state.");
        Console.WriteLine($"UI and actual background alarm checks passed. Device DPI: {main.DeviceDpi}; previews: {Path.GetFullPath(directory)}");
    }

    internal static void CheckText(Control root)
    {
        foreach (var control in Descendants(root).Where(c => c.Visible && !string.IsNullOrEmpty(c.Text)))
        {
            if (control is not Label && control is not CheckBox && control is not RoundedButton) continue;
            if (control is Label { AutoEllipsis: true }) continue; // Card excerpts and delete notices intentionally abbreviate.
            var available = control.ClientSize - control.Padding.Size;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            var required = TextRenderer.MeasureText(control.Text, control.Font, new Size(Math.Max(1, available.Width), int.MaxValue), flags);
            if (required.Height > available.Height + 2 || required.Width > available.Width + 2)
                throw new InvalidOperationException($"Clipped text '{control.Text}': available={available}, required={required}, DPI={control.DeviceDpi}");
        }
    }

    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
