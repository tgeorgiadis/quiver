using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Quiver.Core.Models;
using Quiver.Core.Services;
using Quiver.Models;

namespace Quiver.Services;

public static class GameDownloadInstallService
{
    public static async Task DownloadAndInstallAsync(
        GameInfo game,
        HttpClient httpClient,
        string gamesFolder,
        GitHubRelease? latestRelease,
        AppSettings settings,
        GameStatus triggerStatus,
        IGameDownloadDialogs? dialogs = null)
    {
        dialogs ??= AvaloniaGameDownloadDialogs.Instance;

        if (string.IsNullOrEmpty(game.FolderName))
        {
            await dialogs.ShowErrorAsync("App configuration is invalid (missing folder name).", "Configuration Error");
            return;
        }

        if (string.IsNullOrEmpty(game.Repository))
        {
            await dialogs.ShowErrorAsync("App configuration is invalid (missing repository).", "Configuration Error");
            return;
        }

        var apiToken = settings?.GitHubApiToken ?? string.Empty;

        try
        {
            game.Status = triggerStatus == GameStatus.UpdateAvailable ? GameStatus.Updating : GameStatus.Downloading;
            game.DownloadProgress = 0;

            var gamePath = game.GetInstallPath(gamesFolder);
            var versionFile = Path.Combine(gamePath, "version.txt");

            if (latestRelease == null)
            {
                if (GitHubApiCache.TryGetCachedVersion(game.Repository, out var cache) && cache?.CachedRelease != null)
                {
                    latestRelease = cache.CachedRelease;
                }
                else
                {
                    game.DownloadProgress = 5;
                    var releaseResult = await GitHubReleaseService.FetchReleasesAsync(
                        httpClient,
                        game.Repository,
                        apiToken).ConfigureAwait(false);

                    if (releaseResult.Releases.Count == 0)
                    {
                        await dialogs.ShowErrorAsync($"No releases found for {game.Name}.", "No Releases");
                        game.Status = GameStatus.NotInstalled;
                        game.DownloadProgress = 0;
                        return;
                    }

                    latestRelease = releaseResult.Releases.FirstOrDefault();

                    if (latestRelease == null)
                    {
                        await dialogs.ShowErrorAsync($"No valid releases found for {game.Name}.", "No Releases");
                        game.Status = GameStatus.NotInstalled;
                        game.DownloadProgress = 0;
                        return;
                    }

                    GitHubApiCache.SetCache(
                        game.Repository,
                        latestRelease.tag_name,
                        releaseResult.ETag ?? string.Empty,
                        latestRelease);
                }
            }

            game.DownloadProgress = 10;

            if (File.Exists(versionFile))
            {
                var existingVersion = (await File.ReadAllTextAsync(versionFile).ConfigureAwait(false))?.Trim();
                if (existingVersion == latestRelease.tag_name)
                {
                    game.Status = GameStatus.Installed;
                    game.InstalledVersion = existingVersion;
                    game.LatestVersion = latestRelease.tag_name;
                    game.DownloadProgress = 0;
                    return;
                }
            }

            var availableAssets = GitHubReleaseService.GetDownloadableAssets(latestRelease);

            if (availableAssets.Count == 0)
            {
                await dialogs.ShowErrorAsync($"No download files found for {game.Name}.", "No Assets");
                game.Status = GameStatus.NotInstalled;
                game.DownloadProgress = 0;
                return;
            }

            game.AvailableDownloads = availableAssets;

            if (availableAssets.Count > 1 && game.SelectedDownload == null)
            {
                game.NotifyMultipleDownloadsChanged();
                game.Status = GameStatus.NotInstalled;
                game.DownloadProgress = 0;
                return;
            }

            var asset = game.SelectedDownload ?? availableAssets[0];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                PlatformAssetMatcher.IsWindowsAsset(asset.name))
            {
                if (!WindowsRunnerService.IsWindowsRunnerAvailable(settings))
                {
                    if (!await dialogs.ConfirmDownloadWithoutRunnerAsync())
                    {
                        game.Status = GameStatus.NotInstalled;
                        game.DownloadProgress = 0;
                        return;
                    }
                }
                else if (!await dialogs.ConfirmDownloadWithRunnerAsync())
                {
                    game.Status = GameStatus.NotInstalled;
                    game.DownloadProgress = 0;
                    return;
                }
            }

            var downloadPath = Path.Combine(Path.GetTempPath(), asset.name);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, asset.browser_download_url);
                using var downloadResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                downloadResponse.EnsureSuccessStatusCode();

                var totalBytes = downloadResponse.Content.Headers.ContentLength ?? 0;
                var canReportProgress = totalBytes > 0;

                using (var contentStream = await downloadResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            var downloadPercent = (double)totalRead / totalBytes;
                            game.DownloadProgress = 10 + (downloadPercent * 80);
                        }
                    }
                }

                game.DownloadProgress = 90;
                game.Status = GameStatus.Installing;
                game.DownloadProgress = 95;

                await GameInstallationService.InstallOrUpdateGameAsync(
                    downloadPath,
                    gamePath,
                    asset.name,
                    latestRelease.tag_name,
                    game.GetInstallationOptions()).ConfigureAwait(false);

                game.DownloadProgress = 100;
                await Task.Delay(500).ConfigureAwait(false);

                game.InstalledVersion = latestRelease.tag_name;
                if (string.IsNullOrWhiteSpace(game.LatestVersion) ||
                    LauncherVersionService.IsNewerVersion(latestRelease.tag_name, game.LatestVersion))
                {
                    game.LatestVersion = latestRelease.tag_name;
                }

                game.Status = GameStatus.Installed;
                game.DownloadProgress = 0;
                game.SelectedDownload = null;
                game.AvailableDownloads = null;
            }
            finally
            {
                var wasSingleExecutable = asset.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                          asset.name.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase);

                if (!wasSingleExecutable && File.Exists(downloadPath))
                {
                    try
                    {
                        File.Delete(downloadPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete temp file {downloadPath}: {ex.Message}");
                    }
                }
            }

            if (game.GameManager != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    game.GameManager.OnPropertyChanged(nameof(GameManager.Games));
                });
            }
        }
        catch (HttpRequestException ex)
        {
            if (GameDialogService.IsGitHubRateLimitError(ex))
            {
                await dialogs.ShowRateLimitExceededAsync();
            }
            else
            {
                await dialogs.ShowErrorAsync(
                    $"Network error installing {game.Name}: {ex.Message}\n\nPlease check your internet connection.",
                    "Network Error");
            }

            game.Status = GameStatus.NotInstalled;
            game.DownloadProgress = 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            await dialogs.ShowErrorAsync(
                $"Permission error installing {game.Name}: {ex.Message}\n\nPlease check folder permissions.",
                "Permission Error");
            game.Status = GameStatus.NotInstalled;
            game.DownloadProgress = 0;
        }
        catch (Exception ex)
        {
            await dialogs.ShowErrorAsync(
                InstallationErrorMessages.FormatInstallationError(game.Name, ex.Message),
                "Installation Error");
            game.Status = GameStatus.NotInstalled;
            game.DownloadProgress = 0;
        }
    }
}
