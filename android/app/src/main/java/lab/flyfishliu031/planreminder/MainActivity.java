package lab.flyfishliu031.planreminder;

import android.app.*;
import android.content.*;
import android.graphics.*;
import android.graphics.drawable.*;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.io.*;
import java.time.*;
import java.time.format.DateTimeFormatter;
import java.util.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity {
  static final int CANVAS = Color.rgb(243, 246, 245),
      INK = Color.rgb(32, 51, 47),
      MUTED = Color.rgb(99, 115, 110),
      ACCENT = Color.rgb(20, 108, 96),
      SOFT = Color.rgb(231, 242, 239),
      LINE = Color.rgb(222, 231, 227),
      DANGER = Color.rgb(172, 65, 75);
  final Handler handler = new Handler(Looper.getMainLooper());
  static final ExecutorService io = Executors.newSingleThreadExecutor();
  private static PlanStore sharedStore;

  static synchronized PlanStore appStore(Context c) {
    if (sharedStore == null) sharedStore = new PlanStore(c.getFilesDir());
    return sharedStore;
  }

  PlanStore store;
  LocalDate selected = LocalDate.now();
  int mode; // 0: selected date, 1: unscheduled, 2: long-term overview, 3: timetable.
  DailyTimelineView timeline;
  ScrollView mainScroll;
  boolean ready, busy;
  LinearLayout root, body, list, footer;
  TextView clock, heading;
  Button dateButton;
  private Plan deleted;
  private long undoUntil;
  EditorForm editor;
  private Bundle draft;
  private final Runnable tick =
      new Runnable() {
        public void run() {
          if (clock != null)
            clock.setText(
                ZonedDateTime.now()
                    .format(DateTimeFormatter.ofPattern("MM月dd日 EEEE · HH:mm", Locale.CHINA)));
          if (ready && editor == null && !LocalDate.now().equals(lastToday)) render();
          if (timeline != null) timeline.invalidate();
          lastToday = LocalDate.now();
          handler.postDelayed(this, 30000);
        }
      };
  private LocalDate lastToday = LocalDate.now();

  @Override
  public void onCreate(Bundle state) {
    super.onCreate(state);
    if (state != null) {
      selected = LocalDate.parse(state.getString("selected", LocalDate.now().toString()));
      mode = state.getInt("mode");
      draft = state.getBundle("draft");
    }
    store = appStore(this);
    root = column();
    root.setBackgroundColor(CANVAS);
    inset(root, getWindow());
    setContentView(root);
    ScrollView scroll = new ScrollView(this);
    mainScroll = scroll;
    scroll.setFillViewport(true);
    body = column();
    body.setPadding(dp(20), dp(20), dp(20), dp(16));
    scroll.addView(body);
    root.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
    footer = column();
    footer.setPadding(dp(20), dp(12), dp(20), dp(12));
    root.addView(footer);
    body.addView(text("正在读取计划…", 18, INK));
    io.execute(
        () -> {
          try {
            store.load();
            AlarmScheduler.sync(this, store);
            runOnUiThread(
                () -> {
                  if (isDestroyed() || isFinishing()) return;
                  ready = true;
                  render();
                  if (draft != null) {
                    Bundle saved = draft;
                    draft = null;
                    EditorForm.restore(this, saved);
                  }
                });
          } catch (Exception e) {
            runOnUiThread(
                () ->
                    new AlertDialog.Builder(this)
                        .setTitle("计划文件暂时无法读取")
                        .setMessage(
                            "为保护原有数据，暂未开放编辑。"
                                + message(e)
                                + (store.hasPrevious()
                                    ? "\n可以恢复上一次保存，并保留原文件。"
                                    : "\n请保留应用数据并联系项目维护者。"))
                        .setPositiveButton(
                            store.hasPrevious() ? "恢复上次保存" : "关闭",
                            (d, w) -> {
                              if (!store.hasPrevious()) finish();
                              else
                                perform(
                                    () -> {
                                      store.restore();
                                      ready = true;
                                    },
                                    null);
                            })
                        .setNegativeButton("关闭", (d, w) -> finish())
                        .setCancelable(false)
                        .show());
          }
        });
  }

  @Override
  protected void onResume() {
    super.onResume();
    handler.removeCallbacks(tick);
    handler.post(tick);
    if (ready) {
      render();
      io.execute(() -> AlarmScheduler.sync(this, store));
    }
  }

  @Override
  protected void onPause() {
    super.onPause();
    handler.removeCallbacks(tick);
  }

  @Override
  protected void onDestroy() {
    handler.removeCallbacksAndMessages(null);
    if (editor != null) editor.dialog.dismiss();
    super.onDestroy();
  }

  @Override
  protected void onSaveInstanceState(Bundle state) {
    state.putString("selected", selected.toString());
    state.putInt("mode", mode);
    if (editor != null) state.putBundle("draft", editor.snapshot());
    super.onSaveInstanceState(state);
  }

  void render() {
    if (isDestroyed() || !ready) return;
    body.removeAllViews();
    timeline = null;
    footer.removeAllViews();
    LinearLayout top = row();
    TextView brand = text("计划表", 30, INK);
    brand.setTypeface(null, Typeface.BOLD);
    top.addView(brand, new LinearLayout.LayoutParams(0, -2, 1));
    Button settings = button("设置", false);
    settings.setOnClickListener(v -> settings());
    top.addView(settings);
    body.addView(top);
    if (store.all().stream()
            .anyMatch(p -> p.alarm != null && !p.completed && !p.finished(LocalDate.now()))
        && (!AlarmScheduler.exactAllowed(this) || !AlarmScheduler.notificationsAllowed(this))) {
      Button warning = button("闹钟尚未启用 · 点此允许闹钟和通知权限", false);
      warning.setTextColor(DANGER);
      warning.setOnClickListener(v -> AlarmScheduler.permissions(this));
      add(body, warning, 12, 4);
    }
    clock =
        text(
            ZonedDateTime.now()
                .format(DateTimeFormatter.ofPattern("MM月dd日 EEEE · HH:mm", Locale.CHINA)),
            14,
            MUTED);
    add(body, clock, 4, 16);
    LinearLayout dateRow = row();
    dateButton =
        button(selected.format(DateTimeFormatter.ofPattern("yyyy年M月d日 EEEE", Locale.CHINA)), false);
    dateButton.setContentDescription("选择日历日期：" + dateButton.getText());
    dateButton.setOnClickListener(
        v ->
            pickDate(
                selected,
                date -> {
                  selected = date;
                  if (mode != 3) mode = 0;
                  render();
                }));
    dateRow.addView(dateButton, new LinearLayout.LayoutParams(0, -2, 1));
    Button today = button("今天", false);
    today.setOnClickListener(
        v -> {
          selected = LocalDate.now();
          if (mode != 3) mode = 0;
          render();
        });
    dateRow.addView(today);
    add(body, dateRow, 0, 12);
    LinearLayout tabs = row();
    String[] labels = {"当天计划", "时间表", "待安排", "长期计划"};
    int[] modes = {0, 3, 1, 2};
    for (int i = 0; i < 4; i++) {
      if (i == 2) {
        add(body, tabs, 0, 8);
        tabs = row();
      }
      final int index = modes[i];
      Button b = button(labels[i], mode == index);
      b.setOnClickListener(
          v -> {
            mode = index;
            render();
          });
      LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, -2, 1);
      if (i % 2 == 0) lp.setMarginEnd(dp(8));
      tabs.addView(b, lp);
    }
    add(body, tabs, 0, 24);
    List<Plan> plans = store.all();
    plans.removeIf(
        p ->
            mode == 0 || mode == 3
                ? !p.appears(selected)
                : mode == 1 ? p.day != null : !p.recurring());
    plans.sort(
        Comparator.comparing(
                (Plan p) -> mode == 2 ? p.day : p.recurring() ? selected : p.day,
                Comparator.nullsLast(Comparator.naturalOrder()))
            .thenComparing(p -> p.start, Comparator.nullsLast(Comparator.naturalOrder()))
            .thenComparing(p -> p.createdAt));
    heading =
        text(
            (mode == 3 ? "全天时间表" : mode == 0 ? "这一天的安排" : mode == 1 ? "留给未来的想法" : "每天，一点进步")
                + " · "
                + plans.size(),
            18,
            INK);
    heading.setTypeface(null, Typeface.BOLD);
    add(body, heading, 0, 12);
    list = column();
    body.addView(list);
    if (mode == 3) renderTimeline(plans);
    else if (plans.isEmpty()) {
      LinearLayout empty = column();
      empty.setPadding(dp(24), dp(28), dp(24), dp(28));
      empty.setBackground(surface(Color.WHITE, LINE));
      empty.addView(text("暂时没有计划", 20, INK));
      add(empty, text(mode == 2 ? "建立每天重复的计划，记录每一天的进步。" : "添加一件想做的事，也可以先不安排时间。", 16, MUTED), 12, 0);
      list.addView(empty);
    }
    if (mode != 3)
      for (Plan p : plans) {
        if (mode == 0 && p.start != null)
          for (DaySegment s : p.segments(selected))
            add(list, card(p, s.occurrence, () -> {}), 0, 12);
        else add(list, card(p), 0, 12);
      }
    if (deleted != null && SystemClock.elapsedRealtime() < undoUntil) {
      LinearLayout undo = row();
      TextView info = text("计划已删除", 14, MUTED);
      undo.addView(info, new LinearLayout.LayoutParams(0, -2, 1));
      Button b = button("撤销", false);
      Plan restore = deleted;
      b.setEnabled(!busy);
      b.setOnClickListener(
          v -> {
            if (SystemClock.elapsedRealtime() >= undoUntil) {
              deleted = null;
              render();
              toast("撤销时间已过，可从先前备份恢复。");
            } else perform(() -> store.save(restore), () -> deleted = null);
          });
      undo.addView(b);
      footer.addView(undo);
    }
    Button add = button("＋ 添加计划", true);
    add.setEnabled(!busy);
    add.setOnClickListener(
        v -> {
          if (mode == 3) addTimelinePlan(540, 600);
          else new EditorForm(this, null, null);
        });
    footer.addView(add, new LinearLayout.LayoutParams(-1, -2));
    root.setEnabled(!busy);
  }

  private View card(Plan p) {
    LocalDate date = mode == 2 ? LocalDate.now() : selected;
    return card(p, date, () -> {});
  }

  private View card(Plan p, LocalDate date, Runnable dismiss) {
    boolean ended = mode == 2 && p.finished(LocalDate.now()), done = ended || p.checked(date);
    LinearLayout card = column();
    card.setPadding(dp(16), dp(16), dp(16), dp(8));
    card.setBackground(surface(done ? SOFT : Color.WHITE, done ? ACCENT : LINE));
    LinearLayout titleRow = row();
    CheckBox check = new CheckBox(this);
    check.setMinWidth(dp(48));
    check.setMinimumHeight(dp(48));
    check.setChecked(done);
    check.setEnabled(
        !busy && (!p.recurring() || !ended && !date.isAfter(LocalDate.now()) && p.occurs(date)));
    check.setContentDescription(
        (done ? "恢复计划：" : "完成计划：") + p.title + (p.recurring() ? "，" + date : ""));
    check.setOnCheckedChangeListener(
        (b, c) -> {
          if (!busy) {
            dismiss.run();
            perform(() -> store.save(p.toggle(date)), null);
          }
        });
    titleRow.addView(check);
    CompletedText title = new CompletedText(this, done);
    title.setText(p.title);
    title.setTextSize(18);
    title.setTypeface(null, Typeface.BOLD);
    title.setTextColor(Color.parseColor(p.color));
    title.setLineSpacing(dp(4), 1);
    title.setContentDescription(p.title + (done ? "，" + (ended ? "长期计划已结束" : "已完成") : "，待完成"));
    titleRow.addView(title, new LinearLayout.LayoutParams(0, -2, 1));
    card.addView(titleRow);
    if (done) {
      TextView badge =
          text(
              ended ? p.repeatUntil == null ? "已完成目标" : "已结束" : p.recurring() ? "当天已完成" : "已完成",
              14,
              ACCENT);
      badge.setTypeface(null, Typeface.BOLD);
      add(card, badge, 8, 4);
    }
    add(card, text(p.timing(), 14, MUTED), 8, 0);
    if (p.recurring()) add(card, text("本次归属 " + date + "，按开始日期记录完成", 14, MUTED), 6, 0);
    if (p.recurring())
      add(
          card,
          text(
              p.progress()
                  + (p.finished(LocalDate.now())
                      ? " · 已结束"
                      : LocalDate.now().isBefore(p.day) ? " · 尚未开始" : " · 进行中"),
              14,
              MUTED),
          6,
          0);
    if (!p.notes.isBlank()) add(card, text(p.notes, 16, INK), 12, 0);
    LinearLayout actions = row();
    actions.setGravity(Gravity.END);
    if (p.recurring()) {
      Button history = button("记录", false);
      history.setEnabled(!busy);
      history.setOnClickListener(v -> history(p));
      actions.addView(history, new LinearLayout.LayoutParams(0, -2, 1));
    }
    Button edit = button("编辑", false);
    edit.setEnabled(!busy);
    edit.setOnClickListener(
        v -> {
          dismiss.run();
          new EditorForm(this, p, null);
        });
    actions.addView(edit, new LinearLayout.LayoutParams(0, -2, 1));
    Button delete = button("删除", false);
    delete.setTextColor(DANGER);
    delete.setEnabled(!busy);
    delete.setOnClickListener(
        v -> {
          dismiss.run();
          new AlertDialog.Builder(this)
              .setTitle(p.recurring() ? "删除整个长期计划？" : "删除计划？")
              .setMessage(p.recurring() ? "将同时删除全部完成记录，删除后 10 秒内可撤销。" : "删除后 10 秒内可撤销。")
              .setNegativeButton("取消", null)
              .setPositiveButton(
                  "删除",
                  (d, w) ->
                      perform(
                          () -> store.delete(p.id),
                          () -> {
                            deleted = p;
                            undoUntil = SystemClock.elapsedRealtime() + 10000;
                            handler.postDelayed(
                                () -> {
                                  if (editor == null) render();
                                },
                                10050);
                          }))
              .show();
        });
    actions.addView(delete, new LinearLayout.LayoutParams(0, -2, 1));
    add(card, actions, 8, 0);
    return card;
  }

  Plan scheduleSeed(int from, int to) {
    Plan p = new Plan();
    p.day = selected;
    p.start = LocalTime.of(from / 60, from % 60);
    p.endDay = selected.plusDays(to == 1440 ? 1 : 0);
    p.end = LocalTime.of((to % 1440) / 60, to % 60);
    p.color = Plan.COLORS[store.all().size() % Plan.COLORS.length];
    for (String color : Plan.COLORS)
      if (store.all().stream().noneMatch(plan -> plan.color.equalsIgnoreCase(color))) {
        p.color = color;
        break;
      }
    return p;
  }

  void addTimelinePlan(int from, int to) {
    if (!busy) new EditorForm(this, scheduleSeed(from, to), null, true);
  }

  void openTimelinePlan(Plan p, LocalDate occurrence) {
    if (busy) return;
    AlertDialog dialog =
        new AlertDialog.Builder(this)
            .setTitle("计划详情 · " + occurrence)
            .setNegativeButton("关闭", null)
            .create();
    ScrollView scroll = new ScrollView(this);
    scroll.addView(card(p, occurrence, dialog::dismiss));
    dialog.setView(scroll);
    dialog.show();
  }

  private void renderTimeline(List<Plan> plans) {
    LinearLayout nav = row();
    Button previous = button("前一天", false), next = button("后一天", false);
    previous.setEnabled(selected.isAfter(LocalDate.of(1753, 1, 1)));
    next.setEnabled(selected.isBefore(LocalDate.of(9998, 12, 31)));
    previous.setOnClickListener(
        v -> {
          selected = selected.minusDays(1);
          render();
        });
    next.setOnClickListener(
        v -> {
          selected = selected.plusDays(1);
          render();
        });
    nav.addView(previous, new LinearLayout.LayoutParams(0, -2, 1));
    nav.addView(next, new LinearLayout.LayoutParams(0, -2, 1));
    add(list, nav, 0, 8);
    add(list, text("先点空白处选择开始，再点结束；也可点下方添加按钮输入时间。", 14, MUTED), 0, 12);
    timeline = new DailyTimelineView(this, selected, plans);
    if (!timeline.segments.isEmpty()) {
      LinearLayout selection = row();
      Spinner picker = new Spinner(this);
      String[] titles = timeline.segments.stream().map(DaySegment::caption).toArray(String[]::new);
      ArrayAdapter<String> adapter =
          new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, titles);
      adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
      picker.setAdapter(adapter);
      picker.setMinimumHeight(dp(48));
      picker.setContentDescription("时间表中的计划，短计划也可在这里选择");
      selection.addView(picker, new LinearLayout.LayoutParams(0, -2, 1));
      Button open = button("查看", false);
      java.util.List<DaySegment> segments = timeline.segments;
      open.setOnClickListener(
          v -> {
            DaySegment s = segments.get(picker.getSelectedItemPosition());
            openTimelinePlan(s.plan, s.occurrence);
          });
      selection.addView(open);
      add(list, selection, 0, 12);
    }
    Button now = button("定位当前时段", false);
    DailyTimelineView view = timeline;
    now.setOnClickListener(
        v -> {
          int minute =
              selected.equals(LocalDate.now())
                  ? Math.max(0, LocalTime.now().getHour() * 60 - 60)
                  : 480;
          mainScroll.smoothScrollTo(
              0, body.getTop() + list.getTop() + view.getTop() + (int) (minute * view.hour / 60));
        });
    add(list, now, 0, 12);
    java.util.List<Plan> untimed = new ArrayList<>();
    for (Plan p : plans) if (p.start == null) untimed.add(p);
    Button pending = button("未安排时间 · " + untimed.size(), false);
    pending.setEnabled(!untimed.isEmpty());
    pending.setOnClickListener(
        v ->
            new AlertDialog.Builder(this)
                .setTitle("当天未安排时间的计划")
                .setItems(
                    untimed.stream().map(p -> p.title).toArray(String[]::new),
                    (d, w) -> openTimelinePlan(untimed.get(w), selected))
                .setNegativeButton("关闭", null)
                .show());
    add(list, pending, 0, 12);
    add(list, timeline, 0, 12);
  }

  private void history(Plan p) {
    String[] records =
        p.dates.descendingSet().stream().map(LocalDate::toString).toArray(String[]::new);
    new AlertDialog.Builder(this)
        .setTitle("完成记录 · " + records.length + " 天")
        .setItems(
            records,
            (d, w) -> {
              selected = LocalDate.parse(records[w]);
              mode = 0;
              render();
            })
        .setPositiveButton("关闭", null)
        .show();
    if (records.length == 0)
      Toast.makeText(this, "暂无完成记录；在日历中选择已到来的日期后勾选。", Toast.LENGTH_LONG).show();
  }

  private void settings() {
    new AlertDialog.Builder(this)
        .setTitle("设置与备份")
        .setItems(
            new String[] {"导出计划备份", "导入计划备份", "关于计划表", "闹钟权限与通知"},
            (d, w) -> {
              if (w == 3) AlarmScheduler.permissions(this);
              else if (w == 2)
                new AlertDialog.Builder(this)
                    .setTitle("计划表 · v1.6.0")
                    .setMessage(
                        "离线保存于本机，无需账号。\n\n"
                            + "每天重复的长期计划可按持续天数或完成次数结束。通过 JSON 备份与 Windows 版手动迁移；两台设备不会自动同步。\n\n"
                            + "卸载前请先导出备份。")
                    .setPositiveButton("知道了", null)
                    .show();
              else if (!busy) {
                Intent intent =
                    new Intent(w == 0 ? Intent.ACTION_CREATE_DOCUMENT : Intent.ACTION_OPEN_DOCUMENT)
                        .addCategory(Intent.CATEGORY_OPENABLE)
                        .setType(w == 0 ? "application/json" : "*/*");
                if (w == 0)
                  intent.putExtra(Intent.EXTRA_TITLE, "计划表备份-" + LocalDate.now() + ".json");
                startActivityForResult(intent, w == 0 ? 10 : 11);
              }
            })
        .setNegativeButton("关闭", null)
        .show();
  }

  @Override
  protected void onActivityResult(int request, int result, Intent data) {
    super.onActivityResult(request, result, data);
    if (request == 30) {
      if (result == RESULT_OK && data != null && editor != null) editor.ringtoneResult(data);
      return;
    }
    if (result != RESULT_OK || data == null || data.getData() == null) return;
    if (request == 10)
      perform(
          () -> {
            byte[] bytes = store.export();
            try (OutputStream out = getContentResolver().openOutputStream(data.getData(), "wt")) {
              if (out == null) throw new IOException("无法写入所选文件。");
              out.write(bytes);
            }
          },
          () -> toast("备份已导出"));
    if (request == 11)
      new AlertDialog.Builder(this)
          .setTitle("导入备份？")
          .setMessage("相同计划保留较新记录，其余计划合并。现有计划不会被清空。")
          .setNegativeButton("取消", null)
          .setPositiveButton(
              "导入",
              (d, w) ->
                  perform(
                      () -> {
                        try (InputStream in =
                            getContentResolver().openInputStream(data.getData())) {
                          if (in == null) throw new IOException("无法读取备份。");
                          int count = store.merge(in);
                          runOnUiThread(() -> toast("已导入 " + count + " 项更新"));
                        }
                      },
                      null))
          .show();
  }

  @Override
  public void onRequestPermissionsResult(int request, String[] permissions, int[] grants) {
    super.onRequestPermissionsResult(request, permissions, grants);
    if (request == 20 && ready) {
      io.execute(() -> AlarmScheduler.sync(this, store));
      render();
    }
  }

  interface Work {
    void run() throws Exception;
  }

  void perform(Work work, Runnable done) {
    if (busy || isDestroyed()) return;
    busy = true;
    render();
    io.execute(
        () -> {
          try {
            work.run();
            AlarmScheduler.sync(this, store);
            runOnUiThread(
                () -> {
                  if (isDestroyed()) return;
                  busy = false;
                  if (done != null) done.run();
                  render();
                });
          } catch (Exception e) {
            runOnUiThread(
                () -> {
                  if (isDestroyed()) return;
                  busy = false;
                  render();
                  error(message(e));
                });
          }
        });
  }

  void error(String message) {
    new AlertDialog.Builder(this)
        .setTitle("暂时无法完成")
        .setMessage(message)
        .setPositiveButton("知道了", null)
        .show();
  }

  static String message(Exception e) {
    String msg = e.getMessage();
    return msg == null ? "请检查文件格式或设备剩余空间。" : msg;
  }

  void toast(String message) {
    Toast.makeText(this, message, Toast.LENGTH_LONG).show();
  }

  int dp(int n) {
    return Math.round(n * getResources().getDisplayMetrics().density);
  }

  LinearLayout column() {
    LinearLayout l = new LinearLayout(this);
    l.setOrientation(LinearLayout.VERTICAL);
    return l;
  }

  LinearLayout row() {
    LinearLayout l = new LinearLayout(this);
    l.setOrientation(LinearLayout.HORIZONTAL);
    l.setGravity(Gravity.CENTER_VERTICAL);
    return l;
  }

  TextView text(String value, int size, int color) {
    TextView t = new TextView(this);
    t.setText(value);
    t.setTextSize(size);
    t.setTextColor(color);
    t.setLineSpacing(dp(3), 1);
    return t;
  }

  Button button(String label, boolean primary) {
    Button b = new Button(this);
    b.setText(label);
    b.setTextSize(15);
    b.setAllCaps(false);
    b.setSingleLine(false);
    b.setTextColor(primary ? Color.WHITE : INK);
    b.setMinWidth(dp(48));
    b.setMinimumWidth(dp(48));
    b.setMinHeight(dp(48));
    b.setMinimumHeight(dp(48));
    b.setPadding(dp(12), dp(8), dp(12), dp(8));
    b.setBackground(
        new RippleDrawable(
            android.content.res.ColorStateList.valueOf(primary ? 0x3366CCB4 : 0x22146C60),
            surface(primary ? ACCENT : Color.WHITE, primary ? ACCENT : LINE),
            null));
    return b;
  }

  GradientDrawable surface(int color, int border) {
    GradientDrawable d = new GradientDrawable();
    d.setColor(color);
    d.setCornerRadius(dp(12));
    d.setStroke(dp(1), border);
    return d;
  }

  void add(LinearLayout parent, View view, int top, int bottom) {
    LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, -2);
    p.topMargin = dp(top);
    p.bottomMargin = dp(bottom);
    parent.addView(view, p);
  }

  interface DateChosen {
    void accept(LocalDate date);
  }

  void pickDate(LocalDate value, DateChosen chosen) {
    DatePickerDialog picker =
        new DatePickerDialog(
            this,
            (v, y, m, d) -> chosen.accept(LocalDate.of(y, m + 1, d)),
            value.getYear(),
            value.getMonthValue() - 1,
            value.getDayOfMonth());
    picker
        .getDatePicker()
        .setMinDate(
            LocalDate.of(1753, 1, 1)
                .atStartOfDay(ZoneId.systemDefault())
                .toInstant()
                .toEpochMilli());
    picker
        .getDatePicker()
        .setMaxDate(
            LocalDate.of(9998, 12, 31)
                .atStartOfDay(ZoneId.systemDefault())
                .toInstant()
                .toEpochMilli());
    picker.show();
  }

  void inset(View view, Window window) {
    if (Build.VERSION.SDK_INT >= 30) window.setDecorFitsSystemWindows(false);
    view.setOnApplyWindowInsetsListener(
        (v, insets) -> {
          if (Build.VERSION.SDK_INT >= 30) {
            Insets bars =
                insets.getInsets(
                    WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
            Insets ime = insets.getInsets(WindowInsets.Type.ime());
            v.setPadding(bars.left, bars.top, bars.right, Math.max(bars.bottom, ime.bottom));
          } else
            v.setPadding(
                insets.getStableInsetLeft(),
                insets.getStableInsetTop(),
                insets.getStableInsetRight(),
                insets.getStableInsetBottom());
          return insets;
        });
    view.requestApplyInsets();
  }

  static final class CompletedText extends TextView {
    private final boolean marked;
    private final Paint line = new Paint(Paint.ANTI_ALIAS_FLAG);

    CompletedText(Context context, boolean marked) {
      super(context);
      this.marked = marked;
    }

    @Override
    protected void onDraw(Canvas canvas) {
      super.onDraw(canvas);
      if (!marked || getLayout() == null) return;
      line.setColor(getCurrentTextColor());
      line.setStrokeWidth(3 * getResources().getDisplayMetrics().density);
      canvas.save();
      canvas.translate(getTotalPaddingLeft(), getTotalPaddingTop());
      for (int i = 0; i < getLayout().getLineCount(); i++) {
        float y = getLayout().getLineBaseline(i) + getPaint().ascent() * .38f;
        canvas.drawLine(getLayout().getLineLeft(i), y, getLayout().getLineRight(i), y, line);
      }
      canvas.restore();
    }
  }
}
