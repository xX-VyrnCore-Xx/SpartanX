using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Settings;

namespace SpartanX.Core.MenuBar;

/// <summary>Top menu bar: app menu, active app name, status icons and clock.</summary>
public sealed partial class MenuBarViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ISystemStatusService _system;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;

    public MenuBarViewModel(ISettingsService settings, ISystemStatusService system, IUiDispatcher ui, TimeProvider time)
    {
        _settings = settings;
        _system = system;
        _ui = ui;
        _time = time;
        _system.BatteryChanged += (_, b) => _ui.Post(() => Battery = b);
        _system.NetworkChanged += (_, n) => _ui.Post(() => Network = n);
        _system.VolumeChanged += (_, v) => _ui.Post(() => Volume = v);
        _settings.Changed += (_, e) =>
        {
            if (e.Section is MenuBarSettings)
            {
                Tick();
            }
        };
        Battery = system.Battery;
        Network = system.Network;
        Volume = system.Volume;
        Tick();
    }

    public MenuBarSettings Settings => _settings.Current.MenuBar;

    [ObservableProperty] public partial string ClockText { get; set; } = "";
    [ObservableProperty] public partial string ActiveAppName { get; set; } = "";
    [ObservableProperty] public partial BatteryStatus Battery { get; set; }
    [ObservableProperty] public partial NetworkStatus Network { get; set; }
    [ObservableProperty] public partial VolumeStatus Volume { get; set; }

    public string BatteryPercentText => Battery.HasBattery && Settings.ShowBatteryPercentage ? $"{Battery.Percent}%" : "";

    partial void OnBatteryChanged(BatteryStatus value) => OnPropertyChanged(nameof(BatteryPercentText));

    public void Tick() => ClockText = FormatClock(_time.GetLocalNow(), Settings, CultureInfo.CurrentCulture);

    public static string FormatClock(DateTimeOffset now, MenuBarSettings s, CultureInfo culture)
    {
        var use24 = s.ClockFormat switch
        {
            ClockFormat.TwentyFourHour => true,
            ClockFormat.TwelveHour => false,
            _ => !culture.DateTimeFormat.ShortTimePattern.Contains('t'),
        };

        var time = (use24, s.ShowSeconds) switch
        {
            (true, false) => "HH:mm",
            (true, true) => "HH:mm:ss",
            (false, false) => "h:mm tt",
            (false, true) => "h:mm:ss tt",
        };

        var format = s.ShowDate ? $"ddd d MMM  {time}" : time;
        return now.ToString(format, culture);
    }
}
