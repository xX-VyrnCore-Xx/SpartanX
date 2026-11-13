using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpartanX.App.Animation;
using SpartanX.App.Helpers;
using SpartanX.Core.Animation;
using SpartanX.Core.Settings;

namespace SpartanX.App.Settings.Pages;

public sealed partial class AnimationsPage : UserControl
{
    private readonly Spring _w = new(120, SpringPresets.Smooth) { RestThreshold = 0.2 };
    private readonly Spring _h = new(30, SpringPresets.Smooth) { RestThreshold = 0.2 };
    private readonly Func<double, bool> _tick;
    private bool _expanded;

    public AnimationsPage(IServiceProvider services)
    {
        A = services.GetRequiredService<ISettingsService>().Current.Animations;
        InitializeComponent();
        EnumBinder.Bind(ProfileBox, () => A.Profile, v => A.Profile = v);
        _tick = Tick;
        CustomExpander.IsExpanded = A.Profile == AnimationProfile.Custom;
        A.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AnimationSettings.Profile))
            {
                CustomExpander.IsExpanded = A.Profile == AnimationProfile.Custom;
            }
        };
    }

    public AnimationSettings A { get; }

    private void Preview_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _expanded = !_expanded;
        _w.Parameters = _h.Parameters = Motion.Spring(SpringPresets.Smooth);
        _w.Target = _expanded ? 360 : 120;
        _h.Target = _expanded ? 100 : 30;
        if (Motion.Reduced)
        {
            _w.Snap(_w.Target);
            _h.Snap(_h.Target);
            Tick(0);
            return;
        }

        FrameTicker.Run(_tick);
    }

    private bool Tick(double dt)
    {
        var moving = _w.Step(dt) | _h.Step(dt);
        PreviewPill.Width = _w.Value;
        PreviewPill.Height = _h.Value;
        PreviewPill.CornerRadius = new CornerRadius(Math.Min(_h.Value / 2, 28));
        return moving;
    }
}
