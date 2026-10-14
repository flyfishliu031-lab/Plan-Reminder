package lab.flyfishliu031.planreminder;

import static org.junit.Assert.*;

import android.app.Instrumentation;
import android.content.*;
import android.content.pm.ActivityInfo;
import android.graphics.Bitmap;
import android.graphics.Rect;
import android.os.*;
import android.view.*;
import android.view.inputmethod.InputMethodManager;
import android.widget.*;
import androidx.test.core.app.ActivityScenario;
import androidx.test.platform.app.InstrumentationRegistry;
import java.io.*;
import java.time.*;
import java.util.*;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import org.json.*;
import org.junit.*;

public class PlanTests {
  @Test
  public void quickAddFiltersUndoAndSavingRotation() throws Exception {
    String[] id = new String[1];
    try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            try {
              for (Plan p : a.store.all()) a.store.delete(p.id);
              a.selected = LocalDate.now();
              a.switchMode(0);
              new EditorForm(a, null, null);
              View decor = a.editor.dialog.getWindow().getDecorView();
              assertNotNull(findButton(decor, "展开 · 时间与时长"));
              assertNotNull(findButton(decor, "展开 · 长期计划"));
              assertFalse(findCheckBox(decor, "添加闹钟 · 可选").isShown());
              inputs(decor).stream()
                  .filter(v -> "计划内容（必填）".contentEquals(v.getContentDescription()))
                  .findFirst()
                  .get()
                  .setText("只写内容的快速计划");
              id[0] = a.editor.snapshot().getString("draftId");
            } catch (Exception e) {
              throw new AssertionError(e);
            }
          });
      SystemClock.sleep(300);
      scenario.onActivity(this::checkSaveVisible);
      capture("android-editor-quick.png");
      CountDownLatch started = new CountDownLatch(1), release = new CountDownLatch(1);
      MainActivity.io.execute(
          () -> {
            started.countDown();
            try {
              release.await(8, TimeUnit.SECONDS);
            } catch (InterruptedException e) {
              Thread.currentThread().interrupt();
            }
          });
      assertTrue(started.await(5, TimeUnit.SECONDS));
      try {
        scenario.onActivity(a -> a.editor.save());
        scenario.recreate();
      } finally {
        release.countDown();
      }
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertNull("Saving during rotation must not reopen the saved draft", a.editor);
            assertEquals(1, a.store.all().size());
            Plan saved = a.store.all().get(0);
            assertEquals(id[0], saved.id);
            assertNull(saved.start);
            assertNull(saved.alarm);
            assertNull(saved.durationMinutes);
            a.completePlan(saved, a.selected);
          });
      waitReady(scenario);
      scenario.recreate();
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            try {
              Plan edited = a.store.all().get(0);
              assertTrue(edited.completed);
              edited.notes = "撤销完成时也应保留后来编辑的备注";
              a.store.save(edited);
              assertNotNull(findButton(a.footer, "撤销"));
              findButton(a.footer, "撤销").performClick();
            } catch (Exception e) {
              throw new AssertionError(e);
            }
          });
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            try {
              Plan saved = a.store.all().get(0);
              assertFalse(saved.completed);
              assertTrue(saved.notes.contains("后来编辑"));
              Plan other = new Plan();
              other.title = "已完成的其他计划";
              other.day = a.selected;
              other.completed = true;
              a.store.save(other);
              Plan undated = new Plan();
              undated.title = "暂不安排日期的计划";
              a.store.save(undated);
              a.render();
              android.app.AlertDialog filters = a.showFilters();
              View decor = filters.getWindow().getDecorView();
              inputs(decor).get(0).setText("后来编辑");
              ((Spinner) described(decor, "完成状态筛选")).setSelection(1);
              filters.getButton(android.app.AlertDialog.BUTTON_POSITIVE).performClick();
            } catch (Exception e) {
              throw new AssertionError(e);
            }
          });
      SystemClock.sleep(300);
      scenario.onActivity(
          a -> {
            assertEquals(1, a.list.getChildCount());
            assertTrue(a.heading.getText().toString().startsWith("1 项匹配"));
            checkButtons(a.navigation);
            checkButtons(a.footer);
          });
      capture("android-filtered.png");
      scenario.recreate();
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertEquals("后来编辑", a.query);
            assertEquals(1, a.stateFilter);
            assertEquals(1, a.list.getChildCount());
            findButton(a.list, "删除").performClick();
          });
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertFalse(a.store.all().stream().anyMatch(p -> p.id.equals(id[0])));
            findButton(a.footer, "撤销").performClick();
          });
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertTrue(a.store.all().stream().anyMatch(p -> p.id.equals(id[0])));
            android.app.AlertDialog filters = a.showFilters();
            View decor = filters.getWindow().getDecorView();
            inputs(decor).get(0).setText("");
            ((Spinner) described(decor, "完成状态筛选")).setSelection(0);
            ((Spinner) described(decor, "计划范围")).setSelection(1);
            filters.getButton(android.app.AlertDialog.BUTTON_POSITIVE).performClick();
          });
      InstrumentationRegistry.getInstrumentation().waitForIdleSync();
      scenario.onActivity(
          a -> {
            assertEquals(1, a.mode);
            assertEquals(1, a.list.getChildCount());
            Plan dated = a.store.all().stream().filter(p -> p.id.equals(id[0])).findFirst().get();
            a.revealSaved(dated);
            a.render();
            assertEquals("A dated plan saved from the undated view must be revealed", 0, a.mode);
            a.switchMode(3);
            assertEquals(3, a.getSharedPreferences("ui", Context.MODE_PRIVATE).getInt("view", -1));
          });
    }
  }

  private View described(View view, String description) {
    if (view.getContentDescription() != null
        && description.contentEquals(view.getContentDescription())) return view;
    if (view instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) view).getChildCount(); i++) {
        View found = described(((ViewGroup) view).getChildAt(i), description);
        if (found != null) return found;
      }
    return null;
  }

  @Test
  public void timetableOvernightAndSelection() throws Exception {
    Instrumentation inst = InstrumentationRegistry.getInstrumentation();
    Context context = inst.getTargetContext();
    if (Build.VERSION.SDK_INT >= 33)
      inst.getUiAutomation()
          .grantRuntimePermission(
              context.getPackageName(), android.Manifest.permission.POST_NOTIFICATIONS);
    if (Build.VERSION.SDK_INT >= 31) {
      try (ParcelFileDescriptor fd =
              inst.getUiAutomation()
                  .executeShellCommand(
                      "appops set " + context.getPackageName() + " SCHEDULE_EXACT_ALARM allow");
          InputStream in = new ParcelFileDescriptor.AutoCloseInputStream(fd)) {
        while (in.read() != -1) {}
      }
    }
    LocalDate today = LocalDate.now();
    Plan p = new Plan();
    p.title = "每天睡前阅读";
    p.day = today.minusDays(1);
    p.start = LocalTime.of(23, 0);
    p.endDay = today;
    p.end = LocalTime.of(1, 0);
    p.repeatUntil = today;
    p.alarm = new PlanAlarm();
    p.alarm.mode = "end";
    p.validate();
    assertEquals(2, p.segments(today).size());
    assertEquals(today.minusDays(1), p.segments(today).get(0).occurrence);
    assertTrue(p.appears(today.plusDays(1)));
    assertFalse(p.appears(today.plusDays(2)));
    Plan midnight = p.copy();
    midnight.end = LocalTime.MIDNIGHT;
    assertFalse(midnight.appears(today.plusDays(1)));
    Instant midnightDue = today.plusDays(1).atStartOfDay(ZoneId.systemDefault()).toInstant();
    assertTrue(midnight.finished(today.plusDays(1)));
    assertEquals(midnightDue, midnight.alarm.next(midnight, midnightDue));
    Plan tooLong = p.copy();
    tooLong.end = LocalTime.of(23, 1);
    rejects(tooLong::validate);
    rejects(() -> p.toggle(today.plusDays(1)));
    Instant now = today.plusDays(1).atTime(0, 30).atZone(ZoneId.systemDefault()).toInstant();
    Instant due = today.plusDays(1).atTime(1, 0).atZone(ZoneId.systemDefault()).toInstant();
    assertEquals(due, p.alarm.next(p, now));
    assertEquals(today, p.alarm.occurrence(p, due));
    assertNull(p.alarm.next(p.toggle(today), now));
    Plan conflict = new Plan();
    conflict.title = "凌晨安排";
    conflict.day = today.plusDays(1);
    conflict.endDay = conflict.day;
    conflict.start = LocalTime.of(0, 30);
    conflict.end = LocalTime.of(1, 30);
    assertTrue(p.overlaps(conflict));
    assertTrue(conflict.overlaps(p));
    assertFalse(p.toggle(today).overlaps(conflict));
    java.util.List<DaySegment> blocks =
        DaySegment.layout(Arrays.asList(p, conflict), today.plusDays(1));
    assertEquals(2, blocks.size());
    assertEquals(2, blocks.get(0).columns);
    assertNotEquals(blocks.get(0).column, blocks.get(1).column);
    try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            try {
              for (Plan old : a.store.all()) a.store.delete(old.id);
              a.store.save(p);
              Plan daytime = new Plan();
              daytime.title = "专注学习";
              daytime.day = today;
              daytime.start = LocalTime.of(9, 0);
              daytime.end = LocalTime.of(10, 30);
              daytime.endDay = today;
              daytime.color = Plan.COLORS[2];
              a.store.save(daytime);
              Plan overlapping = daytime.copy();
              overlapping.id = UUID.randomUUID().toString();
              overlapping.title = "整理笔记";
              overlapping.start = LocalTime.of(10, 0);
              overlapping.end = LocalTime.of(11, 0);
              overlapping.color = Plan.COLORS[1];
              a.store.save(overlapping);
              a.selected = today;
              a.mode = 3;
              a.render();
            } catch (Exception e) {
              throw new AssertionError(e);
            }
          });
      SystemClock.sleep(350);
      scenario.onActivity(
          a -> {
            Rect visible = new Rect();
            assertTrue(
                "Opening timetable must show the time axis",
                a.timeline.getGlobalVisibleRect(visible));
            assertTrue(
                "Opening timetable must leave usable timeline space", visible.height() >= a.dp(80));
            assertNotNull(findButton(a.navigation, "时间表"));
            Rect headerBounds = new Rect();
            assertTrue(a.dateButton.getGlobalVisibleRect(headerBounds));
          });
      capture("android-timetable.png");
      scenario.onActivity(
          a -> {
            assertNotNull(a.timeline);
            assertEquals(4, a.timeline.segments.size());
            a.mainScroll.scrollTo(0, a.list.getTop() + a.timeline.getTop() + a.dp(9 * 88));
          });
      SystemClock.sleep(350);
      capture("android-timetable-day.png");
      tapTimeline(scenario, 12 * 60 + 7);
      tapTimeline(scenario, 13 * 60 + 11);
      scenario.onActivity(
          a -> {
            assertNotNull(a.editor);
            assertEquals("12:07", a.editor.snapshot().getString("start"));
            assertEquals("13:11", a.editor.snapshot().getString("end"));
            a.editor.dialog.dismiss();
          });
      scenario.onActivity(
          a -> {
            a.timeline.chooseMinute(23 * 60 + 17);
            a.timeline.chooseMinute(1440);
            assertEquals("23:17", a.editor.snapshot().getString("start"));
            assertEquals("00:00", a.editor.snapshot().getString("end"));
            assertEquals(today.plusDays(1).toString(), a.editor.snapshot().getString("endDay"));
            assertTrue(a.editor.snapshot().getBoolean("scheduled"));
            assertFalse(a.editor.snapshot().containsKey("original"));
          });
      scenario.recreate();
      waitReady(scenario);
      SystemClock.sleep(300);
      scenario.onActivity(
          a -> {
            assertEquals(3, a.mode);
            assertEquals("23:17", a.editor.snapshot().getString("start"));
            checkSaveVisible(a);
            try {
              a.store.delete(p.id);
            } catch (Exception e) {
              throw new AssertionError(e);
            }
            inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                .filter(v -> "计划内容（必填）".contentEquals(v.getContentDescription()))
                .findFirst()
                .get()
                .setText("时间表新增跨夜长期计划");
            findCheckBox(a.editor.dialog.getWindow().getDecorView(), "设为长期计划（每天重复）")
                .setChecked(true);
            inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                .filter(v -> "长期计划目标".contentEquals(v.getContentDescription()))
                .findFirst()
                .get()
                .setText("2");
            findButton(a.editor.dialog.getWindow().getDecorView(), "完成次数").performClick();
            findCheckBox(a.editor.dialog.getWindow().getDecorView(), "添加闹钟 · 可选").setChecked(true);
            findButton(a.editor.dialog.getWindow().getDecorView(), "计划结束时响起").performClick();
            a.editor.save();
          });
      waitReady(scenario);
      SystemClock.sleep(400);
      scenario.onActivity(
          a -> {
            Plan saved =
                a.store.all().stream()
                    .filter(plan -> plan.title.equals("时间表新增跨夜长期计划"))
                    .findFirst()
                    .get();
            assertEquals(Integer.valueOf(2), saved.repeatCount);
            assertEquals(1, saved.endOffset());
            assertEquals("end", saved.alarm.mode);
            assertEquals(LocalTime.of(23, 17), saved.start);
            assertEquals(LocalTime.MIDNIGHT, saved.end);
            assertEquals(1, saved.toggle(today).dates.size());
          });
      scenario.onActivity(
          a -> a.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE));
      SystemClock.sleep(1400);
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertEquals(3, a.mode);
            assertNotNull(a.timeline);
            checkButtons(a.footer);
          });
      capture("android-timetable-landscape.png");
    }
  }

  private void tapTimeline(ActivityScenario<MainActivity> scenario, int minute) {
    float[] point = new float[2];
    scenario.onActivity(
        a -> {
          int[] location = new int[2];
          a.timeline.getLocationOnScreen(location);
          point[0] = location[0] + a.timeline.left + a.dp(40);
          point[1] = location[1] + a.timeline.top + minute * a.timeline.hour / 60;
          Rect viewport = new Rect();
          assertTrue(a.mainScroll.getGlobalVisibleRect(viewport));
          if (point[1] < viewport.top + a.dp(16) || point[1] > viewport.bottom - a.dp(16)) {
            a.mainScroll.scrollBy(0, Math.round(point[1] - viewport.exactCenterY()));
            a.timeline.getLocationOnScreen(location);
            point[1] = location[1] + a.timeline.top + minute * a.timeline.hour / 60;
          }
          assertTrue(
              "Tap must be inside the timeline scroll viewport, above fixed actions",
              point[1] > viewport.top && point[1] < viewport.bottom);
        });
    Instrumentation inst = InstrumentationRegistry.getInstrumentation();
    long time = SystemClock.uptimeMillis();
    MotionEvent down =
        MotionEvent.obtain(time, time, MotionEvent.ACTION_DOWN, point[0], point[1], 0);
    MotionEvent up =
        MotionEvent.obtain(time, time + 60, MotionEvent.ACTION_UP, point[0], point[1], 0);
    inst.sendPointerSync(down);
    inst.sendPointerSync(up);
    down.recycle();
    up.recycle();
    inst.waitForIdleSync();
  }

  private CheckBox findCheckBox(View v, String text) {
    if (v instanceof CheckBox && text.contentEquals(((CheckBox) v).getText())) return (CheckBox) v;
    if (v instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++) {
        CheckBox b = findCheckBox(((ViewGroup) v).getChildAt(i), text);
        if (b != null) return b;
      }
    return null;
  }

  @Test
  public void alarmsPersistCancelAndRingInBackground() throws Exception {
    Instrumentation inst = InstrumentationRegistry.getInstrumentation();
    Context c = inst.getTargetContext();
    if (Build.VERSION.SDK_INT >= 33)
      inst.getUiAutomation()
          .grantRuntimePermission(
              c.getPackageName(), android.Manifest.permission.POST_NOTIFICATIONS);
    if (Build.VERSION.SDK_INT >= 31) {
      try (ParcelFileDescriptor fd =
          inst.getUiAutomation()
              .executeShellCommand(
                  "appops set " + c.getPackageName() + " SCHEDULE_EXACT_ALARM allow")) {
        try (InputStream in = new ParcelFileDescriptor.AutoCloseInputStream(fd)) {
          while (in.read() != -1) {}
        }
      }
    }
    assertTrue(AlarmScheduler.exactAllowed(c));
    if (Build.VERSION.SDK_INT >= 34) {
      try (ParcelFileDescriptor fd =
          inst.getUiAutomation()
              .executeShellCommand(
                  "appops set " + c.getPackageName() + " USE_FULL_SCREEN_INTENT allow")) {
        try (InputStream in = new ParcelFileDescriptor.AutoCloseInputStream(fd)) {
          while (in.read() != -1) {}
        }
      }
    }
    Plan p = new Plan();
    p.title = "后台测试闹钟";
    p.alarm = new PlanAlarm();
    p.alarm.afterMinutes = 1;
    p.alarm.at = Instant.now().plusSeconds(3);
    p.alarm.sound = true;
    p.alarm.vibrate = true;
    p.validate();
    assertEquals(p.alarm.at, p.alarm.next(p, Instant.now()));
    assertNull(p.alarm.next(p.toggle(LocalDate.now()), Instant.now()));
    Plan wrong = p.copy();
    wrong.alarm.afterMinutes = 0;
    rejects(wrong::validate);
    Plan schedule = sample();
    schedule.day = LocalDate.now();
    schedule.endDay = schedule.day;
    schedule.start = LocalTime.of(23, 0);
    schedule.end = LocalTime.of(23, 30);
    schedule.alarm = new PlanAlarm();
    schedule.alarm.mode = "end";
    schedule.validate();
    assertEquals(
        schedule.day.atTime(schedule.end).atZone(ZoneId.systemDefault()).toInstant(),
        schedule.alarm.next(
            schedule, schedule.day.atStartOfDay(ZoneId.systemDefault()).toInstant()));
    assertEquals(
        schedule.day.plusDays(1).atTime(schedule.end).atZone(ZoneId.systemDefault()).toInstant(),
        schedule.alarm.next(
            schedule.toggle(schedule.day),
            schedule.day.atStartOfDay(ZoneId.systemDefault()).toInstant()));
    PlanStore store = MainActivity.appStore(c);
    store.load();
    store.save(p);
    AlarmScheduler.sync(c, store);
    assertNotNull(c.getSystemService(android.app.AlarmManager.class).getNextAlarmClock());
    try (ParcelFileDescriptor fd =
        inst.getUiAutomation().executeShellCommand("input keyevent 223")) {
      try (InputStream in = new ParcelFileDescriptor.AutoCloseInputStream(fd)) {
        while (in.read() != -1) {}
      }
    }
    long limit = SystemClock.elapsedRealtime() + 15000;
    while (!AlarmService.running && SystemClock.elapsedRealtime() < limit) SystemClock.sleep(100);
    assertTrue(
        "Real AlarmManager alarm must start background ringing service", AlarmService.running);
    long wakeLimit = SystemClock.elapsedRealtime() + 5000;
    while (!c.getSystemService(PowerManager.class).isInteractive()
        && SystemClock.elapsedRealtime() < wakeLimit) SystemClock.sleep(100);
    assertTrue(
        "Locked-screen alarm must wake the screen",
        c.getSystemService(PowerManager.class).isInteractive());
    long screenLimit = SystemClock.elapsedRealtime() + 8000;
    boolean rendered = false;
    while (SystemClock.elapsedRealtime() < screenLimit) {
      android.view.accessibility.AccessibilityNodeInfo node =
          inst.getUiAutomation().getRootInActiveWindow();
      if (node != null
          && c.getPackageName().contentEquals(node.getPackageName())
          && !node.findAccessibilityNodeInfosByText("关闭闹钟").isEmpty()) {
        rendered = true;
        break;
      }
      SystemClock.sleep(100);
    }
    assertTrue("Lock screen must display the alarm title and stop button", rendered);
    SystemClock.sleep(300);
    capture("android-ringing.png");
    assertTrue(
        Arrays.stream(
                c.getSystemService(android.app.NotificationManager.class).getActiveNotifications())
            .anyMatch(n -> n.getId() == 501));
    store.load();
    Plan fired = store.all().stream().filter(plan -> plan.id.equals(p.id)).findFirst().get();
    assertEquals(p.alarm.at, fired.alarm.firedAt);
    assertNull(fired.alarm.next(fired, Instant.now()));
    store.save(p.toggle(LocalDate.now()));
    store.save(p);
    assertEquals(
        p.alarm.at,
        store.all().stream().filter(plan -> plan.id.equals(p.id)).findFirst().get().alarm.firedAt);
    c.stopService(new Intent(c, AlarmService.class));
    SystemClock.sleep(300);
    assertFalse(AlarmService.running);
    Plan future = p.copy();
    future.id = UUID.randomUUID().toString();
    future.alarm.at = Instant.now().plusSeconds(60);
    store.save(future);
    AlarmScheduler.sync(c, store);
    store.save(future.toggle(LocalDate.now()));
    AlarmScheduler.sync(c, store);
    assertFalse(
        c.getSharedPreferences("alarms", Context.MODE_PRIVATE)
            .getStringSet("scheduled", Collections.emptySet())
            .contains(future.id));
    future.completed = false;
    store.save(future);
    AlarmScheduler.sync(c, store);
    store.delete(future.id);
    AlarmScheduler.sync(c, store);
    assertFalse(
        c.getSharedPreferences("alarms", Context.MODE_PRIVATE)
            .getStringSet("scheduled", Collections.emptySet())
            .contains(future.id));
    store.delete(p.id);
  }

  @Test
  public void alarmSettingsLayout() throws Exception {
    try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            Plan p = new Plan();
            p.title = "十分钟后休息一下";
            p.day = LocalDate.now();
            p.alarm = new PlanAlarm();
            p.alarm.afterMinutes = 10;
            p.alarm.at = Instant.now().plusSeconds(600);
            new EditorForm(a, p, null);
            ScrollView scroll = findScroll(a.editor.dialog.getWindow().getDecorView());
            scroll.post(() -> scroll.smoothScrollTo(0, a.dp(740)));
          });
      SystemClock.sleep(700);
      scenario.onActivity(this::checkSaveVisible);
      capture("android-alarm-settings.png");
      scenario.recreate();
      waitReady(scenario);
      SystemClock.sleep(500);
      scenario.onActivity(
          a -> {
            assertTrue(a.editor.snapshot().getBoolean("alarmEnabled"));
            assertEquals("10", a.editor.snapshot().getString("alarmMinutes"));
            checkSaveVisible(a);
            a.editor.dialog.dismiss();
          });
    }
  }

  private ScrollView findScroll(View view) {
    if (view instanceof ScrollView) return (ScrollView) view;
    if (view instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) view).getChildCount(); i++) {
        ScrollView result = findScroll(((ViewGroup) view).getChildAt(i));
        if (result != null) return result;
      }
    return null;
  }

  private Plan sample() {
    Plan p = new Plan();
    p.title = "每天学习，积累一点进步";
    p.day = LocalDate.now().minusDays(3);
    p.repeatCount = 3;
    p.durationMinutes = 30;
    return p;
  }

  private void rejects(Runnable work) {
    try {
      work.run();
      fail("Invalid plan accepted");
    } catch (IllegalArgumentException expected) {
    }
  }

  @Test
  public void recurringGoalsAndUndo() {
    Plan p = sample();
    LocalDate day = p.day;
    p = p.toggle(day).toggle(day.plusDays(1)).toggle(day.plusDays(2));
    assertTrue(p.finished(LocalDate.now()));
    assertFalse(p.appears(day.plusDays(3)));
    p = p.toggle(day.plusDays(1));
    assertFalse(p.finished(LocalDate.now()));
    assertTrue(p.appears(day.plusDays(3)));
    assertEquals(2, p.dates.size());
    Plan future = p;
    rejects(() -> future.toggle(LocalDate.now().plusDays(1)));
    p.repeatCount = null;
    p.repeatUntil = day.plusDays(2);
    assertTrue(p.finished(day.plusDays(3)));
    assertTrue(p.appears(day.plusDays(2)));
    assertFalse(p.appears(day.plusDays(3)));
    Plan invalid = p.copy();
    invalid.repeatUntil = day;
    rejects(invalid::validate);
  }

  @Test
  public void crossDayAndOverlapBoundaries() {
    Plan p = new Plan();
    p.title = "跨天计划";
    p.day = LocalDate.now().minusDays(1);
    p.start = LocalTime.of(23, 0);
    p.endDay = p.day.plusDays(1);
    p.end = LocalTime.MIDNIGHT;
    p.validate();
    assertFalse(p.appears(p.endDay));
    p.end = LocalTime.of(1, 0);
    assertTrue(p.appears(p.endDay));
    Plan daily = sample();
    daily.day = p.endDay;
    daily.start = LocalTime.of(0, 30);
    daily.endDay = daily.day;
    daily.end = LocalTime.of(1, 30);
    daily.validate();
    assertTrue(p.overlaps(daily));
    assertTrue(daily.overlaps(p));
    daily = daily.toggle(daily.day);
    assertFalse(p.overlaps(daily));
    Plan backwards = p.copy();
    backwards.endDay = p.day;
    rejects(backwards::validate);
  }

  @Test
  public void diskPersistenceAtomicImportAndWindowsSchema() throws Exception {
    Context context = InstrumentationRegistry.getInstrumentation().getTargetContext();
    File dir = new File(context.getCacheDir(), "store-test-" + UUID.randomUUID());
    PlanStore store = new PlanStore(dir);
    Plan p = sample().toggle(LocalDate.now().minusDays(1));
    p.start = LocalTime.of(9, 0);
    p.endDay = p.day;
    p.end = LocalTime.of(10, 0);
    store.save(p);
    byte[] backup = store.export();
    JSONObject json = new JSONObject(new String(backup, "UTF-8"));
    assertEquals("09:00:00", json.getJSONArray("plans").getJSONObject(0).getString("start"));
    assertTrue(
        json.getJSONArray("plans").getJSONObject(0).get("durationMinutes") instanceof Number);
    PlanStore reloaded = new PlanStore(dir);
    reloaded.load();
    assertEquals(p.dates, reloaded.all().get(0).dates);
    assertEquals(0, reloaded.merge(new ByteArrayInputStream(backup)));
    Plan newer = p.copy();
    newer.notes = "更新";
    newer.updatedAt = p.updatedAt.plusSeconds(5);
    store.save(newer);
    assertEquals(0, store.merge(new ByteArrayInputStream(backup)));
    assertEquals("更新", store.all().get(0).notes);
    byte[] current = store.export();
    json.getJSONArray("plans").put(json.getJSONArray("plans").getJSONObject(0));
    try {
      store.merge(new ByteArrayInputStream(json.toString().getBytes("UTF-8")));
      fail("Duplicate accepted");
    } catch (IllegalArgumentException expected) {
    }
    assertArrayEquals(current, store.export());
    store.delete(p.id);
    store.save(newer);
    store.restore();
    assertEquals(0, store.all().size());
    // A Windows schema-1 document with +08 offset and optional null fields remains readable.
    Plan ordinary = new Plan();
    ordinary.title = "Windows 旧备份";
    JSONObject win = ordinary.json();
    win.remove("repeatCount");
    win.remove("repeatUntil");
    win.remove("completedDates");
    win.put("createdAt", "2026-10-08T09:00:00+08:00");
    win.put("updatedAt", "2026-10-08T09:00:00+08:00");
    byte[] legacy =
        new JSONObject()
            .put("schemaVersion", 1)
            .put("plans", new JSONArray().put(win))
            .toString()
            .getBytes("UTF-8");
    assertEquals("Windows 旧备份", PlanStore.read(new ByteArrayInputStream(legacy)).get(0).title);
  }

  @Test
  public void formGrowsSavesAndRestoresDraft() throws Exception {
    try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            new EditorForm(a, null, null);
            EditText title =
                inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                    .filter(v -> v.getContentDescription().toString().startsWith("计划内容"))
                    .findFirst()
                    .get();
            int initial = title.getHeight();
            title.setText("第 1 行\n第 2 行\n第 3 行\n第 4 行\n第 5 行\n第 6 行\n第 7 行\n第 8 行");
            title.setTag(initial);
          });
      Thread.sleep(350);
      scenario.onActivity(
          a -> {
            EditText title =
                inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                    .filter(v -> v.getContentDescription().toString().startsWith("计划内容"))
                    .findFirst()
                    .get();
            assertTrue(title.getHeight() >= title.getLineHeight() * 8);
            assertTrue(title.getHeight() > (int) title.getTag());
          });
      scenario.recreate();
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertNotNull(a.editor);
            EditText title =
                inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                    .filter(v -> v.getContentDescription().toString().startsWith("计划内容"))
                    .findFirst()
                    .get();
            assertTrue(title.getText().toString().contains("第 8 行"));
            title.setText("界面保存验证");
            a.editor.save();
          });
      waitReady(scenario);
      Thread.sleep(300);
      scenario.onActivity(
          a -> assertTrue(a.store.all().stream().anyMatch(p -> p.title.equals("界面保存验证"))));
    }
  }

  @Test
  public void screenshotsAndLayout() throws Exception {
    try (ActivityScenario<MainActivity> scenario = ActivityScenario.launch(MainActivity.class)) {
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            try {
              for (Plan p : a.store.all()) a.store.delete(p.id);
              Plan p = sample();
              p.color = Plan.COLORS[2];
              a.store.save(p);
              Plan completed = new Plan();
              completed.title = "整理本周的读书笔记\n为下一步留个方向";
              completed.notes = "保留完成记录，随时可以恢复。";
              completed.day = LocalDate.now();
              completed.color = Plan.COLORS[1];
              completed.durationMinutes = 45;
              completed.completed = true;
              a.store.save(completed);
              Plan simple = new Plan();
              simple.title = "梳理今天的工作安排";
              simple.day = LocalDate.now();
              simple.start = LocalTime.of(9, 0);
              simple.endDay = simple.day;
              simple.end = LocalTime.of(9, 30);
              a.store.save(simple);
              a.mode = 0;
              a.selected = LocalDate.now();
              a.render();
            } catch (Exception e) {
              throw new AssertionError(e);
            }
          });
      Thread.sleep(500);
      capture("android-main.png");
      scenario.onActivity(a -> ((ScrollView) a.body.getParent()).fullScroll(View.FOCUS_DOWN));
      Thread.sleep(350);
      capture("android-completed.png");
      scenario.onActivity(
          a -> {
            checkButtons(a.footer);
            new EditorForm(
                a, a.store.all().stream().filter(Plan::recurring).findFirst().get(), null);
          });
      Thread.sleep(500);
      capture("android-editor.png");
      scenario.onActivity(
          a -> {
            a.editor.dialog.dismiss();
            a.mode = 2;
            a.render();
          });
      Thread.sleep(350);
      capture("android-long-term.png");
      scenario.onActivity(
          a -> {
            a.pickDate(a.selected, date -> {});
          });
      Thread.sleep(350);
      capture("android-calendar.png");
      InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(KeyEvent.KEYCODE_BACK);
      scenario.onActivity(
          a -> {
            new EditorForm(a, null, null);
            EditText title =
                inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                    .filter(v -> v.getContentDescription().toString().startsWith("计划内容"))
                    .findFirst()
                    .get();
            title.setText("第 1 行\n第 2 行\n第 3 行\n第 4 行\n第 5 行\n第 6 行\n第 7 行\n第 8 行");
            title.setSelection(title.length());
            title.requestFocus();
            title.postDelayed(
                () ->
                    ((InputMethodManager) a.getSystemService(Context.INPUT_METHOD_SERVICE))
                        .showSoftInput(title, InputMethodManager.SHOW_IMPLICIT),
                350);
          });
      waitKeyboard(scenario);
      scenario.onActivity(
          a -> {
            View decor = a.editor.dialog.getWindow().getDecorView();
            if (Build.VERSION.SDK_INT >= 30)
              assertTrue(
                  "Keyboard did not open",
                  decor.getRootWindowInsets().isVisible(WindowInsets.Type.ime()));
            else {
              Rect area = new Rect();
              decor.getWindowVisibleDisplayFrame(area);
              assertTrue(
                  "Keyboard did not open",
                  area.bottom < a.getResources().getDisplayMetrics().heightPixels - a.dp(150));
            }
            checkSaveVisible(a);
          });
      capture("android-keyboard.png");
      scenario.onActivity(
          a -> {
            ((InputMethodManager) a.getSystemService(Context.INPUT_METHOD_SERVICE))
                .hideSoftInputFromWindow(
                    a.editor.dialog.getWindow().getDecorView().getWindowToken(), 0);
            a.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE);
          });
      Thread.sleep(1500);
      waitReady(scenario);
      scenario.onActivity(
          a -> {
            assertNotNull(a.editor);
            assertTrue(
                inputs(a.editor.dialog.getWindow().getDecorView()).stream()
                    .anyMatch(v -> v.getText().toString().contains("第 8 行")));
            checkSaveVisible(a);
          });
      capture("android-landscape.png");
    }
  }

  private void waitReady(ActivityScenario<MainActivity> scenario) throws Exception {
    for (int i = 0; i < 100; i++) {
      boolean[] done = {false};
      scenario.onActivity(a -> done[0] = a.ready && !a.busy);
      if (done[0]) return;
      Thread.sleep(100);
    }
    fail("Activity did not finish loading/saving");
  }

  private void waitKeyboard(ActivityScenario<MainActivity> scenario) throws Exception {
    long end = SystemClock.elapsedRealtime() + 8000;
    boolean[] visible = {false};
    while (SystemClock.elapsedRealtime() < end) {
      scenario.onActivity(
          a -> {
            View decor = a.editor.dialog.getWindow().getDecorView();
            EditText title =
                inputs(decor).stream()
                    .filter(v -> v.getContentDescription().toString().startsWith("计划内容"))
                    .findFirst()
                    .get();
            if (decor.hasWindowFocus()) {
              title.requestFocus();
              if (Build.VERSION.SDK_INT >= 30 && title.getWindowInsetsController() != null)
                title.getWindowInsetsController().show(WindowInsets.Type.ime());
              else
                ((InputMethodManager) a.getSystemService(Context.INPUT_METHOD_SERVICE))
                    .showSoftInput(title, InputMethodManager.SHOW_IMPLICIT);
            }
            if (Build.VERSION.SDK_INT >= 30)
              visible[0] =
                  decor.getRootWindowInsets() != null
                      && decor.getRootWindowInsets().isVisible(WindowInsets.Type.ime());
            else {
              Rect area = new Rect();
              decor.getWindowVisibleDisplayFrame(area);
              visible[0] =
                  area.bottom < a.getResources().getDisplayMetrics().heightPixels - a.dp(150);
            }
          });
      if (visible[0]) {
        SystemClock.sleep(400);
        return;
      }
      SystemClock.sleep(100);
    }
    capture("keyboard-failure.png");
    fail("Keyboard did not open");
  }

  private List<EditText> inputs(View v) {
    List<EditText> out = new ArrayList<>();
    if (v instanceof EditText) out.add((EditText) v);
    if (v instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++)
        out.addAll(inputs(((ViewGroup) v).getChildAt(i)));
    return out;
  }

  private void checkButtons(View v) {
    if (v instanceof Button && v.getVisibility() == View.VISIBLE)
      assertTrue(
          "Touch target too short",
          v.getHeight() >= Math.round(48 * v.getResources().getDisplayMetrics().density));
    if (v instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++)
        checkButtons(((ViewGroup) v).getChildAt(i));
  }

  private void capture(String name) throws Exception {
    Instrumentation inst = InstrumentationRegistry.getInstrumentation();
    inst.waitForIdleSync();
    Bitmap bitmap = inst.getUiAutomation().takeScreenshot();
    assertNotNull(bitmap);
    File dir = new File(inst.getTargetContext().getExternalFilesDir(null), "screenshots");
    dir.mkdirs();
    if (inst.getTargetContext().getResources().getConfiguration().fontScale > 1.5f)
      name = "large-text-" + name;
    try (FileOutputStream out = new FileOutputStream(new File(dir, name))) {
      assertTrue(bitmap.compress(Bitmap.CompressFormat.PNG, 100, out));
    }
    bitmap.recycle();
  }

  private Button findButton(View v, String text) {
    if (v instanceof Button && ((Button) v).getText().toString().equals(text)) return (Button) v;
    if (v instanceof ViewGroup)
      for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++) {
        Button b = findButton(((ViewGroup) v).getChildAt(i), text);
        if (b != null) return b;
      }
    return null;
  }

  private void checkSaveVisible(MainActivity a) {
    View decor = a.editor.dialog.getWindow().getDecorView();
    Button save = findButton(decor, "保存计划");
    assertNotNull(save);
    Rect visible = new Rect(), bounds = new Rect();
    decor.getWindowVisibleDisplayFrame(visible);
    assertTrue(save.getGlobalVisibleRect(bounds));
    assertTrue("Save button clipped", bounds.height() == save.getHeight());
    assertTrue("Keyboard covers save", bounds.bottom <= visible.bottom);
  }
}
