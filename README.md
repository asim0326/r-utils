# r-utils

A tiny Windows system-tray app for Roblox. You open it and do nothing else — it
turns on two things automatically and stays out of the way:

1. **Multi-instance** — run as many Roblox clients at once as you want.
2. **FPS unlock** — removes the frame-rate cap (target 9999 ≈ "no limit").

~Nothing to configure. Launch it before you launch Roblox.~

## How it works

### Multi-instance (`SingletonHolder.cs`)
Roblox enforces single-instance by registering a named kernel object
(`ROBLOX_singletonEvent`) when it starts. If that name already exists, the new
client hands its launch off to the running one and exits.

r-utils claims that name **first**, as the owner, so Roblox can never register
it — its single-instance guard simply never fires, and every launch opens a
fresh window. (Same technique as MultiBloxy.) We also claim the legacy
`ROBLOX_singletonMutex` name for older builds.

> **Must be running before Roblox.** If Roblox is already open, it owns the name
> and r-utils can't claim it. The tray icon shows *BLOCKED* — close all Roblox
> windows and pick **Re-apply now**.

### FPS unlock (`FpsUnlocker.cs`)
Instead of editing Roblox's memory (the old rbxfpsunlocker approach, which
Hyperion anti-cheat can flag), r-utils writes a **FastFlag** that Roblox reads
natively at startup:

```json
{ "DFIntTaskSchedulerTargetFps": 9999 }
```

It merges this into `ClientSettings\ClientAppSettings.json` inside every
installed Roblox version folder under
`%LOCALAPPDATA%\Roblox\Versions\version-XXXX\`, preserving any flags already
there. A `FileSystemWatcher` re-applies automatically when a Roblox update
creates a new version folder. No injection, no memory writes — nothing for
anti-cheat to see.

## Build

Just double-click **`build.bat`**. It builds a **self-contained, single-file
`r-utils.exe`** into the `build\` folder next to it, then offers to launch it.

- The produced exe needs **no dependencies to run** — copy `build\r-utils.exe`
  to any Windows 10/11 x64 machine and run it, nothing to install.
- **Building** does require the **.NET 9 SDK (or newer)** on the machine you
  build on. If it's missing, `build.bat` tells you and links the download.
  (There's no way around this — compiling .NET needs the SDK. Only the *output*
  is dependency-free.)

Typical workflow: push this repo to GitHub → clone/download on another machine →
double-click `build.bat` → run the exe it produces.

Manual equivalent:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o build
```

r-utils appears in the system tray (a blue **R**); right-click for status /
Re-apply / Start-with-Windows / Exit. Exiting releases the singleton name so
Roblox goes back to normal single-instance behaviour.

> On Windows 11 the tray icon may start in the hidden-icons (`^`) overflow —
> drag it onto the taskbar to pin it.

## Caveats

- **Version-fragile.** If Roblox renames the singleton object or the FPS flag,
  update the constants in `SingletonHolder.cs` / `FpsUnlocker.cs`.
- **ToS gray area.** Multi-instance and FastFlag tweaks aren't clearly banned
  and are widely used, but they live near things Roblox dislikes. Use at your
  own risk.
- **"No limit" is bounded by your hardware** (GPU/monitor). Extremely high frame
  rates can cause odd physics/throttling behaviour in some games.

## Status

Core logic (FPS flag writing/merging, singleton acquire/block/release) is
covered by self-tests. Multi-instance behaviour against a live Roblox client has
**not** yet been verified on a machine with Roblox installed — do that before
relying on it.
