<p align="center">
  <img src="docs/assets/logo.png" width="128" alt="SpartanX logo" />
</p>

<h1 align="center">SpartanX</h1>

<p align="center">
  <b>A fluid Dynamic Island, Dock and menu bar for Windows 11.</b><br/>
  Spring-physics animations, a macOS-style Setup Assistant and a System Settings–style preferences window.
</p>

<p align="center">
  <a href="https://github.com/xX-VyrnCore-Xx/SpartanX/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/xX-VyrnCore-Xx/SpartanX/actions/workflows/ci.yml/badge.svg" /></a>
  <a href="https://github.com/xX-VyrnCore-Xx/SpartanX/releases"><img alt="Release" src="https://img.shields.io/github/v/release/xX-VyrnCore-Xx/SpartanX?include_prereleases&sort=semver" /></a>
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-B3202A" /></a>
  <img alt="Windows 11" src="https://img.shields.io/badge/Windows%2011-22H2%2B-0078D4" />
</p>

<p align="center"><a href="README.it.md">🇮🇹 Leggi in italiano</a> · <a href="macos/README.md">🍎 macOS version</a></p>

---

> **Status: early preview (0.1).** Core features work; expect rough edges. Feedback and PRs are very welcome.

## ✨ Features

> **On a Mac?** SpartanX for macOS is a native SwiftUI Dynamic Island around the notch — see [macos/README.md](macos/README.md). Everything below is the Windows 11 app.

### Dynamic Island
- **Black pill under the top edge** that morphs between *compact*, *peek* (hover) and *expanded* (click) with real spring physics — interrupted animations keep their velocity.
- **Now Playing** from any app in the Windows media flyout (Spotify, browsers, Apple Music, VLC…): album art, title, artist, progress, previous / play-pause / next and a **live audio meter** driven by the real output level.
- **System activities**: volume changes, charger plugged/unplugged, low battery, network connected/lost.
- **Timer** with quick presets (1/5/10/25 min), pause and cancel.
- **Weather** for your city (Open-Meteo, no account, no API key).
- **Notifications** mirroring *(needs the upcoming signed package — see [Roadmap](docs/ROADMAP.md))*.

### Dock
- **macOS-style magnification** with a smooth cosine falloff and per-icon springs.
- **Launch bounce**, running-app indicators, active-app click-to-minimise.
- **Pinned apps + running apps**, your Windows taskbar pins imported on first run.
- **Folder stacks** (Downloads by default) opened as a grid, **Recycle Bin**.
- **Auto-hide**, bottom / left / right position, drag & drop apps or folders to pin.
- Right-click menus: window list, Keep in Dock, Show in Explorer, Quit.
- **Replaces the Windows taskbar** (optional) — always restored when SpartanX quits, even after a crash.

### Menu bar
- SpartanX menu (Settings, Start, Task Manager, Sleep, Restart, Shut down, Lock).
- Active app name, Wi-Fi/Ethernet, volume slider, battery, notification center, clock with calendar.
- Registered as a shell AppBar, so maximised windows start below it.

### Setup Assistant & Settings
- First-run **"hello" in many languages**, then Language → Appearance → Dock → Island → Privacy, with live previews.
- **System Settings–style window**: sidebar with coloured icons and search; General, Appearance, Dock, Dynamic Island, Menu Bar, Animations, Keyboard Shortcuts, Modules & Plugins, About.
- Light / Dark / Auto, **Spartan red** default accent (or Windows accent, presets, custom hex), Mica / Acrylic / Mica Alt.
- **Animation control**: Smooth / Snappy / Bouncy / Custom spring (response + damping), speed, Reduce Motion.
- **Global shortcuts** (recordable): `Ctrl+Alt+D` Dock hiding, `Ctrl+Alt+I` Island, `Ctrl+Alt+,` Settings, `Ctrl+Alt+Space` play/pause, `Ctrl+Alt+T` 5-min timer.
- **English and Italian** UI.
- **Installer + in-app updates** from GitHub Releases.

## 📦 Install

1. Download **`SpartanX-Setup-<version>-x64.exe`** from [Releases](https://github.com/xX-VyrnCore-Xx/SpartanX/releases) (`-arm64.exe` for ARM PCs such as Snapdragon laptops).
2. Run it and follow the wizard. It installs for your user only — **no admin rights** — into `%LOCALAPPDATA%\Programs\SpartanX`, adds a Start menu entry and registers SpartanX in **Settings → Apps → Installed apps**.
3. The Setup Assistant opens on first launch.

Updates install themselves: Settings → General → **Check now → Update now** (the installer runs silently and restarts SpartanX).

**Requirements:** Windows 11 22H2 or later, x64 or ARM64. No other runtime needed.

> ⚠️ The installer is not code-signed yet, so SmartScreen may warn (*More info → Run anyway*). Verify the `.sha256` file published next to it.

### Uninstall

Windows **Settings → Apps → Installed apps → SpartanX → Uninstall**. The uninstaller first closes every SpartanX process, restores the Windows taskbar and removes launch at login, then asks whether to delete your settings too.

Command-line helpers (also used by the installer):

| Command | Effect |
|---|---|
| `SpartanX.exe --quit` | Closes all SpartanX processes and restores the taskbar |
| `SpartanX.exe --uninstall` | `--quit` + removes launch at login |
| `… --purge` | Also deletes `%LOCALAPPDATA%\SpartanX` (settings) |
| `… --silent` | No confirmation dialog |

## 🛠️ Build from source

Prerequisites: Windows 11, [.NET 10 SDK](https://dotnet.microsoft.com/download), optionally Visual Studio 2026 with the *WinUI application development* workload.

```bash
git clone https://github.com/xX-VyrnCore-Xx/SpartanX.git
```

```bash
cd SpartanX
```

```bash
dotnet test tests/SpartanX.Core.Tests
```

```bash
dotnet run --project src/SpartanX.App -p:Platform=x64 -r win-x64
```

The platform-independent logic (`SpartanX.Core`) builds and tests on Linux and macOS too.

## 🧭 Project structure

```
SpartanX/
├─ src/
│  ├─ SpartanX.Core/        # Cross-platform: settings, view-models, spring physics,
│  │                        # magnification math, localisation, updater, weather
│  └─ SpartanX.App/         # WinUI 3 app (Windows App SDK 1.8, unpackaged)
├─ macos/                   # Native SwiftUI Dynamic Island for macOS (Swift package)
│     ├─ Shell/             # Island, Dock, Menu bar windows + ShellController
│     ├─ Welcome/           # Setup Assistant
│     ├─ Settings/          # System Settings–style window and pages
│     ├─ Services/          # Media (SMTC), audio, battery, network, windows, hotkeys…
│     ├─ Interop/           # Win32: AppBar, taskbar, window styles
│     └─ Animation/         # Frame ticker + composition helpers
├─ tests/SpartanX.Core.Tests/
└─ docs/                    # Architecture, roadmap, plugin proposal
```

Read more in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 🔒 Privacy

No telemetry, no account. SpartanX only goes online to check GitHub Releases for updates (can be turned off) and, if you enable the weather module, to send your **city name** to [Open-Meteo](https://open-meteo.com). Settings live in `%LOCALAPPDATA%\SpartanX\settings.json`.

## ❓ FAQ

**My taskbar disappeared and SpartanX isn't running.** Start SpartanX and quit it from its menu, or turn off *Replace the Windows taskbar* in Settings → General. SpartanX remembers your original taskbar state and restores it.

**Notifications don't appear in the island.** Windows only exposes notifications to apps with package identity. The unpackaged ZIP can't read them yet; a signed package is planned.

**Multiple monitors?** The shell currently lives on the primary display. Per-monitor Docks are on the roadmap.

## 🤝 Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). Security issues: [SECURITY.md](SECURITY.md).

## 📄 License

[MIT](LICENSE) © SpartanX contributors.

SpartanX is an independent project and is not affiliated with Apple or Microsoft. "Dynamic Island" and "Dock" describe UI concepts; all trademarks belong to their owners.
