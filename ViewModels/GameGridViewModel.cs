using System.Collections.ObjectModel;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.ViewModels;

public class GameGridViewModel
{
    public IReadOnlyList<GameInfo> SortGames(IEnumerable<GameInfo> games, string sortMode, string gamesFolder)
    {
        var list = games.Where(g => g != null).Cast<GameInfo>().ToList();

        return sortMode switch
        {
            "NameDesc" => list.OrderByDescending(g => g.Name ?? string.Empty).ToList(),
            "NameIgnoreArticles" => list
                .OrderBy(g => NameSortHelper.GetAlphabeticalSortKey(g.Name), StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "Installed" => list
                .OrderByDescending(g => g.IsInstalled)
                .ThenBy(g => g.Name ?? string.Empty)
                .ToList(),
            "NotInstalled" => list
                .OrderBy(g => g.IsInstalled)
                .ThenBy(g => g.Name ?? string.Empty)
                .ToList(),
            "LastPlayed" => list
                .OrderByDescending(g => GetLastPlayedTime(g, gamesFolder))
                .ThenBy(g => g.Name ?? string.Empty)
                .ToList(),
            _ => list.OrderBy(g => g.Name ?? string.Empty).ToList(),
        };
    }

    public void ApplySort(ObservableCollection<GameInfo> games, string sortMode, string gamesFolder)
    {
        if (games.Count == 0)
            return;

        var sorted = SortGames(games, sortMode, gamesFolder);
        games.Clear();
        foreach (var game in sorted)
            games.Add(game);
    }

    public static DateTime GetLastPlayedTime(GameInfo game, string gamesFolder)
    {
        if (string.IsNullOrEmpty(game.FolderName))
            return DateTime.MinValue;

        try
        {
            var gamePath = game.GetInstallPath(gamesFolder);
            var lastPlayedPath = Path.Combine(gamePath, "LastPlayed.txt");

            if (File.Exists(lastPlayedPath))
            {
                var content = File.ReadAllText(lastPlayedPath).Trim();
                if (DateTime.TryParseExact(content, "yyyy-MM-dd HH:mm:ss", null,
                        System.Globalization.DateTimeStyles.None, out var lastPlayed))
                    return lastPlayed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read LastPlayed for {game.Name}: {ex.Message}");
        }

        return DateTime.MinValue;
    }
}
