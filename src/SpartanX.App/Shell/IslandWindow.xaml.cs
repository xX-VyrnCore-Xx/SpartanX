using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SpartanX.App.Animation;
using SpartanX.App.Controls;
using SpartanX.App.Helpers;
using SpartanX.App.Interop;
using SpartanX.App.Services;
using SpartanX.Core.Animation;
using SpartanX.Core.Island;
using SpartanX.Core.Localization;
using Windows.Graphics;

namespace SpartanX.App.Shell;

/// <summary>
/// The Dynamic Island: a black pill under the top-centre of the screen that morphs between
/// compact / peek / expanded with spring physics. The window is as large as the biggest
/// state but a window region makes everything outside the pill click-through.
/// </summary>
public sealed partial class IslandWindow : Window
{
    private const int MarginDip = 8;

    private readonly Spring _width;
    private readonly Spring _height;
    private readonly Func<double, bool> _animate;
    private readonly DispatcherQueueTimer _ticker;
    private readonly Dictionary<string, FrameworkElement> _panels;
    private FrameworkElement? _currentPanel;
    private double _scale = 1;
    private bool _animating;

    internal IslandWindow(IslandViewModel viewModel, SystemStatusService system, ThemeService theme)
    {
        ViewModel = viewModel;
        InitializeComponent();

        WindowHelper.MakeShellSurface(this, topmost: true, noActivate: true);
        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        theme.Register(this, useBackdrop: false);
        AudioBars.PeakSource = () => system.OutputPeak;

        var size = viewModel.TargetSize;
        _width = new Spring(size.Width, SpringPresets.Smooth) { RestThreshold = 0.3 };
        _height = new Spring(size.Height, SpringPresets.Smooth) { RestThreshold = 0.3 };
        _animate = Animate;
        ApplySize(size.Width, size.Height);

        _panels = new()
        {
            ["Compact.Media"] = CompactMedia,
            ["Compact.Timer"] = CompactTimer,
            ["Compact.Volume"] = CompactVolume,
            ["Compact.Battery"] = CompactBattery,
            ["Compact.Network"] = CompactNetwork,
            ["Compact.Notification"] = CompactNotification,
            ["Compact.Weather"] = CompactWeather,
            ["Compact.TimerFinished"] = CompactTimerDone,
            ["Peek.Idle"] = PeekIdle,
            ["Peek.Media"] = PeekMedia,
            ["Peek.Timer"] = PeekTimer,
            ["Peek.Volume"] = PeekVolume,
            ["Peek.Battery"] = PeekBattery,
            ["Peek.Network"] = PeekNetwork,
            ["Peek.Notification"] = PeekNotification,
            ["Peek.Weather"] = PeekWeather,
            ["Peek.TimerFinished"] = PeekTimerDone,
            ["Expanded.Media"] = ExpandedMedia,
            ["Expanded.Dashboard"] = ExpandedDashboard,
            ["Expanded.Timer"] = ExpandedTimer,
            ["Expanded.Notification"] = ExpandedNotification,
        };

        ViewModel.PropertyChanged += OnViewModelChanged;
        _ticker = DispatcherQueue.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(250);
        _ticker.Tick += (_, _) => { ViewModel.Tick(); UpdateTimerButton(); };
        _ticker.Start();

        UpdatePanel();
        UpdateGlyphs();
    }

    public IslandViewModel ViewModel { get; }

    public void Place()
    {
        _scale = this.GetScale();
        var display = DisplayArea.Primary.OuterBounds;
        var w = (int)Math.Ceiling((IslandLayout.Max.Width + MarginDip * 2) * _scale);
        var h = (int)Math.Ceiling((IslandLayout.Max.Height + MarginDip * 2) * _scale);
        WindowHelper.PlaceTopmost(this, display.X + (display.Width - w) / 2, display.Y, w, h);
        UpdateRegion();
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
            AppWindow.Hide();
        }
    }

    // ---- State -------------------------------------------------------------------

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.TargetSize):
                AnimateTo(ViewModel.TargetSize);
                break;
            case nameof(IslandViewModel.Mode):
            case nameof(IslandViewModel.Activity):
                UpdatePanel();
                break;
            case nameof(IslandViewModel.Media):
                _ = UpdateArtworkAsync();
                UpdatePlayButton();
                if (ViewModel.Mode == IslandMode.Expanded)
                {
                    UpdatePanel();
                }

                break;
            case nameof(IslandViewModel.Volume):
            case nameof(IslandViewModel.Battery):
            case nameof(IslandViewModel.Network):
            case nameof(IslandViewModel.Weather):
                UpdateGlyphs();
                break;
        }
    }

    private string PanelKey()
    {
        var activity = ViewModel.Activity;
        return ViewModel.Mode switch
        {
            IslandMode.Expanded => activity switch
            {
                IslandActivityKind.Timer => "Expanded.Timer",
                IslandActivityKind.Notification => "Expanded.Notification",
                _ when ViewModel.Media is { Title.Length: > 0 } && ViewModel.Settings.MediaModule => "Expanded.Media",
                _ => "Expanded.Dashboard",
            },
            IslandMode.Peek => $"Peek.{activity}",
            _ => $"Compact.{activity}",
        };
    }

    private void UpdatePanel()
    {
        _panels.TryGetValue(PanelKey(), out var next);
        if (ReferenceEquals(next, _currentPanel))
        {
            return;
        }

        if (_currentPanel is not null)
        {
            Motion.FadeScale(_currentPanel, show: false, fromScale: 0.96);
        }

        _currentPanel = next;
        if (next is not null)
        {
            // Let the pill grow a little before the content appears, like iOS.
            var growing = ViewModel.Mode != IslandMode.Compact;
            Motion.FadeScale(next, show: true, fromScale: growing ? 0.9 : 0.96, delayMs: growing ? 90 : 40);
        }
    }

    private async Task UpdateArtworkAsync()
    {
        var image = await Bind.FromBytesAsync(ViewModel.Media?.Thumbnail);
        ArtCompact.Source = image;
        ArtPeek.Source = image;
        ArtExpanded.Source = image;
    }

    private void UpdatePlayButton() =>
        PlayPauseButton.Content = ViewModel.IsMediaPlaying ? "\uE769" : "\uE768";

    private void UpdateTimerButton() =>
        TimerPauseButton.Content = ViewModel.Timer.IsPaused ? "\uE768" : "\uE769";

    private void UpdateGlyphs()
    {
        var v = ViewModel.Volume;
        var volumeGlyph = v.IsMuted || v.Level <= 0.001 ? "\uE74F" : v.Level < 0.34 ? "\uE993" : v.Level < 0.67 ? "\uE994" : "\uE995";
        CompactVolumeIcon.Glyph = volumeGlyph;
        PeekVolumeIcon.Glyph = volumeGlyph;

        var b = ViewModel.Battery;
        var level = Math.Clamp(b.Percent / 10, 0, 10);
        var batteryGlyph = b.IsPluggedIn ? char.ConvertFromUtf32(0xEBAB + level) : level == 10 ? "\uE83F" : char.ConvertFromUtf32(0xE850 + level);
        var batteryBrush = new SolidColorBrush(b.IsLow ? Microsoft.UI.ColorHelper.FromArgb(255, 255, 69, 58) : Microsoft.UI.ColorHelper.FromArgb(255, 48, 209, 88));
        CompactBatteryIcon.Glyph = batteryGlyph;
        PeekBatteryIcon.Glyph = batteryGlyph;
        CompactBatteryIcon.Foreground = batteryBrush;
        PeekBatteryIcon.Foreground = batteryBrush;

        var n = ViewModel.Network;
        var netGlyph = !n.IsConnected ? "\uE7BA" : n.IsWifi ? "\uE701" : "\uE839";
        CompactNetworkIcon.Glyph = netGlyph;
        PeekNetworkIcon.Glyph = netGlyph;

        PeekWeatherDescription.Text = ViewModel.Weather is { } w ? Localizer.Get(w.DescriptionKey) : "";
    }

    // ---- Pointer ------------------------------------------------------------------

    private void Pill_PointerEntered(object sender, PointerRoutedEventArgs e) => ViewModel.OnPointerEntered();

    private void Pill_PointerExited(object sender, PointerRoutedEventArgs e) => ViewModel.OnPointerExited();

    private void Pill_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Ignore taps that land on a button inside the island.
        for (var d = e.OriginalSource as DependencyObject; d is not null && d != Pill; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ButtonBase)
            {
                return;
            }
        }

        ViewModel.ToggleCommand.Execute(null);
    }

    // ---- Spring morph -------------------------------------------------------------

    private void AnimateTo(IslandSize size)
    {
        var parameters = Motion.Spring(SpringPresets.Smooth);
        _width.Parameters = parameters;
        _height.Parameters = parameters;
        _width.Target = size.Width;
        _height.Target = size.Height;

        if (Motion.Reduced)
        {
            _width.Snap(size.Width);
            _height.Snap(size.Height);
            ApplySize(size.Width, size.Height);
            return;
        }

        // Grow the hit region immediately so a growing pill is never clipped.
        UpdateRegion(Math.Max(size.Width, _width.Value), Math.Max(size.Height, _height.Value));
        if (!_animating)
        {
            _animating = true;
            FrameTicker.Run(_animate);
        }
    }

    private bool Animate(double dt)
    {
        var moving = _width.Step(dt) | _height.Step(dt);
        ApplySize(_width.Value, _height.Value);
        if (!moving)
        {
            _animating = false;
            UpdateRegion();
        }

        return moving;
    }

    private void ApplySize(double width, double height)
    {
        Pill.Width = Math.Max(20, width);
        Pill.Height = Math.Max(20, height);
        Pill.CornerRadius = new CornerRadius(Math.Min(Math.Max(20, height) / 2, 30));
    }

    private void UpdateRegion() => UpdateRegion(_width.Value, _height.Value);

    private void UpdateRegion(double widthDip, double heightDip)
    {
        var windowWidth = (IslandLayout.Max.Width + MarginDip * 2) * _scale;
        var w = widthDip * _scale;
        var h = (heightDip + 2) * _scale;
        var x = (windowWidth - w) / 2;
        WindowHelper.SetHitRegion(this, new RectInt32((int)Math.Floor(x) - 2, 0, (int)Math.Ceiling(w) + 4, (int)Math.Ceiling(h) + 4));
    }
}
