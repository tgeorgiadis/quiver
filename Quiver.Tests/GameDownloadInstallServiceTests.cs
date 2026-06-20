using System.IO.Compression;
using System.Net;
using FluentAssertions;
using Quiver.Core.Models;
using Quiver.Core.Services;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class GameDownloadInstallServiceTests
{
    public GameDownloadInstallServiceTests()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "QuiverTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(cacheDir);
        GitHubApiCache.Initialize(cacheDir);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_installs_single_zip_asset()
    {
        var gamesFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gamesFolder);

        try
        {
            const string assetName = "payload.zip";
            var release = new GitHubRelease
            {
                tag_name = "v1.2.3",
                assets =
                [
                    new GitHubAsset
                    {
                        name = assetName,
                        browser_download_url = "https://example.com/download/asset",
                    },
                ],
            };

            var game = new GameInfo
            {
                Name = "Test Game",
                Repository = "owner/test-game",
                FolderName = "TestGameFolder",
            };

            var dialogs = new RecordingDialogs();
            using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateMinimalZipWithExe()),
            }));

            await GameDownloadInstallService.DownloadAndInstallAsync(
                game,
                client,
                gamesFolder,
                release,
                new AppSettings(),
                GameStatus.NotInstalled,
                dialogs);

            dialogs.LastError.Should().BeNull("install failed with: {0}", dialogs.LastError);
            game.Status.Should().Be(GameStatus.Installed);
            game.InstalledVersion.Should().Be("v1.2.3");

            var gamePath = game.GetInstallPath(gamesFolder);
            File.Exists(Path.Combine(gamePath, "version.txt")).Should().BeTrue();
            File.ReadAllText(Path.Combine(gamePath, "version.txt")).Trim().Should().Be("v1.2.3");
        }
        finally
        {
            if (Directory.Exists(gamesFolder))
                Directory.Delete(gamesFolder, true);
        }
    }

    [Fact]
    public async Task DownloadAndInstallAsync_resets_status_when_release_has_no_assets()
    {
        var gamesFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gamesFolder);

        try
        {
            var game = new GameInfo
            {
                Name = "Empty Assets",
                Repository = "owner/empty",
                FolderName = "EmptyAssets",
            };

            var release = new GitHubRelease
            {
                tag_name = "v1.0.0",
                assets = [],
            };

            using var client = new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called when there are no assets")));

            await GameDownloadInstallService.DownloadAndInstallAsync(
                game,
                client,
                gamesFolder,
                release,
                new AppSettings(),
                GameStatus.NotInstalled,
                HeadlessGameDownloadDialogs.Instance);

            game.Status.Should().Be(GameStatus.NotInstalled);
            game.DownloadProgress.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(gamesFolder))
                Directory.Delete(gamesFolder, true);
        }
    }

    [Fact]
    public async Task DownloadAndInstallAsync_waits_for_asset_selection_when_multiple_downloads()
    {
        var gamesFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(gamesFolder);

        try
        {
            var game = new GameInfo
            {
                Name = "Multi Asset",
                Repository = "owner/multi",
                FolderName = "MultiAsset",
            };

            var release = new GitHubRelease
            {
                tag_name = "v2.0.0",
                assets =
                [
                    new GitHubAsset { name = "linux.zip", browser_download_url = "https://example.com/linux.zip" },
                    new GitHubAsset { name = "win.zip", browser_download_url = "https://example.com/win.zip" },
                ],
            };

            using var client = new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called before asset selection")));

            await GameDownloadInstallService.DownloadAndInstallAsync(
                game,
                client,
                gamesFolder,
                release,
                new AppSettings(),
                GameStatus.NotInstalled,
                HeadlessGameDownloadDialogs.Instance);

            game.Status.Should().Be(GameStatus.NotInstalled);
            game.AvailableDownloads.Should().HaveCount(2);
            game.SelectedDownload.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(gamesFolder))
                Directory.Delete(gamesFolder, true);
        }
    }

    private static byte[] CreateMinimalZipWithExe()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("game.exe");
            using var writer = entry.Open();
            writer.Write(new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
        }

        return ms.ToArray();
    }

    private sealed class RecordingDialogs : IGameDownloadDialogs
    {
        public string? LastError { get; private set; }

        public Task<bool> ConfirmDownloadWithoutRunnerAsync() => Task.FromResult(true);

        public Task<bool> ConfirmDownloadWithRunnerAsync() => Task.FromResult(true);

        public Task ShowRateLimitExceededAsync() => Task.CompletedTask;

        public Task ShowErrorAsync(string message, string title)
        {
            LastError = $"{title}: {message}";
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
