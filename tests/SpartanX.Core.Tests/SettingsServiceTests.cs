using SpartanX.Core.Settings;

namespace SpartanX.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spartanx-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Round_trips_to_disk()
    {
        using (var s = new SettingsService(_dir))
        {
            s.Current.Dock.IconSize = 64;
            s.Current.Appearance.Theme = AppTheme.Dark;
            s.Current.Dock.PinnedApps.Add(new PinnedApp("Edge", @"C:\Edge\msedge.exe"));
            await s.SaveAsync();
        }

        using var reloaded = new SettingsService(_dir);
        Assert.Equal(64, reloaded.Current.Dock.IconSize);
        Assert.Equal(AppTheme.Dark, reloaded.Current.Appearance.Theme);
        Assert.Equal("Edge", Assert.Single(reloaded.Current.Dock.PinnedApps).Name);
        Assert.Contains("\"dark\"", File.ReadAllText(Path.Combine(_dir, "settings.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not json");

        using var s = new SettingsService(_dir);

        Assert.Equal(52, s.Current.Dock.IconSize);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json.corrupt")));
    }

    [Fact]
    public void Changes_raise_event_and_are_debounced_to_disk()
    {
        using var s = new SettingsService(_dir, TimeSpan.FromMilliseconds(20));
        string? changed = null;
        s.Changed += (_, e) => changed = e.PropertyName;

        s.Current.Island.WeatherCity = "Roma";

        Assert.Equal(nameof(IslandSettings.WeatherCity), changed);
        var file = Path.Combine(_dir, "settings.json");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((!File.Exists(file) || !File.ReadAllText(file).Contains("Roma")) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        Assert.Contains("Roma", File.ReadAllText(file));
    }

    [Fact]
    public void Missing_sections_get_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "general": { "firstRunCompleted": true } }""");

        using var s = new SettingsService(_dir);

        Assert.True(s.Current.General.FirstRunCompleted);
        Assert.True(s.Current.Island.Enabled);
        Assert.Equal(AppearanceSettings.SpartanRed, s.Current.Appearance.CustomAccent);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }
}
