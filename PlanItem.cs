using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace PlanReminder;

public sealed record PlanItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Notes { get; init; } = "";
    public DateOnly? Day { get; init; }
    public TimeOnly? Start { get; init; }
    public DateOnly? EndDay { get; init; }
    public TimeOnly? End { get; init; }
    public int? DurationMinutes { get; init; }
    public string Color { get; init; } = Palette.Colors[0];
    public bool IsCompleted { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

    [JsonIgnore] public DateTime? StartsAt => Day is { } day && Start is { } time ? day.ToDateTime(time) : null;
    [JsonIgnore] public DateTime? EndsAt => EndDay is { } day && End is { } time ? day.ToDateTime(time) : null;

    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("计划编号无效。");
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 500)
            throw new ArgumentException("请填写计划标题（最多 500 字）。");
        if (Notes is null || Notes.Length > 5000) throw new ArgumentException("备注最多 5000 字。");
        if (Day is { } selected && (selected.Year < 1753 || selected.Year > 9998) ||
            EndDay is { } ending && (ending.Year < 1753 || ending.Year > 9998))
            throw new ArgumentException("日期须在 1753 年到 9998 年之间。");
        if (Color is null || !Regex.IsMatch(Color, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("计划颜色无效。");
        var hasTime = Start is not null || End is not null || EndDay is not null;
        if (hasTime && (Day is null || Start is null || EndDay is null || End is null))
            throw new ArgumentException("设置时间段时，请填写日期、开始时间、结束日期和结束时间。");
        if (hasTime && EndsAt <= StartsAt)
            throw new ArgumentException("结束时间须晚于开始时间；跨天计划请调整结束日期。");
        if (DurationMinutes is <= 0 or > 525600)
            throw new ArgumentException("预期时长须在 1 分钟到 365 天之间。");
    }

    public bool AppearsOn(DateOnly day) => Day == day ||
        (StartsAt is { } start && EndsAt is { } end &&
         start <= day.ToDateTime(TimeOnly.MaxValue) && end > day.ToDateTime(TimeOnly.MinValue));

    public bool Overlaps(PlanItem other) => !IsCompleted && !other.IsCompleted && Id != other.Id &&
        StartsAt is { } start && EndsAt is { } end &&
        other.StartsAt is { } otherStart && other.EndsAt is { } otherEnd && start < otherEnd && end > otherStart;

    public string TimeDescription()
    {
        var parts = new List<string>();
        if (Start is { } start && End is { } end)
            parts.Add(Day == EndDay ? $"{start:HH:mm} — {end:HH:mm}" :
                $"{Day:MM月dd日} {start:HH:mm} — {EndDay:MM月dd日} {end:HH:mm}");
        else parts.Add(Day is null ? "待安排" : "未设置时间段");
        if (DurationMinutes is { } minutes) parts.Add($"预计 {FormatDuration(minutes)}");
        return string.Join("   ·   ", parts);
    }

    public static string FormatDuration(int minutes) => minutes < 60 ? $"{minutes} 分钟" :
        minutes % 60 == 0 ? $"{minutes / 60} 小时" : $"{minutes / 60} 小时 {minutes % 60} 分钟";
}

public static class Palette
{
    public static readonly string[] Colors =
        ["#6654D9", "#237DAB", "#25806F", "#A86020", "#BE526B", "#536AA3", "#887031", "#8C59AA"];

    public static string Next(IEnumerable<PlanItem> plans)
    {
        var used = plans.Select(p => p.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var color in Colors) if (!used.Contains(color)) return color;
        for (var index = used.Count; ; index++)
        {
            var hue = index * 137.508 % 360 / 60;
            const double chroma = .5, offset = .22;
            var second = chroma * (1 - Math.Abs(hue % 2 - 1));
            var (r, g, b) = (int)hue switch
            {
                0 => (chroma, second, 0d), 1 => (second, chroma, 0d),
                2 => (0d, chroma, second), 3 => (0d, second, chroma),
                4 => (second, 0d, chroma), _ => (chroma, 0d, second)
            };
            r += offset; g += offset; b += offset;
            static double Linear(double channel) => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
            while (.2126 * Linear(r) + .7152 * Linear(g) + .0722 * Linear(b) > .18)
            { r *= .94; g *= .94; b *= .94; } // Keep automatically chosen text colors readable on white.
            var candidate = $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
