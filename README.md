# Clickold

Clickold records and replays mouse and keyboard actions on Windows. Use it to repeat a desktop task or run through the same sequence of clicks and keystrokes during manual testing.

**Clickold is free for everyone to download and use.** The portable release for Windows x64 includes .NET and runs without installation. [Download Clickold.exe](https://github.com/ITSTEPNOV/Clickold/releases/latest/download/Clickold.exe)

## Quick start

1. Press **F9** to record and **F10** to finish. Save the recording as a JSON macro if you want to reuse it.
2. Set the repeat count and speed, or select **Loop** for continuous playback.
3. Press **F6** to play, **F8** to pause or resume, and **F7** to stop. You can reassign these global hotkeys in the app.

Clickold uses recorded screen coordinates, so keep your target window in the same position when replaying. Macro files can contain typed text and other recorded input. Review them before sharing.

The interface follows the Windows display language. Russian and Simplified Chinese are available, with English as the fallback. The Standard theme includes the robot illustration. The red music note plays or stops **Cuck Chair**, an original track by the project author created with Suno V6.

## Build

Install the .NET 8 SDK on Windows and run:

```powershell
dotnet publish -c Release -p:PublishProfile=Portable
```

The executable is written to `bin/Portable/Clickold.exe`. Theme and hotkey preferences are stored in `%LOCALAPPDATA%\Clickold`.
