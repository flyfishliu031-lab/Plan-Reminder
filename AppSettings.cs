using System.Text.Json;

namespace PlanReminder;

internal sealed class AppSettings(string directory)
{
    private sealed record SettingsData(bool ExitOnClose = false, string View = "plans");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    internal string FilePath { get; } = Path.Combine(Path.GetFullPath(directory), "settings.json");
    internal bool ExitOnClose { get; private set; }
    internal string View { get; private set; } = "plans";

    internal void Load()
    {
        if (!File.Exists(FilePath)) return;
        if (new FileInfo(FilePath).Length > 64 * 1024) throw new InvalidDataException("设置文件过大。");
        var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath), JsonOptions)
            ?? throw new InvalidDataException("设置文件为空。");
        ExitOnClose = data.ExitOnClose;
        View = data.View is "timetable" or "unscheduled" or "series" ? data.View : "plans";
    }

    internal void Save(bool exitOnClose, string? view = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new SettingsData(exitOnClose, view ?? View), JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
            ExitOnClose = exitOnClose;
            View = view ?? View;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
