using System.Text.Json;
using Quiver.Core.Services;

namespace Quiver.Services;

public sealed class LauncherUpdateCheckInfo
{
    public DateTime LastCheckTime { get; init; }
    public bool UpdateAvailable { get; init; }
    public string LastKnownVersion { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
}

public sealed class LauncherUpdateService
{
    private static readonly QuiverProfile Profile = QuiverProfile.Instance;

    public static int ComputePendingUpdatesCount(bool launcherUpdatePending, int gameUpdatesPending) =>
        (launcherUpdatePending ? 1 : 0) + Math.Max(0, gameUpdatesPending);

    public string ReadInstalledVersion(string? baseDirectory = null)
    {
        baseDirectory ??= AppDomain.CurrentDomain.BaseDirectory;
        var updateCheckFilePath = Path.Combine(baseDirectory, "update_check.json");

        try
        {
            if (File.Exists(updateCheckFilePath))
            {
                var json = File.ReadAllText(updateCheckFilePath);
                var updateInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (updateInfo != null &&
                    updateInfo.TryGetValue("CurrentVersion", out var versionElement))
                {
                    var version = versionElement.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
        }
        catch
        {
            // Fall through to version.txt
        }

        return LauncherVersionService.ReadInstalledVersion(baseDirectory);
    }

    public LauncherUpdateCheckInfo LoadUpdateCheckInfo(string? baseDirectory = null)
    {
        baseDirectory ??= AppDomain.CurrentDomain.BaseDirectory;
        var updateCheckFilePath = Path.Combine(baseDirectory, "update_check.json");

        if (!File.Exists(updateCheckFilePath))
        {
            return new LauncherUpdateCheckInfo
            {
                CurrentVersion = ReadInstalledVersion(baseDirectory),
            };
        }

        try
        {
            var json = File.ReadAllText(updateCheckFilePath);
            var info = JsonSerializer.Deserialize<LauncherUpdateCheckInfo>(json);
            if (info != null)
                return info;
        }
        catch
        {
            // Fall through to empty info
        }

        return new LauncherUpdateCheckInfo
        {
            CurrentVersion = ReadInstalledVersion(baseDirectory),
        };
    }

    public bool IsLauncherUpdatePending(string? baseDirectory = null)
    {
        var info = LoadUpdateCheckInfo(baseDirectory);
        if (!info.UpdateAvailable || string.IsNullOrWhiteSpace(info.LastKnownVersion))
            return false;

        var installedVersion = string.IsNullOrWhiteSpace(info.CurrentVersion)
            ? ReadInstalledVersion(baseDirectory)
            : info.CurrentVersion;

        return LauncherVersionService.IsNewerVersion(info.LastKnownVersion, installedVersion);
    }

    public async Task<string?> FetchLatestReleaseTagAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        var ownsClient = httpClient == null;
        httpClient ??= CreateClient();

        try
        {
            var response = await httpClient
                .GetStringAsync($"https://api.github.com/repos/{Profile.Repository}/releases/latest", cancellationToken)
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("tag_name").GetString();
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    public bool IsUpdateAvailable(string installedVersion, string? latestTag) =>
        !string.IsNullOrWhiteSpace(latestTag) &&
        LauncherVersionService.IsNewerVersion(latestTag, installedVersion);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("User-Agent", Profile.CliUserAgent);
        return client;
    }
}
