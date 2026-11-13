using SpartanX.Core.Abstractions;
using SpartanX.Core.Island;
using SpartanX.Core.Settings;
using SpartanX.Core.Weather;

namespace SpartanX.Core.Tests;

public class IslandViewModelTests
{
    private sealed class FakeMedia : IMediaService
    {
        public MediaSnapshot? Current { get; set; }
        public event EventHandler<MediaSnapshot?>? Changed;
        public Task StartAsync() => Task.CompletedTask;
        public Task PlayPauseAsync() => Task.CompletedTask;
        public Task NextAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public void Push(MediaSnapshot? m) { Current = m; Changed?.Invoke(this, m); }
    }

    private sealed class FakeSystem : ISystemStatusService
    {
        public VolumeStatus Volume => new(0.5, false);
        public BatteryStatus Battery => BatteryStatus.None;
        public NetworkStatus Network => NetworkStatus.Offline;
        public event EventHandler<VolumeStatus>? VolumeChanged;
        public event EventHandler<BatteryStatus>? BatteryChanged;
        public event EventHandler<NetworkStatus>? NetworkChanged;
        public void Start() { }
        public void SetVolume(double level) => VolumeChanged?.Invoke(this, new VolumeStatus(level, false));
        public void ToggleMute() { }
        public void Silence() { BatteryChanged?.Invoke(this, Battery); NetworkChanged?.Invoke(this, Network); }
    }

    private sealed class FakeNotifications : INotificationService
    {
        public bool IsAvailable => false;
        public string? UnavailableReasonKey => null;
        public event EventHandler<IslandNotification>? NotificationArrived;
        public Task<bool> RequestAccessAsync() => Task.FromResult(false);
        public Task StartAsync() => Task.CompletedTask;
        public void Raise(IslandNotification n) => NotificationArrived?.Invoke(this, n);
    }

    private sealed class NoWeather : IWeatherService
    {
        public Task<WeatherSnapshot?> GetCurrentAsync(string city, bool fahrenheit, CancellationToken cancellationToken = default) =>
            Task.FromResult<WeatherSnapshot?>(null);
    }

    private static readonly MediaSnapshot Song = new("Song", "Artist", "", "app", true, TimeSpan.Zero, TimeSpan.FromMinutes(3), null, true, true);

    private readonly ObservableSettings _settings = new();
    private readonly FakeMedia _media = new();

    private IslandViewModel Create() => new(_settings, _media, new FakeSystem(), new FakeNotifications(), new NoWeather(),
        Abstractions.InlineDispatcher.Instance, new ManualTime(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Turning_media_module_off_hides_media_immediately()
    {
        var vm = Create();
        _media.Push(Song);
        Assert.Equal(IslandActivityKind.Media, vm.Activity);

        _settings.Current.Island.MediaModule = false;
        Assert.Equal(IslandActivityKind.Idle, vm.Activity);

        _settings.Current.Island.MediaModule = true;
        Assert.Equal(IslandActivityKind.Media, vm.Activity);
    }

    [Fact]
    public void Timer_is_ignored_when_module_is_off_and_cancelled_when_turned_off()
    {
        var vm = Create();
        vm.StartTimerCommand.Execute("5");
        Assert.Equal(IslandActivityKind.Timer, vm.Activity);

        _settings.Current.Island.TimerModule = false;
        Assert.False(vm.Timer.IsActive);
        Assert.Equal(IslandActivityKind.Idle, vm.Activity);

        vm.StartTimerCommand.Execute("5");
        Assert.False(vm.Timer.IsActive);
    }

    [Fact]
    public void Hover_peeks_and_leave_collapses()
    {
        var vm = Create();
        vm.OnPointerEntered();
        Assert.Equal(IslandMode.Peek, vm.Mode);
        vm.OnPointerExited();
        Assert.Equal(IslandMode.Compact, vm.Mode);

        _settings.Current.Island.Interaction = IslandInteraction.ClickOnly;
        vm.OnPointerEntered();
        Assert.Equal(IslandMode.Compact, vm.Mode);
    }
}

/// <summary>In-memory settings that raise Changed like the real service.</summary>
internal sealed class ObservableSettings : ISettingsService
{
    public ObservableSettings()
    {
        foreach (var section in Current.Sections)
        {
            section.PropertyChanged += (s, e) => Changed?.Invoke(this, new SettingChangedEventArgs(s!, e.PropertyName));
        }
    }

    public AppSettings Current { get; } = new();

    public event EventHandler<SettingChangedEventArgs>? Changed;

    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void ResetToDefaults() { }
}
