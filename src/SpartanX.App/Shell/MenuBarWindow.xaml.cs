using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Localization;
using SpartanX.Core.MenuBar;
using SpartanX.Core.Settings;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Shell;

/// <summary>macOS-style menu bar along the top edge. Registered as an AppBar so maximised
/// windows start below it.</summary>
public sealed partial class MenuBarWindow : Window
{
    public const int HeightDip = 34;

    private readonly ISystemStatusService _system;
    private readonly IWindowTracker _windows;
    private readonly IAppLauncher _launcher;
    private readonly DispatcherQueueTimer _clock;
    private readonly ShellAppBar _appBar;
    private bool _updatingSlider;

    internal MenuBarWindow(MenuBarViewModel viewModel, ISystemStatusService system, IWindowTracker windows, IAppLauncher launcher, ThemeService theme)
    {
        ViewModel = viewModel;
        _system = system;
        _windows = windows;
        _launcher = launcher;
        InitializeComponent();

        WindowHelper.MakeShellSurface(this, topmost: true, noActivate: false);
        theme.Register(this);
        _appBar = new ShellAppBar(this.GetHwnd());

        _clock = DispatcherQueue.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) => ViewModel.Tick();
        _clock.Start();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MenuBarViewModel.Battery) or nameof(MenuBarViewModel.Network) or nameof(MenuBarViewModel.Volume))
            {
                UpdateIcons();
            }
        };
        _windows.Changed += (_, _) => UpdateActiveApp();
        UpdateIcons();
        UpdateActiveApp();
    }

    public MenuBarViewModel ViewModel { get; }

    public void Place()
    {
        var display = DisplayArea.Primary.OuterBounds;
        var height = (int)Math.Round(HeightDip * this.GetScale());
        var monitor = new RECT { Left = display.X, Top = display.Y, Right = display.X + display.Width, Bottom = display.Y + display.Height };
        var rc = _appBar.Reserve(ABE_TOP, monitor, height);
        WindowHelper.PlaceTopmost(this, rc.Left, rc.Top, rc.Width, height);
    }

    private bool _shownOnce;

    /// <summary>The first show goes through Activate() so WinUI loads the content; later ones never steal focus.</summary>
    private void ShowWithoutFocus()
    {
        if (!_shownOnce)
        {
            _shownOnce = true;
            Activate();
        }

        AppWindow.Show(false);
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            Place();
            ShowWithoutFocus();
        }
        else
        {
            _appBar.Release();
            AppWindow.Hide();
        }
    }

    public void ReleaseAppBar() => _appBar.Release();

    private void UpdateActiveApp()
    {
        var fg = _windows.Windows.FirstOrDefault(w => w.Handle == _windows.ForegroundWindow);
        if (fg?.ExecutablePath is { } exe)
        {
            ViewModel.ActiveAppName = _launcher.GetDisplayName(exe);
        }
        else if (string.IsNullOrEmpty(ViewModel.ActiveAppName))
        {
            ViewModel.ActiveAppName = Localizer.Get("MenuBar.Desktop");
        }
    }

    private void UpdateIcons()
    {
        var n = ViewModel.Network;
        NetworkIcon.Glyph = !n.IsConnected ? "\uE7BA" : n.IsWifi ? WifiGlyph(n.SignalBars) : "\uE839";
        ToolTipService.SetToolTip(NetworkButton, n.IsConnected ? n.Name ?? Localizer.Get("Island.Network.Connected") : Localizer.Get("Island.Network.Disconnected"));

        var v = ViewModel.Volume;
        VolumeIcon.Glyph = v.IsMuted || v.Level <= 0.001 ? "\uE74F" : v.Level < 0.34 ? "\uE993" : v.Level < 0.67 ? "\uE994" : "\uE995";
        _updatingSlider = true;
        VolumeSlider.Value = Math.Round(v.Level * 100);
        _updatingSlider = false;

        var b = ViewModel.Battery;
        var level = Math.Clamp(b.Percent / 10, 0, 10);
        BatteryIcon.Glyph = b.IsCharging || b.IsPluggedIn
            ? char.ConvertFromUtf32(0xEBAB + level)
            : level == 10 ? "\uE83F" : char.ConvertFromUtf32(0xE850 + level);
    }

    private static string WifiGlyph(int bars) => bars switch
    {
        <= 1 => "\uE872",
        2 => "\uE873",
        3 => "\uE874",
        _ => "\uE701",
    };

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingSlider)
        {
            _system.SetVolume(e.NewValue / 100);
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e) => _system.ToggleMute();
    private void SoundSettings_Click(object sender, RoutedEventArgs e) => ShellActions.Open("ms-settings:sound");
    private void Network_Click(object sender, RoutedEventArgs e) => ShellActions.Open("ms-availablenetworks:");
    private void Battery_Click(object sender, RoutedEventArgs e) => ShellActions.Open("ms-settings:batterysaver");
    private void Notifications_Click(object sender, RoutedEventArgs e) => ShellActions.Open("ms-actioncenter:");
    private void About_Click(object sender, RoutedEventArgs e) => App.Current.OpenSettings("About");
    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.OpenSettings();
    private void WindowsSettings_Click(object sender, RoutedEventArgs e) => ShellActions.OpenWindowsSettings();
    private void Start_Click(object sender, RoutedEventArgs e) => ShellActions.OpenStartMenu();
    private void TaskManager_Click(object sender, RoutedEventArgs e) => ShellActions.OpenTaskManager();
    private void Sleep_Click(object sender, RoutedEventArgs e) => ShellActions.Sleep();
    private void Restart_Click(object sender, RoutedEventArgs e) => ShellActions.Shutdown(restart: true);
    private void Shutdown_Click(object sender, RoutedEventArgs e) => ShellActions.Shutdown(restart: false);
    private void Lock_Click(object sender, RoutedEventArgs e) => ShellActions.Lock();
    private void Quit_Click(object sender, RoutedEventArgs e) => App.Current.Quit();
}
