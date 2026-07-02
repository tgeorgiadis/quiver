using FluentAssertions;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class CatalogSyncTests
{
    [Fact]
    public async Task FetchSourceAsync_sets_UpdateAvailable_when_version_differs()
    {
        var sourceId = Guid.NewGuid().ToString();
        var (service, tempDir) = TestFixtures.CreateIsolatedCatalogService();

        try
        {
            var cachePath = Path.Combine(service.CatalogSourcesCacheFolder, $"{sourceId}.json");
            await File.WriteAllTextAsync(cachePath, """
                {
                  "version": "2.0.0",
                  "apps": [
                    { "name": "App", "repository": "owner/app", "folderName": "AppFolder" }
                  ]
                }
                """);

            var source = new AppCatalogSource
            {
                Id = sourceId,
                Name = "Test",
                Location = "unused",
                Enabled = true,
                AcknowledgedListVersion = "1.0.0",
            };

            using var httpClient = new HttpClient();
            var fetched = await service.FetchSourceAsync(httpClient, source);

            fetched.Should().BeTrue();
            source.CachedListVersion.Should().Be("2.0.0");
            source.UpdateAvailable.Should().BeTrue();
        }
        finally
        {
            TestFixtures.CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task RegisterNewSourceAsync_acknowledges_initial_version()
    {
        var sourceId = Guid.NewGuid().ToString();
        var (service, tempDir) = TestFixtures.CreateIsolatedCatalogService();

        try
        {
            var source = new AppCatalogSource
            {
                Id = sourceId,
                Name = "Test",
                Location = "unused",
                Enabled = true,
            };

            var json = """
                {
                  "version": "1.5.0",
                  "apps": [
                    { "name": "App", "repository": "owner/app", "folderName": "AppFolder" }
                  ]
                }
                """;

            await service.RegisterNewSourceAsync(new HttpClient(), source, json);

            source.CachedListVersion.Should().Be("1.5.0");
            source.AcknowledgedListVersion.Should().Be("1.5.0");
            source.UpdateAvailable.Should().BeFalse();
        }
        finally
        {
            TestFixtures.CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task LoadLocalCatalogAsync_returns_only_local_apps()
    {
        var (service, tempDir) = TestFixtures.CreateIsolatedCatalogService();

        try
        {
            var settings = new AppSettings();
            settings.EnsureInitialized();

            await service.SaveLocalAppsAsync(
            [
                new GameInfo { Repository = "owner/local", Name = "Local", FolderName = "LocalFolder" },
            ]);

            var apps = await service.LoadLocalCatalogAsync(settings);

            apps.Should().ContainSingle();
            apps[0].Repository.Should().Be("owner/local");
        }
        finally
        {
            TestFixtures.CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task RefreshUpdateAvailableAsync_auto_acknowledges_when_no_actionable_rows()
    {
        var sourceId = Guid.NewGuid().ToString();
        var (service, tempDir) = TestFixtures.CreateIsolatedCatalogService();

        try
        {
            var cachePath = Path.Combine(service.CatalogSourcesCacheFolder, $"{sourceId}.json");
            await File.WriteAllTextAsync(cachePath, """
                {
                  "version": "1.0.0",
                  "apps": [
                    { "name": "App", "repository": "owner/app", "folderName": "AppFolder" }
                  ]
                }
                """);

            await service.SaveLocalAppsAsync(
            [
                new GameInfo { Repository = "owner/app", Name = "App", FolderName = "AppFolder" },
            ]);

            var source = new AppCatalogSource
            {
                Id = sourceId,
                Name = "Test",
                CachedListVersion = "1.0.0",
                AcknowledgedListVersion = "0",
                UpdateAvailable = true,
            };

            await service.RefreshUpdateAvailableAsync(source);

            source.AcknowledgedListVersion.Should().Be("1.0.0");
            source.UpdateAvailable.Should().BeFalse();
            source.PendingReviewCount.Should().Be(0);
        }
        finally
        {
            TestFixtures.CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task RefreshUpdateAvailableAsync_does_not_auto_ack_when_actionable_rows_remain()
    {
        var sourceId = Guid.NewGuid().ToString();
        var (service, tempDir) = TestFixtures.CreateIsolatedCatalogService();

        try
        {
            var cachePath = Path.Combine(service.CatalogSourcesCacheFolder, $"{sourceId}.json");
            await File.WriteAllTextAsync(cachePath, """
                {
                  "version": "1.0.0",
                  "apps": [
                    { "name": "App", "repository": "owner/app", "folderName": "AppFolder" }
                  ]
                }
                """);

            await service.SaveLocalAppsAsync([]);

            var source = new AppCatalogSource
            {
                Id = sourceId,
                Name = "Test",
                CachedListVersion = "1.0.0",
                AcknowledgedListVersion = "0",
            };

            await service.RefreshUpdateAvailableAsync(source);

            source.AcknowledgedListVersion.Should().Be("0");
            source.UpdateAvailable.Should().BeTrue();
            source.PendingReviewCount.Should().Be(1);
        }
        finally
        {
            TestFixtures.CleanupDirectory(tempDir);
        }
    }
}
