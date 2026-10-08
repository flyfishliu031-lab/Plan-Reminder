package lab.flyfishliu031.planreminder;

import java.time.*;
import java.util.*;
import org.json.*;

final class PlanAlarm {
  String mode = "after", ringtone;
  Integer afterMinutes;
  Instant at, firedAt;
  boolean sound = true, vibrate = true;

  void validate(Plan p) {
    if (!Arrays.asList("after", "start", "end").contains(mode)) Plan.fail("请选择有效的闹钟方式。");
    if (mode.equals("after")) {
      if (afterMinutes == null
          || afterMinutes < 1
          || afterMinutes > 525600
          || at == null
          || at.atOffset(ZoneOffset.UTC).getYear() < 1753
          || at.atOffset(ZoneOffset.UTC).getYear() > 9998) Plan.fail("倒计时须为 1–525600 分钟。");
    } else if (p.start == null || afterMinutes != null || at != null)
      Plan.fail("开始／结束闹钟需要完整的计划时间段。");
    if (ringtone != null && (ringtone.length() > 2048 || !ringtone.startsWith("content://")))
      Plan.fail("铃声地址无效。");
  }

  Instant next(Plan p, Instant now) {
    ZoneId zone = ZoneId.systemDefault();
    if (p.completed || p.finished(now.atZone(zone).toLocalDate())) return null;
    Instant earliest = now.minusSeconds(300);
    if (mode.equals("after"))
      return eligible(at, earliest) && (!p.recurring() || !p.checked(at.atZone(zone).toLocalDate()))
          ? at
          : null;
    if (!p.recurring()) {
      Instant time =
          (mode.equals("start") ? p.day.atTime(p.start) : p.endDay.atTime(p.end))
              .atZone(zone)
              .toInstant();
      return eligible(time, earliest) ? time : null;
    }
    LocalDate day = earliest.atZone(zone).toLocalDate();
    if (day.isBefore(p.day)) day = p.day;
    for (int i = 0; i <= p.dates.size() + 2 && day.getYear() <= 9998; i++, day = day.plusDays(1)) {
      if (!p.appears(day)) return null;
      if (p.checked(day)) continue;
      Instant time = day.atTime(mode.equals("start") ? p.start : p.end).atZone(zone).toInstant();
      if (eligible(time, earliest)) return time;
    }
    return null;
  }

  private boolean eligible(Instant time, Instant earliest) {
    return time != null && !time.isBefore(earliest) && (firedAt == null || time.isAfter(firedAt));
  }

  String description() {
    return mode.equals("after")
        ? "闹钟 · "
            + at.atZone(ZoneId.systemDefault())
                .format(java.time.format.DateTimeFormatter.ofPattern("MM/dd HH:mm"))
            + "（一次）"
        : "闹钟 · " + (mode.equals("start") ? "开始时" : "结束时");
  }

  JSONObject json() throws JSONException {
    JSONObject o = new JSONObject();
    o.put("mode", mode);
    o.put("afterMinutes", afterMinutes == null ? JSONObject.NULL : afterMinutes);
    o.put("at", at == null ? JSONObject.NULL : at.toString());
    o.put("firedAt", firedAt == null ? JSONObject.NULL : firedAt.toString());
    o.put("sound", sound);
    o.put("vibrate", vibrate);
    o.put("ringtone", ringtone == null ? JSONObject.NULL : ringtone);
    return o;
  }

  static PlanAlarm from(JSONObject o) throws JSONException {
    Set<String> allowed =
        new HashSet<>(
            Arrays.asList("mode", "afterMinutes", "at", "firedAt", "sound", "vibrate", "ringtone"));
    Iterator<String> keys = o.keys();
    while (keys.hasNext()) if (!allowed.contains(keys.next())) Plan.fail("闹钟包含未知字段。");
    PlanAlarm a = new PlanAlarm();
    Object mode = o.get("mode");
    if (!(mode instanceof String)) Plan.fail("闹钟方式无效。");
    a.mode = (String) mode;
    a.afterMinutes = Plan.number(o, "afterMinutes");
    a.at = instant(o, "at");
    a.firedAt = instant(o, "firedAt");
    for (String key : new String[] {"sound", "vibrate"})
      if (o.has(key) && !(o.get(key) instanceof Boolean)) Plan.fail("闹钟开关无效。");
    a.sound = o.optBoolean("sound", true);
    a.vibrate = o.optBoolean("vibrate", true);
    if (!o.isNull("ringtone")) {
      if (!(o.get("ringtone") instanceof String)) Plan.fail("铃声地址无效。");
      a.ringtone = o.getString("ringtone");
    }
    return a;
  }

  private static Instant instant(JSONObject o, String key) throws JSONException {
    return o.isNull(key) ? null : OffsetDateTime.parse(o.getString(key)).toInstant();
  }
}
