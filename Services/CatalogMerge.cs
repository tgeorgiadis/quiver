using Quiver.Models;

namespace Quiver.Services;

internal static class CatalogMerge
{
    internal static List<GameInfo>? ApplyChoice(
        CatalogUpdateChoice choice,
        List<GameInfo> acceptedApps,
        List<GameInfo> remoteApps,
        Func<GameInfo, GameInfo> clone)
    {
        return choice switch
        {
            CatalogUpdateChoice.ApplyAll => remoteApps.Select(clone).ToList(),
            CatalogUpdateChoice.ApplyNewOnly => MergeAcceptedWithRemote(acceptedApps, remoteApps, clone),
            CatalogUpdateChoice.KeepCurrent => null,
            _ => null,
        };
    }

    internal static List<GameInfo> MergeAcceptedWithRemote(
        List<GameInfo> acceptedApps,
        List<GameInfo> remoteApps,
        Func<GameInfo, GameInfo> clone)
    {
        var merged = acceptedApps
            .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
            .ToDictionary(a => a.Repository!, a => clone(a), StringComparer.OrdinalIgnoreCase);

        foreach (var remote in remoteApps)
        {
            if (string.IsNullOrWhiteSpace(remote.Repository))
                continue;

            merged[remote.Repository] = clone(remote);
        }

        return merged.Values.ToList();
    }
}
