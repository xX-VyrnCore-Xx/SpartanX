using SpartanX.Core.Abstractions;
using SpartanX.Core.Dock;
using SpartanX.Core.Settings;

namespace SpartanX.Core.Tests;

public class DockViewModelTests
{
    private readonly InMemorySettings _settings = new();
    private readonly FakeWindowTracker _windows = new();
    private readonly FakeLauncher _launcher = new();

    private DockViewModel Create() => new(_settings, _windows, _launcher, new NullIcons());

    [Fact]
    public void Pinned_apps_come_first_then_running_unpinned()
    {
        _settings.Current.Dock.PinnedApps.Add(new PinnedApp("Edge", "/apps/edge.exe"));
        _windows.List.Add(new TrackedWindow(1, "Notepad", "/apps/notepad.exe", 10, false));
        _windows.List.Add(new TrackedWindow(2, "Edge", "/apps/edge.exe", 11, false));
        var vm = Create();
        vm.Rebuild();

        Assert.Equal(["Edge", "notepad"], vm.Apps.Select(a => a.DisplayName));
        Assert.True(vm.Apps[0].IsPinned && vm.Apps[0].IsRunning);
        Assert.False(vm.Apps[1].IsPinned);
    }

    [Fact]
    public void Shortcut_pins_match_their_target_executable()
    {
        _settings.Current.Dock.PinnedApps.Add(new PinnedApp("Code", "/apps/code.lnk"));
        _windows.List.Add(new TrackedWindow(5, "VS Code", "/apps/code.exe", 1, false));
        var vm = Create();
        vm.Rebuild();

        Assert.Single(vm.Apps);
        Assert.True(vm.Apps[0].IsRunning);
    }

    [Fact]
    public void Clicking_active_app_minimizes_otherwise_activates()
    {
        _windows.List.Add(new TrackedWindow(7, "Notepad", "/apps/notepad.exe", 1, false));
        _windows.ForegroundWindow = 7;
        var vm = Create();
        vm.Rebuild();

        vm.ActivateCommand.Execute(vm.Apps[0]);
        Assert.Contains("minimize:7", _windows.Calls);

        _windows.ForegroundWindow = 0;
        vm.Rebuild();
        vm.ActivateCommand.Execute(vm.Apps[0]);
        Assert.Contains("activate:7", _windows.Calls);
    }

    [Fact]
    public void Launching_a_pinned_app_requests_bounce()
    {
        _settings.Current.Dock.PinnedApps.Add(new PinnedApp("Edge", "/apps/edge.exe"));
        var vm = Create();
        vm.Rebuild();
        var bounced = false;
        vm.Apps[0].BounceRequested += (_, _) => bounced = true;

        vm.ActivateCommand.Execute(vm.Apps[0]);

        Assert.True(bounced);
        Assert.Equal(["/apps/edge.exe"], _launcher.Launched);
    }

    [Fact]
    public void Rebuild_preserves_item_instances()
    {
        _settings.Current.Dock.PinnedApps.Add(new PinnedApp("Edge", "/apps/edge.exe"));
        var vm = Create();
        vm.Rebuild();
        var first = vm.Apps[0];
        _windows.List.Add(new TrackedWindow(1, "Edge", "/apps/edge.exe", 1, false));
        _windows.Raise();

        Assert.Same(first, vm.Apps[0]);
        Assert.True(first.IsRunning);
    }

    [Fact]
    public void Toggle_pin_adds_and_removes()
    {
        _windows.List.Add(new TrackedWindow(1, "Notepad", "/apps/notepad.exe", 1, false));
        var vm = Create();
        vm.Rebuild();

        vm.TogglePinCommand.Execute(vm.Apps[0]);
        Assert.Single(_settings.Current.Dock.PinnedApps);
        vm.TogglePinCommand.Execute(vm.Apps[0]);
        Assert.Empty(_settings.Current.Dock.PinnedApps);
    }
}
