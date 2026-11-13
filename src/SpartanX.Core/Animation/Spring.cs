namespace SpartanX.Core.Animation;

/// <summary>
/// Parameters of a damped harmonic spring expressed the way SwiftUI does:
/// <c>response</c> is the period of the undamped oscillation (seconds) and
/// <c>dampingFraction</c> is 1 for critical damping, &lt;1 for bounce.
/// </summary>
public readonly record struct SpringParameters(double Response, double DampingFraction)
{
    public double Stiffness => Math.Pow(2 * Math.PI / Math.Max(Response, 0.01), 2);

    public double Damping => 4 * Math.PI * DampingFraction / Math.Max(Response, 0.01);

    public SpringParameters WithSpeed(double multiplier) =>
        this with { Response = Response * Math.Clamp(multiplier, 0.1, 4) };
}

public static class SpringPresets
{
    /// <summary>Default: quick, very slight overshoot. Used for the Island.</summary>
    public static readonly SpringParameters Smooth = new(0.42, 0.78);
    /// <summary>Fast and tight, no visible bounce. Used for hover feedback.</summary>
    public static readonly SpringParameters Snappy = new(0.28, 0.92);
    /// <summary>Playful overshoot, like the iPhone Dynamic Island morph.</summary>
    public static readonly SpringParameters Bouncy = new(0.5, 0.62);
    /// <summary>Used for Dock icon bounce on launch.</summary>
    public static readonly SpringParameters Launch = new(0.36, 0.35);
}

/// <summary>
/// A frame-rate independent spring integrator (semi-implicit Euler with fixed sub-steps).
/// Retargeting keeps the current velocity, which is what makes interrupted animations
/// feel continuous instead of snapping.
/// </summary>
public sealed class Spring
{
    private const double MaxStep = 1.0 / 240.0;
    private const double RestVelocity = 0.01;

    public Spring(double initialValue, SpringParameters parameters)
    {
        Value = initialValue;
        Target = initialValue;
        Parameters = parameters;
    }

    public double Value { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; set; }
    public SpringParameters Parameters { get; set; }

    /// <summary>Distance from the target under which the spring is considered settled.</summary>
    public double RestThreshold { get; set; } = 0.05;

    public bool IsAtRest =>
        Math.Abs(Target - Value) < RestThreshold && Math.Abs(Velocity) < RestVelocity + RestThreshold;

    /// <summary>Jumps to a value with no animation.</summary>
    public void Snap(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    /// <summary>Adds an instantaneous impulse (e.g. Dock bounce).</summary>
    public void Kick(double velocity) => Velocity += velocity;

    /// <summary>Advances the simulation. Returns true while still moving.</summary>
    public bool Step(double deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return !IsAtRest;
        }

        // Avoid a huge jump after the window was hidden / debugger paused.
        deltaSeconds = Math.Min(deltaSeconds, 0.1);
        var k = Parameters.Stiffness;
        var c = Parameters.Damping;

        while (deltaSeconds > 0)
        {
            var dt = Math.Min(deltaSeconds, MaxStep);
            var force = -k * (Value - Target) - c * Velocity;
            Velocity += force * dt;
            Value += Velocity * dt;
            deltaSeconds -= dt;
        }

        if (IsAtRest)
        {
            Value = Target;
            Velocity = 0;
            return false;
        }

        return true;
    }
}
