using SpartanX.Core.Abstractions;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace SpartanX.App.Services;

/// <summary>
/// Mirrors Windows toast notifications into the Island via <see cref="UserNotificationListener"/>.
/// Windows only grants this API to apps with package identity, so in the unpackaged ZIP build it
/// reports itself unavailable (see docs/ROADMAP.md: sparse package).
/// </summary>
internal sealed class NotificationService : INotificationService, IDisposable
{
    private UserNotificationListener? _listener;
    private readonly HashSet<uint> _seen = [];
    private Timer? _poll;
    private bool _polling;

    public bool IsAvailable { get; private set; } = true;

    public string? UnavailableReasonKey { get; private set; }

    public event EventHandler<IslandNotification>? NotificationArrived;

    public async Task<bool> RequestAccessAsync()
    {
        try
        {
            _listener ??= UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            IsAvailable = status == UserNotificationListenerAccessStatus.Allowed;
            UnavailableReasonKey = IsAvailable ? null : "Notifications.Unavailable.Denied";
        }
        catch (Exception)
        {
            IsAvailable = false;
            UnavailableReasonKey = "Notifications.Unavailable.NoIdentity";
        }

        return IsAvailable;
    }

    public async Task StartAsync()
    {
        if (_poll is not null || !await RequestAccessAsync())
        {
            return;
        }

        // Prime with existing notifications so we only announce new ones.
        foreach (var n in await _listener!.GetNotificationsAsync(NotificationKinds.Toast))
        {
            _seen.Add(n.Id);
        }

        // Polling is used instead of NotificationChanged, which needs a background task registration.
        _poll = new Timer(_ => _ = PollAsync(), null, 1500, 1500);
    }

    private async Task PollAsync()
    {
        if (_polling || _listener is null)
        {
            return;
        }

        _polling = true;
        try
        {
            var current = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            foreach (var n in current.Where(n => _seen.Add(n.Id)).OrderBy(n => n.CreationTime))
            {
                var binding = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                var texts = binding?.GetTextElements().Select(t => t.Text).ToList() ?? [];
                NotificationArrived?.Invoke(this, new IslandNotification(
                    n.Id,
                    n.AppInfo.DisplayInfo.DisplayName,
                    texts.ElementAtOrDefault(0) ?? "",
                    string.Join(" ", texts.Skip(1)),
                    null,
                    n.CreationTime));
            }

            _seen.IntersectWith(current.Select(n => n.Id));
        }
        catch (Exception)
        {
            // Transient failures (e.g. session lock) are ignored; next poll retries.
        }
        finally
        {
            _polling = false;
        }
    }

    public void Dispose() => _poll?.Dispose();
}
