using System.Text.Json;
using System.Text.Json.Nodes;

namespace RUtils;

/// <summary>
/// "Unlocks" Roblox's frame rate by writing a FastFlag rather than editing
/// process memory. Roblox reads <c>ClientSettings/ClientAppSettings.json</c>
/// from each version folder at startup and honours the frame cap there.
///
/// This is the modern, anti-cheat-safe method: no injection, no memory writes,
/// nothing for Hyperion to detect — just a config value Roblox already reads.
/// (rbxfpsunlocker's old approach patched the task scheduler in memory, which
/// this deliberately avoids.)
/// </summary>
public sealed class FpsUnlocker : IDisposable
{
    private const string FpsFlag = "DFIntTaskSchedulerTargetFps";

    /// <summary>Effectively "no limit" — real ceiling is the GPU/monitor.</summary>
    public int TargetFps { get; }

    private readonly string _versionsRoot;
    private FileSystemWatcher? _watcher;

    public FpsUnlocker(int targetFps = 9999, string? versionsRoot = null)
    {
        TargetFps = targetFps;
        _versionsRoot = versionsRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "Versions");
    }

    /// <summary>Roblox not installed yet (or at a non-standard path).</summary>
    public bool RobloxInstalled => Directory.Exists(_versionsRoot);

    /// <summary>Number of version folders the flag was written to on the last apply.</summary>
    public int LastAppliedCount { get; private set; }

    /// <summary>
    /// Applies the flag to every installed version, then watches for new
    /// versions (Roblox updates create a fresh folder) and applies to those too.
    /// </summary>
    public void Start()
    {
        ApplyToAll();

        if (!RobloxInstalled)
            return;

        _watcher = new FileSystemWatcher(_versionsRoot)
        {
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };
        // A Roblox update drops in a new version-* folder with its own exe;
        // re-apply so the new client is covered too.
        _watcher.Created += (_, _) => SafeApplyToAll();
        _watcher.Renamed += (_, _) => SafeApplyToAll();
    }

    private void SafeApplyToAll()
    {
        try { ApplyToAll(); } catch { /* watcher callbacks must never throw */ }
    }

    public int ApplyToAll()
    {
        LastAppliedCount = 0;

        if (!RobloxInstalled)
            return 0;

        foreach (var versionDir in Directory.GetDirectories(_versionsRoot))
        {
            // Only touch folders that actually contain a player/studio client.
            bool isClient =
                File.Exists(Path.Combine(versionDir, "RobloxPlayerBeta.exe")) ||
                File.Exists(Path.Combine(versionDir, "RobloxStudioBeta.exe"));

            if (!isClient)
                continue;

            if (ApplyToVersion(versionDir))
                LastAppliedCount++;
        }

        return LastAppliedCount;
    }

    /// <summary>Merge the FPS flag into a version's ClientAppSettings.json,
    /// preserving any flags already there.</summary>
    private bool ApplyToVersion(string versionDir)
    {
        try
        {
            var settingsDir = Path.Combine(versionDir, "ClientSettings");
            Directory.CreateDirectory(settingsDir);
            var file = Path.Combine(settingsDir, "ClientAppSettings.json");

            JsonObject root;
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file);
                root = string.IsNullOrWhiteSpace(text)
                    ? new JsonObject()
                    : (JsonNode.Parse(text) as JsonObject ?? new JsonObject());
            }
            else
            {
                root = new JsonObject();
            }

            // Already correct? Skip the write to avoid needless disk churn.
            if (root.TryGetPropertyValue(FpsFlag, out var existing)
                && existing is JsonValue v
                && v.TryGetValue(out int current)
                && current == TargetFps)
            {
                return true;
            }

            root[FpsFlag] = TargetFps;

            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(file, root.ToJsonString(opts));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
