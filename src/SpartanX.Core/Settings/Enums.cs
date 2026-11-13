namespace SpartanX.Core.Settings;

public enum AppTheme { System, Light, Dark }

public enum AccentMode { Spartan, Windows, Custom }

public enum BackdropMaterial { Mica, Acrylic, Glass, Solid }

public enum DockPosition { Bottom, Left, Right }

public enum IslandInteraction
{
    /// <summary>Hover shows the expanded preview, click opens the full panel.</summary>
    HoverExpandClickOpen,
    /// <summary>Only a click changes the island size.</summary>
    ClickOnly,
}

public enum ClockFormat { Auto, TwentyFourHour, TwelveHour }

public enum AnimationProfile { Smooth, Snappy, Bouncy, Custom }
