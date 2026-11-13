namespace SpartanX.Core.Island;

/// <summary>Things the island can show. Order is also the default priority (higher wins).</summary>
public enum IslandActivityKind
{
    Idle = 0,
    Weather = 10,
    Media = 20,
    Timer = 30,
    Network = 40,
    Battery = 50,
    Volume = 60,
    Notification = 70,
    TimerFinished = 80,
}

/// <summary>
/// A live activity. Persistent activities (media, timer) stay until removed; transient ones
/// (volume, notification...) expire on their own.
/// </summary>
public sealed record IslandActivity(IslandActivityKind Kind, DateTimeOffset? ExpiresAt = null, object? Payload = null)
{
    public bool IsTransient => ExpiresAt is not null;

    public int Priority => (int)Kind;
}
