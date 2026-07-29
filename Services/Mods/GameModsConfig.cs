namespace Quiver.Services.Mods;

/// <summary>Helpers for normalizing and comparing an app's mods catalog config.</summary>
public static class GameModsConfig
{
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim().Replace('\\', '/').Trim('/');
        if (trimmed.Length == 0)
            return string.Empty;

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(s => s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            return string.Empty;

        return string.Join('/', segments);
    }

    public static string NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return ModProviderIds.Thunderstore;

        return provider.Trim().ToLowerInvariant();
    }

    public static List<GameModSource> NormalizeSources(IEnumerable<GameModSource>? sources)
    {
        if (sources == null)
            return [];

        var result = new List<GameModSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (source == null)
                continue;

            var url = source.SourceUrl?.Trim() ?? string.Empty;
            if (url.Length == 0)
                continue;

            var provider = NormalizeProvider(source.Provider);
            var key = $"{provider}|{url}";
            if (!seen.Add(key))
                continue;

            result.Add(new GameModSource
            {
                Provider = provider,
                SourceUrl = url,
            });
        }

        return result;
    }

    public static bool AreEquivalent(
        string? pathA,
        IEnumerable<GameModSource>? sourcesA,
        string? pathB,
        IEnumerable<GameModSource>? sourcesB)
    {
        if (!string.Equals(NormalizePath(pathA), NormalizePath(pathB), StringComparison.OrdinalIgnoreCase))
            return false;

        var a = NormalizeSources(sourcesA)
            .Select(s => $"{s.Provider}|{s.SourceUrl}")
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var b = NormalizeSources(sourcesB)
            .Select(s => $"{s.Provider}|{s.SourceUrl}")
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
    }

    public static string FormatForDisplay(string? path, IEnumerable<GameModSource>? sources)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedSources = NormalizeSources(sources);
        if (normalizedPath.Length == 0 && normalizedSources.Count == 0)
            return string.Empty;

        var sourceText = string.Join("; ", normalizedSources.Select(s => $"{s.Provider}: {s.SourceUrl}"));
        if (normalizedPath.Length == 0)
            return sourceText;
        if (sourceText.Length == 0)
            return $"path={normalizedPath}";

        return $"path={normalizedPath}; {sourceText}";
    }

    public static bool HasUsableConfig(string? path, IEnumerable<GameModSource>? sources, ModProviderRegistry? registry = null)
    {
        if (NormalizePath(path).Length == 0)
            return false;

        var normalized = NormalizeSources(sources);
        if (normalized.Count == 0)
            return false;

        if (registry == null)
            return true;

        return normalized.Any(s => registry.TryGet(s.Provider, out _));
    }
}
