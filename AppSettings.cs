using System.Text.Json;

namespace PlanReminder;

internal sealed class AppSettings(string directory)
{
    private sealed record SettingsData(bool ExitOnClose = false);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    internal string FilePath { get; } = Path.Combine(Path.GetFullPath(directory), "settings.json");
    internal bool ExitOnClose { get; private set; }

    internal void Load()
    {
        if (!File.Exists(FilePath)) return;
        if (new FileInfo(FilePath).Length > 64 * 1024) throw new InvalidDataException("设置文件过大。");
        var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath), JsonOptions)
            ?? throw new InvalidDataException("设置文件为空。");
        ExitOnClose = data.ExitOnClose;
    }

    internal void Save(bool exitOnClose)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new SettingsData(exitOnClose), JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
            ExitOnClose = exitOnClose;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
