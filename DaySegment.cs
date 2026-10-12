namespace PlanReminder;

internal sealed record DaySegment(PlanItem Plan, DateOnly Occurrence, double Start, double End)
{
    internal int Column { get; set; }
    internal int Columns { get; set; } = 1;
    internal string Caption => $"{Clock(Start)}—{Clock(End)} · {Plan.Title}" +
        (Occurrence != ViewDate ? $"（{Occurrence:MM/dd} 开始）" : "");
    internal DateOnly ViewDate { get; set; }
    internal static string Clock(double minutes) => minutes >= 1440 ? "24:00" : $"{(int)minutes / 60:00}:{(int)minutes % 60:00}";

    internal static DaySegment[] Layout(IEnumerable<PlanItem> plans, DateOnly day)
    {
        var segments = plans.SelectMany(p => p.Segments(day)).OrderBy(s => s.Start).ThenBy(s => s.End).ThenBy(s => s.Plan.Id).ToArray();
        var group = new List<DaySegment>();
        double groupEnd = -1;
        void Finish() {
            var columns = group.Count == 0 ? 1 : group.Max(s => s.Column) + 1;
            foreach (var s in group) s.Columns = columns;
            group.Clear();
        }
        foreach (var segment in segments) {
            segment.ViewDate = day;
            if (segment.Start >= groupEnd) { Finish(); groupEnd = segment.End; }
            var occupied = group.Where(s => s.End > segment.Start).Select(s => s.Column).ToHashSet();
            while (occupied.Contains(segment.Column)) segment.Column++;
            group.Add(segment); groupEnd = Math.Max(groupEnd, segment.End);
        }
        Finish(); return segments;
    }
}
