using System.Threading;

namespace RUtils;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Surface any crash instead of dying silently.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write($"UNHANDLED (domain): {e.ExceptionObject}");
        Application.ThreadException += (_, e) =>
            Log.Write($"UNHANDLED (thread): {e.Exception}");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // Only one r-utils at a time — two of them fighting over the Roblox
        // singleton name would be pointless.
        using var selfLock = new Mutex(true, "r-utils-singleinstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("r-utils is already running (check the system tray).",
                "r-utils", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            Log.Write($"FATAL in Run: {ex}");
            MessageBox.Show("r-utils failed to start:\n\n" + ex.Message,
                "r-utils", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
