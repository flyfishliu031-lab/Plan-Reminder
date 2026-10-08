using System.Security.Cryptography;
using System.Text;

namespace PlanReminder;

internal static class Program
{
    internal static string AppVersion => typeof(Program).Assembly.GetName().Version!.ToString(3);
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--ui-check") && args.Contains("--ui-check-96"))
        {
            // Separate-process baseline test; never changes Windows display settings.
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        else ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(Theme.Body);
        if (args.Contains("--self-test"))
        {
            try { SelfTest.Run(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        var uiIndex = Array.IndexOf(args, "--ui-check");
        if (uiIndex >= 0 && uiIndex + 1 < args.Length)
        {
            try { UiCheck.Run(args[uiIndex + 1]); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        var dataIndex = Array.IndexOf(args, "--data-dir");
        var directory = dataIndex >= 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanReminder");
        var store = new PlanStore(directory);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(store.FilePath.ToUpperInvariant())))[..24];
        using var openRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PlanReminder-Open-" + identity, out var createdOpenRequest);
        using var mutex = new Mutex(true, "Local\\PlanReminder-" + identity, out var firstInstance);
        if (!firstInstance)
        {
            openRequest.Set();
            if (createdOpenRequest)
                MessageBox.Show($"计划表 v{AppVersion} 无法接管正在运行的旧版。请先关闭旧版窗口，再启动此版本。新版主窗口标题会显示版本号。", "请先退出旧版计划表", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        try
        {
            try { store.Load(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
                if (File.Exists(store.FilePath + ".bak") && MessageBox.Show(
                    $"无法读取计划数据。\n{ex.Message}\n\n是否尝试恢复上一次自动备份？原文件会另存保留。",
                    "恢复计划", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    store.RestoreBackup();
                else
                {
                    MessageBox.Show($"为保护原有数据，软件已停止加载。\n请检查以下文件或从备份恢复：\n{store.FilePath}",
                        "无法读取计划", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            if (args.Contains("--demo") && !File.Exists(store.FilePath)) AddDemoPlans(store);
            var settings = new AppSettings(directory);
            string? settingsError = null;
            try { settings.Load(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { settingsError = ex.Message; }
            using var main = new MainForm(store, settings);
            if (settingsError is not null)
                main.Shown += (_, _) => MessageBox.Show(main, $"无法读取设置，暂按默认方式运行。可在设置中重新保存。\n\n{settingsError}", "设置读取失败", MessageBoxButtons.OK, MessageBoxIcon.Information);
            using var activationTimer = new System.Windows.Forms.Timer { Interval = 250 };
            activationTimer.Tick += (_, _) => { if (openRequest.WaitOne(0)) main.RestoreWindow(); };
            activationTimer.Start();
            Application.Run(main);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"软件无法继续运行。\n\n{ex.Message}\n\n数据位置：{store.FilePath}", "计划表", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }

    internal static void AddDemoPlans(PlanStore store)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        store.Save(new PlanItem { Title = "梳理今天的工作安排", Day = today, Start = new TimeOnly(9, 0), End = new TimeOnly(9, 30), EndDay = today,
            DurationMinutes = 30, Color = Palette.Colors[0], Notes = "列出重点事项，给每件事留出合适的时间。" });
        store.Save(new PlanItem { Title = "整理本周的读书笔记", Day = today, DurationMinutes = 45,
            Color = Palette.Colors[1], IsCompleted = true, Notes = "保留完成记录，随时可以恢复。" });
        store.Save(new PlanItem { Title = "散步，休息一下", Day = today, Start = new TimeOnly(17, 30), End = new TimeOnly(18, 0), EndDay = today,
            Color = Palette.Colors[2] });
        store.Save(new PlanItem { Title = "准备周末出行清单", Day = today, Color = Palette.Colors[3] });
        store.Save(new PlanItem { Title = "挑选下一本想读的书", DurationMinutes = 20, Color = Palette.Colors[4] });
        store.Save(new PlanItem { Title = "晚间整理", Day = today.AddDays(1), Color = Palette.Colors[5] });
    }
}
