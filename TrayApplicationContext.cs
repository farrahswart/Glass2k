namespace Glass2k;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
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
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ShowAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Glass2k is running in the notification area.",
            "About Glass2k",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ExitApplication(object? sender, EventArgs e)
    {
        ExitThread();
    }
}