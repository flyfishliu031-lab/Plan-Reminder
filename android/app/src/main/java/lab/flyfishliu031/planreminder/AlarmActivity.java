package lab.flyfishliu031.planreminder;

import android.app.*;
import android.content.*;
import android.os.*;
import android.view.*;
import android.widget.*;

public final class AlarmActivity extends Activity {
  private final Handler handler = new Handler(Looper.getMainLooper());
  private final Runnable monitor =
      new Runnable() {
        public void run() {
          if (!AlarmService.running) finish();
          else handler.postDelayed(this, 250);
        }
      };

  @Override
  public void onCreate(Bundle state) {
    super.onCreate(state);
    if (Build.VERSION.SDK_INT >= 27) {
      setShowWhenLocked(true);
      setTurnScreenOn(true);
    } else
      getWindow()
          .addFlags(
              WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED
                  | WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON);
    getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    LinearLayout root = new LinearLayout(this);
    root.setOrientation(LinearLayout.VERTICAL);
    root.setGravity(Gravity.CENTER);
    root.setPadding(32, 48, 32, 48);
    root.setBackgroundColor(MainActivity.CANVAS);
    TextView title = new TextView(this);
    title.setText("时间到了");
    title.setTextSize(30);
    title.setTextColor(MainActivity.ACCENT);
    root.addView(title);
    ScrollView scroll = new ScrollView(this);
    TextView content = new TextView(this);
    content.setText(getIntent().getStringExtra("titles"));
    content.setTextSize(22);
    content.setTextColor(MainActivity.INK);
    content.setPadding(0, 32, 0, 32);
    scroll.addView(content);
    root.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
    Button stop = new Button(this);
    stop.setText("关闭闹钟");
    stop.setTextSize(18);
    stop.setMinHeight(Math.round(56 * getResources().getDisplayMetrics().density));
    root.addView(stop, new LinearLayout.LayoutParams(-1, -2));
    stop.setOnClickListener(
        v -> {
          stopService(new Intent(this, AlarmService.class));
          finish();
        });
    setContentView(root);
    root.setOnApplyWindowInsetsListener(
        (v, insets) -> {
          if (Build.VERSION.SDK_INT >= 30) {
            android.graphics.Insets bars =
                insets.getInsets(
                    WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
            v.setPadding(32 + bars.left, 24 + bars.top, 32 + bars.right, 24 + bars.bottom);
          }
          return insets;
        });
    if (Build.VERSION.SDK_INT >= 30) getWindow().setDecorFitsSystemWindows(false);
    handler.postDelayed(monitor, 500);
    handler.postDelayed(this::finish, 61000);
  }

  @Override
  protected void onDestroy() {
    handler.removeCallbacksAndMessages(null);
    super.onDestroy();
  }

  @Override
  protected void onNewIntent(Intent intent) {
    super.onNewIntent(intent);
    setIntent(intent);
    recreate();
  }
}
