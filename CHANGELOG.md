# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- **SpartanX for macOS**: a native SwiftUI Dynamic Island around the notch (pill on Macs without one) — Now Playing for Music and Spotify, timer, battery/charging, weather; hover to peek, click to open; macOS-style Setup Assistant and System Settings-style preferences (EN/IT); open at login. Universal DMG (Apple Silicon + Intel), macOS 14+.

## [0.1.2] - 2026-11-13

### Changed
- **Releases now ship a real installer** (`SpartanX-Setup-<version>-<arch>.exe`, Inno Setup): per-user, no admin, Start menu entry, listed in Windows *Installed apps* with an uninstaller that closes SpartanX and restores the taskbar first. The ZIPs, `Install.cmd` and `Uninstall.cmd` are gone.
- In-app updates download and run the installer silently.

### Fixed
- Restarting (language change, *Reset all settings*) or updating left SpartanX closed: the new instance saw the old one still running and exited.
- Album art was decoded again every second while music played.
- Removing every app from the Dock re-imported the taskbar pins at the next start.
- Changing theme or accent made Settings and the menu bar flash (backdrop was recreated).
- Turning off the Timer / Now Playing / system status / notifications modules didn't take effect until restart; quick timers still showed with the Timer module off.
- When SpartanX started at sign-in before Explorer, the hidden taskbar could keep its strip reserved at the bottom of the screen.
- Restoring the taskbar no longer pops up a taskbar that was set to auto-hide.

## [0.1.1] - 2026-11-13

### Added
- `SpartanX.exe --quit` and `--uninstall [--purge] [--silent]`: close every SpartanX process (graceful, then forced), give the work area back, restore the taskbar, remove launch at login and optionally the settings.
- Settings → General → *Prepare to uninstall*.
- `Install.cmd` (per-user install + Start menu shortcut) and `Uninstall.cmd` in the release ZIP.

### Changed
- The in-app updater also handles packages that wrap files in a `SpartanX\` folder.
- Quitting now guarantees the process exits within 3 seconds.

## [0.1.0] - 2026-11-13

### Added
- Dynamic Island with compact / peek / expanded states and spring morphing.
- Island activities: Now Playing (SMTC) with live audio meter, volume, battery, network, timer, weather (Open-Meteo), notifications (packaged builds only).
- Dock with macOS-style magnification, launch bounce, running indicators, folder stacks, Recycle Bin, auto-hide, left/right/bottom positions, drag & drop pinning and context menus.
- Menu bar with SpartanX menu, active app name, status icons, volume flyout and calendar.
- Optional replacement of the Windows taskbar with crash-safe restore.
- macOS-style Setup Assistant (multilingual "hello", language, appearance, Dock, Island, privacy).
- System Settings–style preferences with search: General, Appearance, Dock, Dynamic Island, Menu Bar, Animations, Keyboard Shortcuts, Modules & Plugins, About.
- Global, recordable keyboard shortcuts.
- English and Italian localisation.
- In-app updates from GitHub Releases; CI and release workflows.
