using Quiver.Core.Services;

namespace Quiver.Services
{
    public sealed class QuiverProfile : LauncherProfile
    {
        public static QuiverProfile Instance { get; } = new();

        public override string DisplayName => "Quiver";
        public override string ApplicationId => "Quiver";
        public override string Repository => "tgeorgiadis/quiver";
        public override string ExecutableName => "Quiver";
        public override string DefaultInstallFolderName => "Apps";
        public override string UserAgent => "Quiver/1.0";
        public override string CliUserAgent => "Quiver-CLI";
        public override string UpdaterUserAgent => "Quiver-Updater";
        public override string SteamTag => "Quiver";
    }
}
