using SpartanX.Core.Animation;

namespace SpartanX.Core.Tests;

public class SpringTests
{
    [Fact]
    public void Settles_on_target()
    {
        var spring = new Spring(0, SpringPresets.Smooth) { Target = 100 };
        for (var i = 0; i < 600 && spring.Step(1 / 60.0); i++) { }

        Assert.True(spring.IsAtRest);
        Assert.Equal(100, spring.Value);
    }

    [Fact]
    public void Bouncy_spring_overshoots_but_critical_does_not()
    {
        static double Peak(SpringParameters p)
        {
            var s = new Spring(0, p) { Target = 100 };
            var peak = 0.0;
            for (var i = 0; i < 600; i++)
            {
                s.Step(1 / 120.0);
                peak = Math.Max(peak, s.Value);
            }

            return peak;
        }

        Assert.True(Peak(SpringPresets.Bouncy) > 102);
        Assert.True(Peak(new SpringParameters(0.4, 1.0)) <= 100.01);
    }

    [Fact]
    public void Result_is_independent_of_frame_rate()
    {
        var a = new Spring(0, SpringPresets.Smooth) { Target = 1 };
        var b = new Spring(0, SpringPresets.Smooth) { Target = 1 };
        for (var i = 0; i < 30; i++) a.Step(1 / 60.0);
        for (var i = 0; i < 72; i++) b.Step(1 / 144.0);

        Assert.Equal(a.Value, b.Value, 2);
    }

    [Fact]
    public void Retarget_preserves_velocity()
    {
        var s = new Spring(0, SpringPresets.Smooth) { Target = 100 };
        s.Step(0.05);
        var v = s.Velocity;
        s.Target = 50;

        Assert.Equal(v, s.Velocity);
    }

    [Fact]
    public void Speed_multiplier_scales_response()
    {
        Assert.Equal(0.21, SpringPresets.Smooth.WithSpeed(0.5).Response, 3);
        Assert.True(SpringPresets.Smooth.WithSpeed(0.5).Stiffness > SpringPresets.Smooth.Stiffness);
    }
}
