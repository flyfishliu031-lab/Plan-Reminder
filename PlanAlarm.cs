namespace PlanReminder;

public sealed record PlanAlarm
{
    public string Mode { get; init; } = "after";
    public int? AfterMinutes { get; init; }
    public DateTimeOffset? At { get; init; }
    public DateTimeOffset? FiredAt { get; init; }
    public bool Sound { get; init; } = true;
    public bool Vibrate { get; init; } = true;
    public string? Ringtone { get; init; }

    public void Validate(PlanItem plan)
    {
        if (Mode is not ("after" or "start" or "end")) throw new ArgumentException("请选择有效的闹钟方式。");
        if (Mode == "after" && (AfterMinutes is null or < 1 or > 525600 || At is null || At.Value.Year is < 1753 or > 9998))
            throw new ArgumentException("倒计时须为 1 到 525600 分钟。");
        if (Mode != "after" && (plan.StartsAt is null || AfterMinutes is not null || At is not null))
            throw new ArgumentException("开始／结束闹钟需要完整的计划时间段。");
        if (Ringtone is { } tone && (tone.Length > 2048 || !Uri.TryCreate(tone, UriKind.Absolute, out var uri) || uri.Scheme != "content"))
            throw new ArgumentException("闹钟铃声地址无效。");
    }

    // Only catch up recent alarms. Old backups and missed days must not produce an alarm storm.
    public DateTimeOffset? Next(PlanItem plan, DateTimeOffset now)
    {
        if (plan.IsCompleted || plan.SeriesFinished(DateOnly.FromDateTime(now.LocalDateTime))) return null;
        var earliest = now.AddMinutes(-5);
        bool Eligible(DateTimeOffset time) => time >= earliest && (FiredAt is null || time > FiredAt);
        if (Mode == "after") return At is { } at && Eligible(at) &&
            (!plan.IsRecurring || !plan.IsCompleteOn(DateOnly.FromDateTime(at.LocalDateTime))) ? at : null;
        if (!plan.IsRecurring) {
            var time = Local(Mode == "start" ? plan.StartsAt!.Value : plan.EndsAt!.Value);
            return Eligible(time) ? time : null;
        }
        var first = DateOnly.FromDateTime(earliest.LocalDateTime);
        if (first < plan.Day) first = plan.Day!.Value;
        for (var i = 0; i <= plan.CompletionDates.Count + 2 && first.Year <= 9998; i++, first = first.AddDays(1)) {
            if (!plan.AppearsOn(first)) return null;
            if (plan.IsCompleteOn(first)) continue;
            var time = Local(first.ToDateTime(Mode == "start" ? plan.Start!.Value : plan.End!.Value));
            if (Eligible(time)) return time;
        }
        return null;
    }

    private static DateTimeOffset Local(DateTime time)
    {
        var zone = TimeZoneInfo.Local;
        while (zone.IsInvalidTime(time)) time = time.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(time) ? zone.GetAmbiguousTimeOffsets(time).Max() : zone.GetUtcOffset(time);
        return new DateTimeOffset(time, offset);
    }

    public string Description => Mode == "after" ? $"闹钟 · {At?.ToLocalTime():MM/dd HH:mm}（一次）" :
        $"闹钟 · {(Mode == "start" ? "开始时" : "结束时")}";
}
