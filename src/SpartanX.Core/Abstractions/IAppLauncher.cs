namespace SpartanX.Core.Abstractions;

public interface IAppLauncher
{
    Task<bool> LaunchAsync(string path, string? arguments = null);

    void RevealInExplorer(string path);

    /// <summary>Resolves a .lnk to its target executable; returns other paths unchanged.</summary>
    string ResolveExecutable(string path);

    /// <summary>Friendly display name for an executable or shortcut.</summary>
    string GetDisplayName(string path);
}

/// <summary>Produces platform image objects (WinUI ImageSource) for files.</summary>
public interface IIconProvider
{
    Task<object?> GetIconAsync(string path, int size);
}

public interface IStartupService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
