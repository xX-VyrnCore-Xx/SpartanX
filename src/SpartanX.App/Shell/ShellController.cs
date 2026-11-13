using Microsoft.UI.Dispatching;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Dock;
using SpartanX.Core.Island;
using SpartanX.Core.MenuBar;
using SpartanX.Core.Settings;

namespace SpartanX.App.Shell;

/// <summary>
/// Owns the three shell surfaces (menu bar, Island, Dock), the Windows taskbar state and global
/// shortcuts, and keeps them in sync with settings.
/// </summary>
internal sealed class ShellController : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly SystemStatusService _system;
    private readonly IWindowTracker _windows;
    private readonly IAppLauncher _launcher;
    private readonly IMediaService _media;
    private readonly IStartupService _startup;
    private readonly HotkeyService _hotkeys;
    private readonly ThemeService _theme;
    private readonly DockViewModel _dockVm;
    private readonly IslandViewModel _islandVm;
    private readonly MenuBarViewModel _menuVm;
    private readonly DispatcherQueue _queue;
    private readonly TaskbarController _taskbar = new(App.DataDirectory);
    private DispatcherQueueTimer? _enforcer;
    private WinUIEx.Messaging.WindowMessageMonitor? _displayMonitor;
    private MenuBarWindow? _menuBar;
    private IslandWindow? _island;
    private DockWindow? _dock;
    private bool _disposed;

    public ShellController(
        ISettingsService settings,
        SystemStatusService system,
        IWindowTracker windows,
        IAppLauncher launcher,
        IMediaService media,
        IStartupService startup,
        HotkeyService hotkeys,
        ThemeService theme,
        DockViewModel dockVm,
        IslandViewModel islandVm,
        MenuBarViewModel menuVm,
        DispatcherQueue queue)
    {
        _settings = settings;
        _system = system;
        _windows = windows;
        _launcher = launcher;
        _media = media;
        _startup = startup;
        _hotkeys = hotkeys;
        _theme = theme;
        _dockVm = dockVm;
        _islandVm = islandVm;
        _menuVm = menuVm;
        _queue = queue;
    }

    private AppSettings S => _settings.Current;

    public void Start()
    {
        _system.Start();
        _windows.Start();
        SeedPinnedAppsOnFirstRun();

        _menuBar = new MenuBarWindow(_menuVm, _system, _windows, _launcher, _theme);
        _island = new IslandWindow(_islandVm, _system, _theme);
        _dock = new DockWindow(_dockVm, _launcher, _theme);

        ApplyTaskbar();
        ApplyVisibility();
        SyncStartup();

        _hotkeys.Attach(_menuBar);
        _hotkeys.Triggered += OnHotkey;
        _settings.Changed += OnSettingsChanged;
        _ = _islandVm.StartAsync();

        // Explorer sometimes brings the taskbar back (e.g. after it restarts); keep it hidden.
        _enforcer = _queue.CreateTimer();
        _enforcer.Interval = TimeSpan.FromSeconds(2);
        _enforcer.Tick += (_, _) => _taskbar.EnforceHidden();
        _enforcer.Start();

        // Re-layout when monitors, resolution, DPI or the work area change.
        _displayMonitor = new WinUIEx.Messaging.WindowMessageMonitor(_menuBar);
        _displayMonitor.WindowMessageReceived += (_, e) =>
        {
            if (e.Message.MessageId is WM_DISPLAYCHANGE or WM_DPICHANGED)
            {
                _queue.TryEnqueue(Relayout);
            }
        };
    }

    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_DPICHANGED = 0x02E0;

    private void Relayout()
    {
        if (S.MenuBar.Enabled) _menuBar?.Place();
        if (S.Island.Enabled) _island?.Place();
        if (S.Dock.Enabled) _dock?.Layout();
    }

    private void OnSettingsChanged(object? sender, SettingChangedEventArgs e)
    {
        switch (e.Section)
        {
            case GeneralSettings when e.PropertyName == nameof(GeneralSettings.HideWindowsTaskbar):
                ApplyTaskbar();
                break;
            case GeneralSettings when e.PropertyName == nameof(GeneralSettings.LaunchAtStartup):
                SyncStartup();
                break;
            case DockSettings when e.PropertyName == nameof(DockSettings.Enabled):
            case IslandSettings when e.PropertyName == nameof(IslandSettings.Enabled):
            case MenuBarSettings when e.PropertyName == nameof(MenuBarSettings.Enabled):
                ApplyVisibility();
                break;
            case IslandSettings when e.PropertyName == nameof(IslandSettings.WeatherCity) || e.PropertyName == nameof(IslandSettings.WeatherModule) || e.PropertyName == nameof(IslandSettings.UseFahrenheit):
                _ = _islandVm.RefreshWeatherAsync();
                break;
            case AppSettings:
                ApplyTaskbar();
                ApplyVisibility();
                break;
        }
    }

    private void ApplyVisibility()
    {
        _menuBar?.SetVisible(S.MenuBar.Enabled);
        _island?.SetVisible(S.Island.Enabled);
        _dock?.SetVisible(S.Dock.Enabled);
    }

    private void ApplyTaskbar()
    {
        // Only hide the taskbar when something replaces it.
        if (S.General.HideWindowsTaskbar && S.Dock.Enabled)
        {
            _taskbar.Hide();
        }
        else
        {
            _taskbar.Restore();
        }
    }

    private void SyncStartup()
    {
        try
        {
            if (_startup.IsEnabled != S.General.LaunchAtStartup)
            {
                _startup.SetEnabled(S.General.LaunchAtStartup);
            }
        }
        catch (Exception e)
        {
            App.Log(e);
        }
    }

    private void OnHotkey(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleDock:
                S.Dock.AutoHide = !S.Dock.AutoHide;
                break;
            case HotkeyAction.OpenIsland:
                _islandVm.ToggleCommand.Execute(null);
                break;
            case HotkeyAction.OpenSettings:
                App.Current.OpenSettings();
                break;
            case HotkeyAction.PlayPause:
                _ = _media.PlayPauseAsync();
                break;
            case HotkeyAction.StartTimer:
                _islandVm.StartTimerCommand.Execute("5");
                break;
        }
    }

    /// <summary>The first time the shell runs, copy the user's taskbar pins into the Dock.</summary>
    private void SeedPinnedAppsOnFirstRun()
    {
        var dock = S.Dock;
        if (dock.PinnedApps.Count > 0 || dock.Stacks.Count > 0)
        {
            return;
        }

        var pins = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
        try
        {
            if (Directory.Exists(pins))
            {
                foreach (var lnk in Directory.EnumerateFiles(pins, "*.lnk").OrderBy(f => f))
                {
                    dock.PinnedApps.Add(new PinnedApp(Path.GetFileNameWithoutExtension(lnk), lnk));
                }
            }
        }
        catch (IOException)
        {
        }

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            dock.Stacks.Add(downloads);
        }
    }

    /// <summary>Last-chance cleanup from crash handlers: give the user their taskbar back.</summary>
    public void EmergencyRestore()
    {
        try
        {
            _menuBar?.ReleaseAppBar();
            _dock?.ReleaseAppBar();
            _taskbar.Restore();
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _displayMonitor?.Dispose();
        _enforcer?.Stop();
        _hotkeys.Dispose();
        EmergencyRestore();
        (_windows as IDisposable)?.Dispose();
        _menuBar?.Close();
        _island?.Close();
        _dock?.Close();
    }
}
