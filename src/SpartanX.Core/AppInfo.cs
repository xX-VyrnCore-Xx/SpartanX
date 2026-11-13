using System.Reflection;

namespace SpartanX.Core;

/// <summary>Static facts about the running build.</summary>
public static class AppInfo
{
    public const string Name = "SpartanX";
    public const string RepositoryOwner = "xX-VyrnCore-Xx";
    public const string RepositoryName = "SpartanX";
    public const string RepositoryUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}";

    /// <summary>Informational version without the "+commit" build metadata.</summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
