using System.ComponentModel;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SpartanX.App.Animation;
using SpartanX.Core.Animation;
using SpartanX.Core.Dock;

namespace SpartanX.App.Controls;

/// <summary>
/// One Dock tile: icon, running indicator and launch bounce. Its size is driven by the Dock
/// window's magnification pass through <see cref="SetScale"/>.
/// </summary>
public sealed partial class DockIconView : Grid
{
    private readonly Border _tile;
    private readonly Image _image;
    private readonly FontIcon _fallback;
    private readonly Ellipse _indicator;
    private readonly Spring _scale = new(1, SpringPresets.Snappy) { RestThreshold = 0.001 };
    private readonly Spring _bounce = new(0, SpringPresets.Launch) { RestThreshold = 0.2 };
    private readonly Func<double, bool> _bounceTick;
    private int _bouncesLeft;
    private bool _vertical;
    private bool _startEdge;

    public DockIconView(DockItemViewModel item, double baseSize)
    {
        Item = item;
        BaseSize = baseSize;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        _image = new Image { Stretch = Stretch.Uniform };
        _fallback = new FontIcon
        {
            FontFamily = (FontFamily)Application.Current.Resources["SymbolFont"],
            Glyph = item.Kind switch
            {
                DockItemKind.Stack => "\uE8B7",
                DockItemKind.Trash => "\uE74D",
                _ => "\uE737",
            },
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        };

        var fallbackTile = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = item.Kind == DockItemKind.Trash
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 99, 99, 102))
                : (Brush)Application.Current.Resources["SpartanAccentBrush"],
            Child = _fallback,
        };

        _tile = new Border { Child = new Grid { Children = { fallbackTile, _image } }, Margin = new Thickness(0, 0, 0, IndicatorGap) };
        _indicator = new Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = (Brush)Application.Current.Resources["DockIndicatorBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        Children.Add(_tile);
        Children.Add(_indicator);
        ElementCompositionPreview.SetIsTranslationEnabled(_tile, true);

        _bounceTick = BounceTick;
        item.PropertyChanged += OnItemChanged;
        item.BounceRequested += (_, _) => Bounce();
        UpdateFromItem();
        ApplySize(1);
    }

    public DockItemViewModel Item { get; }

    public double BaseSize { get; set; }

    /// <summary>Space reserved between the tile and the screen-side edge for the running dot.</summary>
    public const double IndicatorGap = 10;

    /// <summary>Size across the dock axis; must fit the largest magnified tile so nothing is clipped.</summary>
    public double CrossSize { get; set; } = 120;

    public double CurrentScale => _scale.Value;

    /// <summary>Total length along the dock axis at the current scale (icon + spacing).</summary>
    public double Length => BaseSize * _scale.Value;

    public void SetOrientation(bool vertical, bool startEdge)
    {
        _vertical = vertical;
        // Grow away from the screen edge.
        _tile.VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _tile.HorizontalAlignment = vertical ? (startEdge ? HorizontalAlignment.Left : HorizontalAlignment.Right) : HorizontalAlignment.Center;
        _indicator.HorizontalAlignment = vertical ? (startEdge ? HorizontalAlignment.Left : HorizontalAlignment.Right) : HorizontalAlignment.Center;
        _indicator.VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _tile.Margin = vertical
            ? (startEdge ? new Thickness(IndicatorGap, 0, 0, 0) : new Thickness(0, 0, IndicatorGap, 0))
            : new Thickness(0, 0, 0, IndicatorGap);
        _indicator.Margin = vertical
            ? (startEdge ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 3, 0))
            : new Thickness(0, 0, 0, 3);
        _startEdge = startEdge;
        ApplySize(_scale.Value);
    }

    public void SetScaleTarget(double scale, bool immediate)
    {
        _scale.Parameters = Motion.Spring(SpringPresets.Snappy);
        if (immediate || Motion.Reduced)
        {
            _scale.Snap(scale);
            ApplySize(scale);
        }
        else
        {
            _scale.Target = scale;
        }
    }

    /// <summary>Advances the magnification spring. Returns true while moving.</summary>
    public bool StepScale(double dt)
    {
        var moving = _scale.Step(dt);
        ApplySize(_scale.Value);
        return moving;
    }

    public void SetShowIndicator(bool show) =>
        _indicator.Opacity = show && Item.IsRunning ? 1 : 0;

    private void ApplySize(double scale)
    {
        var s = BaseSize * scale;
        _tile.Width = s;
        _tile.Height = s;
        if (_tile.Child is Grid g && g.Children[0] is Border fb)
        {
            fb.CornerRadius = new CornerRadius(s * 0.225);
        }

        _fallback.FontSize = s * 0.45;
        if (_vertical)
        {
            Height = s;
            Width = CrossSize;
        }
        else
        {
            Width = s;
            Height = CrossSize;
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DockItemViewModel.Icon) or nameof(DockItemViewModel.IsRunning) or nameof(DockItemViewModel.IsLaunching))
        {
            UpdateFromItem();
        }
    }

    private void UpdateFromItem()
    {
        _image.Source = Item.Icon as ImageSource;
        _image.Visibility = _image.Source is null ? Visibility.Collapsed : Visibility.Visible;
        _indicator.Opacity = Item.IsRunning ? 1 : 0;
        if (!Item.IsLaunching)
        {
            _bouncesLeft = Math.Min(_bouncesLeft, 0);
        }
    }

    /// <summary>macOS launch bounce: hops until the app's window shows up (max 3).</summary>
    public void Bounce()
    {
        if (Motion.Reduced)
        {
            return;
        }

        _bouncesLeft = 3;
        Hop();
        FrameTicker.Run(_bounceTick);
    }

    private void Hop()
    {
        _bounce.Parameters = Motion.Spring(SpringPresets.Launch);
        _bounce.Kick(-BaseSize * 11);
        _bouncesLeft--;
    }

    private bool BounceTick(double dt)
    {
        var moving = _bounce.Step(dt);
        var offset = (float)Math.Min(0, _bounce.Value);
        var visual = ElementCompositionPreview.GetElementVisual(_tile);
        // Hop away from the screen edge the Dock is attached to.
        var hop = _vertical ? new Vector3(_startEdge ? -offset : offset, 0, 0) : new Vector3(0, offset, 0);
        visual.Properties.InsertVector3("Translation", hop);

        if (!moving && _bouncesLeft > 0 && Item.IsLaunching)
        {
            Hop();
            return true;
        }

        return moving;
    }
}
