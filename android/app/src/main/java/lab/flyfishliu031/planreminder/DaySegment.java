package lab.flyfishliu031.planreminder;

import java.time.LocalDate;
import java.util.*;

final class DaySegment {
  final Plan plan;
  final LocalDate occurrence, viewDate;
  final double start, end;
  int column, columns = 1;

  DaySegment(Plan plan, LocalDate occurrence, LocalDate viewDate, double start, double end) {
    this.plan = plan;
    this.occurrence = occurrence;
    this.viewDate = viewDate;
    this.start = start;
    this.end = end;
  }

  static String clock(double minute) {
    return String.format(Locale.ROOT, "%02d:%02d", (int) minute / 60, (int) minute % 60);
  }

  String caption() {
    return clock(start)
        + "—"
        + clock(end)
        + " · "
        + plan.title
        + (occurrence.equals(viewDate) ? "" : "（" + occurrence + " 开始）");
  }

  static List<DaySegment> layout(List<Plan> plans, LocalDate date) {
    List<DaySegment> all = new ArrayList<>();
    for (Plan p : plans) all.addAll(p.segments(date));
    all.sort(
        Comparator.comparingDouble((DaySegment s) -> s.start)
            .thenComparingDouble(s -> s.end)
            .thenComparing(s -> s.plan.id));
    List<DaySegment> group = new ArrayList<>();
    double groupEnd = -1;
    for (DaySegment s : all) {
      if (s.start >= groupEnd) {
        finish(group);
        groupEnd = s.end;
      }
      Set<Integer> occupied = new HashSet<>();
      for (DaySegment previous : group) if (previous.end > s.start) occupied.add(previous.column);
      while (occupied.contains(s.column)) s.column++;
      group.add(s);
      groupEnd = Math.max(groupEnd, s.end);
    }
    finish(group);
    return all;
  }

  private static void finish(List<DaySegment> group) {
    int columns = 1;
    for (DaySegment s : group) columns = Math.max(columns, s.column + 1);
    for (DaySegment s : group) s.columns = columns;
    group.clear();
  }
}
