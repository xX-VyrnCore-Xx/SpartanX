using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using SpartanX.Core.Input;
using SpartanX.Core.Localization;
using SpartanX.Core.MenuBar;
using SpartanX.Core.Settings;
using SpartanX.Core.Updates;
using SpartanX.Core.Welcome;

namespace SpartanX.Core.Tests;

public class KeyGestureTests
{
    [Theory]
    [InlineData("Win+Alt+D", KeyModifiers.Win | KeyModifiers.Alt, 0x44, "Win+Alt+D")]
    [InlineData("ctrl + alt + space", KeyModifiers.Ctrl | KeyModifiers.Alt, 0x20, "Ctrl+Alt+Space")]
    [InlineData("Shift+Win+F12", KeyModifiers.Shift | KeyModifiers.Win, 0x7B, "Win+Shift+F12")]
    [InlineData("Alt+1", KeyModifiers.Alt, 0x31, "Alt+1")]
    public void Parses_and_normalises(string text, KeyModifiers mods, int vk, string canonical)
    {
        Assert.True(KeyGesture.TryParse(text, out var g));
        Assert.Equal(mods, g!.Modifiers);
        Assert.Equal(vk, g.VirtualKey);
        Assert.Equal(canonical, g.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+NotAKey")]
    public void Rejects_invalid(string text) => Assert.False(KeyGesture.TryParse(text, out _));
}

public class SemVersionTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.2", 1)]
    [InlineData("1.0.0", "1.0.0-beta.2", 1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("2.0", "1.9.9", 1)]
    [InlineData("1.0.0+abc", "1.0.0", 0)]
    public void Compares_by_precedence(string a, string b, int expected)
    {
        Assert.True(SemVersion.TryParse(a, out var va));
        Assert.True(SemVersion.TryParse(b, out var vb));
        Assert.Equal(expected, Math.Sign(va.CompareTo(vb)));
    }

    [Fact]
    public void Rejects_garbage() => Assert.False(SemVersion.TryParse("latest", out _));
}

public class GitHubReleaseClientTests
{
    private const string Json = """
    [
      { "tag_name": "v0.3.0-beta.1", "html_url": "https://x/3b", "body": "beta", "draft": false, "prerelease": true,
        "assets": [ { "name": "SpartanX-0.3.0-beta.1-win-x64.zip", "browser_download_url": "https://x/3b.zip" } ] },
      { "tag_name": "v0.2.0", "html_url": "https://x/2", "body": "notes", "draft": false, "prerelease": false,
        "assets": [ { "name": "SpartanX-0.2.0-win-x64.zip", "browser_download_url": "https://x/2-x64.zip" },
                    { "name": "SpartanX-0.2.0-win-arm64.zip", "browser_download_url": "https://x/2-arm.zip" } ] },
      { "tag_name": "v9.9.9", "html_url": "https://x/9", "body": "", "draft": true, "prerelease": false, "assets": [] }
    ]
    """;

    private static GitHubReleaseClient Client(Architecture arch) =>
        new(new HttpClient(new StubHandler(Json)), "o", "r", arch);

    [Fact]
    public async Task Picks_newest_stable_release_and_matching_asset()
    {
        SemVersion.TryParse("0.1.0", out var current);
        var update = await Client(Architecture.Arm64).GetUpdateAsync(current, includePrereleases: false);

        Assert.NotNull(update);
        Assert.Equal("v0.2.0", update.TagName);
        Assert.Equal("https://x/2-arm.zip", update.PackageUrl);
    }

    [Fact]
    public async Task Includes_prereleases_when_asked()
    {
        SemVersion.TryParse("0.1.0", out var current);
        var update = await Client(Architecture.X64).GetUpdateAsync(current, includePrereleases: true);

        Assert.Equal("v0.3.0-beta.1", update!.TagName);
    }

    [Fact]
    public async Task Returns_null_when_up_to_date()
    {
        SemVersion.TryParse("0.2.0", out var current);
        Assert.Null(await Client(Architecture.X64).GetUpdateAsync(current, includePrereleases: false));
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}

public class LocalizationTests
{
    [Fact]
    public void Every_language_has_exactly_the_english_keys()
    {
        var en = Localizer.LoadTable("en");
        Assert.NotEmpty(en);
        foreach (var lang in Localizer.SupportedLanguages)
        {
            var table = Localizer.LoadTable(lang.Tag);
            Assert.Empty(en.Keys.Except(table.Keys));
            Assert.Empty(table.Keys.Except(en.Keys));
        }
    }

    [Theory]
    [InlineData("it-IT", "it")]
    [InlineData("en_US", "en")]
    [InlineData("xx", "en")]
    public void Resolves_language_tags(string tag, string expected) => Assert.Equal(expected, Localizer.Resolve(tag));

    [Fact]
    public void Every_settings_enum_value_has_a_label()
    {
        var en = Localizer.LoadTable("en");
        var enums = typeof(AppSettings).Assembly.GetTypes().Where(t => t.IsEnum && t.Namespace == typeof(AppSettings).Namespace);
        foreach (var type in enums)
        {
            foreach (var name in Enum.GetNames(type))
            {
                Assert.True(en.ContainsKey($"Enum.{type.Name}.{name}"), $"Missing Enum.{type.Name}.{name}");
            }
        }
    }

    [Fact]
    public void Missing_key_returns_key() => Assert.Equal("No.Such.Key", Localizer.Get("No.Such.Key"));
}

public class MenuBarClockTests
{
    private static readonly DateTimeOffset Afternoon = new(2026, 3, 9, 15, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Formats_24h_with_seconds()
    {
        var s = new MenuBarSettings { ClockFormat = ClockFormat.TwentyFourHour, ShowSeconds = true, ShowDate = false };
        Assert.Equal("15:04:05", MenuBarViewModel.FormatClock(Afternoon, s, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Formats_12h_with_date()
    {
        var s = new MenuBarSettings { ClockFormat = ClockFormat.TwelveHour, ShowDate = true };
        Assert.Equal("Mon 9 Mar  3:04 PM", MenuBarViewModel.FormatClock(Afternoon, s, CultureInfo.InvariantCulture));
    }
}

public class WelcomeTests
{
    [Fact]
    public void Walks_all_steps_and_marks_first_run_done()
    {
        var settings = new InMemorySettings();
        var vm = new WelcomeViewModel(settings);
        var finished = false;
        vm.Finished += (_, _) => finished = true;

        Assert.True(vm.IsFirst);
        for (var i = 0; i < WelcomeViewModel.StepCount - 1; i++)
        {
            vm.NextCommand.Execute(null);
        }

        Assert.True(vm.IsLast);
        Assert.Equal(1, vm.Progress);
        vm.BackCommand.Execute(null);
        Assert.Equal(-1, vm.LastDirection);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        Assert.True(finished);
        Assert.True(settings.Current.General.FirstRunCompleted);
    }

    [Fact]
    public void Cannot_go_back_to_hello() => Assert.False(new WelcomeViewModel(new InMemorySettings()) { Step = WelcomeStep.Language }.CanGoBack);
}
