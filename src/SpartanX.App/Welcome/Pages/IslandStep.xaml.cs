using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SpartanX.App.Animation;
using SpartanX.App.Helpers;
using SpartanX.Core.Animation;
using SpartanX.Core.Settings;
using SpartanX.Core.Welcome;

namespace SpartanX.App.Welcome.Pages;

public sealed partial class IslandStep : UserControl
{
    private readonly DispatcherQueueTimer _loop;
    private readonly Spring _w = new(90, SpringPresets.Bouncy) { RestThreshold = 0.2 };
    private readonly Spring _h = new(18, SpringPresets.Bouncy) { RestThreshold = 0.2 };
    private readonly Func<double, bool> _tick;
    private bool _expanded;

    public IslandStep(WelcomeViewModel vm)
    {
        Island = vm.Settings.Island;
        InitializeComponent();
        EnumBinder.Bind(InteractionBox, () => Island.Interaction, v => Island.Interaction = v);

        _tick = Tick;
        _loop = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _loop.Interval = TimeSpan.FromSeconds(1.8);
        _loop.Tick += (_, _) => Toggle();
        Loaded += (_, _) => _loop.Start();
        Unloaded += (_, _) => _loop.Stop();
    }

    public IslandSettings Island { get; }

    private void Toggle()
    {
        _expanded = !_expanded;
        _w.Parameters = _h.Parameters = Motion.Spring(SpringPresets.Bouncy);
        _w.Target = _expanded ? 220 : 90;
        _h.Target = _expanded ? 52 : 18;
        Motion.FadeScale(DemoContent, _expanded, 0.9, _expanded ? 120 : 0);
        FrameTicker.Run(_tick);
    }

    private bool Tick(double dt)
    {
        var moving = _w.Step(dt) | _h.Step(dt);
        DemoPill.Width = _w.Value;
        DemoPill.Height = _h.Value;
        DemoPill.CornerRadius = new CornerRadius(Math.Min(_h.Value / 2, 20));
        return moving;
    }
}
