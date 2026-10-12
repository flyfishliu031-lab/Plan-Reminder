package lab.flyfishliu031.planreminder;

import android.content.res.ColorStateList;
import android.graphics.*;
import android.graphics.drawable.RippleDrawable;
import android.text.TextUtils;
import android.view.*;
import android.widget.*;
import java.time.*;
import java.util.*;

final class DailyTimelineView extends FrameLayout {
  final MainActivity a;
  final LocalDate day;
  final java.util.List<DaySegment> segments;
  final float hour, top, left;
  private Integer anchor;
  private boolean touching;
  private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

  DailyTimelineView(MainActivity a, LocalDate day, java.util.List<Plan> plans) {
    super(a);
    this.a = a;
    this.day = day;
    segments = DaySegment.layout(plans, day);
    hour = a.dp(88);
    top = a.dp(20);
    paint.setTextSize(13 * getResources().getDisplayMetrics().scaledDensity);
    left = Math.max(a.dp(56), paint.measureText("24:00") + a.dp(8));
    setBackgroundColor(Color.WHITE);
    setWillNotDraw(false);
    setContentDescription("全天时间表，点击空白处选择开始和结束时间；上方计划选择框和添加计划按钮也可操作");
    for (DaySegment s : segments) {
      Button b = a.button("", false);
      b.setTag(s);
      b.setMinWidth(0);
      b.setMinimumWidth(0);
      b.setMinHeight(0);
      b.setMinimumHeight(0);
      b.setPadding(a.dp(6), a.dp(2), a.dp(6), 0);
      b.setTextSize(14);
      b.setGravity(Gravity.TOP | Gravity.START);
      b.setSingleLine(true);
      b.setEllipsize(TextUtils.TruncateAt.END);
      int color = Color.parseColor(s.plan.color);
      int light =
          Color.rgb(
              (Color.red(color) + 7 * 255) / 8,
              (Color.green(color) + 7 * 255) / 8,
              (Color.blue(color) + 7 * 255) / 8);
      b.setBackground(
          new RippleDrawable(ColorStateList.valueOf(0x22146C60), a.surface(light, color), null));
      if (s.plan.checked(s.occurrence))
        b.setPaintFlags(b.getPaintFlags() | Paint.STRIKE_THRU_TEXT_FLAG);
      b.setContentDescription(s.caption() + (s.plan.checked(s.occurrence) ? "，已完成" : "，待完成"));
      b.setOnClickListener(v -> a.openTimelinePlan(s.plan, s.occurrence));
      addView(b, new LayoutParams(1, 1));
    }
  }

  @Override
  protected void onMeasure(int w, int h) {
    int width = MeasureSpec.getSize(w);
    for (int i = 0; i < getChildCount(); i++) {
      Button b = (Button) getChildAt(i);
      DaySegment s = (DaySegment) b.getTag();
      int columnWidth = Math.max(1, (int) (width - left - a.dp(8)) / s.columns);
      int height = Math.max(a.dp(3), (int) ((s.end - s.start) * hour / 60) - a.dp(2));
      LayoutParams lp = (LayoutParams) b.getLayoutParams();
      lp.width = Math.max(1, columnWidth - a.dp(4));
      lp.height = height;
      lp.leftMargin = (int) left + s.column * columnWidth + a.dp(2);
      lp.topMargin = (int) (top + s.start * hour / 60);
      b.setText(
          height
                  < Math.max(
                      a.dp(36),
                      b.getPaint().getFontSpacing() + b.getPaddingTop() + b.getPaddingBottom())
              ? ""
              : s.plan.title.replace('\n', ' '));
    }
    super.onMeasure(
        w, MeasureSpec.makeMeasureSpec((int) (top * 2 + 24 * hour), MeasureSpec.EXACTLY));
  }

  @Override
  protected void onDraw(Canvas c) {
    super.onDraw(c);
    paint.setTextSize(13 * getResources().getDisplayMetrics().scaledDensity);
    for (int h = 0; h <= 24; h++) {
      float y = top + h * hour;
      paint.setColor(MainActivity.LINE);
      paint.setStrokeWidth(a.dp(1));
      c.drawLine(left, y, getWidth() - a.dp(8), y, paint);
      paint.setColor(MainActivity.MUTED);
      c.drawText(String.format(Locale.ROOT, "%02d:00", h), a.dp(2), y + a.dp(4), paint);
    }
    if (anchor != null) {
      paint.setColor(MainActivity.ACCENT);
      float y = top + anchor * hour / 60;
      c.drawLine(left, y, getWidth(), y, paint);
    }
    if (day.equals(LocalDate.now())) {
      float y = top + LocalTime.now().toSecondOfDay() * hour / 3600;
      paint.setColor(MainActivity.DANGER);
      paint.setStrokeWidth(a.dp(2));
      c.drawLine(left - a.dp(6), y, getWidth(), y, paint);
      c.drawCircle(left - a.dp(6), y, a.dp(4), paint);
    }
  }

  @Override
  public boolean onTouchEvent(MotionEvent e) {
    if (e.getX() < left) return false;
    if (e.getAction() == MotionEvent.ACTION_DOWN) {
      touching = true;
      return true;
    }
    if (e.getAction() == MotionEvent.ACTION_CANCEL) {
      touching = false;
      return true;
    }
    if (e.getAction() == MotionEvent.ACTION_UP && touching) {
      touching = false;
      performClick();
      chooseMinute(Math.max(0, Math.min(1440, Math.round((e.getY() - top) * 60 / hour))));
      return true;
    }
    return touching;
  }

  @Override
  public boolean performClick() {
    super.performClick();
    return true;
  }

  void chooseMinute(int minute) {
    if (anchor == null) {
      anchor = Math.min(1439, minute);
      a.toast("开始 " + DaySegment.clock(anchor) + "，请点击结束时间；可在编辑窗口精确调整");
      invalidate();
    } else {
      int from = anchor, to = minute;
      anchor = null;
      invalidate();
      if (from == to) to = Math.min(1440, from + 60);
      a.addTimelinePlan(Math.min(from, to), Math.max(from, to));
    }
  }
}
