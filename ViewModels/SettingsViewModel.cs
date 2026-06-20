using System.Collections.ObjectModel;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.ViewModels;

public class SettingsViewModel
{
    private readonly ISettingsStore _settingsStore;

    public SettingsViewModel(ISettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore ?? SettingsStoreProvider.Default;
    }

    public AppSettings Current => _settingsStore.Current;

    public AppSettings Load() => _settingsStore.Load();

    public void Save(AppSettings settings) => _settingsStore.Save(settings);

    public void SaveCurrent() => _settingsStore.Save(_settingsStore.Current);
}
