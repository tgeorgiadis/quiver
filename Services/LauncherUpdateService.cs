using System.Text.Json;
using Quiver.Core.Services;

namespace Quiver.Services;

public sealed class LauncherUpdateService
{
    private static readonly QuiverProfile Profile = QuiverProfile.Instance;

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
