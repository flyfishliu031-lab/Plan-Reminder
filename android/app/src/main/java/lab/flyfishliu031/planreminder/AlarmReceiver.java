package lab.flyfishliu031.planreminder;

import android.content.*;
import android.net.Uri;
import java.time.*;

public final class AlarmReceiver extends BroadcastReceiver {
  @Override
  public void onReceive(Context c, Intent intent) {
    PendingResult result = goAsync();
    MainActivity.io.execute(
        () -> {
          try {
            PlanStore store = MainActivity.appStore(c);
            store.load();
            if ("lab.flyfishliu031.planreminder.RING".equals(intent.getAction())) {
              Uri data = intent.getData();
              String id = data == null ? null : data.getLastPathSegment();
              Instant occurrence = Instant.parse(intent.getStringExtra("occurrence"));
              Plan p =
                  store.all().stream().filter(plan -> plan.id.equals(id)).findFirst().orElse(null);
              // Re-read current data: deleted, edited or completed plans must not ring from stale
              // intents.
              boolean due = false;
              if (p != null && p.alarm != null) {
                if (p.alarm.mode.equals("after")) {
                  long trigger = AlarmScheduler.countdownTrigger(c, p, Instant.now());
                  due =
                      occurrence.equals(p.alarm.at)
                          && trigger >= 0
                          && trigger <= System.currentTimeMillis() + 2000;
                } else
                  due =
                      occurrence.equals(p.alarm.next(p, Instant.now()))
                          && !occurrence.isAfter(Instant.now().plusSeconds(2));
              }
              if (due) {
                Plan fired = p.copy();
                fired.alarm.firedAt = occurrence;
                store.save(fired);
                c.startForegroundService(
                    new Intent(c, AlarmService.class)
                        .putExtra("plan", p.json().toString())
                        .putExtra("occurrence", occurrence.toString()));
              }
            }
            AlarmScheduler.sync(c, store);
          } catch (Exception e) {
            android.util.Log.e("PlanAlarm", "Alarm could not be processed", e);
          } finally {
            result.finish();
          }
        });
  }
}
