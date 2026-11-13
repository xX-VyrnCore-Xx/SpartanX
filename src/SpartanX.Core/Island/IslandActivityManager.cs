namespace SpartanX.Core.Island;

/// <summary>
/// Keeps the set of live activities and decides which one the island shows.
/// One activity per kind: posting the same kind again replaces it (e.g. repeated volume ticks
/// extend the same toast instead of queueing many).
/// </summary>
public sealed class IslandActivityManager(TimeProvider time)
{
    private readonly Dictionary<IslandActivityKind, IslandActivity> _activities = [];

    public IslandActivity Current { get; private set; } = new(IslandActivityKind.Idle);

    /// <summary>Raised when <see cref="Current"/> changes kind or payload.</summary>
    public event EventHandler<IslandActivity>? CurrentChanged;

    public IReadOnlyCollection<IslandActivity> All => _activities.Values;

    public void SetPersistent(IslandActivityKind kind, object? payload = null)
    {
        _activities[kind] = new IslandActivity(kind, null, payload);
        Evaluate();
    }

    public void ShowTransient(IslandActivityKind kind, TimeSpan duration, object? payload = null)
    {
        _activities[kind] = new IslandActivity(kind, time.GetUtcNow() + duration, payload);
        Evaluate();
    }

    public void Remove(IslandActivityKind kind)
    {
        if (_activities.Remove(kind))
        {
            Evaluate();
        }
    }

    public bool Has(IslandActivityKind kind) => _activities.ContainsKey(kind);

    /// <summary>Expires transient activities. Call periodically (e.g. every 250 ms).</summary>
    public void Tick()
    {
        var now = time.GetUtcNow();
        var expired = _activities.Values.Where(a => a.ExpiresAt <= now).Select(a => a.Kind).ToList();
        foreach (var kind in expired)
        {
            _activities.Remove(kind);
        }

        Evaluate();
    }

    private void Evaluate()
    {
        var best = _activities.Values.MaxBy(a => a.Priority) ?? new IslandActivity(IslandActivityKind.Idle);
        if (best.Kind != Current.Kind || !Equals(best.Payload, Current.Payload) || best.ExpiresAt != Current.ExpiresAt)
        {
            Current = best;
            CurrentChanged?.Invoke(this, best);
        }
    }
}
