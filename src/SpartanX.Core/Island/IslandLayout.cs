namespace SpartanX.Core.Island;

public enum IslandMode
{
    /// <summary>Small pill, like the notch.</summary>
    Compact,
    /// <summary>Hover or auto-expanded on an event: one-line live activity.</summary>
    Peek,
    /// <summary>Clicked open: full panel with controls.</summary>
    Expanded,
}

public readonly record struct IslandSize(double Width, double Height)
{
    public double CornerRadius => Math.Min(Height / 2, 28);
}

/// <summary>Target sizes for every (mode, activity) pair. Pure so it can be unit-tested.</summary>
public static class IslandLayout
{
    /// <summary>Largest size the island can ever take; the host window is sized to fit it.</summary>
    public static readonly IslandSize Max = new(440, 200);

    public static IslandSize GetSize(IslandMode mode, IslandActivityKind activity) => mode switch
    {
        IslandMode.Expanded => activity switch
        {
            IslandActivityKind.Notification => new(420, 120),
            _ => new(420, 190),
        },
        IslandMode.Peek => activity switch
        {
            IslandActivityKind.Idle => new(240, 44),
            IslandActivityKind.Notification => new(380, 72),
            IslandActivityKind.Volume or IslandActivityKind.Battery or IslandActivityKind.Network => new(300, 44),
            _ => new(340, 60),
        },
        _ => activity switch
        {
            IslandActivityKind.Idle => new(150, 32),
            IslandActivityKind.Volume or IslandActivityKind.Battery or IslandActivityKind.Network => new(260, 34),
            IslandActivityKind.Notification => new(300, 34),
            _ => new(220, 34),
        },
    };
}
