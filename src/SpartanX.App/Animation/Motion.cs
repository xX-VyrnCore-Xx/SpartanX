using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using SpartanX.Core.Animation;
using SpartanX.Core.Settings;

namespace SpartanX.App.Animation;

/// <summary>
/// Composition-thread animations (run off the UI thread, so they stay smooth even when the app is busy)
/// configured from the user's Animation settings.
/// </summary>
internal static class Motion
{
    public static AnimationSettings? Settings { get; set; }

    public static bool Reduced => Settings?.ReduceMotion == true;

    public static SpringParameters Spring(SpringParameters preset)
    {
        if (Settings is null)
        {
            return preset;
        }

        var p = Settings.Profile switch
        {
            AnimationProfile.Snappy => SpringPresets.Snappy,
            AnimationProfile.Bouncy => SpringPresets.Bouncy,
            AnimationProfile.Custom => new SpringParameters(Settings.SpringResponse, Settings.SpringDampingFraction),
            _ => preset,
        };
        return p.WithSpeed(Settings.SpeedMultiplier);
    }

    public static TimeSpan Duration(double ms) =>
        TimeSpan.FromMilliseconds(Reduced ? 1 : ms * (Settings?.SpeedMultiplier ?? 1));

    public static Visual VisualOf(UIElement element) => ElementCompositionPreview.GetElementVisual(element);

    /// <summary>Fade + gentle scale, the "content morph" used inside the Island and Welcome pages.</summary>
    public static void FadeScale(UIElement element, bool show, double fromScale = 0.94, double delayMs = 0)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;
        if (element is FrameworkElement fe)
        {
            visual.CenterPoint = new Vector3((float)(fe.ActualWidth / 2), (float)(fe.ActualHeight / 2), 0);
        }

        if (show)
        {
            element.Visibility = Visibility.Visible;
        }

        var duration = Duration(show ? 320 : 160);
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.2f, 1f));

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(1f, show ? 1f : 0f, ease);
        opacity.Duration = duration;
        opacity.DelayTime = TimeSpan.FromMilliseconds(show ? delayMs : 0);
        opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var scale = compositor.CreateVector3KeyFrameAnimation();
        var s = show ? 1f : (float)fromScale;
        scale.InsertKeyFrame(1f, new Vector3(s, s, 1), ease);
        scale.Duration = duration;
        scale.DelayTime = opacity.DelayTime;

        if (show && visual.Opacity >= 0.99f)
        {
            visual.Opacity = 0;
            visual.Scale = new Vector3((float)fromScale, (float)fromScale, 1);
        }

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Scale", scale);
        batch.End();
        if (!show)
        {
            batch.Completed += (_, _) =>
            {
                if (visual.Opacity < 0.01f)
                {
                    element.Visibility = Visibility.Collapsed;
                }
            };
        }
    }

    /// <summary>Slides an element in from an offset (used for Welcome page transitions).</summary>
    public static void SlideIn(UIElement element, float fromX, float fromY = 0, double durationMs = 520)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(0f, new Vector3(fromX, fromY, 0));
        offset.InsertKeyFrame(1f, Vector3.Zero, ease);
        offset.Duration = Duration(durationMs);

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f, ease);
        opacity.Duration = Duration(durationMs * 0.8);

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        visual.StartAnimation("Translation", offset);
        visual.StartAnimation("Opacity", opacity);
    }

    /// <summary>Implicit offset animation: any layout move of the element glides instead of jumping.</summary>
    public static void EnableImplicitOffset(UIElement element, double durationMs = 280)
    {
        var visual = VisualOf(element);
        var compositor = visual.Compositor;
        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.Target = "Offset";
        offset.InsertExpressionKeyFrame(1f, "this.FinalValue");
        offset.Duration = Duration(durationMs);
        var collection = compositor.CreateImplicitAnimationCollection();
        collection["Offset"] = offset;
        visual.ImplicitAnimations = collection;
    }
}
