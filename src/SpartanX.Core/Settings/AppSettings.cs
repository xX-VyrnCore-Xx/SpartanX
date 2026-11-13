using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpartanX.Core.Settings;

/// <summary>
/// Root of every user preference. Each section is observable so the UI can bind to it
/// two-way and every window updates live while the user drags a slider.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Bumped whenever the on-disk shape changes in a non-additive way.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public DockSettings Dock { get; set; } = new();
    public IslandSettings Island { get; set; } = new();
    public MenuBarSettings MenuBar { get; set; } = new();
    public AnimationSettings Animations { get; set; } = new();
    public ShortcutSettings Shortcuts { get; set; } = new();

    [JsonIgnore]
    public IEnumerable<ObservableObject> Sections =>
        [General, Appearance, Dock, Island, MenuBar, Animations, Shortcuts];
}

public sealed partial class GeneralSettings : ObservableObject
{
    /// <summary>BCP-47 tag ("en", "it"). Empty = follow Windows.</summary>
    [ObservableProperty] public partial string Language { get; set; } = "";
    [ObservableProperty] public partial bool FirstRunCompleted { get; set; }
    [ObservableProperty] public partial bool LaunchAtStartup { get; set; } = true;
    [ObservableProperty] public partial bool CheckForUpdates { get; set; } = true;
    [ObservableProperty] public partial bool IncludePrereleases { get; set; }
    [ObservableProperty] public partial bool HideWindowsTaskbar { get; set; } = true;
}

public sealed partial class AppearanceSettings : ObservableObject
{
    public const string SpartanRed = "#B3202A";

    [ObservableProperty] public partial AppTheme Theme { get; set; } = AppTheme.System;
    [ObservableProperty] public partial AccentMode AccentMode { get; set; } = AccentMode.Spartan;
    /// <summary>Hex colour used when <see cref="AccentMode"/> is Custom.</summary>
    [ObservableProperty] public partial string CustomAccent { get; set; } = SpartanRed;
    [ObservableProperty] public partial BackdropMaterial Material { get; set; } = BackdropMaterial.Acrylic;
}

public sealed partial class DockSettings : ObservableObject
{
    [ObservableProperty] public partial bool Enabled { get; set; } = true;
    [ObservableProperty] public partial DockPosition Position { get; set; } = DockPosition.Bottom;
    [ObservableProperty] public partial double IconSize { get; set; } = 52;
    [ObservableProperty] public partial bool Magnification { get; set; } = true;
    /// <summary>Peak scale of the icon directly under the cursor (1 = off).</summary>
    [ObservableProperty] public partial double MagnificationScale { get; set; } = 1.75;
    [ObservableProperty] public partial bool AutoHide { get; set; }
    [ObservableProperty] public partial bool ShowRunningIndicators { get; set; } = true;
    [ObservableProperty] public partial bool BounceOnLaunch { get; set; } = true;
    [ObservableProperty] public partial bool MinimizeOnClickWhenActive { get; set; } = true;
    /// <summary>True once the Windows taskbar pins were imported, so emptying the Dock sticks.</summary>
    [ObservableProperty] public partial bool PinsImported { get; set; }

    public ObservableCollection<PinnedApp> PinnedApps { get; set; } = [];
    public ObservableCollection<string> Stacks { get; set; } = [];
}

/// <summary>An app pinned in the Dock. <see cref="Path"/> may be an .exe, .lnk or shell URI.</summary>
public sealed record PinnedApp(string Name, string Path, string? Arguments = null);

public sealed partial class IslandSettings : ObservableObject
{
    [ObservableProperty] public partial bool Enabled { get; set; } = true;
    [ObservableProperty] public partial IslandInteraction Interaction { get; set; } = IslandInteraction.HoverExpandClickOpen;
    [ObservableProperty] public partial bool AutoExpandOnEvents { get; set; } = true;
    [ObservableProperty] public partial double AutoCollapseSeconds { get; set; } = 4;

    [ObservableProperty] public partial bool MediaModule { get; set; } = true;
    [ObservableProperty] public partial bool NotificationsModule { get; set; } = true;
    [ObservableProperty] public partial bool SystemStatusModule { get; set; } = true;
    [ObservableProperty] public partial bool TimerModule { get; set; } = true;
    [ObservableProperty] public partial bool WeatherModule { get; set; } = true;

    /// <summary>City shown by the weather module, geocoded through Open-Meteo.</summary>
    [ObservableProperty] public partial string WeatherCity { get; set; } = "";
    [ObservableProperty] public partial bool UseFahrenheit { get; set; }
}

public sealed partial class MenuBarSettings : ObservableObject
{
    [ObservableProperty] public partial bool Enabled { get; set; } = true;
    [ObservableProperty] public partial ClockFormat ClockFormat { get; set; } = ClockFormat.Auto;
    [ObservableProperty] public partial bool ShowSeconds { get; set; }
    [ObservableProperty] public partial bool ShowDate { get; set; } = true;
    [ObservableProperty] public partial bool ShowBatteryPercentage { get; set; } = true;
    [ObservableProperty] public partial bool ShowActiveAppName { get; set; } = true;
}

public sealed partial class AnimationSettings : ObservableObject
{
    [ObservableProperty] public partial AnimationProfile Profile { get; set; } = AnimationProfile.Smooth;
    /// <summary>Multiplies every animation duration. 0.5 = twice as fast.</summary>
    [ObservableProperty] public partial double SpeedMultiplier { get; set; } = 1.0;
    /// <summary>Apple-style spring "response" in seconds (used by the Custom profile).</summary>
    [ObservableProperty] public partial double SpringResponse { get; set; } = 0.42;
    /// <summary>Apple-style damping fraction: 1 = no overshoot, lower = bouncier.</summary>
    [ObservableProperty] public partial double SpringDampingFraction { get; set; } = 0.78;
    [ObservableProperty] public partial bool ReduceMotion { get; set; }
}

public sealed partial class ShortcutSettings : ObservableObject
{
    [ObservableProperty] public partial string ToggleDock { get; set; } = "Ctrl+Alt+D";
    [ObservableProperty] public partial string OpenIsland { get; set; } = "Ctrl+Alt+I";
    [ObservableProperty] public partial string OpenSettings { get; set; } = "Ctrl+Alt+,";
    [ObservableProperty] public partial string PlayPause { get; set; } = "Ctrl+Alt+Space";
    [ObservableProperty] public partial string StartTimer { get; set; } = "Ctrl+Alt+T";
}
