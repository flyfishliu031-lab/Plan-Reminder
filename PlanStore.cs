using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlanReminder;

public sealed record PlanDocument
{
    public int SchemaVersion { get; init; } = 3;
    public List<PlanItem> Plans { get; init; } = [];
}

public sealed class PlanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public string FilePath { get; }
    public IReadOnlyList<PlanItem> Plans => plans.AsReadOnly();
    private List<PlanItem> plans = [];

    public PlanStore(string directory) => FilePath = Path.Combine(Path.GetFullPath(directory), "plans.json");

    public void Load() => plans = File.Exists(FilePath) ? Read(FilePath) : [];

    public void Save(PlanItem item)
    {
        item.Validate();
        var updated = plans.ToList();
        var index = updated.FindIndex(p => p.Id == item.Id);
        if (index < 0) updated.Add(item); else updated[index] = item;
        Commit(updated);
    }

    public void Delete(Guid id) => Commit(plans.Where(p => p.Id != id).ToList());

    public void Export(string destination)
    {
        var fullPath = Path.GetFullPath(destination);
        if (string.Equals(fullPath, FilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, FilePath + ".bak", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("备份请另存到其他位置，避免覆盖正在使用的数据文件。");
        WriteAtomic(fullPath, plans, false);
    }

    public int Import(string source)
    {
        var imported = Read(source); // Validate the entire backup before touching current data.
        var updated = plans.ToDictionary(p => p.Id);
        var count = 0;
        foreach (var item in imported)
        {
            if (!updated.TryGetValue(item.Id, out var current) || item.UpdatedAt > current.UpdatedAt)
            {
                updated[item.Id] = item;
                count++;
            }
        }
        if (count > 0) Commit(updated.Values.ToList());
        return count;
    }

    public void RestoreBackup()
    {
        var restored = Read(FilePath + ".bak");
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        WriteAtomic(FilePath, restored, false);
        plans = restored;
    }

    private void Commit(List<PlanItem> updated)
    {
        WriteAtomic(FilePath, updated, true);
        plans = updated; // Only change the visible state after the disk write succeeds.
    }

    private static List<PlanItem> Read(string path)
    {
        if (new FileInfo(path).Length > 20 * 1024 * 1024)
            throw new InvalidDataException("数据文件超过 20 MB，请检查是否选择了正确的备份。");
        var document = JsonSerializer.Deserialize<PlanDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("数据文件为空。");
        if (document.SchemaVersion is not (1 or 2 or 3) || document.Plans is null)
            throw new InvalidDataException("不支持此备份版本。");
        if (document.Plans.Any(p => p is null) || document.Plans.Select(p => p.Id).Distinct().Count() != document.Plans.Count)
            throw new InvalidDataException("备份存在重复编号或无效计划。");
        foreach (var item in document.Plans) item.Validate();
        return document.Plans;
    }

    private static void WriteAtomic(string path, List<PlanItem> data, bool backup)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new PlanDocument { Plans = data }, JsonOptions);
                if (stream.Length > 20 * 1024 * 1024)
                    throw new InvalidDataException("计划数据超过 20 MB，请先导出备份并清理部分旧计划。");
                stream.Flush(true);
            }
            if (backup && File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
