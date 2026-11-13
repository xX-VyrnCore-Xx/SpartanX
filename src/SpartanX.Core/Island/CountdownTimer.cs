namespace SpartanX.Core.Island;

/// <summary>A pausable countdown that derives its state from a clock instead of counting ticks.</summary>
public sealed class CountdownTimer(TimeProvider time)
{
    private DateTimeOffset _endsAt;
    private TimeSpan _remainingWhenPaused;

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsActive => IsRunning || IsPaused;
    public TimeSpan Duration { get; private set; }

    public event EventHandler? Completed;

    public TimeSpan Remaining => IsPaused
        ? _remainingWhenPaused
        : IsRunning ? Max(_endsAt - time.GetUtcNow(), TimeSpan.Zero) : TimeSpan.Zero;

    /// <summary>0 at start, 1 when finished.</summary>
    public double Progress => Duration > TimeSpan.Zero ? 1 - Remaining / Duration : 0;

    public void Start(TimeSpan duration)
    {
        Duration = duration;
        _endsAt = time.GetUtcNow() + duration;
        IsRunning = true;
        IsPaused = false;
    }

    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        _remainingWhenPaused = Remaining;
        IsRunning = false;
        IsPaused = true;
    }

    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        _endsAt = time.GetUtcNow() + _remainingWhenPaused;
        IsPaused = false;
        IsRunning = true;
    }

    public void Cancel()
    {
        IsRunning = false;
        IsPaused = false;
        Duration = TimeSpan.Zero;
    }

    /// <summary>Checks for completion. Call periodically.</summary>
    public void Tick()
    {
        if (IsRunning && Remaining <= TimeSpan.Zero)
        {
            IsRunning = false;
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    public static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
