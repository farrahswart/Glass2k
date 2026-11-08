namespace Glass2k;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ForegroundWindowTracker foregroundWindowTracker;
    private readonly WindowOpacityService windowOpacityService;
    private readonly NotifyIcon trayIcon;

    public TrayApplicationContext()
    {
        foregroundWindowTracker = new ForegroundWindowTracker();
        windowOpacityService = new WindowOpacityService();

        var menu = new ContextMenuStrip();
        var opacityMenu = new ToolStripMenuItem("Last active window opacity");
        opacityMenu.DropDownItems.Add("100% (restore)", null, (_, _) => SetOpacity(100));
        opacityMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (var opacity in new[] { 90, 80, 70, 60, 50 })
        {
            opacityMenu.DropDownItems.Add($"{opacity}%", null, (_, _) => SetOpacity(opacity));
        }

        menu.Items.Add(opacityMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About Glass2k", null, ShowAbout);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, ExitApplication);

        trayIcon = new NotifyIcon
        {
            Text = "Glass2k",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += ShowAbout;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            windowOpacityService.RestoreAll();
            foregroundWindowTracker.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ShowAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Glass2k is running in the notification area. Choose an opacity level to change the last active window.",
            "About Glass2k",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SetOpacity(int opacityPercent)
    {
        var window = foregroundWindowTracker.LastExternalWindow;
        if (window == IntPtr.Zero)
        {
            MessageBox.Show(
                "Activate the window you want to change, then choose an opacity level from the Glass2k tray menu.",
                "No active window",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!windowOpacityService.TrySetOpacity(window, opacityPercent, out var error))
        {
            MessageBox.Show(error, "Glass2k", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExitApplication(object? sender, EventArgs e)
    {
        Dispose();
        ExitThread();
    }
}