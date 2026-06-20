using Quiver.Models;
using AppCatalogSource = Quiver.AppCatalogSource;

namespace Quiver.Services
{
    public class CatalogDiff
    {
        public List<GameInfo> Added { get; set; } = [];
        public List<GameInfo> Removed { get; set; } = [];
        public List<GameInfo> Changed { get; set; } = [];

        public int AddedCount => Added.Count;
        public int RemovedCount => Removed.Count;
        public int ChangedCount => Changed.Count;

        public bool HasChanges => AddedCount > 0 || RemovedCount > 0 || ChangedCount > 0;
    }

    public class PendingCatalogUpdate
    {
        public AppCatalogSource Source { get; set; } = null!;
        public CatalogDiff Diff { get; set; } = new();
        public List<GameInfo> RemoteApps { get; set; } = [];
        public List<GameInfo> AcceptedApps { get; set; } = [];
    }

    public enum CatalogUpdateChoice
    {
        ApplyAll,
        ApplyNewOnly,
        KeepCurrent,
    }
}
