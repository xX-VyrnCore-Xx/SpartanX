using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Localization;
using SpartanX.Core.Settings;
using SpartanX.Core.Weather;

namespace SpartanX.Core.Island;

/// <summary>
/// State of the Dynamic Island. Services feed activities in, the view animates
/// toward <see cref="TargetSize"/> and swaps templates on <see cref="Activity"/>.
/// </summary>
public sealed partial class IslandViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IMediaService _media;
    private readonly ISystemStatusService _system;
    private readonly INotificationService _notifications;
    private readonly IWeatherService _weather;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private bool _hovered;
    private bool _autoPeek;
    private BatteryStatus _lastBattery;
    private NetworkStatus _lastNetwork;
    private DateTimeOffset _lastWeatherFetch = DateTimeOffset.MinValue;

    public IslandViewModel(
        ISettingsService settings,
        IMediaService media,
        ISystemStatusService system,
        INotificationService notifications,
        IWeatherService weather,
        IUiDispatcher ui,
        TimeProvider time)
    {
        _settings = settings;
        _media = media;
        _system = system;
        _notifications = notifications;
        _weather = weather;
        _ui = ui;
        _time = time;

        Activities = new IslandActivityManager(time);
        Timer = new CountdownTimer(time);
        Activities.CurrentChanged += (_, a) => OnActivityChanged(a);
        Timer.Completed += (_, _) => OnTimerCompleted();

        _media.Changed += (_, m) => _ui.Post(() => HandleMedia(m));
        _system.VolumeChanged += (_, v) => _ui.Post(() => HandleVolume(v));
        _system.BatteryChanged += (_, b) => _ui.Post(() => HandleBattery(b));
        _system.NetworkChanged += (_, n) => _ui.Post(() => HandleNetwork(n));
        _notifications.NotificationArrived += (_, n) => _ui.Post(() => HandleNotification(n));

        _lastBattery = system.Battery;
        _lastNetwork = system.Network;
        UpdateTarget();
    }

    public IslandSettings Settings => _settings.Current.Island;
    public IslandActivityManager Activities { get; }
    public CountdownTimer Timer { get; }

    [ObservableProperty] public partial IslandMode Mode { get; set; } = IslandMode.Compact;
    [ObservableProperty] public partial IslandActivityKind Activity { get; set; } = IslandActivityKind.Idle;
    [ObservableProperty] public partial IslandSize TargetSize { get; set; } = IslandLayout.GetSize(IslandMode.Compact, IslandActivityKind.Idle);

    [ObservableProperty] public partial MediaSnapshot? Media { get; set; }
    [ObservableProperty] public partial VolumeStatus Volume { get; set; }
    [ObservableProperty] public partial BatteryStatus Battery { get; set; }
    [ObservableProperty] public partial NetworkStatus Network { get; set; }
    [ObservableProperty] public partial IslandNotification? Notification { get; set; }
    [ObservableProperty] public partial WeatherSnapshot? Weather { get; set; }
    [ObservableProperty] public partial string TimerText { get; set; } = "0:00";
    [ObservableProperty] public partial double TimerProgress { get; set; }
    [ObservableProperty] public partial string ClockText { get; set; } = "";
    [ObservableProperty] public partial string DateText { get; set; } = "";

    public bool IsMediaPlaying => Media?.IsPlaying == true;

    partial void OnMediaChanged(MediaSnapshot? value) => OnPropertyChanged(nameof(IsMediaPlaying));

    partial void OnModeChanged(IslandMode value) => UpdateTarget();

    /// <summary>Starts services. Call once from the UI thread.</summary>
    public async Task StartAsync()
    {
        Volume = _system.Volume;
        Battery = _system.Battery;
        Network = _system.Network;
        Tick();
        await _media.StartAsync();
        HandleMedia(_media.Current);
        if (Settings.NotificationsModule)
        {
            await _notifications.StartAsync();
        }

        await RefreshWeatherAsync();
    }

    /// <summary>Called by the view ~4 times per second.</summary>
    public void Tick()
    {
        var now = _time.GetLocalNow();
        ClockText = now.ToString("t");
        DateText = now.ToString("ddd d MMM");

        Timer.Tick();
        if (Timer.IsActive)
        {
            TimerText = CountdownTimer.Format(Timer.Remaining);
            TimerProgress = Timer.Progress;
        }

        Activities.Tick();

        if (now - _lastWeatherFetch > TimeSpan.FromMinutes(30))
        {
            _ = RefreshWeatherAsync();
        }
    }

    // ---- Pointer -----------------------------------------------------------

    public void OnPointerEntered()
    {
        _hovered = true;
        if (Settings.Interaction == IslandInteraction.HoverExpandClickOpen && Mode == IslandMode.Compact)
        {
            Mode = IslandMode.Peek;
        }
    }

    public void OnPointerExited()
    {
        _hovered = false;
        if (Mode == IslandMode.Peek && !_autoPeek)
        {
            Mode = IslandMode.Compact;
        }
        else if (Mode == IslandMode.Expanded)
        {
            Mode = IslandMode.Compact;
        }
    }

    [RelayCommand]
    private void Toggle() => Mode = Mode == IslandMode.Expanded ? IslandMode.Compact : IslandMode.Expanded;

    [RelayCommand]
    private void Collapse() => Mode = IslandMode.Compact;

    // ---- Media --------------------------------------------------------------

    [RelayCommand] private Task PlayPauseAsync() => _media.PlayPauseAsync();
    [RelayCommand] private Task NextAsync() => _media.NextAsync();
    [RelayCommand] private Task PreviousAsync() => _media.PreviousAsync();

    private void HandleMedia(MediaSnapshot? media)
    {
        var trackChanged = media is not null && (Media?.Title != media.Title || Media?.Artist != media.Artist);
        Media = media;
        if (!Settings.MediaModule || media is null || string.IsNullOrEmpty(media.Title))
        {
            Activities.Remove(IslandActivityKind.Media);
            return;
        }

        Activities.SetPersistent(IslandActivityKind.Media);
        if (trackChanged && media.IsPlaying)
        {
            AutoPeek();
        }
    }

    // ---- Timer --------------------------------------------------------------

    [RelayCommand]
    private void StartTimer(string? minutes)
    {
        if (!double.TryParse(minutes, System.Globalization.CultureInfo.InvariantCulture, out var m) || m <= 0)
        {
            return;
        }

        Timer.Start(TimeSpan.FromMinutes(m));
        TimerText = CountdownTimer.Format(Timer.Remaining);
        Activities.SetPersistent(IslandActivityKind.Timer);
    }

    [RelayCommand]
    private void ToggleTimerPause()
    {
        if (Timer.IsPaused)
        {
            Timer.Resume();
        }
        else
        {
            Timer.Pause();
        }
    }

    [RelayCommand]
    private void CancelTimer()
    {
        Timer.Cancel();
        Activities.Remove(IslandActivityKind.Timer);
        Activities.Remove(IslandActivityKind.TimerFinished);
    }

    private void OnTimerCompleted()
    {
        Activities.Remove(IslandActivityKind.Timer);
        Activities.ShowTransient(IslandActivityKind.TimerFinished, TimeSpan.FromSeconds(8));
        AutoPeek();
    }

    // ---- System ---------------------------------------------------------------

    private void HandleVolume(VolumeStatus volume)
    {
        Volume = volume;
        if (Settings.SystemStatusModule)
        {
            Activities.ShowTransient(IslandActivityKind.Volume, TimeSpan.FromSeconds(1.8));
        }
    }

    private void HandleBattery(BatteryStatus battery)
    {
        Battery = battery;
        var pluggedChanged = battery.IsPluggedIn != _lastBattery.IsPluggedIn;
        var becameLow = battery.IsLow && !_lastBattery.IsLow;
        _lastBattery = battery;
        if (Settings.SystemStatusModule && battery.HasBattery && (pluggedChanged || becameLow))
        {
            Activities.ShowTransient(IslandActivityKind.Battery, TimeSpan.FromSeconds(3));
            AutoPeek();
        }
    }

    private void HandleNetwork(NetworkStatus network)
    {
        Network = network;
        var changed = network.IsConnected != _lastNetwork.IsConnected;
        _lastNetwork = network;
        if (Settings.SystemStatusModule && changed)
        {
            Activities.ShowTransient(IslandActivityKind.Network, TimeSpan.FromSeconds(2.5));
        }
    }

    private void HandleNotification(IslandNotification notification)
    {
        if (!Settings.NotificationsModule)
        {
            return;
        }

        Notification = notification;
        Activities.ShowTransient(IslandActivityKind.Notification, TimeSpan.FromSeconds(Math.Max(2, Settings.AutoCollapseSeconds)), notification.Id);
        AutoPeek();
    }

    // ---- Weather ---------------------------------------------------------------

    public async Task RefreshWeatherAsync()
    {
        _lastWeatherFetch = _time.GetLocalNow();
        if (!Settings.WeatherModule || string.IsNullOrWhiteSpace(Settings.WeatherCity))
        {
            Weather = null;
            Activities.Remove(IslandActivityKind.Weather);
            return;
        }

        var weather = await _weather.GetCurrentAsync(Settings.WeatherCity, Settings.UseFahrenheit);
        _ui.Post(() =>
        {
            Weather = weather;
            if (weather is null)
            {
                Activities.Remove(IslandActivityKind.Weather);
            }
            else
            {
                Activities.SetPersistent(IslandActivityKind.Weather);
            }
        });
    }

    // ---- Layout ----------------------------------------------------------------

    private void AutoPeek()
    {
        if (!Settings.AutoExpandOnEvents || Mode == IslandMode.Expanded)
        {
            return;
        }

        _autoPeek = true;
        Mode = IslandMode.Peek;
        UpdateTarget();
    }

    private void OnActivityChanged(IslandActivity activity)
    {
        Activity = activity.Kind;
        if (_autoPeek && !activity.IsTransient && activity.Kind != IslandActivityKind.Media)
        {
            // The event that auto-expanded us is gone: fall back unless the pointer is on us.
            _autoPeek = false;
            if (!_hovered && Mode == IslandMode.Peek)
            {
                Mode = IslandMode.Compact;
            }
        }
        else if (_autoPeek && activity.Kind == IslandActivityKind.Media)
        {
            ScheduleAutoCollapse();
        }

        UpdateTarget();
    }

    private async void ScheduleAutoCollapse()
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, Settings.AutoCollapseSeconds)));
        _ui.Post(() =>
        {
            if (_autoPeek && !_hovered && Mode == IslandMode.Peek)
            {
                _autoPeek = false;
                Mode = IslandMode.Compact;
            }
        });
    }

    private void UpdateTarget() => TargetSize = IslandLayout.GetSize(Mode, Activity);

    public string VolumePercentText => $"{Math.Round(Volume.Level * 100)}%";

    partial void OnVolumeChanged(VolumeStatus value) => OnPropertyChanged(nameof(VolumePercentText));

    public string BatteryText => Battery.IsPluggedIn
        ? Localizer.Get(Battery.IsCharging ? "Island.Battery.Charging" : "Island.Battery.PluggedIn") + $" · {Battery.Percent}%"
        : Battery.IsLow ? Localizer.Get("Island.Battery.Low") + $" · {Battery.Percent}%" : $"{Battery.Percent}%";

    partial void OnBatteryChanged(BatteryStatus value) => OnPropertyChanged(nameof(BatteryText));

    public string NetworkText => Network.IsConnected
        ? Network.Name ?? Localizer.Get("Island.Network.Connected")
        : Localizer.Get("Island.Network.Disconnected");

    partial void OnNetworkChanged(NetworkStatus value) => OnPropertyChanged(nameof(NetworkText));
}
