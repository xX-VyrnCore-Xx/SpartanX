using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Settings;

namespace SpartanX.Core.Dock;

/// <summary>
/// Merges pinned apps with running windows into the ordered list the Dock shows:
/// pinned apps, then running unpinned apps, then a separator and folder stacks.
/// </summary>
public sealed partial class DockViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IWindowTracker _windows;
    private readonly IAppLauncher _launcher;
    private readonly IIconProvider _icons;

    public DockViewModel(ISettingsService settings, IWindowTracker windows, IAppLauncher launcher, IIconProvider icons)
    {
        _settings = settings;
        _windows = windows;
        _launcher = launcher;
        _icons = icons;

        _windows.Changed += (_, _) => Rebuild();
        _settings.Changed += (_, e) =>
        {
            if (e.PropertyName is nameof(DockSettings.PinnedApps) or nameof(DockSettings.Stacks) or null)
            {
                Rebuild();
            }
        };
    }

    public DockSettings Settings => _settings.Current.Dock;

    /// <summary>App items (pinned + running).</summary>
    public ObservableCollection<DockItemViewModel> Apps { get; } = [];

    /// <summary>Folder stacks shown after the separator.</summary>
    public ObservableCollection<DockItemViewModel> Stacks { get; } = [];

    [ObservableProperty] public partial bool HasStacks { get; set; }

    public static string NormalizeKey(string path) =>
        System.IO.Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant();

    /// <summary>Rebuilds the item list while preserving existing view-models (and their icons/animations).</summary>
    public void Rebuild()
    {
        var existing = Apps.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
        var windowsByExe = _windows.Windows
            .Where(w => !string.IsNullOrEmpty(w.ExecutablePath))
            .GroupBy(w => NormalizeKey(w.ExecutablePath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TrackedWindow>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        var desired = new List<DockItemViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in Settings.PinnedApps)
        {
            var key = SafeKey(_launcher.ResolveExecutable(pin.Path));
            if (key is null || !seen.Add(key))
            {
                continue;
            }

            var item = existing.GetValueOrDefault(key) ?? Create(key, pin.Name, pin.Path, pin.Arguments);
            item.IsPinned = true;
            desired.Add(item);
        }

        foreach (var (key, windows) in windowsByExe)
        {
            if (!seen.Add(key))
            {
                continue;
            }

            var exe = windows[0].ExecutablePath!;
            var item = existing.GetValueOrDefault(key) ?? Create(key, _launcher.GetDisplayName(exe), exe, null);
            item.IsPinned = false;
            desired.Add(item);
        }

        var foreground = _windows.ForegroundWindow;
        foreach (var item in desired)
        {
            item.Windows = windowsByExe.GetValueOrDefault(item.Key) ?? [];
            item.IsRunning = item.Windows.Count > 0;
            item.IsActive = item.Windows.Any(w => w.Handle == foreground);
            if (item.IsRunning)
            {
                item.IsLaunching = false;
            }
        }

        Sync(Apps, desired);
        RebuildStacks();
    }

    private void RebuildStacks()
    {
        var existing = Stacks.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
        var desired = new List<DockItemViewModel>();
        foreach (var folder in Settings.Stacks)
        {
            var key = SafeKey(folder);
            if (key is null || desired.Any(d => d.Key == key))
            {
                continue;
            }

            desired.Add(existing.GetValueOrDefault(key) ?? CreateStack(key, folder));
        }

        Sync(Stacks, desired);
        HasStacks = Stacks.Count > 0;
    }

    /// <summary>Minimal in-place sync so ItemsRepeater animates only real changes.</summary>
    private static void Sync(ObservableCollection<DockItemViewModel> target, List<DockItemViewModel> desired)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], desired[i]))
            {
                continue;
            }

            var current = target.IndexOf(desired[i]);
            if (current >= 0)
            {
                target.Move(current, i);
            }
            else
            {
                target.Insert(i, desired[i]);
            }
        }
    }

    private DockItemViewModel Create(string key, string name, string path, string? args)
    {
        var item = new DockItemViewModel(DockItemKind.App, key, name, path, args);
        _ = LoadIconAsync(item, path);
        return item;
    }

    private DockItemViewModel CreateStack(string key, string folder)
    {
        var name = System.IO.Path.GetFileName(folder.TrimEnd('\\', '/'));
        var item = new DockItemViewModel(DockItemKind.Stack, key, string.IsNullOrEmpty(name) ? folder : name, folder);
        _ = LoadIconAsync(item, folder);
        return item;
    }

    private async Task LoadIconAsync(DockItemViewModel item, string path)
    {
        try
        {
            item.Icon = await _icons.GetIconAsync(path, (int)Math.Ceiling(Settings.IconSize * Settings.MagnificationScale));
        }
        catch (Exception)
        {
            // Missing icon is cosmetic: the view shows a placeholder glyph.
        }
    }

    private static string? SafeKey(string path)
    {
        try
        {
            return NormalizeKey(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task ActivateAsync(DockItemViewModel? item)
    {
        if (item is null || item.Path is null)
        {
            return;
        }

        if (item.Kind == DockItemKind.Stack)
        {
            return; // The view opens the stack flyout.
        }

        if (item.IsRunning)
        {
            if (item.IsActive && Settings.MinimizeOnClickWhenActive)
            {
                foreach (var w in item.Windows)
                {
                    _windows.Minimize(w.Handle);
                }
            }
            else
            {
                // Bring the most recently listed window forward (EnumWindows is z-ordered).
                _windows.Activate(item.Windows[0].Handle);
            }

            return;
        }

        await LaunchAsync(item);
    }

    [RelayCommand]
    private Task NewWindowAsync(DockItemViewModel? item) =>
        item?.Path is null ? Task.CompletedTask : LaunchAsync(item);

    private async Task LaunchAsync(DockItemViewModel item)
    {
        item.IsLaunching = true;
        if (Settings.BounceOnLaunch)
        {
            item.RequestBounce();
        }

        var ok = await _launcher.LaunchAsync(item.Path!, item.Arguments);
        if (!ok)
        {
            item.IsLaunching = false;
        }
    }

    [RelayCommand]
    private void Quit(DockItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var w in item.Windows)
        {
            _windows.Close(w.Handle);
        }
    }

    [RelayCommand]
    private void TogglePin(DockItemViewModel? item)
    {
        if (item?.Path is null || item.Kind != DockItemKind.App)
        {
            return;
        }

        var pins = Settings.PinnedApps;
        var existing = pins.FirstOrDefault(p => SafeKey(_launcher.ResolveExecutable(p.Path)) == item.Key);
        if (existing is not null)
        {
            pins.Remove(existing);
        }
        else
        {
            pins.Add(new PinnedApp(item.DisplayName, item.Path, item.Arguments));
        }
    }

    [RelayCommand]
    private void Reveal(DockItemViewModel? item)
    {
        if (item?.Path is not null)
        {
            _launcher.RevealInExplorer(item.Path);
        }
    }

    [RelayCommand]
    private void RemoveStack(DockItemViewModel? item)
    {
        if (item?.Path is not null)
        {
            Settings.Stacks.Remove(item.Path);
        }
    }

    /// <summary>Moves a pinned app to a new position after a drag & drop.</summary>
    public void MovePinned(DockItemViewModel item, int newIndex)
    {
        var pins = Settings.PinnedApps;
        var old = pins.ToList().FindIndex(p => SafeKey(_launcher.ResolveExecutable(p.Path)) == item.Key);
        if (old < 0)
        {
            return;
        }

        pins.Move(old, Math.Clamp(newIndex, 0, pins.Count - 1));
    }
}
