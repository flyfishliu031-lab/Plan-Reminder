package lab.flyfishliu031.planreminder;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.os.*;
import android.provider.Settings;
import java.time.*;
import java.util.*;

final class AlarmScheduler {
  static final String CHANNEL = "plan-alarms";

  static boolean exactAllowed(Context c) {
    return Build.VERSION.SDK_INT < 31
        || c.getSystemService(AlarmManager.class).canScheduleExactAlarms();
  }

  static boolean notificationsAllowed(Context c) {
    NotificationManager n = c.getSystemService(NotificationManager.class);
    NotificationChannel channel = n.getNotificationChannel(CHANNEL);
    return n.areNotificationsEnabled()
        && (channel == null || channel.getImportance() != NotificationManager.IMPORTANCE_NONE);
  }

  static String status(Context c) {
    return "准时闹钟："
        + (exactAllowed(c) ? "已允许" : "待允许")
        + "\n通知："
        + (notificationsAllowed(c) ? "已允许" : "待允许")
        + "\n锁屏弹出："
        + (Build.VERSION.SDK_INT < 34
                || c.getSystemService(NotificationManager.class).canUseFullScreenIntent()
            ? "已允许"
            : "未允许（仍可响铃和显示通知）");
  }

  static PendingIntent pending(Context c, String id, Instant occurrence) {
    Intent i =
        new Intent(c, AlarmReceiver.class)
            .setAction("lab.flyfishliu031.planreminder.RING")
            .setData(Uri.parse("planalarm://plan/" + id));
    if (occurrence != null) i.putExtra("occurrence", occurrence.toString());
    return PendingIntent.getBroadcast(
        c, 0, i, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
  }

  // Preserve elapsed countdowns across activity/process restarts and wall-clock changes.
  // After a reboot, restore the remaining interval from the persisted UTC deadline.
  static long countdownTrigger(Context c, Plan p, Instant now) {
    if (p.completed
        || p.finished(LocalDate.now())
        || p.recurring() && p.checked(LocalDate.now())
        || p.alarm.firedAt != null && !p.alarm.at.isAfter(p.alarm.firedAt)) return -1;
    SharedPreferences prefs = c.getSharedPreferences("alarms", Context.MODE_PRIVATE);
    String key = p.id + ":countdown", signature = p.alarm.at.toString();
    int boot = Settings.Global.getInt(c.getContentResolver(), Settings.Global.BOOT_COUNT, -1);
    long deadline = prefs.getLong(key + ":elapsed", -1);
    if (!signature.equals(prefs.getString(key, null))
        || boot != prefs.getInt(key + ":boot", -2)
        || deadline < 0) {
      deadline = SystemClock.elapsedRealtime() + Duration.between(now, p.alarm.at).toMillis();
      prefs
          .edit()
          .putString(key, signature)
          .putInt(key + ":boot", boot)
          .putLong(key + ":elapsed", deadline)
          .apply();
    }
    long remaining = deadline - SystemClock.elapsedRealtime();
    return remaining < -300000 ? -1 : System.currentTimeMillis() + remaining;
  }

  static void sync(Context context, PlanStore store) {
    Context c = context.getApplicationContext();
    AlarmManager manager = c.getSystemService(AlarmManager.class);
    SharedPreferences prefs = c.getSharedPreferences("alarms", Context.MODE_PRIVATE);
    Set<String> old = new HashSet<>(prefs.getStringSet("scheduled", Collections.emptySet()));
    for (String id : old) manager.cancel(pending(c, id, null));
    Set<String> ids = new HashSet<>();
    if (exactAllowed(c) && notificationsAllowed(c)) {
      Instant now = Instant.now();
      for (Plan p : store.all()) {
        if (p.alarm == null || p.completed || p.finished(LocalDate.now())) continue;
        long countdown = p.alarm.mode.equals("after") ? countdownTrigger(c, p, now) : -1;
        Instant occurrence =
            p.alarm.mode.equals("after") ? countdown < 0 ? null : p.alarm.at : p.alarm.next(p, now);
        if (occurrence == null) continue;
        PendingIntent pi = pending(c, p.id, occurrence);
        long trigger =
            Math.max(
                System.currentTimeMillis() + 100,
                p.alarm.mode.equals("after") ? countdown : occurrence.toEpochMilli());
        Intent show = new Intent(c, MainActivity.class);
        PendingIntent info =
            PendingIntent.getActivity(
                c, 0, show, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        try {
          manager.setAlarmClock(new AlarmManager.AlarmClockInfo(trigger, info), pi);
          ids.add(p.id);
        } catch (SecurityException ignored) {
          break;
        }
      }
    }
    prefs.edit().putStringSet("scheduled", ids).apply();
    // Updating or completing a plan also stops its currently ringing alarm.
    Intent refresh = new Intent(c, AlarmService.class).setAction("refresh");
    if (AlarmService.running) c.startService(refresh);
  }

  static void permissions(Activity a) {
    new AlertDialog.Builder(a)
        .setTitle("闹钟权限")
        .setMessage(status(a) + "\n\n允许准时闹钟和通知后，锁屏及后台也能提醒。系统闹钟音量、勿扰模式及厂商省电设置会影响响铃。强制停止应用后须重新打开。")
        .setItems(
            new String[] {"允许准时闹钟", "允许通知", "允许锁屏弹出"},
            (d, w) -> {
              try {
                if (w == 0 && Build.VERSION.SDK_INT >= 31)
                  a.startActivity(
                      new Intent(
                          Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM,
                          Uri.parse("package:" + a.getPackageName())));
                else if (w == 1
                    && Build.VERSION.SDK_INT >= 33
                    && a.checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS)
                        != android.content.pm.PackageManager.PERMISSION_GRANTED)
                  a.requestPermissions(
                      new String[] {android.Manifest.permission.POST_NOTIFICATIONS}, 20);
                else if (w == 1)
                  a.startActivity(
                      new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                          .putExtra(Settings.EXTRA_APP_PACKAGE, a.getPackageName()));
                else if (w == 2 && Build.VERSION.SDK_INT >= 34)
                  a.startActivity(
                      new Intent(
                          Settings.ACTION_MANAGE_APP_USE_FULL_SCREEN_INTENT,
                          Uri.parse("package:" + a.getPackageName())));
                else
                  android.widget.Toast.makeText(a, "当前系统无需此项授权", android.widget.Toast.LENGTH_SHORT)
                      .show();
              } catch (ActivityNotFoundException e) {
                a.startActivity(
                    new Intent(
                        Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:" + a.getPackageName())));
              }
            })
        .setNegativeButton("关闭", null)
        .show();
  }
}
