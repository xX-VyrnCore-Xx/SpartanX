namespace SpartanX.Core.Abstractions;

/// <summary>What is playing right now, as reported by Windows' media sessions (SMTC).</summary>
public sealed record MediaSnapshot(
    string Title,
    string Artist,
    string AlbumTitle,
    string SourceAppId,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    byte[]? Thumbnail,
    bool CanGoNext,
    bool CanGoPrevious)
{
    public double Progress => Duration > TimeSpan.Zero
        ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0, 1)
        : 0;
}

public interface IMediaService
{
    MediaSnapshot? Current { get; }

    /// <summary>Raised on a background thread; consumers must marshal to the UI thread.</summary>
    event EventHandler<MediaSnapshot?>? Changed;

    Task StartAsync();

    Task PlayPauseAsync();

    Task NextAsync();

    Task PreviousAsync();
}
