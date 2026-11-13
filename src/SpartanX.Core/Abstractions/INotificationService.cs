namespace SpartanX.Core.Abstractions;

public sealed record IslandNotification(
    uint Id,
    string AppName,
    string Title,
    string Body,
    byte[]? AppIcon,
    DateTimeOffset ArrivedAt);

public interface INotificationService
{
    /// <summary>False when Windows does not allow this process to read notifications.</summary>
    bool IsAvailable { get; }

    /// <summary>Human readable reason (localisation key) when <see cref="IsAvailable"/> is false.</summary>
    string? UnavailableReasonKey { get; }

    event EventHandler<IslandNotification>? NotificationArrived;

    Task<bool> RequestAccessAsync();

    Task StartAsync();
}
