package lab.flyfishliu031.planreminder;

import java.time.*;
import java.time.temporal.ChronoUnit;
import java.util.*;
import org.json.*;

final class Plan {
  static final String[] COLORS = {
    "#6654D9", "#237DAB", "#25806F", "#A86020", "#BE526B", "#536AA3", "#887031", "#8C59AA"
  };
  String id = UUID.randomUUID().toString(), title = "", notes = "", color = COLORS[0];
  LocalDate day, endDay, repeatUntil;
  LocalTime start, end;
  Integer durationMinutes, repeatCount;
  PlanAlarm alarm;
  boolean completed;
  final TreeSet<LocalDate> dates = new TreeSet<>();
  Instant createdAt = Instant.now(), updatedAt = createdAt;

  boolean recurring() {
    return repeatUntil != null || repeatCount != null;
  }

  boolean goalReached() {
    return repeatCount != null && dates.size() >= repeatCount;
  }

  boolean finished(LocalDate today) {
    return recurring()
        && (repeatUntil != null
                && today.isAfter(
                    repeatUntil.plusDays(
                        endOffset() > 0 && LocalTime.MIDNIGHT.equals(end)
                            ? endOffset() - 1
                            : endOffset()))
            || repeatCount != null && dates.size() >= repeatCount);
  }

  LocalDate lastDay() {
    if (recurring())
      return repeatUntil != null
          ? repeatUntil
          : dates.size() >= repeatCount ? dates.last() : LocalDate.of(9998, 12, 31);
    return endDay == null ? day : end.equals(LocalTime.MIDNIGHT) ? endDay.minusDays(1) : endDay;
  }

  boolean appears(LocalDate date) {
    return start == null ? occurs(date) : !segments(date).isEmpty();
  }

  int endOffset() {
    return day == null || endDay == null ? 0 : (int) ChronoUnit.DAYS.between(day, endDay);
  }

  boolean occurs(LocalDate date) {
    return day != null && !date.isBefore(day) && !date.isAfter(lastDay());
  }

  List<DaySegment> segments(LocalDate date) {
    List<DaySegment> result = new ArrayList<>();
    if (start == null || end == null) return result;
    LocalDate[] candidates =
        recurring() ? new LocalDate[] {date.minusDays(1), date} : new LocalDate[] {day};
    for (LocalDate occurrence : candidates) {
      if (recurring() && !occurs(occurrence)) continue;
      LocalDateTime from = (recurring() ? occurrence : day).atTime(start);
      LocalDateTime to = (recurring() ? occurrence.plusDays(endOffset()) : endDay).atTime(end);
      double first = Math.max(0, Duration.between(date.atStartOfDay(), from).toMillis() / 60000d);
      double last = Math.min(1440, Duration.between(date.atStartOfDay(), to).toMillis() / 60000d);
      if (first < last) result.add(new DaySegment(this, occurrence, date, first, last));
    }
    return result;
  }

  boolean checked(LocalDate date) {
    return recurring() ? dates.contains(date) : completed;
  }

  Plan toggle(LocalDate date) {
    Plan result = copy();
    if (!recurring()) result.completed = !completed;
    else {
      if (!occurs(date) || date.isAfter(LocalDate.now()))
        throw new IllegalArgumentException("只能记录计划范围内已经到来的日期。");
      if (!result.dates.remove(date)) result.dates.add(date);
    }
    result.updatedAt = Instant.now();
    return result;
  }

  void validate() {
    if (!UUID.fromString(id).toString().equalsIgnoreCase(id)
        || id.equals("00000000-0000-0000-0000-000000000000")) fail("计划编号无效。");
    if (title == null || title.trim().isEmpty() || title.length() > 500) fail("请填写计划内容，最多 500 字。");
    if (notes == null || notes.length() > 5000) fail("备注最多 5000 字。");
    if (color == null || !color.matches("#[0-9a-fA-F]{6}")) fail("颜色须为 #RRGGBB，例如 #146C60。");
    for (LocalDate date : new LocalDate[] {day, endDay, repeatUntil})
      if (date != null && (date.getYear() < 1753 || date.getYear() > 9998))
        fail("日期须在 1753–9998 年之间。");
    if (start != null || end != null || endDay != null) {
      if (day == null || start == null || end == null || endDay == null)
        fail("时间段需要完整的开始和结束日期、时间。");
      if (!endDay.atTime(end).isAfter(day.atTime(start))) fail("结束时间须晚于开始时间；跨天请调整结束日期。");
    }
    if (durationMinutes != null && (durationMinutes < 1 || durationMinutes > 525600))
      fail("预期时长须为 1–525600 分钟。");
    if (alarm != null) alarm.validate(this);
    if (recurring()) {
      if (day == null || repeatCount != null && repeatUntil != null || completed)
        fail("长期计划需要开始日期和一种目标，按日期记录完成。");
      if (repeatUntil != null
          && (repeatUntil.isBefore(day) || ChronoUnit.DAYS.between(day, repeatUntil) >= 3650))
        fail("持续天数须为 1–3650 天。");
      if (repeatCount != null && (repeatCount < 1 || repeatCount > 10000))
        fail("完成次数须为 1–10000 次。");
      if (start != null
          && (endOffset() > 1
              || Duration.between(day.atTime(start), endDay.atTime(end))
                      .compareTo(Duration.ofDays(1))
                  > 0)) fail("每天重复的时间段最多 24 小时，可跨午夜到次日。");
      if (repeatUntil != null && repeatUntil.plusDays(endOffset()).getYear() > 9998)
        fail("长期计划结束时间超出可用日期范围。");
    }
    if (dates.size() > 10000) fail("完成记录最多 10000 个日期。");
    for (LocalDate date : dates)
      if (!recurring()
          || date.isBefore(day)
          || date.getYear() > 9998
          || repeatUntil != null && date.isAfter(repeatUntil)) fail("目标日期范围须包含已有完成记录。");
  }

  boolean overlaps(Plan other) {
    if (id.equals(other.id) || completed || other.completed || start == null || other.start == null)
      return false;
    if (!recurring() && !other.recurring())
      return day.atTime(start).isBefore(other.endDay.atTime(other.end))
          && endDay.atTime(end).isAfter(other.day.atTime(other.start));
    LocalDate first = day.isAfter(other.day) ? day : other.day;
    LocalDate leftLast = lastDay().plusDays(recurring() ? endOffset() : 0),
        rightLast = other.lastDay().plusDays(other.recurring() ? other.endOffset() : 0);
    LocalDate last = leftLast.isBefore(rightLast) ? leftLast : rightLast;
    long candidates =
        Math.min(
            ChronoUnit.DAYS.between(first, last), 2L * (dates.size() + other.dates.size()) + 3);
    for (long n = 0; n <= candidates; n++) {
      LocalDate date = first.plusDays(n);
      for (DaySegment a : segments(date))
        for (DaySegment b : other.segments(date))
          if (!checked(a.occurrence)
              && !other.checked(b.occurrence)
              && a.start < b.end
              && a.end > b.start) return true;
    }
    return false;
  }

  String timing() {
    String text =
        start == null
            ? day == null ? "待安排" : "未设置时间段"
            : recurring()
                ? "每天 " + clock(start) + " — " + (endOffset() > 0 ? "次日 " : "") + clock(end)
                : day.equals(endDay)
                    ? clock(start) + " — " + clock(end)
                    : day + " " + clock(start) + " — " + endDay + " " + clock(end);
    return text
        + (durationMinutes == null ? "" : " · 预计 " + durationMinutes + " 分钟")
        + (alarm == null ? "" : "\n" + alarm.description());
  }

  String progress() {
    return "每天 · "
        + (repeatUntil == null
            ? "已完成 " + dates.size() + " / " + repeatCount + " 次"
            : "至 " + repeatUntil + " · 已完成 " + dates.size() + " 天");
  }

  static String clock(LocalTime time) {
    return String.format(Locale.ROOT, "%02d:%02d", time.getHour(), time.getMinute());
  }

  static void fail(String text) {
    throw new IllegalArgumentException(text);
  }

  Plan copy() {
    try {
      return from(json());
    } catch (JSONException e) {
      throw new IllegalStateException(e);
    }
  }

  JSONObject json() throws JSONException {
    JSONObject o = new JSONObject();
    o.put("id", id);
    o.put("title", title);
    o.put("notes", notes);
    o.put("color", color);
    o.put("day", value(day));
    o.put("start", value(start));
    o.put("endDay", value(endDay));
    o.put("end", value(end));
    o.put("durationMinutes", value(durationMinutes));
    o.put("alarm", alarm == null ? JSONObject.NULL : alarm.json());
    o.put("isCompleted", completed);
    o.put("repeatUntil", value(repeatUntil));
    o.put("repeatCount", value(repeatCount));
    JSONArray a = new JSONArray();
    for (LocalDate date : dates) a.put(date.toString());
    o.put("completedDates", a);
    o.put("createdAt", createdAt.toString());
    o.put("updatedAt", updatedAt.toString());
    return o;
  }

  private static Object value(Object o) {
    if (o == null) return JSONObject.NULL;
    if (o instanceof Number) return o;
    if (o instanceof LocalTime) {
      LocalTime t = (LocalTime) o;
      String fraction =
          t.getNano() == 0
              ? ""
              : "." + String.format(Locale.ROOT, "%07d", t.getNano() / 100).replaceAll("0+$", "");
      return clock(t) + String.format(Locale.ROOT, ":%02d", t.getSecond()) + fraction;
    }
    return o.toString();
  }

  static Plan from(JSONObject o) throws JSONException {
    Set<String> allowed =
        new HashSet<>(
            Arrays.asList(
                "id",
                "title",
                "notes",
                "color",
                "day",
                "start",
                "endDay",
                "end",
                "durationMinutes",
                "alarm",
                "isCompleted",
                "repeatUntil",
                "repeatCount",
                "completedDates",
                "createdAt",
                "updatedAt"));
    Iterator<String> keys = o.keys();
    while (keys.hasNext()) if (!allowed.contains(keys.next())) fail("备份包含未知字段。");
    Plan p = new Plan();
    p.id = UUID.fromString(string(o, "id")).toString();
    p.title = string(o, "title");
    p.notes = o.has("notes") ? string(o, "notes") : "";
    p.color = string(o, "color");
    p.day = date(o, "day");
    p.endDay = date(o, "endDay");
    p.repeatUntil = date(o, "repeatUntil");
    p.start = o.isNull("start") ? null : LocalTime.parse(o.getString("start"));
    p.end = o.isNull("end") ? null : LocalTime.parse(o.getString("end"));
    p.durationMinutes = number(o, "durationMinutes");
    p.alarm = o.isNull("alarm") ? null : PlanAlarm.from(o.getJSONObject("alarm"));
    p.repeatCount = number(o, "repeatCount");
    if (o.has("isCompleted") && !(o.get("isCompleted") instanceof Boolean)) fail("完成状态无效。");
    p.completed = o.optBoolean("isCompleted", false);
    if (!o.isNull("completedDates")) {
      JSONArray a = o.getJSONArray("completedDates");
      if (a.length() > 10000) fail("完成记录过多。");
      for (int i = 0; i < a.length(); i++)
        if (!p.dates.add(LocalDate.parse(a.getString(i)))) fail("完成记录存在重复日期。");
    }
    p.createdAt = OffsetDateTime.parse(o.getString("createdAt")).toInstant();
    p.updatedAt = OffsetDateTime.parse(o.getString("updatedAt")).toInstant();
    p.validate();
    return p;
  }

  private static LocalDate date(JSONObject o, String key) throws JSONException {
    return o.isNull(key) ? null : LocalDate.parse(o.getString(key));
  }

  private static String string(JSONObject o, String key) throws JSONException {
    Object value = o.get(key);
    if (!(value instanceof String)) fail("备份文字字段无效：" + key);
    return (String) value;
  }

  static Integer number(JSONObject o, String key) throws JSONException {
    if (o.isNull(key)) return null;
    Object n = o.get(key);
    if (!(n instanceof Number) || ((Number) n).doubleValue() != ((Number) n).intValue())
      fail("备份数值无效。");
    return ((Number) n).intValue();
  }
}
