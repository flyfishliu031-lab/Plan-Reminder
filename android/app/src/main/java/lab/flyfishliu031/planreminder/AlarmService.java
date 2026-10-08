package lab.flyfishliu031.planreminder;

import android.app.*;
import android.content.*;
import android.media.*;
import android.net.Uri;
import android.os.*;
import java.time.*;
import java.util.*;
import org.json.*;

public final class AlarmService extends Service {
  static volatile boolean running;
  private final Map<String, Plan> ringing = new LinkedHashMap<>();
  private final Map<String, LocalDate> dates = new HashMap<>();
  private final Handler handler = new Handler(Looper.getMainLooper());
  private Ringtone tone;
  private Vibrator vibrator;
  private PowerManager.WakeLock wake;
  private static final int ID = 501;
  private final Runnable repeatTone =
      new Runnable() {
        public void run() {
          if (tone != null && !tone.isPlaying()) tone.play();
          handler.postDelayed(this, 2000);
        }
      };

  @Override
  public void onCreate() {
    super.onCreate();
    running = true;
    vibrator = getSystemService(Vibrator.class);
    wake =
        getSystemService(PowerManager.class)
            .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "PlanReminder:alarm");
    wake.setReferenceCounted(false);
    wake.acquire(65000);
    NotificationChannel channel =
        new NotificationChannel(
            AlarmScheduler.CHANNEL, "计划闹钟", NotificationManager.IMPORTANCE_HIGH);
    channel.setSound(null, null);
    channel.enableVibration(false);
    getSystemService(NotificationManager.class).createNotificationChannel(channel);
  }

  @Override
  public int onStartCommand(Intent intent, int flags, int startId) {
    if (intent == null || "stop".equals(intent.getAction())) {
      stopSelf();
      return START_NOT_STICKY;
    }
    if ("refresh".equals(intent.getAction())) {
      for (String id : new ArrayList<>(ringing.keySet())) {
        Plan current =
            MainActivity.appStore(this).all().stream()
                .filter(p -> p.id.equals(id))
                .findFirst()
                .orElse(null);
        if (current == null
            || current.alarm == null
            || current.checked(dates.get(id))
            || current.finished(LocalDate.now())) ringing.remove(id);
      }
      if (ringing.isEmpty()) {
        stopSelf();
        return START_NOT_STICKY;
      }
    } else
      try {
        Plan p = Plan.from(new JSONObject(intent.getStringExtra("plan")));
        ringing.put(p.id, p);
        dates.put(
            p.id,
            p.alarm.mode.equals("after")
                ? LocalDate.now()
                : Instant.parse(intent.getStringExtra("occurrence"))
                    .atZone(ZoneId.systemDefault())
                    .toLocalDate());
        handler.removeCallbacksAndMessages(null);
        handler.postDelayed(this::stopSelf, 60000);
      } catch (Exception e) {
        stopSelf();
        return START_NOT_STICKY;
      }
    startForeground(ID, notification());
    wake.acquire(65000);
    play();
    handler.removeCallbacks(repeatTone);
    if (Build.VERSION.SDK_INT < 28) handler.postDelayed(repeatTone, 2000);
    return START_NOT_STICKY;
  }

  private Notification notification() {
    Intent view =
        new Intent(this, AlarmActivity.class)
            .putExtra("titles", titles())
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
    PendingIntent content =
        PendingIntent.getActivity(
            this, ID, view, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    PendingIntent stop =
        PendingIntent.getService(
            this,
            ID,
            new Intent(this, AlarmService.class).setAction("stop"),
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    Notification.Builder b =
        new Notification.Builder(this, AlarmScheduler.CHANNEL)
            .setSmallIcon(R.drawable.app_icon)
            .setContentTitle("计划表 · 时间到了")
            .setContentText(titles())
            .setStyle(new Notification.BigTextStyle().bigText(titles()))
            .setCategory(Notification.CATEGORY_ALARM)
            .setVisibility(Notification.VISIBILITY_PRIVATE)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setContentIntent(content)
            .addAction(new Notification.Action.Builder(null, "关闭闹钟", stop).build());
    if (Build.VERSION.SDK_INT < 34
        || getSystemService(NotificationManager.class).canUseFullScreenIntent())
      b.setFullScreenIntent(content, true);
    return b.build();
  }

  private String titles() {
    StringJoiner text = new StringJoiner("\n");
    for (Plan p : ringing.values()) text.add(p.title);
    return text.toString();
  }

  private void play() {
    if (tone != null) tone.stop();
    vibrator.cancel();
    Plan sound = ringing.values().stream().filter(p -> p.alarm.sound).findFirst().orElse(null);
    if (sound != null) {
      Uri uri =
          sound.alarm.ringtone == null
              ? RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)
              : Uri.parse(sound.alarm.ringtone);
      try {
        tone = RingtoneManager.getRingtone(this, uri);
      } catch (RuntimeException ignored) {
        tone = null;
      }
      if (tone == null)
        tone =
            RingtoneManager.getRingtone(
                this, RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM));
      if (tone != null) {
        tone.setAudioAttributes(
            new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_ALARM)
                .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                .build());
        if (Build.VERSION.SDK_INT >= 28) tone.setLooping(true);
        tone.play();
      }
    }
    if (ringing.values().stream().anyMatch(p -> p.alarm.vibrate))
      vibrator.vibrate(
          VibrationEffect.createWaveform(new long[] {0, 500, 500}, 0),
          new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM).build());
  }

  @Override
  public void onDestroy() {
    running = false;
    handler.removeCallbacksAndMessages(null);
    if (tone != null) tone.stop();
    vibrator.cancel();
    if (wake.isHeld()) wake.release();
    stopForeground(STOP_FOREGROUND_REMOVE);
    super.onDestroy();
  }

  @Override
  public IBinder onBind(Intent intent) {
    return null;
  }
}
