using System.Security.Cryptography;
using System.Text;

namespace PlanReminder;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(Theme.Body);
        if (args.Contains("--self-test"))
        {
            try { SelfTest.Run(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        var dataIndex = Array.IndexOf(args, "--data-dir");
        var directory = dataIndex >= 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanReminder");
        var store = new PlanStore(directory);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(store.FilePath.ToUpperInvariant())))[..24];
        using var mutex = new Mutex(true, "Local\\PlanReminder-" + identity, out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("计划表已在运行，请切换到已有窗口。", "计划表", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            Application.Run(new MainForm(store));
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"软件无法继续运行。\n\n{ex.Message}\n\n数据位置：{store.FilePath}", "计划表", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static void AddDemoPlans(PlanStore store)
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
