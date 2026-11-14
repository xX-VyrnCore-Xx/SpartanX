# SpartanX for macOS

A native **SwiftUI** Dynamic Island that lives around the MacBook notch (or as a pill at the top
centre on Macs without one). macOS already has a Dock and a menu bar, so on the Mac SpartanX is
just the Island, plus its Setup Assistant and Settings.

| | |
|---|---|
| **Requires** | macOS 14 Sonoma or later · Apple Silicon or Intel (universal binary) |
| **UI** | SwiftUI (`@Observable`, native springs, `blurReplace` transitions), hosted in a non-activating `NSPanel` above the menu bar |
| **Now Playing** | Music and Spotify via Apple Events (macOS asks once per player) |
| **Also** | Timer with quick presets, battery & charging, weather (Open-Meteo), open at login (`SMAppService`) |

## Install

1. Download `SpartanX-<version>-macOS.dmg` from [Releases](https://github.com/xX-VyrnCore-Xx/SpartanX/releases).
2. Open it and drag **SpartanX** to **Applications**.
3. The app is not notarized yet, so the first launch is blocked:
   - **macOS 14:** right-click SpartanX in Applications → **Open** → **Open**.
   - **macOS 15+:** open it once, then **System Settings → Privacy & Security → Open Anyway**.
4. The Setup Assistant appears. SpartanX then lives in the menu bar (capsule icon).

Uninstall: menu bar icon → **Quit SpartanX**, turn off *Open at login* in Settings first if you enabled it, then drag the app to the Bin.

## Build from source

```bash
cd macos
```

```bash
swift test
```

```bash
swift run
```

`scripts/build-dmg.sh <version>` produces the universal, ad-hoc-signed `.app` and the DMG (this is what the Release workflow runs).

## Layout

```
macos/
├─ Package.swift
├─ Sources/SpartanXCore/   # pure logic, unit-tested: island state, activities, timer, SemVer, updates, weather, strings
├─ Sources/SpartanX/       # SwiftUI + AppKit app
│  ├─ Island/              # NSPanel controller, model, views (IslandShape, ears, peek, expanded)
│  ├─ Services/            # Now Playing (Apple Events), battery (IOKit), open at login
│  ├─ Settings/            # System Settings-style window
│  └─ Welcome/             # "hello" Setup Assistant
├─ Tests/SpartanXCoreTests/
├─ Resources/Info.plist    # LSUIElement agent app, Apple Events usage string
└─ scripts/build-dmg.sh
```
