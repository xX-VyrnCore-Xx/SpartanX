using NAudio.CoreAudioApi;
using SpartanX.Core.Abstractions;
using Windows.Networking.Connectivity;
using BatteryStatus = SpartanX.Core.Abstractions.BatteryStatus;
using NetworkStatus = SpartanX.Core.Abstractions.NetworkStatus;
using PowerBatteryStatus = Windows.System.Power.BatteryStatus;
using PowerManager = Windows.System.Power.PowerManager;
using PowerSupplyStatus = Windows.System.Power.PowerSupplyStatus;

namespace SpartanX.App.Services;

/// <summary>Volume (Core Audio), battery (PowerManager) and network (NetworkInformation).</summary>
internal sealed class SystemStatusService : ISystemStatusService, IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private bool _started;

    public VolumeStatus Volume { get; private set; }
    public BatteryStatus Battery { get; private set; } = BatteryStatus.None;
    public NetworkStatus Network { get; private set; } = NetworkStatus.Offline;

    public event EventHandler<VolumeStatus>? VolumeChanged;
    public event EventHandler<BatteryStatus>? BatteryChanged;
    public event EventHandler<NetworkStatus>? NetworkChanged;

    /// <summary>Instantaneous output peak (0..1) for the Island's audio bars.</summary>
    public double OutputPeak
    {
        get
        {
            try
            {
                return _device?.AudioMeterInformation.MasterPeakValue ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        AttachAudio();

        PowerManager.BatteryStatusChanged += (_, _) => UpdateBattery(true);
        PowerManager.RemainingChargePercentChanged += (_, _) => UpdateBattery(true);
        PowerManager.PowerSupplyStatusChanged += (_, _) => UpdateBattery(true);
        UpdateBattery(false);

        NetworkInformation.NetworkStatusChanged += _ => UpdateNetwork(true);
        UpdateNetwork(false);
    }

    private void AttachAudio()
    {
        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _device.AudioEndpointVolume.OnVolumeNotification += data =>
            {
                Volume = new VolumeStatus(data.MasterVolume, data.Muted);
                VolumeChanged?.Invoke(this, Volume);
            };
            Volume = new VolumeStatus(_device.AudioEndpointVolume.MasterVolumeLevelScalar, _device.AudioEndpointVolume.Mute);
        }
        catch (Exception)
        {
            _device = null; // No output device.
        }
    }

    public void SetVolume(double level)
    {
        if (_device is null)
        {
            return;
        }

        _device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(level, 0, 1);
    }

    public void ToggleMute()
    {
        if (_device is not null)
        {
            _device.AudioEndpointVolume.Mute = !_device.AudioEndpointVolume.Mute;
        }
    }

    private void UpdateBattery(bool raise)
    {
        var status = PowerManager.BatteryStatus;
        var supply = PowerManager.PowerSupplyStatus;
        Battery = status == PowerBatteryStatus.NotPresent
            ? BatteryStatus.None
            : new BatteryStatus(
                true,
                PowerManager.RemainingChargePercent,
                status == PowerBatteryStatus.Charging,
                supply != PowerSupplyStatus.NotPresent);
        if (raise)
        {
            BatteryChanged?.Invoke(this, Battery);
        }
    }

    private void UpdateNetwork(bool raise)
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            var level = profile?.GetNetworkConnectivityLevel() ?? NetworkConnectivityLevel.None;
            var connected = level is NetworkConnectivityLevel.InternetAccess or NetworkConnectivityLevel.ConstrainedInternetAccess;
            var isWifi = profile?.IsWlanConnectionProfile == true;
            string? name = null;
            if (isWifi)
            {
                name = profile!.WlanConnectionProfileDetails?.GetConnectedSsid();
            }

            name ??= profile?.GetNetworkNames().FirstOrDefault();
            var bars = profile?.GetSignalBars() ?? (connected ? 4 : 0);
            Network = new NetworkStatus(connected, isWifi, name, bars);
        }
        catch (Exception)
        {
            Network = NetworkStatus.Offline;
        }

        if (raise)
        {
            NetworkChanged?.Invoke(this, Network);
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator?.Dispose();
    }
}
