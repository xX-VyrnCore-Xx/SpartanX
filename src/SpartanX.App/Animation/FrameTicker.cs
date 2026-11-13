using System.Diagnostics;
using Microsoft.UI.Xaml.Media;

namespace SpartanX.App.Animation;

/// <summary>
/// Calls subscribers once per display frame (CompositionTarget.Rendering) with the real elapsed
/// time, and unhooks itself when nobody is animating so an idle shell costs ~0% CPU.
/// </summary>
internal static class FrameTicker
{
    private static readonly List<Func<double, bool>> Callbacks = [];
    private static readonly Stopwatch Clock = new();
    private static bool _hooked;
    private static double _last;

    /// <summary>Adds a callback that receives delta seconds and returns false when finished.</summary>
    public static void Run(Func<double, bool> callback)
    {
        if (!Callbacks.Contains(callback))
        {
            Callbacks.Add(callback);
        }

        if (!_hooked)
        {
            _hooked = true;
            Clock.Restart();
            _last = 0;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private static void OnRendering(object? sender, object e)
    {
        var now = Clock.Elapsed.TotalSeconds;
        var dt = now - _last;
        _last = now;

        for (var i = Callbacks.Count - 1; i >= 0; i--)
        {
            if (!Callbacks[i](dt))
            {
                Callbacks.RemoveAt(i);
            }
        }

        if (Callbacks.Count == 0)
        {
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }
    }
}
