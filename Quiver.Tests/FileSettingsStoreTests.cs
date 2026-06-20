using FluentAssertions;
using Quiver;
using Quiver.Services;

namespace Quiver.Tests;

public class FileSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsPath;

    public FileSettingsStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Quiver.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _settingsPath = Path.Combine(_tempDirectory, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var store = new FileSettingsStore(_settingsPath);

        store.Current.FirstStartup.Should().BeTrue();
        store.Current.AppCatalogSources.Should().NotBeNull();
        store.Current.HiddenApps.Should().NotBeNull();
    }

    [Fact]
    public void Save_and_Load_round_trip_settings()
    {
        var store = new FileSettingsStore(_settingsPath);
        var settings = store.Load();
        settings.GitHubApiToken = "test-token";
        settings.SortBy = "Name";
        settings.ListScope = AppListScope.InstalledOnly;
        settings.ManuallyHiddenApps.Add("folder:TestGame");

        store.Save(settings);

        var reloaded = new FileSettingsStore(_settingsPath).Load();
        reloaded.GitHubApiToken.Should().Be("test-token");
        reloaded.SortBy.Should().Be("Name");
        reloaded.ListScope.Should().Be(AppListScope.InstalledOnly);
        reloaded.ManuallyHiddenApps.Should().Contain("folder:TestGame");
        reloaded.HiddenApps.Should().BeEmpty();
    }

    [Fact]
    public void Current_reflects_last_saved_settings()
    {
        var store = new FileSettingsStore(_settingsPath);
        var settings = store.Load();
        settings.CommunityCatalogIndexUrl = "community-app-lists/index.json";

        store.Save(settings);

        store.Current.CommunityCatalogIndexUrl.Should().Be("community-app-lists/index.json");
    }

    [Fact]
    public void AppSettings_static_methods_use_default_store()
    {
        var original = SettingsStoreProvider.Default;
        try
        {
            SettingsStoreProvider.Default = new FileSettingsStore(_settingsPath);
            var settings = AppSettings.Load();
            settings.AppsPath = "D:\\Apps";
            AppSettings.Save(settings);

            AppSettings.Load().AppsPath.Should().Be("D:\\Apps");
        }
        finally
        {
            SettingsStoreProvider.Default = original;
        }
    }
}
