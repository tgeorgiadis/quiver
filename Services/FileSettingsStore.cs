using System.Text.Json;

namespace Quiver.Services;

public class FileSettingsStore : ISettingsStore
{
    private readonly string _settingsPath;
    private AppSettings _current;

    public FileSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        _current = ReadFromDisk();
    }

    public AppSettings Current => _current;

    public AppSettings Load()
    {
        _current = ReadFromDisk();
        return _current;
    }

    public void Save(AppSettings settings)
    {
        settings.EnsureInitialized();
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
        _current = settings;
    }

    private AppSettings ReadFromDisk()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return CreateDefault();

            var json = File.ReadAllText(_settingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return CreateDefault();

            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefault();
            settings.EnsureInitialized();
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            return CreateDefault();
        }
    }

    private static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        settings.EnsureInitialized();
        return settings;
    }
}
