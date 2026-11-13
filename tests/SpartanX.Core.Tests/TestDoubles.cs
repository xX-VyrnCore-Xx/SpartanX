using SpartanX.Core.Abstractions;
using SpartanX.Core.Settings;

namespace SpartanX.Core.Tests;

/// <summary>Deterministic clock for timer / activity tests.</summary>
internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class InMemorySettings : ISettingsService
{
    public AppSettings Current { get; } = new();

    public event EventHandler<SettingChangedEventArgs>? Changed;

    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void ResetToDefaults() => Changed?.Invoke(this, new SettingChangedEventArgs(Current, null));

    public void RaiseChanged(object section, string property) => Changed?.Invoke(this, new SettingChangedEventArgs(section, property));
}

internal sealed class FakeWindowTracker : IWindowTracker
{
    public List<TrackedWindow> List { get; } = [];
    public List<string> Calls { get; } = [];

    public IReadOnlyList<TrackedWindow> Windows => List;
    public nint ForegroundWindow { get; set; }

    public event EventHandler? Changed;

    public void Start() { }
    public void Activate(nint handle) => Calls.Add($"activate:{handle}");
    public void Minimize(nint handle) => Calls.Add($"minimize:{handle}");
    public void Close(nint handle) => Calls.Add($"close:{handle}");

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeLauncher : IAppLauncher
{
    public List<string> Launched { get; } = [];

    public Task<bool> LaunchAsync(string path, string? arguments = null)
    {
        Launched.Add(path);
        return Task.FromResult(true);
    }

    public void RevealInExplorer(string path) { }

    public string ResolveExecutable(string path) => path.EndsWith(".lnk") ? path[..^4] + ".exe" : path;

    public string GetDisplayName(string path) => Path.GetFileNameWithoutExtension(path);
}

internal sealed class NullIcons : IIconProvider
{
    public Task<object?> GetIconAsync(string path, int size) => Task.FromResult<object?>(null);
}
