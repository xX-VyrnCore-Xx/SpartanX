using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SpartanX.App.Animation;
using SpartanX.Core.Animation;

namespace SpartanX.App.Controls;

/// <summary>
/// Small "now playing" equaliser like the iPhone island. Bar heights follow the real output
/// peak level (Core Audio meter) with per-bar springs so they move organically.
/// </summary>
public sealed partial class AudioBars : StackPanel
{
    private const int BarCount = 4;
    private const double MinBar = 3;
    private static readonly double[] Weights = [0.65, 1.0, 0.8, 0.55];

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly Spring[] _springs = new Spring[BarCount];
    private readonly Random _random = new();
    private readonly Func<double, bool> _tick;
    private double _sinceShuffle;
    private bool _loaded;
    private bool _running;

    public AudioBars()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2.5;
        VerticalAlignment = VerticalAlignment.Center;
        for (var i = 0; i < BarCount; i++)
        {
            _bars[i] = new Rectangle
            {
                Width = 3,
                Height = MinBar,
                RadiusX = 1.5,
                RadiusY = 1.5,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _springs[i] = new Spring(MinBar, SpringPresets.Snappy);
            Children.Add(_bars[i]);
        }

        _tick = Tick;
        Loaded += (_, _) =>
        {
            _loaded = true;
            UpdateRunning();
        };
        Unloaded += (_, _) => _loaded = false;
        ApplyBrush();
    }

    /// <summary>Returns the 0..1 output level. Set once by the app.</summary>
    public static Func<double>? PeakSource { get; set; }

    public double MaxBarHeight { get; set; } = 16;

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(AudioBars), new PropertyMetadata(false, (d, _) => ((AudioBars)d).UpdateRunning()));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(AudioBars), new PropertyMetadata(null, (d, _) => ((AudioBars)d).ApplyBrush()));

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    private void ApplyBrush()
    {
        var brush = BarBrush ?? (Application.Current.Resources.TryGetValue("SpartanAccentBrush", out var b) ? b as Brush : null);
        foreach (var bar in _bars)
        {
            bar.Fill = brush;
        }
    }

    private void UpdateRunning()
    {
        if (_loaded && !_running)
        {
            _running = true;
            FrameTicker.Run(_tick);
        }
    }

    private bool Tick(double dt)
    {
        _sinceShuffle += dt;
        var playing = IsPlaying && _loaded && Visibility == Visibility.Visible;
        var peak = playing ? Math.Clamp(PeakSource?.Invoke() ?? 0.6, 0, 1) : 0;
        if (playing && peak < 0.02)
        {
            peak = 0.25; // Apps that bypass the default endpoint still get gentle motion.
        }

        if (_sinceShuffle > 0.09)
        {
            _sinceShuffle = 0;
            for (var i = 0; i < BarCount; i++)
            {
                var jitter = 0.55 + _random.NextDouble() * 0.45;
                _springs[i].Target = MinBar + (MaxBarHeight - MinBar) * Math.Sqrt(peak) * Weights[i] * jitter;
            }
        }

        var moving = false;
        for (var i = 0; i < BarCount; i++)
        {
            moving |= _springs[i].Step(dt);
            _bars[i].Height = Math.Max(MinBar, _springs[i].Value);
        }

        _running = playing || moving;
        return _running;
    }
}
