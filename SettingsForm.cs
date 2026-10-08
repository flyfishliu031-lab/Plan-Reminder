namespace PlanReminder;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings settings;
    private readonly CheckBox exitOnClose = new() { Text = "关闭窗口后退出程序", AutoSize = true, AccessibleName = "关闭窗口后退出程序" };
    private readonly Label error = Theme.Label("", Theme.Small, Theme.Danger);

    internal SettingsForm(AppSettings preferences)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        settings = preferences; exitOnClose.Checked = settings.ExitOnClose;
        Text = "设置"; Icon = Theme.AppIcon; Font = Theme.Body; ForeColor = Theme.Ink; BackColor = Theme.Canvas;
        ClientSize = new Size(540, 320); MinimumSize = new Size(420, 300);
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 20, 24, 0), Margin = Padding.Empty };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = Theme.Label("设置", Theme.EditorHeading); heading.Margin = new Padding(0, 0, 0, 8); heading.Dock = DockStyle.Top;
        var intro = Theme.Label("选择关闭主窗口时的行为。", Theme.Small, Theme.Muted);
        intro.Dock = DockStyle.Top; intro.Margin = new Padding(0, 0, 0, 20);
        var panel = new SurfacePanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var options = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize)); options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        exitOnClose.Dock = DockStyle.Top; exitOnClose.Margin = new Padding(0, 0, 0, 12);
        var hint = Theme.Label("不勾选时，关闭窗口会隐藏到系统托盘。点击托盘图标可重新打开，右键菜单可退出程序。", Theme.Small, Theme.Muted);
        hint.Dock = DockStyle.Top;
        options.SizeChanged += (_, _) => hint.MaximumSize = new Size(Math.Max(1, options.ClientSize.Width), 0);
        options.Controls.Add(exitOnClose, 0, 0); options.Controls.Add(hint, 0, 1); panel.Controls.Add(options);
        error.Dock = DockStyle.Top; error.Margin = new Padding(0, 12, 0, 12); error.Visible = false;
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(24, 12, 24, 20), BackColor = Color.White };
        var save = Theme.Button("保存设置", true); var cancel = Theme.Button("取消"); cancel.Width = 88; cancel.DialogResult = DialogResult.Cancel;
        save.Margin = Padding.Empty; cancel.Margin = new Padding(0, 0, 12, 0);
        save.Click += (_, _) => SaveSettings(); actions.Controls.AddRange([save, cancel]); AcceptButton = save; CancelButton = cancel;
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(intro, 0, 1); layout.Controls.Add(panel, 0, 2);
        layout.Controls.Add(error, 0, 3);
        layout.SizeChanged += (_, _) => error.MaximumSize = new Size(Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal), 0);
        scroll.Layout += (_, _) => layout.MaximumSize = new Size(Math.Max(1, scroll.ClientSize.Width - scroll.Padding.Horizontal -
            (scroll.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)), 0);
        scroll.Controls.Add(layout); root.Controls.Add(scroll, 0, 0); root.Controls.Add(actions, 0, 1);
        Controls.Add(root); ResumeLayout(true);
        Load += (_, _) => Theme.FitWindow(this);
    }

    internal void SaveSettings()
    {
        try { settings.Save(exitOnClose.Checked); DialogResult = DialogResult.OK; Close(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { error.Text = $"设置未保存：{ex.Message}"; error.Visible = true; }
    }
}
