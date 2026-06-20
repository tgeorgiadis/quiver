using System;
using System.IO;
using System.Text.Json;
using Quiver.Core.Models;
using Quiver.Services;

namespace Quiver
{
    public enum AppListScope
    {
        AllApps,
        InstalledOnly,
    }

    public enum TagFilterMatchMode
    {
        Any,
        All,
    }

    public class TagDisplayFilter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public TagFilterMatchMode MatchMode { get; set; } = TagFilterMatchMode.Any;
    }

    public class AppCatalogSource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public DateTime? LastFetchedUtc { get; set; }
        public string? LastError { get; set; }
        public string? CommunityListId { get; set; }
        public string? AcceptedContentHash { get; set; }
        public bool UpdateAvailable { get; set; }
    }

    public class AppSettings
    {
        public bool FirstStartup { get; set; } = true;
        public bool IconFill { get; set; } = false;
        public bool UseGridView { get; set; } = true;
        public bool GridCompactCards { get; set; } = true;
        public float IconOpacity { get; set; } = 1.0f;
        public int IconSize { get; set; } = 124;
        public int IconMargin { get; set; } = 8;
        public int SlotTextMargin { get; set; } = 112;
        public int SlotSize { get; set; } = 152;
        public int ActionButtonSize { get; set; } = 36;
        public bool WindowBorderRounding { get; set; } = true;
        public bool ShowOSTopBar { get; set; } = false;
        public string PrimaryColor { get; set; } = "#18181b";
        public string SecondaryColor { get; set; } = "#404040";
        public TargetOS Platform { get; set; } = TargetOS.Auto;
        public List<string> HiddenApps { get; set; } = new List<string>();
        public List<string> ManuallyHiddenApps { get; set; } = new List<string>();
        public string AppsPath { get; set; } = string.Empty;
        public string GitHubApiToken { get; set; } = string.Empty;
        public string SortBy { get; set; } = "LastPlayed";
        public bool StartFullscreen { get; set; } = false;
        public bool CloseAfterLaunch {  get; set; } = false;
        public string BackgroundImagePath { get; set; } = string.Empty;
        public string LauncherMusicPath { get; set; } = string.Empty;
        public float MusicVolume { get; set; } = 0.2f;
        public float BackgroundOpacity { get; set; } = 0.15f;
        public bool EnableGamepadInput { get; set; } = true;
        public string LinuxWindowsLaunchCommand { get; set; } = string.Empty;
        public List<AppCatalogSource> AppCatalogSources { get; set; } = new List<AppCatalogSource>();
        public string CommunityCatalogIndexUrl { get; set; } = string.Empty;
        public List<TagDisplayFilter> TagDisplayFilters { get; set; } = new List<TagDisplayFilter>();
        public string? ActiveTagDisplayFilterId { get; set; }
        public AppListScope ListScope { get; set; } = AppListScope.AllApps;
        public Dictionary<string, List<string>> UserAppTags { get; set; } = new Dictionary<string, List<string>>();

        public void EnsureInitialized()
        {
            AppCatalogSources ??= new List<AppCatalogSource>();
            HiddenApps ??= new List<string>();
            ManuallyHiddenApps ??= new List<string>();
            TagDisplayFilters ??= new List<TagDisplayFilter>();
            UserAppTags ??= new Dictionary<string, List<string>>();

            if (HiddenApps.Count > 0)
            {
                ListScope = AppListScope.InstalledOnly;
                HiddenApps.Clear();
            }
        }

        public static AppSettings Load() => SettingsStoreProvider.Default.Load();

        public static void Save(AppSettings settings) => SettingsStoreProvider.Default.Save(settings);
    }
}

