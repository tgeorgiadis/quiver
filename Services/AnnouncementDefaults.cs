namespace Quiver.Services;

/// <summary>
/// Remote announcement banner. Edit announcement.json on the Quiver repo main branch
/// and push to publish without shipping a Quiver release. Change <c>id</c> to re-show
/// the banner for users who dismissed a previous notice.
/// </summary>
public static class AnnouncementDefaults
{
    public const string RemoteUrl =
        "https://raw.githubusercontent.com/tgeorgiadis/quiver/main/announcement.json";
}
