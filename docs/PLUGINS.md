# Plugin SDK — proposal

> Status: **design draft**. Nothing here is implemented yet; feedback welcome in issues.

## Goals

- Let third parties add **island activities** (e.g. a delivery tracker, CI status, calendar countdown) and **Dock items**.
- Keep the shell safe: a crashing plugin must not take down the Dock or leave the taskbar hidden.
- Keep the authoring model small and C#-first.

## Sketch

```csharp
public interface ISpartanPlugin
{
    string Id { get; }                 // "com.example.ci-status"
    string DisplayName { get; }
    Task StartAsync(IPluginHost host, CancellationToken ct);
}

public interface IPluginHost
{
    // Show or update a live activity. Priority is clamped below system activities.
    void PostActivity(PluginActivity activity);
    void ClearActivity(string activityId);

    // Simple key/value settings stored under the plugin's id.
    IPluginSettings Settings { get; }
}

public sealed record PluginActivity(
    string Id,
    string CompactText,
    string? PeekTitle = null,
    string? PeekSubtitle = null,
    string? IconGlyph = null,
    double? Progress = null,
    TimeSpan? Duration = null);
```

## Isolation options under consideration

1. **Out-of-process** plugins talking JSON-RPC over a named pipe (safest; any language).
2. In-process `AssemblyLoadContext` (simplest; a crash can still affect the host).

Plugins would be discovered from `%LOCALAPPDATA%\SpartanX\Plugins\<id>\plugin.json`.
