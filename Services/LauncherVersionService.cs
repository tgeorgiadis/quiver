namespace Quiver.Services;

public static class LauncherVersionService
{
    public static string NormalizeVersionString(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        var normalized = version.Trim().TrimStart('v', 'V');
        var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();

        while (segments.Count < 3)
            segments.Add("0");

        return string.Join(".", segments.Take(4));
    }

    public static bool IsNewerVersion(string candidateVersion, string baselineVersion)
    {
        try
        {
            var candidate = new Version(NormalizeVersionString(candidateVersion));
            var baseline = new Version(NormalizeVersionString(baselineVersion));
            return candidate.CompareTo(baseline) > 0;
        }
        catch
        {
            return !candidateVersion.TrimStart('v', 'V').Equals(
                baselineVersion.TrimStart('v', 'V'),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool AreVersionsEquivalent(string? firstVersion, string? secondVersion)
    {
        if (string.IsNullOrWhiteSpace(firstVersion) || string.IsNullOrWhiteSpace(secondVersion))
            return false;

        try
        {
            return new Version(NormalizeVersionString(firstVersion))
                .Equals(new Version(NormalizeVersionString(secondVersion)));
        }
        catch
        {
            return firstVersion.TrimStart('v', 'V').Trim()
                .Equals(secondVersion.TrimStart('v', 'V').Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string ReadInstalledVersion(string? baseDirectory = null)
    {
        baseDirectory ??= AppDomain.CurrentDomain.BaseDirectory;
        var versionFilePath = Path.Combine(baseDirectory, "version.txt");

        try
        {
            return File.Exists(versionFilePath)
                ? File.ReadAllText(versionFilePath).Trim()
                : "Version information not found";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading version: {ex.Message}");
            return "Version loading failed";
        }
    }
}
