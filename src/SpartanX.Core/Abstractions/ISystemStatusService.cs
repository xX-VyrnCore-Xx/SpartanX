namespace SpartanX.Core.Abstractions;

public readonly record struct VolumeStatus(double Level, bool IsMuted);

public readonly record struct BatteryStatus(bool HasBattery, int Percent, bool IsCharging, bool IsPluggedIn)
{
    public static readonly BatteryStatus None = new(false, 100, false, true);

    public bool IsLow => HasBattery && !IsPluggedIn && Percent <= 20;
}

public readonly record struct NetworkStatus(bool IsConnected, bool IsWifi, string? Name, int SignalBars)
{
    public static readonly NetworkStatus Offline = new(false, false, null, 0);
}

public interface ISystemStatusService
{
    VolumeStatus Volume { get; }
    BatteryStatus Battery { get; }
    NetworkStatus Network { get; }

    /// <summary>All events are raised on background threads.</summary>
    event EventHandler<VolumeStatus>? VolumeChanged;
    event EventHandler<BatteryStatus>? BatteryChanged;
    event EventHandler<NetworkStatus>? NetworkChanged;

    void Start();

    void SetVolume(double level);

    void ToggleMute();
}
