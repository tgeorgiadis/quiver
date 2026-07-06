namespace Quiver.Services;

/// <summary>
/// Top-level install-folder entries that self-update must never overwrite.
/// </summary>
public static class UpdaterUserDataPreservation
{
    public static readonly string[] PreservedFileNames =
    [
        "apps.json",
        "settings.json",
        "games.json",
    ];

    public static readonly string[] PreservedDirectoryNames =
    [
        "Cache",
    ];

    public static bool IsPreservedTopLevelEntry(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        foreach (var fileName in PreservedFileNames)
        {
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var directoryName in PreservedDirectoryNames)
        {
            if (string.Equals(name, directoryName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static IEnumerable<string> GetUpdateEntriesToApply(IEnumerable<string> updateEntries) =>
        updateEntries.Where(entry => !IsPreservedTopLevelEntry(entry));

    public static string BuildWindowsPreservedEntryCheckSubroutine()
    {
        var lines = new List<string>
        {
            ":IsPreservedUserDataEntry",
        };

        foreach (var fileName in PreservedFileNames)
            lines.Add($@"if /I ""%~1""==""{fileName}"" exit /b 0");

        foreach (var directoryName in PreservedDirectoryNames)
            lines.Add($@"if /I ""%~1""==""{directoryName}"" exit /b 0");

        lines.Add("exit /b 1");
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildUnixPreserveCasePattern()
    {
        var patterns = PreservedFileNames
            .Concat(PreservedDirectoryNames)
            .Select(name => name.Replace("\"", "\\\""));
        return string.Join("|", patterns);
    }
}
