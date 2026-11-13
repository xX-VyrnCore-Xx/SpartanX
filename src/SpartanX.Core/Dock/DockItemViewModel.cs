using CommunityToolkit.Mvvm.ComponentModel;
using SpartanX.Core.Abstractions;

namespace SpartanX.Core.Dock;

public enum DockItemKind { App, Separator, Stack, Trash }

public sealed partial class DockItemViewModel : ObservableObject
{
    public DockItemViewModel(DockItemKind kind, string key, string displayName, string? path = null, string? arguments = null)
    {
        Kind = kind;
        Key = key;
        DisplayName = displayName;
        Path = path;
        Arguments = arguments;
    }

    public DockItemKind Kind { get; }

    /// <summary>Normalised executable path used to match windows to pinned items.</summary>
    public string Key { get; }

    public string? Path { get; }

    public string? Arguments { get; }

    [ObservableProperty] public partial string DisplayName { get; set; }
    [ObservableProperty] public partial bool IsPinned { get; set; }
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial bool IsLaunching { get; set; }
    [ObservableProperty] public partial object? Icon { get; set; }

    public IReadOnlyList<TrackedWindow> Windows { get; internal set; } = [];

    /// <summary>Raised when the view should play the launch bounce.</summary>
    public event EventHandler? BounceRequested;

    internal void RequestBounce() => BounceRequested?.Invoke(this, EventArgs.Empty);

    public override string ToString() => DisplayName;
}
