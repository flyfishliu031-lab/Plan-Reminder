package lab.flyfishliu031.planreminder;

import android.app.*;
import android.graphics.Color;
import android.os.*;
import android.text.*;
import android.view.*;
import android.widget.*;
import org.json.*;
import java.time.*;
import java.time.temporal.ChronoUnit;

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

    EditorForm(MainActivity activity,Plan plan,Bundle state) {
        a=activity; original=plan; a.editor=this;
        LocalDate today=LocalDate.now(); day=plan!=null && plan.day!=null?plan.day:a.selected; endDay=plan!=null && plan.endDay!=null?plan.endDay:day;
        start=plan!=null && plan.start!=null?plan.start:LocalTime.of(9,0); end=plan!=null && plan.end!=null?plan.end:LocalTime.of(10,0);
        color=plan!=null?plan.color:Plan.COLORS[a.store.all().size()%Plan.COLORS.length];
        for(String candidate:Plan.COLORS) if(plan==null && a.store.all().stream().noneMatch(p -> p.color.equalsIgnoreCase(candidate))) { color=candidate;break; }
        dialog=new Dialog(a);dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);dialog.setCanceledOnTouchOutside(false);
        LinearLayout root=a.column();root.setBackgroundColor(MainActivity.CANVAS);dialog.setContentView(root);
        LinearLayout header=a.column();header.setPadding(a.dp(20),a.dp(16),a.dp(20),a.dp(12));header.addView(a.text(plan==null?"添加计划":"编辑计划",26,MainActivity.INK));root.addView(header);
        ScrollView scroll=new ScrollView(a);scroll.setFillViewport(true);LinearLayout form=a.column();form.setPadding(a.dp(20),a.dp(8),a.dp(20),a.dp(24));scroll.addView(form);root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        title=input("计划内容（必填）",plan==null?"":plan.title,500,false); title.setMinLines(2);title.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_FLAG_MULTI_LINE|InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);title.setGravity(Gravity.TOP);field(form,"计划内容",title);
        notes=input("备注（可选）",plan==null?"":plan.notes,5000,false);notes.setMinLines(2);notes.setInputType(InputType.TYPE_CLASS_TEXT|InputType.TYPE_TEXT_FLAG_MULTI_LINE);notes.setGravity(Gravity.TOP);field(form,"备注 · 可选",notes);
        repeated=checkbox("设为长期计划（每天重复）",plan!=null && plan.recurring());a.add(form,repeated,8,0);
        repeatFields=a.column();days=new RadioButton(a);days.setText("持续天数");times=new RadioButton(a);times.setText("完成次数");days.setMinHeight(a.dp(48));times.setMinHeight(a.dp(48));RadioGroup types=new RadioGroup(a);types.setOrientation(RadioGroup.VERTICAL);days.setId(View.generateViewId());times.setId(View.generateViewId());types.addView(days);types.addView(times);types.check(plan!=null && plan.repeatCount!=null?times.getId():days.getId());repeatFields.addView(types);
        int defaultGoal=plan==null?30:plan.repeatCount!=null?plan.repeatCount:plan.repeatUntil!=null?(int)ChronoUnit.DAYS.between(plan.day,plan.repeatUntil)+1:30;
        goal=input("长期计划目标",Integer.toString(defaultGoal),5,true);field(repeatFields,"目标数值",goal);a.add(repeatFields,a.text("持续天数含开始当天，结束日期过后自动结束。完成次数每天最多计一次，达到目标后结束。",14,MainActivity.MUTED),8,4);a.add(form,repeatFields,0,8);
        dated=checkbox("安排日期",plan==null?a.mode!=1:plan.day!=null);a.add(form,dated,8,0);dateFields=a.column();dayButton=a.button("",false);dayButton.setOnClickListener(v -> a.pickDate(day,date -> { boolean aligned=day.equals(endDay);day=date;if(aligned || repeated.isChecked())endDay=date;update(); }));field(dateFields,"日期 / 长期计划开始日期",dayButton);form.addView(dateFields);
        scheduled=checkbox("设置时间段 · 可选",plan!=null && plan.start!=null);a.add(form,scheduled,8,0);timeFields=a.column();startButton=a.button("",false);endButton=a.button("",false);startButton.setOnClickListener(v -> new TimePickerDialog(a,(view,h,m) -> {start=LocalTime.of(h,m);update();},start.getHour(),start.getMinute(),true).show());endButton.setOnClickListener(v -> new TimePickerDialog(a,(view,h,m) -> {end=LocalTime.of(h,m);update();},end.getHour(),end.getMinute(),true).show());field(timeFields,"开始时间",startButton);field(timeFields,"结束时间",endButton);
        endDayButton=a.button("",false);endDayButton.setOnClickListener(v -> a.pickDate(endDay,date -> {endDay=date;update();}));field(timeFields,"结束日期 · 跨天时调整",endDayButton);a.add(form,timeFields,0,8);
        expected=checkbox("设置预期时长 · 可选",plan!=null && plan.durationMinutes!=null);a.add(form,expected,8,0);durationFields=a.column();duration=input("预期时长，分钟",plan!=null && plan.durationMinutes!=null?plan.durationMinutes.toString():"60",6,true);field(durationFields,"预期时长（分钟）",duration);a.add(durationFields,a.text("预期时长可独立设置，无需时间段。",14,MainActivity.MUTED),8,4);form.addView(durationFields);
        LinearLayout colors=a.column();a.add(colors,a.text("计划颜色",16,MainActivity.INK),20,8);
        for(int r=0;r<2;r++) { LinearLayout row=a.row();for(int c=0;c<4;c++) {String value=Plan.COLORS[r*4+c];Button b=a.button("",false);b.setBackground(a.surface(Color.parseColor(value),Color.parseColor(value)));b.setContentDescription("选择颜色 "+value);b.setOnClickListener(v -> {color=value;update();});LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(0,a.dp(48),1);lp.setMarginEnd(a.dp(8));lp.bottomMargin=a.dp(8);row.addView(b,lp);}colors.addView(row); }
        colorButton=a.button("",false);colorButton.setOnClickListener(v -> { EditText hex=input("自定义颜色，例如 #146C60",color,7,false);AlertDialog picker=new AlertDialog.Builder(a).setTitle("自定义颜色").setView(hex).setNegativeButton("取消",null).setPositiveButton("使用",null).create();picker.setOnShowListener(d -> picker.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(view -> {String value=hex.getText().toString().trim();if(!value.matches("#[0-9a-fA-F]{6}")){hex.setError("请输入 #RRGGBB");return;}color=value;update();picker.dismiss();}));picker.show(); });colors.addView(colorButton);form.addView(colors);
        error=a.text("",14,MainActivity.DANGER);error.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);a.add(form,error,12,0);
        LinearLayout footer=a.row();footer.setPadding(a.dp(20),a.dp(12),a.dp(20),a.dp(12));cancel=a.button("取消",false);save=a.button("保存计划",true);LinearLayout.LayoutParams cp=new LinearLayout.LayoutParams(0,-2,1);cp.setMarginEnd(a.dp(12));footer.addView(cancel,cp);footer.addView(save,new LinearLayout.LayoutParams(0,-2,2));root.addView(footer);
        cancel.setOnClickListener(v -> dialog.dismiss());save.setOnClickListener(v -> save());
        repeated.setOnCheckedChangeListener((b,c) -> {if(c) {dated.setChecked(true);endDay=day;}else if(original!=null && !original.dates.isEmpty()){repeated.setChecked(true);error.setText("已有完成记录的长期计划不能直接改为单次计划。");}update();});
        dated.setOnCheckedChangeListener((b,c) -> {if(!c){if(repeated.isChecked()){dated.setChecked(true);return;}scheduled.setChecked(false);}update();});
        scheduled.setOnCheckedChangeListener((b,c) -> {if(c)dated.setChecked(true);update();});expected.setOnCheckedChangeListener((b,c) -> update());
        if(state!=null) apply(state);update();
        dialog.setOnDismissListener(d -> {if(a.editor==this)a.editor=null;});dialog.show();Window window=dialog.getWindow();if(window!=null){window.setLayout(-1,-1);window.setBackgroundDrawableResource(android.R.color.transparent);window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);a.inset(root,window);}
    }
    private CheckBox checkbox(String label,boolean checked) {CheckBox box=new CheckBox(a);box.setText(label);box.setTextColor(MainActivity.INK);box.setTextSize(16);box.setMinHeight(a.dp(48));box.setChecked(checked);return box;}
    private EditText input(String label,String value,int max,boolean numeric) {EditText e=new EditText(a);e.setTextSize(17);e.setTextColor(MainActivity.INK);e.setHint(label);e.setContentDescription(label);e.setText(value);e.setPadding(a.dp(12),a.dp(12),a.dp(12),a.dp(12));e.setMinHeight(a.dp(48));e.setBackground(a.surface(Color.WHITE,MainActivity.LINE));e.setFilters(new InputFilter[]{new InputFilter.LengthFilter(max)});if(numeric)e.setInputType(InputType.TYPE_CLASS_NUMBER);return e;}
    private void field(LinearLayout form,String label,View input) {a.add(form,a.text(label,15,MainActivity.MUTED),12,8);a.add(form,input,0,4);}
    private void update() {repeatFields.setVisibility(repeated.isChecked()?View.VISIBLE:View.GONE);dateFields.setVisibility(dated.isChecked()?View.VISIBLE:View.GONE);timeFields.setVisibility(scheduled.isChecked()?View.VISIBLE:View.GONE);durationFields.setVisibility(expected.isChecked()?View.VISIBLE:View.GONE);dayButton.setText(day.toString());endDayButton.setText(endDay.toString());startButton.setText(Plan.clock(start));endButton.setText(Plan.clock(end));endDayButton.setEnabled(!repeated.isChecked());colorButton.setText("当前 "+color+" · 自定义颜色");colorButton.setTextColor(Color.parseColor(color));}
    void save() {
        error.setText("");
        try {
            Plan p=original==null?new Plan():original.copy();p.title=title.getText().toString().trim();p.notes=notes.getText().toString().trim();p.color=color;p.day=dated.isChecked()?day:null;
            p.start=scheduled.isChecked()?start:null;p.end=scheduled.isChecked()?end:null;p.endDay=scheduled.isChecked()?(repeated.isChecked()?day:endDay):null;
            p.durationMinutes=expected.isChecked()?positive(duration,"预期时长"):null;p.repeatCount=null;p.repeatUntil=null;
            if(repeated.isChecked()) {p.completed=false;int value=positive(goal,"长期计划目标");if(days.isChecked()){if(value>3650)Plan.fail("持续天数最多 3650 天。");p.repeatUntil=day.plusDays(value-1);}else p.repeatCount=value;}
            p.updatedAt=Instant.now();p.validate();
            boolean overlap=a.store.all().stream().anyMatch(p::overlaps);
            if(overlap) new AlertDialog.Builder(a).setTitle("时间段与其他计划重叠").setMessage("仍然保存这项计划？").setNegativeButton("返回修改",null).setPositiveButton("仍然保存",(d,w)->commit(p)).show();else commit(p);
        } catch(Exception e) {error.setText(MainActivity.message(e));error.requestFocus();a.toast(MainActivity.message(e));if(title.getText().toString().trim().isEmpty()){title.setError("请填写计划内容");title.requestFocus();}}
    }
    private int positive(EditText e,String label) {try {int n=Integer.parseInt(e.getText().toString());if(n<1)throw new NumberFormatException();return n;}catch(NumberFormatException ex){e.setError(label+"须为正整数");e.requestFocus();throw new IllegalArgumentException(label+"须为正整数。");}}
    private void commit(Plan p) {save.setEnabled(false);cancel.setEnabled(false);dialog.setCancelable(false);a.perform(() -> a.store.save(p),() -> {a.toast("计划已保存");dialog.dismiss();});a.handler.postDelayed(this::checkSaving,100);}
    private void checkSaving() {if(!dialog.isShowing())return;if(a.busy){a.handler.postDelayed(this::checkSaving,100);return;}save.setEnabled(true);cancel.setEnabled(true);dialog.setCancelable(true);}
    Bundle snapshot() {
        Bundle s=new Bundle();if(original!=null)try{s.putString("original",original.json().toString());}catch(JSONException e){throw new IllegalStateException(e);}
        s.putString("title",title.getText().toString());s.putString("notes",notes.getText().toString());s.putString("duration",duration.getText().toString());s.putString("goal",goal.getText().toString());s.putString("color",color);
        s.putString("day",day.toString());s.putString("endDay",endDay.toString());s.putString("start",start.toString());s.putString("end",end.toString());
        s.putBoolean("dated",dated.isChecked());s.putBoolean("scheduled",scheduled.isChecked());s.putBoolean("expected",expected.isChecked());s.putBoolean("repeated",repeated.isChecked());s.putBoolean("days",days.isChecked());return s;
    }
    private void apply(Bundle s) {title.setText(s.getString("title"));notes.setText(s.getString("notes"));duration.setText(s.getString("duration"));goal.setText(s.getString("goal"));color=s.getString("color");day=LocalDate.parse(s.getString("day"));endDay=LocalDate.parse(s.getString("endDay"));start=LocalTime.parse(s.getString("start"));end=LocalTime.parse(s.getString("end"));repeated.setChecked(s.getBoolean("repeated"));dated.setChecked(s.getBoolean("dated"));scheduled.setChecked(s.getBoolean("scheduled"));expected.setChecked(s.getBoolean("expected"));if(s.getBoolean("days"))days.setChecked(true);else times.setChecked(true);}
    static void restore(MainActivity a,Bundle s) {try {new EditorForm(a,s.containsKey("original")?Plan.from(new JSONObject(s.getString("original"))):null,s);}catch(Exception e){a.error("草稿未能恢复："+MainActivity.message(e));}}
}
