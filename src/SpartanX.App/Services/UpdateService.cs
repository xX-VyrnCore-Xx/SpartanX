using System.Diagnostics;
using System.Net.Http;
using SpartanX.Core;
using SpartanX.Core.Settings;
using SpartanX.Core.Updates;

namespace SpartanX.App.Services;

/// <summary>
/// In-app updates from GitHub Releases. Downloads the ZIP for this architecture, then a tiny
/// PowerShell script waits for SpartanX to exit, extracts it over the install folder and restarts.
/// </summary>
internal sealed class UpdateService(IUpdateClient client, ISettingsService settings, HttpClient http)
{
    public UpdateInfo? Available { get; private set; }

    public event EventHandler<UpdateInfo>? UpdateAvailable;

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!SemVersion.TryParse(AppInfo.Version, out var current))
        {
            return null;
        }

        try
        {
            Available = await client.GetUpdateAsync(current, settings.Current.General.IncludePrereleases, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (Available is not null)
        {
            UpdateAvailable?.Invoke(this, Available);
        }

        return Available;
    }

    /// <summary>Checks once at startup and then every 12 hours when enabled.</summary>
    public async Task RunBackgroundChecksAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        do
        {
            if (settings.Current.General.CheckForUpdates)
            {
                await CheckAsync(cancellationToken);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    public async Task InstallAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (update.PackageUrl is null)
        {
            Process.Start(new ProcessStartInfo(update.ReleasePageUrl) { UseShellExecute = true })?.Dispose();
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "SpartanX-update-" + update.Version);
        Directory.CreateDirectory(work);
        var zip = Path.Combine(work, update.PackageName ?? "update.zip");

        using (var response = await http.GetAsync(update.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(zip);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                read += n;
                if (total > 0)
                {
                    progress?.Report((double)read / total);
                }
            }
        }

        var installDir = AppContext.BaseDirectory.TrimEnd('\\');
        var exe = Environment.ProcessPath!;
        var script = Path.Combine(work, "apply-update.ps1");
        await File.WriteAllTextAsync(script, $$"""
            $ErrorActionPreference = 'Stop'
            Wait-Process -Id {{Environment.ProcessId}} -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 300
            $tmp = Join-Path $env:TEMP ('SpartanX-update-' + [guid]::NewGuid())
            Expand-Archive -LiteralPath '{{zip}}' -DestinationPath $tmp -Force
            # Newer packages wrap everything in a SpartanX\ folder; older ones don't.
            $src = if (Test-Path (Join-Path $tmp 'SpartanX\SpartanX.exe')) { Join-Path $tmp 'SpartanX' } else { $tmp }
            Copy-Item -Path (Join-Path $src '*') -Destination '{{installDir}}' -Recurse -Force
            Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
            Start-Process -FilePath '{{exe}}' -ArgumentList '--updated'
            """, cancellationToken);

        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.Dispose();

        App.Current.Quit();
    }
}
