# Open Taskbar

Click it once and every app pinned to your Windows taskbar opens, maximized.

- Reads your pins fresh on every click, so newly pinned (or unpinned) apps are picked up automatically.
  Desktop apps and Store apps (Teams, WhatsApp, Terminal, Settings, …) are both supported.
- Maximizes each new window after it appears, because many apps ignore the "start maximized" hint.
- Skips apps that are already open, so a second click doesn't create duplicate windows.
- Opens your apps only when you click it: the pinned taskbar icon, or the tray icon.
- Starts silently in the system tray when you sign in to Windows (no window, nothing opened).
  Left-click the tray icon to open all apps; right-click it for Settings and Exit.

## Requirements

- Windows 11 (tested). Windows 10 should work but hasn't been tested.
- Nothing else: it uses .NET Framework 4.8, which is built into Windows.

## Install

1. Download `OpenTaskbarSetup.exe` from the [Releases](../../releases) page and run it. No admin rights
   are needed; it installs to `%LOCALAPPDATA%\Programs\Open Taskbar`.
   - The installer isn't code-signed, so Windows SmartScreen may show "Windows protected your PC".
     Choose **More info**, then **Run anyway**.
2. Open Start, find **Open Taskbar**, right-click it and choose **Pin to taskbar**.
3. Windows 11 puts new tray icons in the overflow menu (the ^ arrow next to the clock). To keep the icon
   visible, drag it onto the taskbar, or turn it on under
   Settings > Personalization > Taskbar > Other system tray icons.

To uninstall, use Settings > Apps > Installed apps > Open Taskbar.

## Settings

Right-click the tray icon and choose **Settings**, hold **Shift** while clicking the pinned icon, or open
**Open Taskbar Settings** from Start, to:

- uncheck pinned apps you don't want opened,
- turn maximizing on or off,
- turn "don't reopen apps that are already open" on or off,
- turn starting in the system tray at sign-in on or off.

Settings live in `%APPDATA%\OpenTaskbar\settings.ini`. `last-run.log` in the same folder records what the
last run launched, skipped and maximized.

## Command line

| Command | What it does |
|---|---|
| `OpenTaskbar.exe` | Opens all pinned apps (what clicking the icon does) |
| `OpenTaskbar.exe --tray` | Runs silently in the system tray (used at sign-in) |
| `OpenTaskbar.exe --settings` | Opens the settings window |
| `OpenTaskbar.exe --list` | Prints the detected pins and which open windows match them; useful when an app isn't picked up |

## Build from source

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer; the app itself compiles with
the C# compiler that ships with Windows.

```powershell
.\build.ps1
```

This produces two files:

- `dist\OpenTaskbarSetup.exe`: the installer. **This is the one to run or publish.**
- `build\OpenTaskbar.exe`: the app itself, which the installer packages. Running it directly opens all
  your pinned apps; it doesn't install anything.

The icon is drawn by `tools\make-icon.ps1`, which writes `assets\app.ico`. It contains several designs:
`.\tools\make-icon.ps1 -PreviewDir <folder>` renders PNG previews of all of them, and
`.\tools\make-icon.ps1 -Design <name>` switches the icon.

## How it works

Explorer stores the taskbar pins in `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband`
(`Favorites`) as a list of shell item IDs; Store apps exist only there, not as `.lnk` files. Each item is
launched through the shell the same way the taskbar launches it. New windows are then matched back to the
pinned apps by executable path or AppUserModelID and maximized.

## Known limitations

- The pin list format is undocumented and could change in a future Windows update. The `.lnk` files in
  `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar` act as a fallback, but they
  don't include Store apps.
- For up to 60 seconds after a click, any new window from a program that starts in that time is also
  maximized. This catches apps that open through a launcher, but it can also catch a window you open
  yourself in that minute.
- Apps running as administrator may not get maximized, because Windows limits what a normal app can do
  to their windows.
- Some Store apps (e.g. WhatsApp) show a blank icon in the settings list. They still launch normally.

## License

[MIT](LICENSE)
