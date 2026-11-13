namespace SpartanX.Core.Abstractions;

/// <summary>Marshals work onto the UI thread. Implemented by the WinUI app with a DispatcherQueue.</summary>
public interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    void Post(Action action);
}

/// <summary>Runs everything inline. Used by tests and design-time.</summary>
public sealed class InlineDispatcher : IUiDispatcher
{
    public static readonly InlineDispatcher Instance = new();

    public bool HasThreadAccess => true;

    public void Post(Action action) => action();
}
