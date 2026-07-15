using Microsoft.Win32;

namespace RUtils;

/// <summary>
/// Toggles "start r-utils when Windows starts" via the per-user Run key
/// (HKCU\...\CurrentVersion\Run). Per-user, so no admin needed.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "r-utils";

    /// <summary>Path to the running executable, quoted for the Run key.</summary>
    private static string? ExePath
    {
        get
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) ? null : $"\"{path}\"";
        }
    }

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                var value = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Enable/disable autostart. Returns true on success.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
                return false;

            if (enabled)
            {
                var path = ExePath;
                if (path is null)
                    return false;
                key.SetValue(ValueName, path);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
