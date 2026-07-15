using System.Threading;

namespace RUtils;

/// <summary>
/// Enables multiple simultaneous Roblox clients.
///
/// Roblox enforces single-instance by registering a named kernel object
/// (<c>ROBLOX_singletonEvent</c>) at startup. If it already exists, the new
/// client forwards its launch to the running one and exits.
///
/// We claim that name FIRST, as the owner, so Roblox can never register it and
/// its single-instance guard never fires. This mirrors what MultiBloxy does.
///
/// CONSTRAINT: r-utils must be holding the name BEFORE Roblox launches. If
/// Roblox is already running, the name is taken and we can't acquire it —
/// callers should surface that state.
/// </summary>
public sealed class SingletonHolder : IDisposable
{
    // Current Roblox uses "ROBLOX_singletonEvent"; older builds used
    // "ROBLOX_singletonMutex". We claim both so the feature survives either.
    private static readonly string[] Names =
    {
        "ROBLOX_singletonEvent",
        "ROBLOX_singletonMutex",
    };

    private readonly List<Mutex> _held = new();

    public bool IsActive { get; private set; }

    /// <summary>
    /// True if we could NOT claim the primary name because something (almost
    /// certainly an already-running Roblox) owns it. Multi-instance won't work
    /// for the current session until Roblox is fully closed and this is retried.
    /// </summary>
    public bool BlockedByExistingRoblox { get; private set; }

    public bool Acquire()
    {
        Release();
        BlockedByExistingRoblox = false;

        bool primaryClaimed = false;

        foreach (var name in Names)
        {
            try
            {
                // initiallyOwned: true -> this thread owns the object.
                var mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);

                if (!createdNew)
                {
                    // The name already existed (e.g. Roblox got there first).
                    // We opened a handle but don't own it — useless for our
                    // purpose, so drop it.
                    mutex.Dispose();
                    if (name == Names[0])
                        BlockedByExistingRoblox = true;
                    continue;
                }

                _held.Add(mutex);
                if (name == Names[0])
                    primaryClaimed = true;
            }
            catch (Exception)
            {
                // A name can exist as a different kernel object type, which can
                // throw rather than return createdNew=false. Treat as taken.
                if (name == Names[0])
                    BlockedByExistingRoblox = true;
            }
        }

        IsActive = primaryClaimed;
        return IsActive;
    }

    public void Release()
    {
        foreach (var mutex in _held)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // not owned by this thread — ignore
            }
            catch (Exception)
            {
                // ignore
            }
            finally
            {
                mutex.Dispose();
            }
        }

        _held.Clear();
        IsActive = false;
    }

    public void Dispose() => Release();
}
