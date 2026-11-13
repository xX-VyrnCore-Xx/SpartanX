using SpartanX.Core.Abstractions;
using Windows.Media.Control;
using Windows.Storage.Streams;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace SpartanX.App.Services;

/// <summary>
/// Reads "now playing" from Windows' System Media Transport Controls: works with Spotify,
/// browsers (YouTube, etc.), Apple Music, VLC and any app that reports to the media flyout.
/// </summary>
internal sealed class MediaService : IMediaService, IDisposable
{
    private SessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private Timer? _timelineTimer;
    private byte[]? _thumbnail;
    private string? _thumbnailKey;

    public MediaSnapshot? Current { get; private set; }

    public event EventHandler<MediaSnapshot?>? Changed;

    public async Task StartAsync()
    {
        if (_manager is not null)
        {
            return;
        }

        try
        {
            _manager = await SessionManager.RequestAsync();
        }
        catch (Exception)
        {
            return; // SMTC unavailable (e.g. N editions without Media Feature Pack).
        }

        _manager.CurrentSessionChanged += (_, _) => AttachSession();
        AttachSession();
        // Position is only pushed occasionally by apps; refresh it while playing.
        _timelineTimer = new Timer(_ => { if (Current?.IsPlaying == true) _ = RefreshAsync(); }, null, 1000, 1000);
    }

    private void AttachSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
            _session.TimelinePropertiesChanged -= OnSessionChanged;
        }

        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionChanged;
            _session.PlaybackInfoChanged += OnSessionChanged;
            _session.TimelinePropertiesChanged += OnSessionChanged;
        }

        _ = RefreshAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var session = _session;
        if (session is null)
        {
            Publish(null);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            var key = $"{props.Title}|{props.Artist}|{props.AlbumTitle}";
            if (key != _thumbnailKey)
            {
                _thumbnailKey = key;
                _thumbnail = await ReadThumbnailAsync(props.Thumbnail);
            }

            var isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var position = timeline.Position;
            if (isPlaying)
            {
                // Extrapolate from the last time the app reported its position.
                position += DateTimeOffset.Now - timeline.LastUpdatedTime;
            }

            var duration = timeline.EndTime - timeline.StartTime;
            if (position > duration && duration > TimeSpan.Zero)
            {
                position = duration;
            }

            Publish(new MediaSnapshot(
                props.Title ?? "",
                props.Artist ?? "",
                props.AlbumTitle ?? "",
                session.SourceAppUserModelId ?? "",
                isPlaying,
                position < TimeSpan.Zero ? TimeSpan.Zero : position,
                duration,
                _thumbnail,
                playback.Controls.IsNextEnabled,
                playback.Controls.IsPreviousEnabled));
        }
        catch (Exception)
        {
            // Sessions can vanish between calls (app closed); the next event will fix state.
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        try
        {
            using var stream = await reference.OpenReadAsync();
            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Publish(MediaSnapshot? snapshot)
    {
        Current = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    public async Task PlayPauseAsync()
    {
        if (_session is not null)
        {
            await _session.TryTogglePlayPauseAsync();
        }
    }

    public async Task NextAsync()
    {
        if (_session is not null)
        {
            await _session.TrySkipNextAsync();
        }
    }

    public async Task PreviousAsync()
    {
        if (_session is not null)
        {
            await _session.TrySkipPreviousAsync();
        }
    }

    public void Dispose() => _timelineTimer?.Dispose();
}
