# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

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
