namespace SpartanX.Core.Abstractions;

/// <summary>A top-level window that would normally appear in the Windows taskbar.</summary>
public sealed record TrackedWindow(nint Handle, string Title, string? ExecutablePath, int ProcessId, bool IsMinimized);

public interface IWindowTracker
{
    IReadOnlyList<TrackedWindow> Windows { get; }

    nint ForegroundWindow { get; }

    /// <summary>Raised on the UI thread when the window list or foreground window changes.</summary>
    event EventHandler? Changed;

    void Start();

    void Activate(nint handle);

    void Minimize(nint handle);

    void Close(nint handle);
}
