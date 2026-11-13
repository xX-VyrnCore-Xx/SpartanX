using System.Collections.Concurrent;
using System.Diagnostics;
using SpartanX.Core.Abstractions;

namespace SpartanX.App.Services;

internal sealed class AppLauncher : IAppLauncher
{
    private readonly ConcurrentDictionary<string, string> _shortcutTargets = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> LaunchAsync(string path, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, arguments ?? "") { UseShellExecute = true })?.Dispose();
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    public string ResolveExecutable(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return _shortcutTargets.GetOrAdd(path, static p =>
        {
            try
            {
                // WScript.Shell is the simplest stable way to read a .lnk without a COM interop library.
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type is null)
                {
                    return p;
                }

                dynamic shell = Activator.CreateInstance(type)!;
                var shortcut = shell.CreateShortcut(p);
                string target = shortcut.TargetPath;
                // Store-app shortcuts have no file target: keep the .lnk as identity.
                return string.IsNullOrWhiteSpace(target) || !File.Exists(target) ? p : target;
            }
            catch (Exception)
            {
                return p;
            }
        });
    }

    public string GetDisplayName(string path)
    {
        try
        {
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                {
                    return info.FileDescription!;
                }
            }
        }
        catch (Exception)
        {
        }

        return Path.GetFileNameWithoutExtension(path);
    }
}
