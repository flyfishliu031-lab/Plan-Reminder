package lab.flyfishliu031.planreminder;

import android.app.*;
import android.content.Intent;
import android.graphics.Color;
import android.media.RingtoneManager;
import android.net.Uri;
import android.os.*;
import android.text.*;
import android.view.*;
import android.view.inputmethod.EditorInfo;
import android.widget.*;
import java.time.*;
import java.time.temporal.ChronoUnit;
import org.json.*;

final class EditorForm {
  private final MainActivity a;
  final Dialog dialog;
  private final Plan original;
  private final EditText title, notes, duration, goal;
  private final CheckBox dated, scheduled, expected, repeated;
  private final RadioButton days, times;
  private final LinearLayout dateFields, timeFields, durationFields, repeatFields;
  private final Button dayButton, endDayButton, startButton, endButton, colorButton, save, cancel;
  private final TextView error;
  private LocalDate day, endDay;
  private LocalTime start, end;
  private String color;
  private final CheckBox alarmEnabled, alarmSound, alarmVibrate, restartAlarm;
  private final RadioButton alarmAfter, alarmStart, alarmEnd;
  private final LinearLayout alarmFields, alarmAfterFields;
  private final EditText alarmMinutes;
  private final Button ringtoneButton;
  private String ringtone;
  private final java.util.Map<String, LinearLayout> sections = new java.util.LinkedHashMap<>();
  private final java.util.Map<String, Button> sectionButtons = new java.util.LinkedHashMap<>();
  private final TextView preview, goalLabel, repeatSummary;
  private final Button permissions, setupTime;
  private final ScrollView bodyScroll;
  private final String draftId;
  private final Instant draftCreated;
  private String pendingRevision;

  EditorForm(MainActivity activity, Plan plan, Bundle state) {
    this(activity, plan, state, false);
  }

  EditorForm(MainActivity activity, Plan plan, Bundle state, boolean isNew) {
    a = activity;
    original = isNew ? null : plan;
    draftId =
        original != null
            ? original.id
            : state != null
                ? state.getString("draftId", java.util.UUID.randomUUID().toString())
                : plan != null ? plan.id : java.util.UUID.randomUUID().toString();
    draftCreated =
        original != null
            ? original.createdAt
            : state != null && state.containsKey("draftCreated")
                ? Instant.parse(state.getString("draftCreated"))
                : Instant.now();
    a.editor = this;
    LocalDate today = LocalDate.now();
    day = plan != null && plan.day != null ? plan.day : a.mode == 2 ? today : a.selected;
    endDay = plan != null && plan.endDay != null ? plan.endDay : day;
    start = plan != null && plan.start != null ? plan.start : LocalTime.of(9, 0);
    end = plan != null && plan.end != null ? plan.end : LocalTime.of(10, 0);
    color = plan != null ? plan.color : Plan.COLORS[a.store.all().size() % Plan.COLORS.length];
    for (String candidate : Plan.COLORS)
      if (plan == null
          && a.store.all().stream().noneMatch(p -> p.color.equalsIgnoreCase(candidate))) {
        color = candidate;
        break;
      }
    dialog = new Dialog(a);
    dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
    dialog.setCanceledOnTouchOutside(false);
    LinearLayout root = a.column();
    root.setBackgroundColor(MainActivity.CANVAS);
    dialog.setContentView(root);
    LinearLayout header = a.column();
    header.setPadding(a.dp(20), a.dp(16), a.dp(20), a.dp(12));
    header.addView(a.text(original == null ? "添加计划" : "编辑计划", 22, MainActivity.INK));
    preview = a.text("", 14, MainActivity.MUTED);
    preview.setMaxLines(2);
    preview.setEllipsize(TextUtils.TruncateAt.END);
    a.add(header, preview, 6, 0);
    root.addView(header);
    ScrollView scroll = new ScrollView(a);
    bodyScroll = scroll;
    scroll.setFillViewport(true);
    LinearLayout form = a.column();
    form.setPadding(a.dp(20), a.dp(8), a.dp(20), a.dp(24));
    scroll.addView(form);
    root.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
    title = input("计划内容（必填）", plan == null ? "" : plan.title, 500, false);
    title.setMinLines(2);
    title.setInputType(
        InputType.TYPE_CLASS_TEXT
            | InputType.TYPE_TEXT_FLAG_MULTI_LINE
            | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
    title.setGravity(Gravity.TOP);
    field(form, "计划内容", title);
    int notesStart = form.getChildCount();
    notes = input("备注（可选）", plan == null ? "" : plan.notes, 5000, false);
    notes.setMinLines(2);
    notes.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE);
    notes.setGravity(Gravity.TOP);
    field(form, "备注 · 可选", notes);
    int repeatStart = form.getChildCount();
    repeated =
        checkbox("设为长期计划（每天重复）", plan != null && plan.recurring() || plan == null && a.mode == 2);
    a.add(form, repeated, 8, 0);
    repeatFields = a.column();
    days = new RadioButton(a);
    days.setText("持续天数");
    times = new RadioButton(a);
    times.setText("完成次数");
    days.setMinHeight(a.dp(48));
    times.setMinHeight(a.dp(48));
    RadioGroup types = new RadioGroup(a);
    types.setOrientation(RadioGroup.VERTICAL);
    days.setId(View.generateViewId());
    times.setId(View.generateViewId());
    types.addView(days);
    types.addView(times);
    types.check(
        plan != null && plan.repeatCount != null || plan == null && a.mode == 2
            ? times.getId()
            : days.getId());
    repeatFields.addView(types);
    int defaultGoal =
        plan == null
            ? 30
            : plan.repeatCount != null
                ? plan.repeatCount
                : plan.repeatUntil != null
                    ? (int) ChronoUnit.DAYS.between(plan.day, plan.repeatUntil) + 1
                    : 30;
    goal = input("长期计划目标", Integer.toString(defaultGoal), 5, true);
    goalLabel = a.text("", 15, MainActivity.MUTED);
    a.add(repeatFields, goalLabel, 12, 8);
    a.add(repeatFields, goal, 0, 4);
    repeatSummary = a.text("", 14, MainActivity.ACCENT);
    a.add(repeatFields, repeatSummary, 8, 0);
    a.add(
        repeatFields,
        a.text(
            "每天重复，按持续天数或完成次数结束。时间段可跨到次日，最长 24 小时；完成记录归属开始日期。修改会影响整个长期计划。", 14, MainActivity.MUTED),
        8,
        4);
    a.add(form, repeatFields, 0, 8);
    int timeStart = form.getChildCount();
    dated = checkbox("安排日期", plan == null ? a.mode != 1 : plan.day != null);
    a.add(form, dated, 8, 0);
    dateFields = a.column();
    dayButton = a.button("", false);
    dayButton.setOnClickListener(
        v ->
            a.pickDate(
                day,
                date -> {
                  long offset = ChronoUnit.DAYS.between(day, endDay);
                  day = date;
                  if (repeated.isChecked())
                    endDay = date.plusDays(Math.max(0, Math.min(1, offset)));
                  else if (offset == 0 || endDay.isBefore(date)) endDay = date;
                  update();
                }));
    field(dateFields, "日期 / 长期计划开始日期", dayButton);
    form.addView(dateFields);
    scheduled = checkbox("设置时间段 · 可选", plan != null && plan.start != null);
    a.add(form, scheduled, 8, 0);
    timeFields = a.column();
    startButton = a.button("", false);
    endButton = a.button("", false);
    startButton.setOnClickListener(
        v ->
            new TimePickerDialog(
                    a,
                    (view, h, m) -> {
                      start = LocalTime.of(h, m);
                      update();
                    },
                    start.getHour(),
                    start.getMinute(),
                    true)
                .show());
    endButton.setOnClickListener(
        v ->
            new TimePickerDialog(
                    a,
                    (view, h, m) -> {
                      end = LocalTime.of(h, m);
                      update();
                    },
                    end.getHour(),
                    end.getMinute(),
                    true)
                .show());
    field(timeFields, "开始时间", startButton);
    field(timeFields, "结束时间", endButton);
    endDayButton = a.button("", false);
    endDayButton.setOnClickListener(
        v ->
            a.pickDate(
                endDay,
                date -> {
                  endDay = date;
                  update();
                }));
    field(timeFields, "结束日期 · 跨天时调整", endDayButton);
    LinearLayout quickTimes = a.row();
    for (int minutes : new int[] {30, 60}) {
      Button quick = a.button(minutes == 30 ? "30 分钟" : "1 小时", false);
      quick.setOnClickListener(
          v -> {
            end = start.plusMinutes(minutes);
            endDay = day.plusDays((start.toSecondOfDay() + minutes * 60) / 86400);
            update();
          });
      quickTimes.addView(quick, new LinearLayout.LayoutParams(0, -2, 1));
    }
    a.add(timeFields, quickTimes, 8, 0);
    a.add(form, timeFields, 0, 8);
    expected = checkbox("设置预期时长 · 可选", plan != null && plan.durationMinutes != null);
    a.add(form, expected, 8, 0);
    durationFields = a.column();
    duration =
        input(
            "预期时长，分钟",
            plan != null && plan.durationMinutes != null ? plan.durationMinutes.toString() : "60",
            6,
            true);
    field(durationFields, "预期时长（分钟）", duration);
    a.add(durationFields, a.text("预期时长可独立设置，无需时间段。", 14, MainActivity.MUTED), 8, 4);
    form.addView(durationFields);
    int alarmStartIndex = form.getChildCount();
    alarmEnabled = checkbox("添加闹钟 · 可选", plan != null && plan.alarm != null);
    a.add(form, alarmEnabled, 20, 0);
    alarmFields = a.column();
    RadioGroup alarmModes = new RadioGroup(a);
    alarmAfter = new RadioButton(a);
    alarmAfter.setText("一段时间后响起");
    alarmStart = new RadioButton(a);
    alarmStart.setText("计划开始时响起");
    alarmEnd = new RadioButton(a);
    alarmEnd.setText("计划结束时响起");
    for (RadioButton radio : new RadioButton[] {alarmAfter, alarmStart, alarmEnd}) {
      radio.setId(View.generateViewId());
      radio.setMinHeight(a.dp(48));
      alarmModes.addView(radio);
    }
    alarmModes.check(
        plan != null && plan.alarm != null && plan.alarm.mode.equals("start")
            ? alarmStart.getId()
            : plan != null && plan.alarm != null && plan.alarm.mode.equals("end")
                ? alarmEnd.getId()
                : alarmAfter.getId());
    alarmFields.addView(alarmModes);
    setupTime = a.button("设置计划时间", false);
    setupTime.setOnClickListener(
        v -> {
          dated.setChecked(true);
          scheduled.setChecked(true);
          openSection("时间与时长");
          startButton.requestFocus();
          startButton.requestRectangleOnScreen(
              new android.graphics.Rect(0, 0, startButton.getWidth(), startButton.getHeight()),
              true);
        });
    alarmFields.addView(setupTime);
    alarmAfterFields = a.column();
    alarmMinutes =
        input(
            "闹钟倒计时分钟数",
            plan != null && plan.alarm != null && plan.alarm.afterMinutes != null
                ? plan.alarm.afterMinutes.toString()
                : "10",
            6,
            true);
    field(alarmAfterFields, "多少分钟后响起", alarmMinutes);
    restartAlarm = checkbox("保存时重新开始倒计时", false);
    alarmAfterFields.addView(restartAlarm);
    a.add(
        alarmAfterFields,
        a.text("保存后开始倒计时，无需时间段。长期计划的倒计时只响一次。编辑时保留原时间，也可勾选重新计时。", 14, MainActivity.MUTED),
        8,
        8);
    alarmFields.addView(alarmAfterFields);
    alarmSound = checkbox("播放铃声", plan == null || plan.alarm == null || plan.alarm.sound);
    alarmVibrate = checkbox("震动", plan == null || plan.alarm == null || plan.alarm.vibrate);
    ringtone = plan != null && plan.alarm != null ? plan.alarm.ringtone : null;
    alarmFields.addView(alarmSound);
    alarmFields.addView(alarmVibrate);
    ringtoneButton = a.button("", false);
    ringtoneButton.setOnClickListener(
        v -> {
          Intent picker =
              new Intent(RingtoneManager.ACTION_RINGTONE_PICKER)
                  .putExtra(RingtoneManager.EXTRA_RINGTONE_TYPE, RingtoneManager.TYPE_ALARM)
                  .putExtra(RingtoneManager.EXTRA_RINGTONE_TITLE, "选择闹钟铃声")
                  .putExtra(RingtoneManager.EXTRA_RINGTONE_SHOW_DEFAULT, true)
                  .putExtra(RingtoneManager.EXTRA_RINGTONE_SHOW_SILENT, false)
                  .putExtra(
                      RingtoneManager.EXTRA_RINGTONE_EXISTING_URI,
                      ringtone == null
                          ? RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)
                          : Uri.parse(ringtone));
          try {
            a.startActivityForResult(picker, 30);
          } catch (android.content.ActivityNotFoundException e) {
            a.error("当前系统没有铃声选择器，将使用系统默认闹钟铃声。");
          }
        });
    a.add(alarmFields, ringtoneButton, 8, 8);
    permissions = a.button("闹钟权限与通知", false);
    permissions.setOnClickListener(v -> AlarmScheduler.permissions(a));
    alarmFields.addView(permissions);
    a.add(
        alarmFields,
        a.text("开始／结束闹钟需要时间段，长期计划每天提醒。响铃一分钟后自动停止，也可从通知中关闭；闹钟音量由手机系统设置。", 14, MainActivity.MUTED),
        8,
        8);
    form.addView(alarmFields);
    int colorsStart = form.getChildCount();
    LinearLayout colors = a.column();
    a.add(colors, a.text("计划颜色", 16, MainActivity.INK), 20, 8);
    for (int r = 0; r < 2; r++) {
      LinearLayout row = a.row();
      for (int c = 0; c < 4; c++) {
        String value = Plan.COLORS[r * 4 + c];
        Button b = a.button("", false);
        b.setBackground(a.surface(Color.parseColor(value), Color.parseColor(value)));
        b.setContentDescription("选择颜色 " + value);
        b.setOnClickListener(
            v -> {
              color = value;
              update();
            });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, a.dp(48), 1);
        lp.setMarginEnd(a.dp(8));
        lp.bottomMargin = a.dp(8);
        row.addView(b, lp);
      }
      colors.addView(row);
    }
    colorButton = a.button("", false);
    colorButton.setOnClickListener(
        v -> {
          EditText hex = input("自定义颜色，例如 #146C60", color, 7, false);
          AlertDialog picker =
              new AlertDialog.Builder(a)
                  .setTitle("自定义颜色")
                  .setView(hex)
                  .setNegativeButton("取消", null)
                  .setPositiveButton("使用", null)
                  .create();
          picker.setOnShowListener(
              d ->
                  picker
                      .getButton(AlertDialog.BUTTON_POSITIVE)
                      .setOnClickListener(
                          view -> {
                            String value = hex.getText().toString().trim();
                            if (!value.matches("#[0-9a-fA-F]{6}")) {
                              hex.setError("请输入 #RRGGBB");
                              return;
                            }
                            color = value;
                            update();
                            picker.dismiss();
                          }));
          picker.show();
        });
    colors.addView(colorButton);
    form.addView(colors);
    fold(form, colorsStart, form.getChildCount() - colorsStart, "计划颜色", false);
    fold(
        form,
        alarmStartIndex,
        colorsStart - alarmStartIndex,
        "闹钟提醒",
        plan != null && plan.alarm != null);
    fold(
        form,
        timeStart,
        alarmStartIndex - timeStart,
        "时间与时长",
        plan != null
            && (plan.start != null
                || plan.durationMinutes != null
                || original != null && plan.day != null));
    fold(form, repeatStart, timeStart - repeatStart, "长期计划", repeated.isChecked());
    fold(form, notesStart, repeatStart - notesStart, "备注", plan != null && !plan.notes.isEmpty());
    error = a.text("", 14, MainActivity.DANGER);
    error.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
    error.setMaxLines(2);
    error.setEllipsize(TextUtils.TruncateAt.END);
    error.setVisibility(View.GONE);
    LinearLayout footer = a.column();
    footer.setPadding(a.dp(20), a.dp(12), a.dp(20), a.dp(12));
    a.add(footer, error, 0, 8);
    LinearLayout actions = a.row();
    cancel = a.button("取消", false);
    save = a.button("保存计划", true);
    LinearLayout.LayoutParams cp = new LinearLayout.LayoutParams(0, -2, 1);
    cp.setMarginEnd(a.dp(12));
    actions.addView(cancel, cp);
    actions.addView(save, new LinearLayout.LayoutParams(0, -2, 2));
    footer.addView(actions);
    root.addView(footer);
    cancel.setOnClickListener(v -> dialog.dismiss());
    save.setOnClickListener(v -> save());
    repeated.setOnCheckedChangeListener(
        (b, c) -> {
          if (c) {
            dated.setChecked(true);
            openSection("长期计划");
          } else if (original != null && !original.dates.isEmpty()) {
            repeated.setChecked(true);
            showError("已有完成记录的长期计划不能直接改为单次计划。");
          }
          update();
        });
    dated.setOnCheckedChangeListener(
        (b, c) -> {
          if (!c) {
            if (repeated.isChecked()) {
              dated.setChecked(true);
              return;
            }
            scheduled.setChecked(false);
          }
          update();
        });
    scheduled.setOnCheckedChangeListener(
        (b, c) -> {
          if (c) dated.setChecked(true);
          if (c) openSection("时间与时长");
          update();
        });
    expected.setOnCheckedChangeListener(
        (b, c) -> {
          if (c) openSection("时间与时长");
          update();
        });
    alarmEnabled.setOnCheckedChangeListener(
        (b, c) -> {
          if (c) openSection("闹钟提醒");
          update();
        });
    alarmModes.setOnCheckedChangeListener((g, id) -> update());
    alarmSound.setOnCheckedChangeListener((b, c) -> update());
    types.setOnCheckedChangeListener((g, id) -> update());
    TextWatcher watch =
        new TextWatcher() {
          public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

          public void onTextChanged(CharSequence s, int start, int before, int count) {
            update();
          }

          public void afterTextChanged(Editable e) {}
        };
    for (EditText input : new EditText[] {goal, duration, alarmMinutes})
      input.addTextChangedListener(watch);
    if (state != null) apply(state);
    if (state != null)
      for (String name : sections.keySet())
        if (state.containsKey("section:" + name))
          setSection(name, state.getBoolean("section:" + name));
    update();
    dialog.setOnDismissListener(
        d -> {
          if (a.editor == this) {
            a.editor = null;
            a.render();
          }
        });
    dialog.show();
    Window window = dialog.getWindow();
    if (window != null) {
      window.setLayout(-1, -1);
      window.setBackgroundDrawableResource(android.R.color.transparent);
      window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
      a.inset(root, window);
    }
  }

  private void fold(LinearLayout parent, int start, int count, String name, boolean expanded) {
    LinearLayout content = a.column();
    for (int i = 0; i < count; i++) {
      View child = parent.getChildAt(start);
      parent.removeViewAt(start);
      content.addView(child);
    }
    LinearLayout wrapper = a.column();
    Button toggle = a.button("", false);
    toggle.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
    wrapper.addView(toggle);
    wrapper.addView(content);
    parent.addView(wrapper, start, new LinearLayout.LayoutParams(-1, -2));
    sections.put(name, content);
    sectionButtons.put(name, toggle);
    setSection(name, expanded);
    toggle.setOnClickListener(v -> setSection(name, content.getVisibility() != View.VISIBLE));
  }

  private void setSection(String name, boolean expanded) {
    if (!sections.containsKey(name)) return;
    sections.get(name).setVisibility(expanded ? View.VISIBLE : View.GONE);
    sectionButtons.get(name).setText((expanded ? "收起 · " : "展开 · ") + name);
  }

  void openSection(String name) {
    setSection(name, true);
  }

  private void showError(String text) {
    error.setText(text);
    error.setContentDescription(text);
    error.setVisibility(View.VISIBLE);
  }

  void refreshPermissions() {
    if (permissions != null)
      permissions.setText(
          AlarmScheduler.exactAllowed(a) && AlarmScheduler.notificationsAllowed(a)
              ? "提醒权限已开启 · 查看设置"
              : "闹钟待开启 · 点此允许提醒");
  }

  private CheckBox checkbox(String label, boolean checked) {
    CheckBox box = new CheckBox(a);
    box.setText(label);
    box.setTextColor(MainActivity.INK);
    box.setTextSize(16);
    box.setMinHeight(a.dp(48));
    box.setChecked(checked);
    return box;
  }

  private EditText input(String label, String value, int max, boolean numeric) {
    EditText e = new EditText(a);
    e.setTextSize(17);
    e.setTextColor(MainActivity.INK);
    e.setHint(label);
    e.setContentDescription(label);
    e.setText(value);
    e.setPadding(a.dp(12), a.dp(12), a.dp(12), a.dp(12));
    e.setMinHeight(a.dp(48));
    e.setBackground(a.surface(Color.WHITE, MainActivity.LINE));
    e.setFilters(new InputFilter[] {new InputFilter.LengthFilter(max)});
    e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI | EditorInfo.IME_FLAG_NO_FULLSCREEN);
    if (numeric) e.setInputType(InputType.TYPE_CLASS_NUMBER);
    return e;
  }

  private void field(LinearLayout form, String label, View input) {
    a.add(form, a.text(label, 15, MainActivity.MUTED), 12, 8);
    a.add(form, input, 0, 4);
  }

  private void update() {
    refreshPermissions();
    setupTime.setVisibility(
        !alarmAfter.isChecked() && !scheduled.isChecked() ? View.VISIBLE : View.GONE);
    alarmFields.setVisibility(alarmEnabled.isChecked() ? View.VISIBLE : View.GONE);
    alarmAfterFields.setVisibility(alarmAfter.isChecked() ? View.VISIBLE : View.GONE);
    restartAlarm.setVisibility(
        original != null && original.alarm != null && original.alarm.mode.equals("after")
            ? View.VISIBLE
            : View.GONE);
    ringtoneButton.setEnabled(alarmSound.isChecked());
    String toneName = "系统默认";
    if (ringtone != null)
      try {
        android.media.Ringtone tone = RingtoneManager.getRingtone(a, Uri.parse(ringtone));
        if (tone != null) toneName = tone.getTitle(a);
      } catch (RuntimeException ignored) {
        toneName = "所选铃声（不可用时使用默认）";
      }
    ringtoneButton.setText("铃声 · " + toneName);
    repeatFields.setVisibility(repeated.isChecked() ? View.VISIBLE : View.GONE);
    dateFields.setVisibility(dated.isChecked() ? View.VISIBLE : View.GONE);
    timeFields.setVisibility(scheduled.isChecked() ? View.VISIBLE : View.GONE);
    durationFields.setVisibility(expected.isChecked() ? View.VISIBLE : View.GONE);
    dayButton.setText(day.toString());
    endDayButton.setText(endDay.toString());
    startButton.setText(Plan.clock(start));
    endButton.setText(Plan.clock(end));
    endDayButton.setEnabled(true);
    colorButton.setText("当前 " + color + " · 自定义颜色");
    colorButton.setTextColor(MainActivity.INK);
    goalLabel.setText(days.isChecked() ? "持续多少天（含开始当天）" : "累计完成多少次（每天最多一次）");
    String target = goal.getText().toString();
    String summary = days.isChecked() ? "持续天数须为 1–3650 天" : "完成次数须为 1–10000 次";
    try {
      int count = Integer.parseInt(target);
      if (count > 0 && count <= (days.isChecked() ? 3650 : 10000))
        summary =
            days.isChecked()
                ? "从 " + day + " 开始，至 " + day.plusDays(count - 1) + "（含当天）"
                : "累计完成 " + count + " 次后结束；今天最多计一次";
    } catch (RuntimeException ignored) {
    }
    repeatSummary.setText(summary);
    String description = dated.isChecked() ? day.toString() : "待安排";
    if (scheduled.isChecked())
      description +=
          " · "
              + Plan.clock(start)
              + "—"
              + (endDay.equals(day) ? "" : endDay.equals(day.plusDays(1)) ? "次日 " : endDay + " ")
              + Plan.clock(end);
    if (expected.isChecked()) description += " · 预计 " + duration.getText() + " 分钟";
    if (repeated.isChecked()) description += " · 每天重复";
    if (alarmEnabled.isChecked())
      description +=
          " · "
              + (alarmAfter.isChecked()
                  ? alarmMinutes.getText() + " 分钟后提醒"
                  : alarmStart.isChecked() ? "开始提醒" : "结束提醒");
    preview.setText((original != null && original.recurring() ? "编辑整个长期计划 · " : "") + description);
  }

  void save() {
    error.setText("");
    error.setVisibility(View.GONE);
    for (EditText input : new EditText[] {title, duration, goal, alarmMinutes})
      input.setError(null);
    scheduled.setError(null);
    endDayButton.setError(null);
    try {
      Plan p = original == null ? new Plan() : original.copy();
      p.id = draftId;
      p.createdAt = draftCreated;
      p.title = title.getText().toString().trim();
      p.notes = notes.getText().toString().trim();
      p.color = color;
      p.day = dated.isChecked() ? day : null;
      p.start = scheduled.isChecked() ? start : null;
      p.end = scheduled.isChecked() ? end : null;
      p.endDay = scheduled.isChecked() ? endDay : null;
      p.durationMinutes = expected.isChecked() ? positive(duration, "预期时长") : null;
      p.repeatCount = null;
      p.repeatUntil = null;
      if (repeated.isChecked()) {
        p.completed = false;
        int value = positive(goal, "长期计划目标");
        if (days.isChecked()) {
          if (value > 3650) Plan.fail("持续天数最多 3650 天。");
          p.repeatUntil = day.plusDays(value - 1);
        } else p.repeatCount = value;
      }
      p.updatedAt = Instant.now();
      p.alarm = null;
      if (alarmEnabled.isChecked()) {
        PlanAlarm alarm = new PlanAlarm();
        alarm.mode = alarmAfter.isChecked() ? "after" : alarmStart.isChecked() ? "start" : "end";
        alarm.afterMinutes = alarmAfter.isChecked() ? positive(alarmMinutes, "闹钟倒计时") : null;
        if (alarm.afterMinutes != null && alarm.afterMinutes > 525600)
          Plan.fail("闹钟倒计时最多 525600 分钟。");
        boolean keep =
            !restartAlarm.isChecked()
                && original != null
                && original.alarm != null
                && original.alarm.mode.equals(alarm.mode)
                && java.util.Objects.equals(original.alarm.afterMinutes, alarm.afterMinutes);
        alarm.at =
            alarmAfter.isChecked()
                ? keep ? original.alarm.at : Instant.now().plusSeconds(alarm.afterMinutes * 60L)
                : null;
        alarm.firedAt = keep ? original.alarm.firedAt : null;
        alarm.sound = alarmSound.isChecked();
        alarm.vibrate = alarmVibrate.isChecked();
        alarm.ringtone = ringtone;
        p.alarm = alarm;
      }
      p.validate();
      java.util.List<Plan> conflicts = new java.util.ArrayList<>();
      for (Plan other : a.store.all()) if (p.overlaps(other)) conflicts.add(other);
      if (!conflicts.isEmpty())
        new AlertDialog.Builder(a)
            .setTitle("时间段与其他计划重叠")
            .setMessage(
                "与以下计划重叠：\n"
                    + conflicts.stream()
                        .limit(3)
                        .map(
                            other ->
                                other.title.length() > 80
                                    ? other.title.substring(0, 80) + "…"
                                    : other.title)
                        .collect(java.util.stream.Collectors.joining("\n")))
            .setNegativeButton(
                "修改时间",
                (d, w) -> {
                  openSection("时间与时长");
                  startButton.requestFocus();
                })
            .setPositiveButton("仍然保存", (d, w) -> commit(p))
            .show();
      else commit(p);
    } catch (Exception e) {
      showError(MainActivity.message(e));
      if (title.getText().toString().trim().isEmpty()) {
        title.setError("请填写计划内容");
        title.requestFocus();
      } else if (alarmEnabled.isChecked() && !alarmAfter.isChecked() && !scheduled.isChecked()) {
        openSection("时间与时长");
        scheduled.setError("请先设置计划时间段");
        scheduled.requestFocus();
      } else if (MainActivity.message(e).contains("闹钟倒计时")) {
        openSection("闹钟提醒");
        alarmMinutes.setError(MainActivity.message(e));
        alarmMinutes.requestFocus();
      } else if (MainActivity.message(e).contains("预期时长")) {
        openSection("时间与时长");
        duration.setError(MainActivity.message(e));
        duration.requestFocus();
      } else if (MainActivity.message(e).contains("时间") || MainActivity.message(e).contains("日期")) {
        openSection("时间与时长");
        endDayButton.setError(MainActivity.message(e));
        endDayButton.requestFocus();
      } else if (repeated.isChecked()) {
        openSection("长期计划");
        goal.setError(MainActivity.message(e));
        goal.requestFocus();
      }
    }
  }

  private int positive(EditText e, String label) {
    try {
      int n = Integer.parseInt(e.getText().toString());
      if (n < 1) throw new NumberFormatException();
      return n;
    } catch (NumberFormatException ex) {
      openSection(e == goal ? "长期计划" : e == alarmMinutes ? "闹钟提醒" : "时间与时长");
      e.setError(label + "须为正整数");
      e.requestFocus();
      throw new IllegalArgumentException(label + "须为正整数。");
    }
  }

  private void commit(Plan p) {
    pendingRevision = p.updatedAt.toString();
    save.setEnabled(false);
    cancel.setEnabled(false);
    dialog.setCancelable(false);
    a.perform(
        () -> a.store.save(p),
        () -> {
          a.toast(
              p.alarm != null
                      && (!AlarmScheduler.exactAllowed(a)
                          || !AlarmScheduler.notificationsAllowed(a))
                  ? "计划已保存；允许闹钟和通知权限后才能响铃"
                  : "计划已保存");
          a.revealSaved(p);
          dialog.dismiss();
        },
        failure -> {
          pendingRevision = null;
          showError("保存失败，填写内容已保留：" + MainActivity.message(failure));
          save.setEnabled(true);
          cancel.setEnabled(true);
          dialog.setCancelable(true);
        });
    a.handler.postDelayed(this::checkSaving, 100);
  }

  private void checkSaving() {
    if (!dialog.isShowing()) return;
    if (a.busy) {
      a.handler.postDelayed(this::checkSaving, 100);
      return;
    }
    save.setEnabled(true);
    cancel.setEnabled(true);
    dialog.setCancelable(true);
  }

  Bundle snapshot() {
    Bundle s = new Bundle();
    s.putString("draftId", draftId);
    s.putString("draftCreated", draftCreated.toString());
    s.putString("pendingRevision", pendingRevision);
    for (String name : sections.keySet())
      s.putBoolean("section:" + name, sections.get(name).getVisibility() == View.VISIBLE);
    if (original != null)
      try {
        s.putString("original", original.json().toString());
      } catch (JSONException e) {
        throw new IllegalStateException(e);
      }
    s.putString("title", title.getText().toString());
    s.putString("notes", notes.getText().toString());
    s.putString("duration", duration.getText().toString());
    s.putString("goal", goal.getText().toString());
    s.putString("color", color);
    s.putString("day", day.toString());
    s.putString("endDay", endDay.toString());
    s.putString("start", start.toString());
    s.putString("end", end.toString());
    s.putBoolean("dated", dated.isChecked());
    s.putBoolean("scheduled", scheduled.isChecked());
    s.putBoolean("expected", expected.isChecked());
    s.putBoolean("repeated", repeated.isChecked());
    s.putBoolean("days", days.isChecked());
    s.putBoolean("alarmEnabled", alarmEnabled.isChecked());
    s.putBoolean("alarmSound", alarmSound.isChecked());
    s.putBoolean("alarmVibrate", alarmVibrate.isChecked());
    s.putBoolean("restartAlarm", restartAlarm.isChecked());
    s.putString(
        "alarmMode", alarmAfter.isChecked() ? "after" : alarmStart.isChecked() ? "start" : "end");
    s.putString("alarmMinutes", alarmMinutes.getText().toString());
    s.putString("ringtone", ringtone);
    return s;
  }

  private void apply(Bundle s) {
    alarmEnabled.setChecked(s.getBoolean("alarmEnabled"));
    alarmSound.setChecked(s.getBoolean("alarmSound", true));
    alarmVibrate.setChecked(s.getBoolean("alarmVibrate", true));
    restartAlarm.setChecked(s.getBoolean("restartAlarm"));
    String alarmMode = s.getString("alarmMode", "after");
    if (alarmMode.equals("after")) alarmAfter.setChecked(true);
    else if (alarmMode.equals("start")) alarmStart.setChecked(true);
    else alarmEnd.setChecked(true);
    alarmMinutes.setText(s.getString("alarmMinutes", "10"));
    ringtone = s.getString("ringtone");
    title.setText(s.getString("title"));
    notes.setText(s.getString("notes"));
    duration.setText(s.getString("duration"));
    goal.setText(s.getString("goal"));
    color = s.getString("color");
    day = LocalDate.parse(s.getString("day"));
    endDay = LocalDate.parse(s.getString("endDay"));
    start = LocalTime.parse(s.getString("start"));
    end = LocalTime.parse(s.getString("end"));
    repeated.setChecked(s.getBoolean("repeated"));
    dated.setChecked(s.getBoolean("dated"));
    scheduled.setChecked(s.getBoolean("scheduled"));
    expected.setChecked(s.getBoolean("expected"));
    if (s.getBoolean("days")) days.setChecked(true);
    else times.setChecked(true);
  }

  static void restore(MainActivity a, Bundle s) {
    try {
      if (s.getString("pendingRevision") != null) {
        Plan saved =
            a.store.all().stream()
                .filter(
                    p ->
                        p.id.equals(s.getString("draftId"))
                            && p.updatedAt.equals(Instant.parse(s.getString("pendingRevision"))))
                .findFirst()
                .orElse(null);
        if (saved != null) {
          a.revealSaved(saved);
          a.render();
          a.toast("计划已保存");
          return;
        }
      }
      new EditorForm(
          a,
          s.containsKey("original") ? Plan.from(new JSONObject(s.getString("original"))) : null,
          s);
    } catch (Exception e) {
      a.error("草稿未能恢复：" + MainActivity.message(e));
    }
  }

  void ringtoneResult(Intent data) {
    Uri uri =
        Build.VERSION.SDK_INT >= 33
            ? data.getParcelableExtra(RingtoneManager.EXTRA_RINGTONE_PICKED_URI, Uri.class)
            : data.getParcelableExtra(RingtoneManager.EXTRA_RINGTONE_PICKED_URI);
    ringtone = uri == null || RingtoneManager.isDefault(uri) ? null : uri.toString();
    update();
  }
}
