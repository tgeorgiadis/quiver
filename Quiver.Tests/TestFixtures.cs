namespace Quiver.Tests;

internal static class TestFixtures
{
    public static string CommunityAppListsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "community-app-lists");

    public static string CommunityIndexPath =>
        Path.Combine(CommunityAppListsDirectory, "index.json");

    public static string N64RecompListPath =>
        Path.Combine(CommunityAppListsDirectory, "n64-recomp.json");

    public static string ReadCommunityIndexJson() =>
        File.ReadAllText(CommunityIndexPath);

    public static string ReadN64RecompListJson() =>
        File.ReadAllText(N64RecompListPath);
}
