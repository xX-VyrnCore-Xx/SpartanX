using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SpartanX.Core.Updates;

public sealed record UpdateInfo(SemVersion Version, string TagName, string Notes, string ReleasePageUrl, string? PackageUrl, string? PackageName)
{
    /// <summary>True when the package is the Inno Setup installer rather than a ZIP.</summary>
    public bool IsInstaller => PackageName?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true;
}

public interface IUpdateClient
{
    /// <summary>Returns the newest release strictly newer than <paramref name="current"/>, or null.</summary>
    Task<UpdateInfo?> GetUpdateAsync(SemVersion current, bool includePrereleases, CancellationToken cancellationToken = default);
}

/// <summary>Checks GitHub Releases of this repository. Prefers the installer
/// <c>SpartanX-Setup-{version}-{arch}.exe</c>; falls back to the legacy
/// <c>SpartanX-{version}-win-{arch}.zip</c> of releases up to v0.1.1.</summary>
public sealed class GitHubReleaseClient : IUpdateClient
{
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _arch;

    public GitHubReleaseClient(HttpClient http, string owner, string repo, Architecture? architecture = null)
    {
        _http = http;
        _owner = owner;
        _repo = repo;
        _arch = (architecture ?? RuntimeInformation.ProcessArchitecture) == Architecture.Arm64 ? "arm64" : "x64";
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Core.AppInfo.Name}-Updater");
        }
    }

    public async Task<UpdateInfo?> GetUpdateAsync(SemVersion current, bool includePrereleases, CancellationToken cancellationToken = default)
    {
        var releases = await _http.GetFromJsonAsync<List<Release>>(
            $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=20", cancellationToken).ConfigureAwait(false) ?? [];

        UpdateInfo? best = null;
        foreach (var release in releases)
        {
            if (release.Draft || (release.Prerelease && !includePrereleases))
            {
                continue;
            }

            if (!SemVersion.TryParse(release.TagName, out var version) || version.CompareTo(current) <= 0)
            {
                continue;
            }

            if (best is not null && version.CompareTo(best.Version) <= 0)
            {
                continue;
            }

            var assets = release.Assets ?? [];
            var asset = assets.FirstOrDefault(a =>
                    a.Name.StartsWith("SpartanX-Setup-", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith($"-{_arch}.exe", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(a => a.Name.EndsWith($"-win-{_arch}.zip", StringComparison.OrdinalIgnoreCase));
            best = new UpdateInfo(version, release.TagName, release.Body ?? "", release.HtmlUrl, asset?.BrowserDownloadUrl, asset?.Name);
        }

        return best;
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] List<Asset>? Assets);

    private sealed record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
