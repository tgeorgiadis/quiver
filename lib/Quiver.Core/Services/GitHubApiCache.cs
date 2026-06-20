using System.Collections.Concurrent;
using System.Text.Json;
using Quiver.Core.Models;

namespace Quiver.Core.Services
{
    public class GameVersionCache
    {
        public string Version { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; }
        public string ETag { get; set; } = string.Empty;
        public GitHubRelease? CachedRelease { get; set; }
        public DateTime LastUpdateCheck { get; set; }
    }

    public static class GitHubApiCache
    {
        private static readonly ConcurrentDictionary<string, GameVersionCache> _cache = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);
        private static readonly TimeSpan InstalledGameUpdateInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan NotInstalledGameUpdateInterval = TimeSpan.FromHours(24);
        private static string? _cacheFilePath;

        public static void Initialize(string cacheDirectory)
        {
            _cacheFilePath = Path.Combine(cacheDirectory, "version_cache.json");
            LoadFromDisk();
        }

        private static void LoadFromDisk()
        {
            if (string.IsNullOrEmpty(_cacheFilePath) || !File.Exists(_cacheFilePath))
                return;

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var diskCache = JsonSerializer.Deserialize<Dictionary<string, GameVersionCache>>(json);
                if (diskCache != null)
                {
                    foreach (var kvp in diskCache)
                    {
                        _cache.TryAdd(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load version cache: {ex.Message}");
            }
        }

        private static void SaveToDisk()
        {
            if (string.IsNullOrEmpty(_cacheFilePath))
                return;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_cache.ToDictionary(k => k.Key, v => v.Value), options);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save version cache: {ex.Message}");
            }
        }

        public static bool TryGetCachedVersion(string repository, out GameVersionCache? cache)
        {
            if (_cache.TryGetValue(repository, out var foundCache))
            {
                if (DateTime.UtcNow - foundCache.LastChecked < CacheExpiry)
                {
                    cache = foundCache;
                    return true;
                }
            }
            cache = null;
            return false;
        }

        public static bool NeedsUpdateCheck(string repository, bool isInstalledGame = true)
        {
            if (!_cache.TryGetValue(repository, out var cache))
                return true;

            var interval = isInstalledGame ? InstalledGameUpdateInterval : NotInstalledGameUpdateInterval;
            return DateTime.UtcNow - cache.LastUpdateCheck >= interval;
        }

        public static void SetCache(string repository, string version, string etag, GitHubRelease? release = null)
        {
            _cache.AddOrUpdate(repository,
                new GameVersionCache
                {
                    Version = version,
                    LastChecked = DateTime.UtcNow,
                    LastUpdateCheck = DateTime.UtcNow,
                    ETag = etag,
                    CachedRelease = release
                },
                (key, old) => new GameVersionCache
                {
                    Version = version,
                    LastChecked = DateTime.UtcNow,
                    LastUpdateCheck = DateTime.UtcNow,
                    ETag = etag ?? old.ETag,
                    CachedRelease = release ?? old.CachedRelease
                });

            SaveToDisk();
        }

        public static string GetETag(string repository)
        {
            return _cache.TryGetValue(repository, out var cache) ? cache.ETag : "";
        }

        public static void RemoveCache(string repository)
        {
            if (string.IsNullOrWhiteSpace(repository))
                return;

            _cache.TryRemove(repository, out _);
            SaveToDisk();
        }
    }
}
