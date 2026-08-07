namespace Quiver.Services;

public static class SettingsStoreProvider
{
    private static ISettingsStore? _default;

    public static ISettingsStore Default
    {
        get => _default ??= new FileSettingsStore(QuiverPaths.SettingsJsonPath);
        set => _default = value;
    }
}
