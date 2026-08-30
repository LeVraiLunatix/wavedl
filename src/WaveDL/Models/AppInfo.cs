using System.Reflection;

namespace WaveDL.Models;

public static class AppInfo
{
    public const string DisplayName = "WaveDL";
    public const string GitHubOwner = "LeVraiLunatix";
    public const string GitHubRepo = "wavedl";

    public static string UserAgent { get; } =
        $"WaveDL/{Version} (+https://github.com/{GitHubOwner}/{GitHubRepo})";

    public static Version Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public static string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";

    public static string ReleasesApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
