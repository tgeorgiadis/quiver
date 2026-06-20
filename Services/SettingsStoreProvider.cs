namespace Quiver.Services;

public static class SettingsStoreProvider
{
    public static ISettingsStore Default { get; set; } = new FileSettingsStore();
}
