# Contributing to SpartanX

Thanks for helping! Italian is welcome in issues and discussions too.

## Ground rules

- Be kind — see the [Code of Conduct](CODE_OF_CONDUCT.md).
- Open an issue before large changes so we can agree on the approach.
- Keep pull requests focused: one feature or fix per PR.

## Development setup

1. Windows 11 22H2+, [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Optional: Visual Studio 2026 with *WinUI application development*, or VS Code / Rider.
3. Clone, then run the tests and the app:

```bash
dotnet test tests/SpartanX.Core.Tests
```

```bash
dotnet run --project src/SpartanX.App -p:Platform=x64 -r win-x64
```

> SpartanX hides the Windows taskbar while running. If you stop the debugger abruptly the taskbar may stay hidden: start SpartanX again and quit it from its menu, or restart Explorer.

## Where things live

| Folder | What |
|---|---|
| `src/SpartanX.Core` | Platform-independent logic: settings, view-models, springs, magnification, localisation, updates. **Put logic here** and unit-test it. |
| `src/SpartanX.App` | WinUI 3 views, Windows services and Win32 interop. Keep it thin. |
| `tests/SpartanX.Core.Tests` | xUnit tests for Core (run on Linux CI too). |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the big picture.

## Coding guidelines

- C# latest, nullable enabled, file-scoped namespaces, `_camelCase` private fields.
- MVVM with CommunityToolkit.Mvvm (`[ObservableProperty]` partial properties, `[RelayCommand]`).
- Animations: drive sizes with `Spring` + `FrameTicker`; use `Motion` (Composition) for opacity/translation. Respect `Motion.Reduced`.
- Never block the UI thread; services raise events on background threads and view-models marshal through `IUiDispatcher`.
- New Win32 calls go in `Interop/NativeMethods.cs`.

## Localisation

UI strings live in `src/SpartanX.Core/Localization/Strings/{en,it}.json`. Add every new key to **both** files — a unit test fails otherwise. In XAML use `{l:Tr Key=Some.Key}`; in code `Localizer.Get("Some.Key")`.

Want to add a language? Copy `en.json` to `<code>.json`, translate, and add it to `Localizer.SupportedLanguages`.

## Commits & PRs

- Conventional-style messages are appreciated: `feat(dock): ...`, `fix(island): ...`, `docs: ...`.
- CI must be green (Core tests on Linux + app build for x64 and ARM64 on Windows).
- For UI changes, include a screenshot or short recording.

## Releasing (maintainers)

1. Update `CHANGELOG.md`.
2. Tag and push: `git tag v0.2.0 && git push origin v0.2.0` (use `-beta.N` suffix for pre-releases).
3. The *Release* workflow builds x64/ARM64 ZIPs with checksums and publishes the GitHub Release; in-app updaters pick it up automatically.
