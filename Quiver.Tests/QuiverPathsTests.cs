using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class QuiverPathsTests : IDisposable
{
    private readonly string? _previousOverride;
    private readonly Func<string?>? _previousPackageDirProvider;
    private readonly Func<string, bool>? _previousWritableTester;

    public QuiverPathsTests()
    {
        _previousOverride = QuiverPaths.OverrideUserDataRoot;
        _previousPackageDirProvider = QuiverPaths.VelopackPackageDirectoryProvider;
        _previousWritableTester = QuiverPaths.DirectoryWritableTester;
    }

    public void Dispose()
    {
        QuiverPaths.OverrideUserDataRoot = _previousOverride;
        QuiverPaths.VelopackPackageDirectoryProvider = _previousPackageDirProvider;
        QuiverPaths.DirectoryWritableTester = _previousWritableTester;
    }

    [Fact]
    public void UserDataRoot_respects_override()
    {
        var temp = Path.Combine(Path.GetTempPath(), "QuiverPaths_" + Guid.NewGuid().ToString("N"));
        QuiverPaths.OverrideUserDataRoot = temp;

        QuiverPaths.UserDataRoot.Should().Be(Path.GetFullPath(temp));
        QuiverPaths.AppsJsonPath.Should().Be(Path.Combine(Path.GetFullPath(temp), "apps.json"));
        QuiverPaths.SettingsJsonPath.Should().Be(Path.Combine(Path.GetFullPath(temp), "settings.json"));
        QuiverPaths.DefaultAppsDirectory.Should().Be(Path.Combine(Path.GetFullPath(temp), "Apps"));
        QuiverPaths.CacheDirectory.Should().Be(Path.Combine(Path.GetFullPath(temp), "Cache"));
    }

    [Fact]
    public void LooksLikeLegacyUserDataDirectory_detects_apps_json()
    {
        var temp = Path.Combine(Path.GetTempPath(), "QuiverLegacy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "apps.json"), "{\"apps\":[]}");
            QuiverPaths.LooksLikeLegacyUserDataDirectory(temp).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ResolveMacOsPackageDirectory_returns_parent_of_app_bundle()
    {
        var portableRoot = Path.Combine(Path.GetTempPath(), "QuiverMacPortable_" + Guid.NewGuid().ToString("N"));
        var appBundle = Path.Combine(portableRoot, "Quiver.app");
        var contents = Path.Combine(appBundle, "Contents", "MacOS");
        Directory.CreateDirectory(contents);
        try
        {
            QuiverPaths.ResolveMacOsPackageDirectory(appBundle)
                .Should().Be(Path.GetFullPath(portableRoot));

            QuiverPaths.ResolveMacOsPackageDirectory(
                    rootAppDir: null,
                    appContentDir: contents)
                .Should().Be(Path.GetFullPath(portableRoot));
        }
        finally
        {
            Directory.Delete(portableRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveUnixUserDataRoot_prefers_writable_package_directory()
    {
        var packageDir = Path.Combine(Path.GetTempPath(), "QuiverPkg_" + Guid.NewGuid().ToString("N"));
        var fallback = Path.Combine(Path.GetTempPath(), "QuiverFallback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageDir);
        try
        {
            QuiverPaths.DirectoryWritableTester = dir =>
                string.Equals(Path.GetFullPath(dir), Path.GetFullPath(packageDir), StringComparison.OrdinalIgnoreCase);

            QuiverPaths.ResolveUnixUserDataRoot(packageDir, fallback)
                .Should().Be(Path.GetFullPath(packageDir));
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveUnixUserDataRoot_falls_back_when_package_directory_not_writable()
    {
        var packageDir = Path.Combine(Path.GetTempPath(), "QuiverPkgRo_" + Guid.NewGuid().ToString("N"));
        var fallback = Path.Combine(Path.GetTempPath(), "QuiverFallback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fallback);
        try
        {
            QuiverPaths.DirectoryWritableTester = _ => false;

            QuiverPaths.ResolveUnixUserDataRoot(packageDir, fallback)
                .Should().Be(Path.GetFullPath(fallback));
        }
        finally
        {
            Directory.Delete(fallback, recursive: true);
        }
    }

    [Fact]
    public void IsDirectoryWritable_detects_writable_temp_folder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "QuiverWritable_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            QuiverPaths.DirectoryWritableTester = null;
            QuiverPaths.IsDirectoryWritable(temp).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
