using System.Diagnostics;

namespace RUtils;

/// <summary>
/// The whole UI: a tray icon that turns both features on at launch and then
/// stays out of the way. The user opens r-utils and does nothing else.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly SingletonHolder _singleton = new();
    private readonly FpsUnlocker _fps = new(targetFps: 9999);

    private readonly ToolStripMenuItem _statusMulti;
    private readonly ToolStripMenuItem _statusFps;
    private readonly ToolStripMenuItem _startupItem;
    private System.Windows.Forms.Timer? _reassert;

    public TrayApplicationContext()
    {
        _statusMulti = new ToolStripMenuItem { Enabled = false };
        _statusFps = new ToolStripMenuItem { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("r-utils") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusMulti);
        menu.Items.Add(_statusFps);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Re-apply now", null, (_, _) => ApplyAll());
        menu.Items.Add("Open Roblox versions folder", null, (_, _) => OpenVersionsFolder());
        menu.Items.Add(new ToolStripSeparator());
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            CheckOnClick = false, // we set Checked ourselves to reflect the real registry state
            Checked = StartupManager.IsEnabled,
        };
        menu.Items.Add(_startupItem);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Text = "r-utils",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ApplyAll();

        // Configure everything BEFORE making it visible (ordering can affect
        // whether the shell renders the icon reliably).
        _tray.Visible = true;

        ApplyAll();

        // Give a visible confirmation it launched — on Windows 11 the icon
        // itself defaults into the hidden "^" overflow, so a toast tells the
        // user it's running and where to look.
        _tray.BalloonTipTitle = "r-utils is running";
        _tray.BalloonTipText =
            "Multi-instance + FPS unlock are on. If you don't see the icon, "
            + "click the ^ (hidden icons) arrow by the clock and drag it onto the taskbar.";
        _tray.ShowBalloonTip(6000);

        // Windows 11 workaround: an icon added during startup sometimes fails to
        // register with the shell. Re-assert visibility a moment later to force a
        // fresh Shell_NotifyIcon add.
        _reassert = new System.Windows.Forms.Timer { Interval = 1500 };
        _reassert.Tick += (_, _) =>
        {
            _reassert!.Stop();
            try
            {
                _tray.Visible = false;
                _tray.Visible = true;
            }
            catch { /* best-effort */ }
        };
        _reassert.Start();
    }

    private void ApplyAll()
    {
        // --- Multi-instance ---
        _singleton.Acquire();

        // --- FPS unlock ---
        _fps.Start();

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string multi = _singleton.IsActive
            ? "Multi-instance: ON"
            : _singleton.BlockedByExistingRoblox
                ? "Multi-instance: BLOCKED (close Roblox, then Re-apply)"
                : "Multi-instance: OFF";

        string fps = !_fps.RobloxInstalled
            ? "FPS unlock: waiting for Roblox install"
            : _fps.LastAppliedCount > 0
                ? $"FPS unlock: ON ({_fps.TargetFps}) x{_fps.LastAppliedCount}"
                : "FPS unlock: no client folders found";

        _statusMulti.Text = multi;
        _statusFps.Text = fps;

        _tray.Text = $"r-utils\n{multi}\n{fps}".Length <= 63
            ? $"r-utils — {(_singleton.IsActive ? "multi ON" : "multi off")}, {(_fps.LastAppliedCount > 0 ? "fps ON" : "fps -")}"
            : "r-utils";

        if (_singleton.BlockedByExistingRoblox)
        {
            _tray.BalloonTipTitle = "r-utils";
            _tray.BalloonTipText =
                "Roblox is already running, so multi-instance couldn't turn on. " +
                "Close all Roblox windows, then pick \"Re-apply now\".";
            _tray.ShowBalloonTip(5000);
        }
    }

    private void ToggleStartup()
    {
        bool desired = !_startupItem.Checked;
        if (StartupManager.SetEnabled(desired))
        {
            _startupItem.Checked = desired;
        }
        else
        {
            // reflect the real state and tell the user it didn't take
            _startupItem.Checked = StartupManager.IsEnabled;
            MessageBox.Show("Couldn't update the Windows startup setting.",
                "r-utils", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenVersionsFolder()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "Versions");
        try
        {
            if (Directory.Exists(root))
                Process.Start(new ProcessStartInfo(root) { UseShellExecute = true });
            else
                MessageBox.Show("Roblox isn't installed at the default location yet.",
                    "r-utils", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { /* ignore */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reassert?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _fps.Dispose();
            _singleton.Dispose(); // releases the singleton name so Roblox goes back to normal
        }
        base.Dispose(disposing);
    }
}
