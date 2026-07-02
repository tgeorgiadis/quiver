using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Quiver.Core.Models;
using Quiver.Core.Services;
using Quiver.Models;
using Quiver.Services;
using Quiver.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Avalonia.Platform;

#if WINDOWS
using NAudio.Wave;
#endif

namespace Quiver
{
    public enum MainViewMode
    {
        Library,
        AppCatalog,
    }

    public enum AppCatalogSubView
    {
        Sources,
        Review,
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly GameManager _gameManager;
        private readonly CatalogViewModel _catalogViewModel = new();
        private readonly GameGridViewModel _gameGridViewModel = new();
        private readonly SettingsViewModel _settingsViewModel = new();
        public ObservableCollection<GameInfo> Games => _gameManager?.Games ?? new ObservableCollection<GameInfo>();
        public ObservableCollection<TagDisplayFilterListItem> TagDisplayFilters { get; } = new();
        public ObservableCollection<CatalogSourceListItem> CatalogSources { get; } = new();
        public ObservableCollection<CatalogSyncRowItem> CatalogSyncRows { get; } = new();

        public int CatalogReviewBadgeCount =>
            CatalogSources.Where(s => s.Enabled).Sum(s => s.PendingReviewCount);

        public bool CatalogReviewBadgeVisible => CatalogReviewBadgeCount > 0;
        private readonly LauncherUpdateService _launcherUpdateService = new();
        private bool _isCheckingUpdates;
        private int _pendingUpdatesCount;
        private DateTime? _lastUpdateCheckTime;

        public bool IsCheckingUpdates
        {
            get => _isCheckingUpdates;
            private set
            {
                if (_isCheckingUpdates == value)
                    return;

                _isCheckingUpdates = value;
                OnPropertyChanged(nameof(IsCheckingUpdates));
                NotifyUpdateCheckUiProperties();
            }
        }

        public int PendingUpdatesCount
        {
            get => _pendingUpdatesCount;
            private set
            {
                if (_pendingUpdatesCount == value)
                    return;

                _pendingUpdatesCount = value;
                OnPropertyChanged(nameof(PendingUpdatesCount));
                NotifyUpdateCheckUiProperties();
            }
        }

        public bool UpdatesBadgeVisible => PendingUpdatesCount > 0 && !IsCheckingUpdates;

        public string UpdatesBadgeText =>
            PendingUpdatesCount > 9 ? "9+" : PendingUpdatesCount.ToString();

        public double CheckForUpdatesIconOpacity => IsCheckingUpdates ? 0.55 : 1.0;

        public DateTime? LastUpdateCheckTime
        {
            get => _lastUpdateCheckTime;
            private set
            {
                if (_lastUpdateCheckTime == value)
                    return;

                _lastUpdateCheckTime = value;
                OnPropertyChanged(nameof(LastUpdateCheckTime));
                NotifyUpdateCheckUiProperties();
            }
        }

        public string CheckForUpdatesToolTip
        {
            get
            {
                if (IsCheckingUpdates)
                    return "Checking launcher and games…";

                var lastChecked = GetLastCheckedText();
                if (PendingUpdatesCount > 0)
                {
                    var updateLabel = PendingUpdatesCount == 1
                        ? "1 update available"
                        : $"{PendingUpdatesCount} updates available";
                    return string.IsNullOrEmpty(lastChecked)
                        ? updateLabel
                        : $"{updateLabel} · {lastChecked}";
                }

                if (!string.IsNullOrEmpty(lastChecked))
                    return $"Up to date · {lastChecked}";

                return "Check for updates";
            }
        }

        private void NotifyUpdateCheckUiProperties()
        {
            OnPropertyChanged(nameof(UpdatesBadgeVisible));
            OnPropertyChanged(nameof(UpdatesBadgeText));
            OnPropertyChanged(nameof(CheckForUpdatesIconOpacity));
            OnPropertyChanged(nameof(CheckForUpdatesToolTip));
        }

        private void RefreshUpdateCheckStatus(DateTime? manualCheckTime = null)
        {
            if (manualCheckTime.HasValue)
                LastUpdateCheckTime = manualCheckTime.Value;
            else
            {
                var info = _launcherUpdateService.LoadUpdateCheckInfo();
                if (info.LastCheckTime != default)
                    LastUpdateCheckTime = info.LastCheckTime.ToLocalTime();
            }

            var launcherPending = _launcherUpdateService.IsLauncherUpdatePending();
            var gamePending = Games.Count(g => g.Status == GameStatus.UpdateAvailable);
            PendingUpdatesCount = LauncherUpdateService.ComputePendingUpdatesCount(launcherPending, gamePending);
        }

        private readonly CatalogSyncViewModel _catalogSyncViewModel = new();
        private AppCatalogSource? _activeCatalogSyncSource;
        private MainViewMode _mainViewMode = MainViewMode.Library;
        private AppCatalogSubView _appCatalogSubView = AppCatalogSubView.Sources;
        private bool _suppressCatalogSourceUiEvents;
        private bool _suppressSettingsUiEvents;
        private bool _isRefreshingCatalogSources;
        private string? _editingDisplayFilterId;
        private string? _tagFilterDragId;
        private double _tagFilterDragStartY;
        private double _tagFilterDragListTop;
        private double _tagFilterDragRowStride;
        private bool _tagFilterDragActive;
        private bool _tagFilterSuppressRowClick;
        private List<string>? _tagFilterOrderAtDragStart;
        private Button? _tagFilterDragRowButton;
        private IPointer? _tagFilterDragPointer;
        public AppSettings _settings = new();
        public App _app = null!;
        public AppSettings Settings => _settings;
        private bool isSettingsPanelOpen = false;
        public string IconFillStretch = "Uniform";
        private string _backgroundImagePath = string.Empty;
        private string _backgroundImageUri = string.Empty;
        public string BackgroundImagePath
        {
            get => _backgroundImagePath;
            set
            {
                if (_backgroundImagePath != value)
                {
                    _backgroundImagePath = value;
                    if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    {
                        _backgroundImageUri = new Uri(value).AbsoluteUri;
                    }
                    else
                    {
                        _backgroundImageUri = string.Empty;
                    }
                    OnPropertyChanged(nameof(BackgroundImagePath));
                    OnPropertyChanged(nameof(BackgroundImageUri));
                }
            }
        }
        public string BackgroundImageUri => _backgroundImageUri ?? string.Empty;
        public float BackgroundOpacity
        {
            get => _settings.BackgroundOpacity;
            set
            {
                if (Math.Abs(_settings.BackgroundOpacity - value) > 0.001f)
                {
                    _settings.BackgroundOpacity = value;
                    OnPropertyChanged(nameof(BackgroundOpacity));
                }
            }
        }
        private System.Threading.CancellationTokenSource? _fadeTaskCts;
        private const int FADE_DURATION_MS = 500;
        #if WINDOWS
        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioFileReader;
        #endif
        private Process? _musicProcess;
        private bool _musicPausedByDeactivation = false;
        private bool _launchedGameOwnsInput;
        private bool _trackingLaunchedGameProcess;
        private string _launcherMusicPath = string.Empty;
        public string LauncherMusicPath
        {
            get => _launcherMusicPath;
            set
            {
                if (_launcherMusicPath != value)
                {
                    _launcherMusicPath = value;
                    OnPropertyChanged(nameof(LauncherMusicPath));
                }
            }
        }
        private float _musicVolume = 0.2f;
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                if (Math.Abs(_musicVolume - value) > 0.001f)
                {
                    _musicVolume = value;
                    OnPropertyChanged(nameof(MusicVolume));

                #if WINDOWS
                if (_audioFileReader != null)
                {
                    _audioFileReader.Volume = value;
                }
                #else
                if (!string.IsNullOrEmpty(LauncherMusicPath) && File.Exists(LauncherMusicPath))
                {
                    PlayLauncherMusic(LauncherMusicPath);
                }
                #endif
                }
            }
        }
        public bool IsFullscreen
        {
            get => _settings.StartFullscreen;
            set
            {
                if (_settings.StartFullscreen != value)
                {
                    _settings.StartFullscreen = value;
                    OnPropertyChanged(nameof(IsFullscreen));
                }
            }
        }
        public bool CloseAfterLaunch
        {
            get => _settings.CloseAfterLaunch;
            set
            {
                if (_settings.CloseAfterLaunch != value)
                {
                    _settings.CloseAfterLaunch = value;
                    OnPropertyChanged(nameof(CloseAfterLaunch));
                }
            }
        }
        public IBrush WindowBackground
        {
            get
            {
                if (_settings?.WindowBorderRounding ?? false)
                {
                    if (IsFullscreen)
                    {
                        return this.Resources["ThemeDarker"] as IBrush ?? Brushes.Transparent;
                    }
                    if (_settings.ShowOSTopBar)
                    {
                        return this.Resources["ThemeDarker"] as IBrush ?? Brushes.Transparent;
                    }
                    return Brushes.Transparent;
                }
                return this.Resources["ThemeDarker"] as IBrush ?? Brushes.Transparent;
            }
        }
        public bool ExtendClientAreaEnabled => !_settings.ShowOSTopBar;
        public ExtendClientAreaChromeHints ChromeHints
        {
            get => _settings.ShowOSTopBar ? ExtendClientAreaChromeHints.PreferSystemChrome : ExtendClientAreaChromeHints.NoChrome;
        }
        private string _currentSortBy = "Name";
        private string _currentVersionString = string.Empty;
        public string currentVersionString
        {
            get => _currentVersionString;
            set
            {
                if (_currentVersionString != value)
                {
                    _currentVersionString = value;
                    OnPropertyChanged(nameof(currentVersionString));
                }
            }
        }
        private string _platformstring = string.Empty;
        public string PlatformString
        {             
            get => _platformstring;
            set
            {
                if (_platformstring != value)
                {
                    _platformstring = value;
                    OnPropertyChanged(nameof(PlatformString));
                    OnPropertyChanged(nameof(IsLinuxPlatform));
                }
            }
        }
        public bool IsLinuxPlatform => PlatformString.Contains("Linux", StringComparison.OrdinalIgnoreCase);

        private bool _isContinueVisible;
        public bool IsContinueVisible
        {
            get => _isContinueVisible;
            set
            {
                if (_isContinueVisible != value)
                {
                    _isContinueVisible = value;
                    OnPropertyChanged(nameof(IsContinueVisible));
                }
            }
        }

        private InputService? _inputService;
        private bool _isProcessingInput = false;
        private bool _hasInitializedFocus = false;

        private bool _isChangelogOpen = false;
        private GameInfo? _currentChangelogGame;

        private GameInfo? _continueGameInfo;
        public GameInfo? ContinueGameInfo
        {
            get => _continueGameInfo;
            set
            {
                if (_continueGameInfo != value)
                {
                    _continueGameInfo = value;
                    OnPropertyChanged(nameof(ContinueGameInfo));
                }
            }
        }
        private bool _isEntryFormOpen = false;
        private bool _entryFormShowValidation;
        private GameInfo? _editingTagsGame;
        public string InfoTextLength = "*";
        private SolidColorBrush _themeColorBrush = new(Colors.Transparent);
        public SolidColorBrush ThemeColorBrush
        {
            get => _themeColorBrush;
            set
            {
                if (_themeColorBrush != value)
                {
                    _themeColorBrush = value;
                    OnPropertyChanged(nameof(ThemeColorBrush));
                    UpdateThemeColors();
                }
            }
        }
        private SolidColorBrush _secondaryColorBrush = new(Colors.Transparent);
        public SolidColorBrush SecondaryColorBrush
        {
            get => _secondaryColorBrush;
            set
            {
                if (_secondaryColorBrush != value)
                {
                    _secondaryColorBrush = value;
                    OnPropertyChanged(nameof(SecondaryColorBrush));
                    UpdateThemeColors();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                _settings = _settingsViewModel.Load();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to load settings: {ex.Message}", "Settings Error");
                _settings = new AppSettings();
            }

            _gameManager = new GameManager();

            // Initialize theme
            ThemeColorBrush = new SolidColorBrush(Color.Parse(_settings?.PrimaryColor ?? "#18181b"));
            SecondaryColorBrush = new SolidColorBrush(Color.Parse(_settings?.SecondaryColor ?? "#404040"));
            UpdateThemeColors();

            _settings.EnsureInitialized();
            if (_settings.FirstStartup)
            {
                ApplySquareLayoutPreset();
                _settings.FirstStartup = false;
                AppSettings.Save(_settings);
            }

            LoadCurrentVersion();
            LoadCurrentPlatform();
            UpdateSettingsUI();

            // Apply fullscreen from settings immediately
            if (_settings.StartFullscreen)
            {
                WindowState = WindowState.FullScreen;
            }

            // Initialize background image from settings
            BackgroundImagePath = _settings.BackgroundImagePath ?? string.Empty;

            // Initialize music from settings
            LauncherMusicPath = _settings.LauncherMusicPath ?? string.Empty;
            MusicVolume = _settings.MusicVolume;
            if (!string.IsNullOrEmpty(LauncherMusicPath) && File.Exists(LauncherMusicPath))
            {
                PlayLauncherMusic(LauncherMusicPath);
            }

            _inputService = new InputService(this, _settings);
            _inputService.OnConfirm += HandleConfirmAction;
            _inputService.OnCancel += HandleCancelAction;

            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;

            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;

            _gameManager.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(GameManager.Games))
                {
                    OnPropertyChanged(nameof(Games));
                    UpdateContinueButtonState();
                    RefreshUpdateCheckStatus();
                    Debug.WriteLine($"Games collection changed. Count: {_gameManager.Games?.Count ?? 0}");
                    foreach (var game in _gameManager.Games ?? new())
                    {
                        SubscribeToGameEvents(game);
                        Debug.WriteLine($"Game: {game.Name}, IconUrl: {game.IconUrl}");
                    }
                }
            };
        }

        // Is Theme Color Light
        private bool IsLightColor(Color color)
        {
            // Calculate perceived brightness using standard formula
            double brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return brightness > 0.5;
        }

        // Theme Color Shades
        private Color GetShadedColor(Color baseColor, double factor)
        {
            byte r = (byte)Math.Min(255, Math.Max(0, baseColor.R * factor));
            byte g = (byte)Math.Min(255, Math.Max(0, baseColor.G * factor));
            byte b = (byte)Math.Min(255, Math.Max(0, baseColor.B * factor));
            return Color.FromRgb(r, g, b);
        }

        private void UpdateThemeColors()
        {
            if (_themeColorBrush == null || _secondaryColorBrush == null) return;

            var primaryColor = _themeColorBrush.Color;
            var secondaryColor = _secondaryColorBrush.Color;
            var themeBase = new SolidColorBrush(primaryColor);
            var themeLighter = new SolidColorBrush(GetShadedColor(primaryColor, 1.3));
            var themeDarker = new SolidColorBrush(GetShadedColor(primaryColor, 0.7));
            var themeBorder = new SolidColorBrush(secondaryColor);

            var textColor = CalculateLuminance(primaryColor) > 0.5 ? Colors.Black : Colors.White;
            var tintedText = new SolidColorBrush(BlendColors(textColor, secondaryColor, 0.08));
            var tintedTextSecondary = new SolidColorBrush(
                CalculateLuminance(primaryColor) > 0.5
                    ? BlendColors(Color.FromRgb(70, 70, 70), secondaryColor, 0.15)
                    : BlendColors(Color.FromRgb(200, 200, 200), secondaryColor, 0.15)
            );

            Resources["ThemeBase"] = themeBase;
            Resources["ThemeLighter"] = themeLighter;
            Resources["ThemeDarker"] = themeDarker;
            Resources["ThemeBorder"] = themeBorder;
            Resources["ThemeText"] = tintedText;
            Resources["ThemeTextSecondary"] = tintedTextSecondary;

            OnPropertyChanged(nameof(WindowBackground));
        }

        private double CalculateLuminance(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        }

        // Color Picker Preset Dialog
        private async void ThemeColorPicker_Click(object sender, RoutedEventArgs e)
        {
            // Simple color presets dialog
            var presets = new Dictionary<string, string>
            {
                { "Black", "#000000" },
                { "Darker Gray", "#101010" },
                { "Dark Gray (Default)", "#18181b" },
                { "Charcoal Gray", "#2c2c2c" },
                { "Slate Gray", "#36454f" },

                { "Dark Navy Blue", "#1e3a5f" },
                { "Deep Navy", "#0f2b46" },
                { "Deep Indigo", "#2c3e50" },
                { "Dark Grayish Blue", "#45475a" },
                { "Midnight Black", "#19191c" },

                { "Deep Forest Green", "#1a4d2e" },
                { "Darkest Green", "#063204" },
                { "Forest Green", "#228b22" },
                { "Deep Moss Green", "#2c5f2d" },
                { "Deep Forest", "#134411" },
                { "Black Forest Green", "#051D01" },
                { "Dark Olive Green", "#556b2f" },
                { "Dark Olive Drab", "#2a2922" },

                { "Deep Purple", "#2d1b4e" },
                { "Deep Plum", "#4b0082" },
                { "Dark Eggplant", "#614051" },

                { "Dark Burgundy", "#4d1f1f" },
                { "Burgundy", "#800020" },
                { "Deep Maroon", "#5c0b0b" },

                { "Light Gray", "#e5e5e5" },
                { "Silver Gray", "#c0c0c0" },
                { "Pale Gray", "#f0f0f0" },

                { "Soft Blue", "#d4e4f7" },
                { "Sky Blue", "#87ceeb" },
                { "Powder Blue", "#b0e0e6" },

                { "Seafoam Green", "#d4f1e8" },
                { "Mint Green", "#98fb98" },
                { "Bright Sea Foam", "#98ff98" }
            };

            await ShowColorPresetsDialog(presets);
        }

        // Secondary Color Picker
        private async void SecondaryColorPicker_Click(object sender, RoutedEventArgs e)
        {
            // Simple color presets dialog
            var presets = new Dictionary<string, string>
            {
                { "Black", "#000000" },
                { "Dark Gray (Default)", "#404040" },
                { "Gray", "#737373" },
                { "Light Gray", "#d4d4d4" },
                { "White", "#ffffff" },

                { "Red", "#ef4444" },
                { "Orange", "#f97316" },
                { "Yellow", "#eab308" },
                { "Lime", "#84cc16" },
                { "Green", "#10b981" },
                { "Teal", "#14b8a6" },
                { "Cyan", "#06b6d4" },
                { "Sky Blue", "#0ea5e9" },
                { "Blue", "#3b82f6" },
                { "Purple", "#a855f7" },
                { "Violet", "#8b5cf6" },
                { "Indigo", "#6366f1" },
                { "Pink", "#ec4899" } 
            };

            await ShowColorPresetsDialog(presets, true);
        }

        private Color BlendColors(Color baseColor, Color blendColor, double blendAmount)
        {
            byte r = (byte)(baseColor.R * (1 - blendAmount) + blendColor.R * blendAmount);
            byte g = (byte)(baseColor.G * (1 - blendAmount) + blendColor.G * blendAmount);
            byte b = (byte)(baseColor.B * (1 - blendAmount) + blendColor.B * blendAmount);
            return Color.FromRgb(r, g, b);
        }

        private async Task ShowColorPresetsDialog(Dictionary<string, string> presets, bool isSecondary = false)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var stackPanel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };

                    // Add Custom Color button at the top
                    var customButton = new Button
                    {
                        Content = "🎨 Custom Color Picker",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Height = 50,
                        Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                        Foreground = new SolidColorBrush(Colors.White),
                        FontWeight = FontWeight.Bold
                    };

                    customButton.Click += async (s, e) =>
                    {
                        // Close presets dialog
                        var window = (s as Button)?.GetVisualRoot() as Window;
                        window?.Close();

                        // Open custom color picker
                        await ShowCustomColorPicker(isSecondary);
                    };

                    stackPanel.Children.Add(customButton);

                    // Add separator
                    stackPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        Margin = new Thickness(0, 5, 0, 5)
                    });

                    foreach (var preset in presets)
                    {
                        var button = new Button
                        {
                            Content = preset.Key,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = 40,
                            Background = new SolidColorBrush(Color.Parse(preset.Value)),
                            Foreground = new SolidColorBrush(IsLightColor(Color.Parse(preset.Value)) ? Colors.Black : Colors.White),
                            Tag = preset.Value
                        };

                        button.Click += (s, e) =>
                        {
                            var colorHex = (s as Button)?.Tag as string;
                            if (!string.IsNullOrEmpty(colorHex))
                            {
                                if (isSecondary)
                                {
                                    _settings.SecondaryColor = colorHex;
                                    SecondaryColorBrush = new SolidColorBrush(Color.Parse(colorHex));
                                }
                                else
                                {
                                    _settings.PrimaryColor = colorHex;
                                    ThemeColorBrush = new SolidColorBrush(Color.Parse(colorHex));
                                }
                                OnSettingChanged();

                                // Close the dialog after selection
                                if (s is Button btn && btn.Parent != null)
                                {
                                    var window = btn.GetVisualRoot() as Window;
                                    window?.Close();
                                }
                            }
                        };

                        stackPanel.Children.Add(button);
                    }

                    var messageBox = new Window
                    {
                        Title = isSecondary ? "Select Secondary Color" : "Select Primary Color",
                        Width = 300,
                        Height = 1000,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new ScrollViewer { Content = stackPanel }
                    };

                    await messageBox.ShowDialog(desktop.MainWindow);
                }
            });
        }

        private async Task ShowCustomColorPicker(bool isSecondary = false)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var currentColor = isSecondary ? SecondaryColorBrush.Color : ThemeColorBrush.Color;
                    var (h, s, l) = RgbToHsl(currentColor);

                    var pickerPanel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };

                    // Preview box
                    var previewBorder = new Border
                    {
                        Width = 260,
                        Height = 60,
                        CornerRadius = new CornerRadius(8),
                        Background = new SolidColorBrush(currentColor),
                        BorderBrush = new SolidColorBrush(Colors.White),
                        BorderThickness = new Thickness(2)
                    };
                    pickerPanel.Children.Add(previewBorder);

                    // HSL Sliders
                    var hSlider = CreateHslSlider("Hue", h, 0, 360, "°");
                    var sSlider = CreateHslSlider("Saturation", s, 0, 100, "%");
                    var lSlider = CreateHslSlider("Lightness", l, 0, 100, "%");

                    pickerPanel.Children.Add(hSlider.panel);
                    pickerPanel.Children.Add(sSlider.panel);
                    pickerPanel.Children.Add(lSlider.panel);

                    // Update preview on slider change
                    EventHandler<AvaloniaPropertyChangedEventArgs> updatePreview = (s, e) =>
                    {
                        var newColor = HslToRgb(hSlider.slider.Value, sSlider.slider.Value, lSlider.slider.Value);
                        previewBorder.Background = new SolidColorBrush(newColor);
                    };

                    hSlider.slider.PropertyChanged += updatePreview;
                    sSlider.slider.PropertyChanged += updatePreview;
                    lSlider.slider.PropertyChanged += updatePreview;

                    // Hex input
                    var hexPanel = new StackPanel { Spacing = 5 };
                    hexPanel.Children.Add(new TextBlock
                    {
                        Text = "Hex Color",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 12
                    });

                    var hexBox = new TextBox
                    {
                        Text = $"#{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}",
                        Watermark = "#RRGGBB",
                        Foreground = new SolidColorBrush(Colors.White),
                        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40))
                    };

                    hexBox.TextChanged += (s, e) =>
                    {
                        try
                        {
                            var text = hexBox.Text?.Trim();
                            if (!string.IsNullOrEmpty(text) && text.StartsWith("#") && text.Length == 7)
                            {
                                var color = Color.Parse(text);
                                var (hue, sat, light) = RgbToHsl(color);
                                hSlider.slider.Value = hue;
                                sSlider.slider.Value = sat;
                                lSlider.slider.Value = light;
                            }
                        }
                        catch { }
                    };

                    // Update hex box when sliders change
                    EventHandler<AvaloniaPropertyChangedEventArgs> updateHex = (s, e) =>
                    {
                        var color = HslToRgb(hSlider.slider.Value, sSlider.slider.Value, lSlider.slider.Value);
                        hexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    };

                    hSlider.slider.PropertyChanged += updateHex;
                    sSlider.slider.PropertyChanged += updateHex;
                    lSlider.slider.PropertyChanged += updateHex;

                    hexPanel.Children.Add(hexBox);
                    pickerPanel.Children.Add(hexPanel);

                    // Buttons
                    var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };

                    var applyButton = new Button
                    {
                        Content = "Apply",
                        Width = 120,
                        Height = 35,
                        Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                        Foreground = new SolidColorBrush(Colors.White)
                    };

                    var cancelButton = new Button
                    {
                        Content = "Cancel",
                        Width = 120,
                        Height = 35,
                        Background = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        Foreground = new SolidColorBrush(Colors.White)
                    };

                    buttonPanel.Children.Add(applyButton);
                    buttonPanel.Children.Add(cancelButton);
                    pickerPanel.Children.Add(buttonPanel);

                    var pickerWindow = new Window
                    {
                        Title = isSecondary ? "Custom Secondary Color" : "Custom Primary Color",
                        Width = 320,
                        Height = 480,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        Content = pickerPanel,
                        CanResize = false
                    };

                    applyButton.Click += (s, e) =>
                    {
                        var finalColor = HslToRgb(hSlider.slider.Value, sSlider.slider.Value, lSlider.slider.Value);
                        var hexColor = $"#{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";

                        if (isSecondary)
                        {
                            _settings.SecondaryColor = hexColor;
                            SecondaryColorBrush = new SolidColorBrush(finalColor);
                        }
                        else
                        {
                            _settings.PrimaryColor = hexColor;
                            ThemeColorBrush = new SolidColorBrush(finalColor);
                        }
                        OnSettingChanged();
                        pickerWindow.Close();
                    };

                    cancelButton.Click += (s, e) => pickerWindow.Close();

                    await pickerWindow.ShowDialog(desktop.MainWindow);
                }
            });
        }

        private (StackPanel panel, Slider slider) CreateHslSlider(string label, double initialValue, double min, double max, string unit)
        {
            var panel = new StackPanel { Spacing = 5 };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                Width = 80
            });

            var valueText = new TextBlock
            {
                Text = $"{(int)initialValue}{unit}",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                Width = 50,
                TextAlignment = TextAlignment.Right
            };
            headerPanel.Children.Add(valueText);

            panel.Children.Add(headerPanel);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = initialValue,
                Width = 260,
                TickFrequency = 1,
                IsSnapToTickEnabled = true
            };

            slider.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Value")
                {
                    valueText.Text = $"{(int)slider.Value}{unit}";
                }
            };

            panel.Children.Add(slider);

            return (panel, slider);
        }

        // Convert RGB to HSL
        private (double h, double s, double l) RgbToHsl(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            double s = 0;
            double l = (max + min) / 2.0;

            if (delta != 0)
            {
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

                if (max == r)
                    h = ((g - b) / delta + (g < b ? 6 : 0)) / 6.0;
                else if (max == g)
                    h = ((b - r) / delta + 2) / 6.0;
                else
                    h = ((r - g) / delta + 4) / 6.0;
            }

            return (h * 360, s * 100, l * 100);
        }

        // Convert HSL to RGB
        private Color HslToRgb(double h, double s, double l)
        {
            h = h / 360.0;
            s = s / 100.0;
            l = l / 100.0;

            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;

                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromRgb(
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255)
            );
        }

        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        private void UpdateContinueButtonState()
        {
            ContinueGameInfo = _gameManager.GetLatestPlayedInstalledGame();
            IsContinueVisible = ContinueGameInfo != null;
        }

        private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Skip if Hovering ComboBox or if system decorations are enabled
            var source = e.Source as Control;
            if (IsDescendantOf(source, SortByComboBox))
            {
                return;
            }
            if (_settings.ShowOSTopBar)
            {
                return;
            }

            var point = e.GetCurrentPoint(this);

            if (point.Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    // Double-click to maximize/restore
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                }
                else
                {
                    // Single-click drag to move
                    BeginMoveDrag(e);
                }
            }
        }

        private bool IsDescendantOf(Control? control, Control? parent)
        {
            if (control == null || parent == null)
                return false;

            while (control != null)
            {
                if (control == parent)
                    return true;
                control = control.Parent as Control;
            }

            return false;
        }

        private void LoadCurrentPlatform()
        {
            if (_settings != null)
            {
                PlatformString = _settings.Platform switch
                {
                    TargetOS.Auto => "Automatic",
                    TargetOS.Windows => "Windows",
                    TargetOS.MacOS => "macOS",
                    TargetOS.LinuxX64 => "Linux x64",
                    TargetOS.LinuxARM64 => "Linux ARM64",
                    _ => "Unknown"
                };
            }
            else
            {
                PlatformString = "Unknown";
            }
        }

        private void LoadCurrentVersion()
        {
            try
            {
                string currentAppDirectory = AppDomain.CurrentDomain.BaseDirectory;
                currentVersionString = LauncherVersionService.ReadInstalledVersion(currentAppDirectory);
            }
            catch (Exception ex)
            {
                currentVersionString = "Unknown";
                Debug.WriteLine($"Failed to load version: {ex.Message}");
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            ApplyRoundedCorners();
            _ = InitializeGamesAsync();
        }

        private async Task InitializeGamesAsync()
        {
            try
            {
                await _gameManager.LoadGamesAsync();
                _settings = AppSettings.Load();

                await _gameManager.CatalogService.EnsureDefaultCommunitySourceFetchedAsync(
                    _gameManager.HttpClient,
                    _settings);
                AppSettings.Save(_settings);

                ApplySorting();

                await RefreshAllCatalogPendingCountsAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DataContext = this;
                    UpdateContinueButtonState();
                    RefreshCatalogSourcesList();
                    RefreshTagDisplayFiltersUI();
                    RefreshSidebarFilterSelection();
                    UpdateMainViewUi();
                    RefreshUpdateCheckStatus();

                    if (!_hasInitializedFocus)
                    {
                        SetInitialFocus();
                        _hasInitializedFocus = true;
                    }
                });

                await NotifyCatalogUpdatesIfNeededAsync();
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _ = ShowMessageBoxAsync($"Failed to load apps: {ex.Message}", "Load Error"));
            }
        }

        private void SetInitialFocus()
        {
            // Small delay to ensure UI is fully rendered
            Dispatcher.UIThread.Post(() =>
            {
                // Try to focus the Continue button if visible
                if (IsContinueVisible && this.FindControl<Button>("ContinueButton") is Button continueBtn)
                {
                    continueBtn.Focus();
                    return;
                }

                // Try Library nav button
                if (this.FindControl<Button>("LibraryNavButton") is Button libraryBtn)
                {
                    libraryBtn.Focus();
                    return;
                }

                // Fallback to first focusable control
                var firstFocusable = this.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.IsVisible && c.IsEnabled && c.Focusable);

                firstFocusable?.Focus();
            }, DispatcherPriority.Loaded);
        }

        public void CloseLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (CloseSettingsPanel())
                return;

            // Close changelog if open
            if (_isChangelogOpen)
            {
                CloseChangelog();
                return;
            }

            if (_isEntryFormOpen)
            {
                CloseEntryFormOverlay();
                return;
            }

            Close();
        }

        public void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            IsFullscreen = !IsFullscreen;
            if (IsFullscreen)
            {
                WindowState = WindowState.FullScreen;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
            OnPropertyChanged(nameof(WindowBackground));
        }

        public void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        public void LayoutPreset_Landscape_Click(object sender, RoutedEventArgs e)
        {
            _settings.IconFill = false;
            _settings.UseGridView = true;
            _settings.GridCompactCards = false;
            _settings.SlotSize = 304;
            _settings.IconSize = 220;
            _settings.IconMargin = 0;
            _settings.SlotTextMargin = 0;
            _settings.IconOpacity = 1.0f;
            InfoTextLength = "90";
            OnSettingChanged();
            UpdateSettingsUI();
        }

        public void LayoutPreset_Portrait_Click(object sender, RoutedEventArgs e)
        {
            _settings.IconFill = false;
            _settings.UseGridView = true;
            _settings.GridCompactCards = false;
            _settings.SlotSize = 144;
            _settings.IconSize = 200;
            _settings.IconMargin = 0;
            _settings.SlotTextMargin = 0;
            _settings.IconOpacity = 1.0f;
            InfoTextLength = "90";
            OnSettingChanged();
            UpdateSettingsUI();
        }

        public void LayoutPreset_Square_Click(object sender, RoutedEventArgs e)
        {
            ApplySquareLayoutPreset();
            OnSettingChanged();
            UpdateSettingsUI();
        }

        public void LayoutPreset_Grid_Click(object sender, RoutedEventArgs e)
        {
            _settings.IconFill = true;
            _settings.UseGridView = true;
            _settings.GridCompactCards = true;
            _settings.SlotSize = 272;
            _settings.IconSize = 200;
            _settings.IconMargin = 0;
            _settings.SlotTextMargin = 0;
            _settings.IconOpacity = 1.0f;
            InfoTextLength = "*";
            OnSettingChanged();
            UpdateSettingsUI();
        }

        public void LayoutPreset_List_Click(object sender, RoutedEventArgs e)
        {
            _settings.IconFill = false;
            _settings.UseGridView = false;
            _settings.SlotSize = 120;
            _settings.IconSize = 116;
            _settings.IconMargin = 8;
            _settings.SlotTextMargin = 112;
            _settings.IconOpacity = 1.0f;
            InfoTextLength = "*";
            OnSettingChanged();
            UpdateSettingsUI();
        }

        private void ApplySquareLayoutPreset()
        {
            _settings.IconFill = false;
            _settings.UseGridView = true;
            _settings.GridCompactCards = false;
            _settings.SlotSize = 152;
            _settings.IconSize = 124;
            _settings.ActionButtonSize = 36;
            _settings.IconMargin = 0;
            _settings.SlotTextMargin = 0;
            _settings.IconOpacity = 1.0f;
            InfoTextLength = "*";
        }

        private async void GameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is GameInfo game)
            {
                try
                {
                    if (game.Status == GameStatus.UpdateAvailable)
                    {
                        ShowUpdateActionMenu(button, game);
                        return;
                    }

                    await game.PerformActionAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings);

                    // Check if multiple downloads need selection
                    if ((game.Status == GameStatus.NotInstalled || game.Status == GameStatus.UpdateAvailable) &&
                        game.HasMultipleDownloads && game.SelectedDownload == null)
                    {
                        if (button != null)
                        {
                            ShowDownloadSelectionMenu(button, game);
                        }
                        return;
                    }

                    // Check if multiple executables need selection
                    if (game.Status == GameStatus.Installed && game.HasMultipleExecutables && string.IsNullOrEmpty(game.SelectedExecutable))
                    {
                        if (button != null)
                        {
                            ShowExecutableSelectionMenu(button, game);
                        }
                        return;
                    }

                    UpdateContinueButtonState();
                }
                catch (Exception ex)
                {
                    await ShowMessageBoxAsync($"Failed to perform action for {game.Name}: {ex.Message}", "Action Error");
                }
                if (_settings.CloseAfterLaunch)
                {
                    Close();
                }
            }
        }

        private void ShowUpdateActionMenu(Control anchor, GameInfo game)
        {
            var contextMenu = new ContextMenu();

            contextMenu.Items.Add(new MenuItem
            {
                Header = $"Update options for {game.Name}:",
                IsEnabled = false,
                FontWeight = FontWeight.Bold
            });
            contextMenu.Items.Add(new Separator());

            var updateNowItem = new MenuItem { Header = "Update Now" };
            updateNowItem.Click += async (_, _) => await HandleUpdateNowAsync(anchor, game);
            contextMenu.Items.Add(updateNowItem);

            var skipItem = new MenuItem { Header = "Skip Update" };
            skipItem.Click += async (_, _) => await HandleSkipUpdateAsync(game);
            contextMenu.Items.Add(skipItem);

            var changeVersionItem = new MenuItem { Header = "Change Version" };
            changeVersionItem.Click += async (_, _) => await HandleChangeVersionAsync(anchor, game);
            contextMenu.Items.Add(changeVersionItem);

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Cancel" });

            OpenContextMenu(anchor, contextMenu);
        }

        private void ShowDownloadSelectionMenu(Button sourceButton, GameInfo game)
        {
            if (game.AvailableDownloads == null || game.AvailableDownloads.Count == 0)
                return;

            var contextMenu = new ContextMenu();

            // Add header
            var headerItem = new MenuItem
            {
                Header = "Select download file:",
                IsEnabled = false,
                FontWeight = FontWeight.Bold
            };
            contextMenu.Items.Add(headerItem);
            contextMenu.Items.Add(new Separator());

            // Get platform identifier for matching
            string platformIdentifier = GameInfo.GetPlatformIdentifier(_settings);

            // Sort downloads: preferred platform first, then others
            var sortedDownloads = game.AvailableDownloads
                .OrderByDescending(asset => GameInfo.MatchesPlatform(asset.name, platformIdentifier))
                .ToList();

            // Add download options
            foreach (var asset in sortedDownloads)
            {
                bool isPreferred = GameInfo.MatchesPlatform(asset.name, platformIdentifier);

                // Detect platform icon
                string? iconPath = GameInfo.GetPlatformIcon(asset.name);

                // Create grid
                var contentGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // Add icon if detected
                if (!string.IsNullOrEmpty(iconPath))
                {
                    var icon = new Avalonia.Controls.Image
                    {
                        Source = new Avalonia.Media.Imaging.Bitmap(
                            Avalonia.Platform.AssetLoader.Open(new Uri(iconPath))),
                        Width = 28,
                        Height = 28,
                        Margin = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(icon, 0);
                    contentGrid.Children.Add(icon);
                }

                // Add filename text
                var displayName = asset.name + (isPreferred ? " (Recommended)" : "");
                var textBlock = new TextBlock
                {
                    Text = displayName,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(textBlock, 1);
                contentGrid.Children.Add(textBlock);

                var menuItem = new MenuItem
                {
                    Header = contentGrid,
                    Tag = asset
                };

                if (isPreferred)
                {
                    menuItem.Classes.Add("accent");
                }

                menuItem.Click += async (s, e) =>
                {
                    var selectedAsset = (s as MenuItem)?.Tag as GitHubAsset;
                    game.SelectedDownload = selectedAsset;
                    try
                    {
                        await game.PerformActionAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings);
                    }
                    catch (Exception ex)
                    {
                        await ShowMessageBoxAsync($"Failed to download {game.Name}: {ex.Message}", "Download Error");
                    }
                };

                contextMenu.Items.Add(menuItem);
            }

            contextMenu.Items.Add(new Separator());

            // Add cancel option
            var cancelItem = new MenuItem
            {
                Header = "Cancel"
            };
            cancelItem.Click += (s, e) =>
            {
                game.SelectedDownload = null;
            };
            contextMenu.Items.Add(cancelItem);

            // Attach to button and open
            sourceButton.ContextMenu = contextMenu;
            contextMenu.PlacementTarget = sourceButton;
            contextMenu.Placement = PlacementMode.Bottom;

            // Focus first download item when opened
            contextMenu.Opened += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var firstDownloadItem = contextMenu.Items.OfType<MenuItem>()
                        .Skip(1)
                        .FirstOrDefault(item => item is MenuItem mi && mi.IsEnabled);
                    firstDownloadItem?.Focus();
                }, DispatcherPriority.Loaded);
            };

            contextMenu.Open(sourceButton);
        }

        private Control? ResolveMenuAnchor(Control sourceControl)
        {
            if (sourceControl is MenuItem menuItem)
            {
                var parent = menuItem.Parent as Control;
                while (parent != null)
                {
                    if (parent is ContextMenu parentMenu && parentMenu.PlacementTarget is Control parentTarget)
                    {
                        return parentTarget;
                    }

                    parent = parent.Parent as Control;
                }

                var visualMenu = menuItem.GetVisualAncestors().OfType<ContextMenu>().FirstOrDefault();
                if (visualMenu?.PlacementTarget is Control visualTarget)
                {
                    return visualTarget;
                }
            }

            return sourceControl;
        }

        private Control? FindGameMenuAnchor(GameInfo game)
        {
            return this.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button =>
                    ReferenceEquals(button.DataContext, game) ||
                    ReferenceEquals(button.Tag, game));
        }

        private void OpenContextMenu(Control anchor, ContextMenu contextMenu)
        {
            if (double.IsNaN(contextMenu.MaxHeight) || contextMenu.MaxHeight <= 0)
            {
                var availableHeight = Bounds.Height > 0 ? Bounds.Height - 120 : 560;
                contextMenu.MaxHeight = Math.Max(240, availableHeight);
            }

            contextMenu.PlacementTarget = anchor;
            contextMenu.Placement = PlacementMode.Bottom;
            AttachOptionsMenuClosedHandler(contextMenu, anchor);
            contextMenu.Open(anchor);
        }

        private void AttachOptionsMenuClosedHandler(ContextMenu contextMenu, Control anchor)
        {
            void OnClosed(object? sender, EventArgs e)
            {
                contextMenu.Closed -= OnClosed;
                var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
                if (ReferenceEquals(focusManager?.GetFocusedElement(), anchor))
                    focusManager.ClearFocus();
            }

            contextMenu.Closed -= OnClosed;
            contextMenu.Closed += OnClosed;
        }

        private void GameCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                return;
            }

            if (sender is not Control card)
            {
                return;
            }

            Control? placementTarget = null;
            ContextMenu? contextMenu = card.ContextMenu;

            if (contextMenu != null)
            {
                placementTarget = card;
            }
            else
            {
                var optionsButton = card.GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(button => button.ContextMenu != null);

                contextMenu = optionsButton?.ContextMenu;
                placementTarget = optionsButton;
            }

            if (contextMenu == null || placementTarget == null)
            {
                return;
            }

            contextMenu.PlacementTarget = placementTarget;
            contextMenu.Placement = PlacementMode.Bottom;
            if (double.IsNaN(contextMenu.MaxHeight) || contextMenu.MaxHeight <= 0)
            {
                var availableHeight = Bounds.Height > 0 ? Bounds.Height - 120 : 560;
                contextMenu.MaxHeight = Math.Max(240, availableHeight);
            }

            AttachOptionsMenuClosedHandler(contextMenu, placementTarget);
            contextMenu.Open(placementTarget);
            e.Handled = true;
        }

        private async Task HandleUpdateNowAsync(Control anchor, GameInfo game)
        {
            try
            {
                game.IsLoading = true;
                var releases = await game.FetchReleasesAsync(_gameManager.HttpClient);
                var latestRelease = releases.FirstOrDefault();
                if (latestRelease == null)
                {
                    await ShowMessageBoxAsync($"No downloadable releases were found for {game.Name}.", "No Releases");
                    return;
                }

                var availableAssets = latestRelease.assets?
                    .Where(asset => !asset.name.Contains("flatpak", StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? [];

                if (availableAssets.Count == 0)
                {
                    await ShowMessageBoxAsync($"No downloadable files were found for {game.Name}.", "No Assets");
                    return;
                }

                if (availableAssets.Count == 1)
                {
                    await game.InstallReleaseAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings, latestRelease, availableAssets[0]);
                    await PersistGameVersionPreferencesAsync(game, null, latestRelease.tag_name);
                    ApplySorting();
                    UpdateContinueButtonState();
                    return;
                }

                ShowReleaseDownloadSelectionMenu(anchor, game, latestRelease, null, latestRelease.tag_name);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to update {game.Name}: {ex.Message}", "Update Error");
            }
            finally
            {
                game.IsLoading = false;
            }
        }

        private async Task HandleSkipUpdateAsync(GameInfo game)
        {
            game.SkipLatestUpdate();
            await PersistGameVersionPreferencesAsync(game, game.PreferredVersion, game.SkippedUpdateVersion);
            ApplySorting();
            UpdateContinueButtonState();
        }

        private async Task HandleChangeVersionAsync(Control anchor, GameInfo game)
        {
            try
            {
                game.IsLoading = true;
                var releases = await game.FetchReleasesAsync(_gameManager.HttpClient);
                if (releases.Count == 0)
                {
                    await ShowMessageBoxAsync($"No downloadable releases were found for {game.Name}.", "No Releases");
                    return;
                }

                ShowVersionSelectionMenu(anchor, game, releases);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to load versions for {game.Name}: {ex.Message}", "Version Selection Error");
            }
            finally
            {
                game.IsLoading = false;
            }
        }

        private void ShowVersionSelectionMenu(Control anchor, GameInfo game, IReadOnlyList<GitHubRelease> releases)
        {
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem
            {
                Header = $"Choose a version for {game.Name}:",
                IsEnabled = false,
                FontWeight = FontWeight.Bold
            });
            contextMenu.Items.Add(new Separator());

            foreach (var release in releases)
            {
                var tags = new List<string>();
                if (!string.IsNullOrWhiteSpace(game.LatestVersion) &&
                    release.tag_name.Equals(game.LatestVersion, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add("Latest");
                }
                if (!string.IsNullOrWhiteSpace(game.InstalledVersion) &&
                    release.tag_name.Equals(game.InstalledVersion, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add("Installed");
                }
                if (!string.IsNullOrWhiteSpace(game.PreferredVersion) &&
                    release.tag_name.Equals(game.PreferredVersion, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add("Preferred");
                }
                if (release.prerelease)
                {
                    tags.Add("Pre-release");
                }

                var header = release.tag_name;
                if (tags.Count > 0)
                {
                    header += $" ({string.Join(", ", tags)})";
                }

                var versionItem = new MenuItem { Header = header };
                versionItem.Click += (_, _) =>
                {
                    var acknowledgedVersion = string.IsNullOrWhiteSpace(game.LatestVersion)
                        ? release.tag_name
                        : game.LatestVersion;
                    ShowReleaseDownloadSelectionMenu(anchor, game, release, release.tag_name, acknowledgedVersion);
                };

                if (!string.IsNullOrWhiteSpace(game.LatestVersion) &&
                    release.tag_name.Equals(game.LatestVersion, StringComparison.OrdinalIgnoreCase))
                {
                    versionItem.Classes.Add("accent");
                }

                contextMenu.Items.Add(versionItem);
            }

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Cancel" });
            OpenContextMenu(anchor, contextMenu);
        }

        private void ShowReleaseDownloadSelectionMenu(Control anchor, GameInfo game, GitHubRelease release, string preferredVersion, string skippedUpdateVersion)
        {
            var availableAssets = release.assets?
                .Where(asset => !asset.name.Contains("flatpak", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(asset => GameInfo.MatchesPlatform(asset.name, GameInfo.GetPlatformIdentifier(_settings)))
                .ToList() ?? [];

            if (availableAssets.Count == 0)
                return;

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem
            {
                Header = $"Choose a download for {release.tag_name}:",
                IsEnabled = false,
                FontWeight = FontWeight.Bold
            });
            contextMenu.Items.Add(new Separator());

            string platformIdentifier = GameInfo.GetPlatformIdentifier(_settings);

            foreach (var asset in availableAssets)
            {
                bool isPreferred = GameInfo.MatchesPlatform(asset.name, platformIdentifier);
                string? iconPath = GameInfo.GetPlatformIcon(asset.name);

                var contentGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                if (!string.IsNullOrEmpty(iconPath))
                {
                    var icon = new Avalonia.Controls.Image
                    {
                        Source = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(iconPath))),
                        Width = 28,
                        Height = 28,
                        Margin = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(icon, 0);
                    contentGrid.Children.Add(icon);
                }

                var displayName = asset.name + (isPreferred ? " (Recommended)" : "");
                var textBlock = new TextBlock
                {
                    Text = displayName,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(textBlock, 1);
                contentGrid.Children.Add(textBlock);

                var menuItem = new MenuItem
                {
                    Header = contentGrid
                };

                if (isPreferred)
                {
                    menuItem.Classes.Add("accent");
                }

                menuItem.Click += async (_, _) =>
                {
                    try
                    {
                        await game.InstallReleaseAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings, release, asset);
                        if (!string.IsNullOrWhiteSpace(skippedUpdateVersion))
                        {
                            game.LatestVersion = skippedUpdateVersion;
                        }
                        await PersistGameVersionPreferencesAsync(game, preferredVersion, skippedUpdateVersion);
                        ApplySorting();
                        UpdateContinueButtonState();
                    }
                    catch (Exception ex)
                    {
                        await ShowMessageBoxAsync($"Failed to download {game.Name}: {ex.Message}", "Download Error");
                    }
                };

                contextMenu.Items.Add(menuItem);
            }

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Cancel" });
            OpenContextMenu(anchor, contextMenu);
        }

        private void ShowExecutableSelectionMenu(Control anchor, GameInfo game)
        {
            if (game.AvailableExecutables == null || game.AvailableExecutables.Count == 0)
                return;

            var contextMenu = new ContextMenu();

            // Add header
            var headerItem = new MenuItem
            {
                Header = "Select executable to launch:",
                IsEnabled = false,
                FontWeight = FontWeight.Bold
            };
            contextMenu.Items.Add(headerItem);
            contextMenu.Items.Add(new Separator());

            // Add executable options
            foreach (var exe in game.AvailableExecutables)
            {
                var displayName = Path.GetFileName(exe);
                var menuItem = new MenuItem
                {
                    Header = displayName,
                    Tag = exe
                };

                menuItem.Click += async (s, e) =>
                {
                    var selectedExe = (s as MenuItem)?.Tag as string;
                    game.SelectedExecutable = selectedExe;

                    // Save the selection
                    if (!string.IsNullOrEmpty(selectedExe))
                    {
                        game.SaveSelectedExecutable(selectedExe, _gameManager.GamesFolder);
                    }

                    try
                    {
                        await game.PerformActionAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings);
                    }
                    catch (Exception ex)
                    {
                        await ShowMessageBoxAsync($"Failed to launch {game.Name}: {ex.Message}", "Launch Error");
                    }
                };

                contextMenu.Items.Add(menuItem);
            }

            contextMenu.Items.Add(new Separator());

            // Add cancel option
            var cancelItem = new MenuItem
            {
                Header = "Cancel"
            };
            cancelItem.Click += (s, e) =>
            {
                game.SelectedExecutable = null;
            };
            contextMenu.Items.Add(cancelItem);

            // Focus first executable item when opened
            contextMenu.Opened += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var firstExecutableItem = contextMenu.Items.OfType<MenuItem>()
                        .Skip(1)
                        .FirstOrDefault(item => item is MenuItem mi && mi.IsEnabled);
                    firstExecutableItem?.Focus();
                }, DispatcherPriority.Loaded);
            };

            OpenContextMenu(anchor, contextMenu);
        }

        private async void SelectDifferentExecutable_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                _ = ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var anchor = (menuItem != null ? ResolveMenuAnchor(menuItem) : null) ?? FindGameMenuAnchor(game);
            if (anchor == null)
            {
                _ = ShowMessageBoxAsync("Unable to open the executable menu for this game.", "Error");
                return;
            }

            game.ClearSelectedExecutable(_gameManager.GamesFolder);
            game.AvailableExecutables = null;

            try
            {
                if (string.IsNullOrWhiteSpace(game.FolderName))
                {
                    await ShowMessageBoxAsync("This game is missing its install folder information.", "Executable Error");
                    return;
                }

                var gamePath = game.GetInstallPath(_gameManager.GamesFolder);
                if (!Directory.Exists(gamePath))
                {
                    await ShowMessageBoxAsync($"Could not find the install folder for {game.Name}.", "Executable Error");
                    return;
                }

                var executables = GameInfo.GetExecutableCandidates(gamePath, SearchOption.TopDirectoryOnly, out _);
                if (executables.Count == 0)
                {
                    executables = GameInfo.GetExecutableCandidates(gamePath, SearchOption.AllDirectories, out _);
                }

                if (executables.Count == 0)
                {
                    await ShowMessageBoxAsync($"No executable files were found for {game.Name}.", "Executable Not Found");
                    return;
                }

                game.AvailableExecutables = executables;

                if (executables.Count == 1)
                {
                    await ShowMessageBoxAsync($"Only one executable was found for {game.Name}, so there is nothing else to choose.", "Single Executable");
                    return;
                }

                ShowExecutableSelectionMenu(anchor, game);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to load executables for {game.Name}: {ex.Message}", "Executable Error");
            }
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                AttachOptionsMenuClosedHandler(button.ContextMenu, button);
                button.ContextMenu.Open();
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            var latestGame = _gameManager.GetLatestPlayedInstalledGame();
            if (latestGame != null)
            {
                try
                {
                    await latestGame.PerformActionAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings);
                }
                catch (Exception ex)
                {
                    await ShowMessageBoxAsync($"Failed to launch {latestGame.Name}: {ex.Message}", "Launch Error");
                }
            }
            else
            {
                await ShowMessageBoxAsync("No installed apps found to continue.", "No App Found");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel == null)
                return;

            if (_isEntryFormOpen)
                CloseEntryFormOverlay();

            isSettingsPanelOpen = !isSettingsPanelOpen;
            SettingsPanel.IsVisible = isSettingsPanelOpen;

            if (isSettingsPanelOpen)
            {
                RefreshCatalogSourcesList();
                Dispatcher.UIThread.Post(() =>
                {
                    EnableGamepadCheckBox?.Focus();
                }, DispatcherPriority.Loaded);
            }
        }

        private bool CloseSettingsPanel()
        {
            if (!isSettingsPanelOpen || SettingsPanel == null)
                return false;

            isSettingsPanelOpen = false;
            SettingsPanel.IsVisible = false;

            var settingsButton = this.FindControl<Button>("SettingsButton");
            if (settingsButton != null)
                settingsButton.Focus();
            else
            {
                var firstFocusable = this.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.IsVisible && c.IsEnabled && c.Focusable && !IsInsideSettingsPanel(c));
                firstFocusable?.Focus();
            }

            return true;
        }

        private void CloseSettingsPanel_Click(object? sender, RoutedEventArgs e)
        {
            CloseSettingsPanel();
        }

        private void UpdateSettingsUI()
        {
            if (_settings == null)
                return;

            _suppressSettingsUiEvents = true;
            try
            {
                if (IconOpacitySlider != null)
                    IconOpacitySlider.Value = _settings.IconOpacity;

                if (IconSizeSlider != null)
                    IconSizeSlider.Value = _settings.IconSize;

                if (IconFillCheckBox != null)
                    IconFillCheckBox.IsChecked = _settings.IconFill;

                if (TextMarginSlider != null)
                    TextMarginSlider.Value = _settings.SlotTextMargin;

                if (IconMarginSlider != null)
                    IconMarginSlider.Value = _settings.IconMargin;

                if (SlotSizeSlider != null)
                    SlotSizeSlider.Value = _settings.SlotSize;

                if (ActionButtonSizeSlider != null)
                    ActionButtonSizeSlider.Value = _settings.ActionButtonSize;

                if (UseGridViewCheckBox != null)
                    UseGridViewCheckBox.IsChecked = _settings.UseGridView;

                if (GridCompactCardsCheckBox != null)
                    GridCompactCardsCheckBox.IsChecked = _settings.GridCompactCards;

                UpdateGridLayoutVisibility();

                if (BackgroundOpacitySlider != null)
                    BackgroundOpacitySlider.Value = _settings.BackgroundOpacity;

                if (ShowOSTopBarCheckBox != null)
                    ShowOSTopBarCheckBox.IsChecked = _settings.ShowOSTopBar;

                if (SortByComboBox != null)
                {
                    var savedSort = _settings.SortBy ?? "Name";
                    _currentSortBy = savedSort;

                    foreach (var entry in SortByComboBox.Items)
                    {
                        if (entry is ComboBoxItem item && item.Tag as string == savedSort)
                        {
                            SortByComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (GitHubTokenTextBox != null)
                    GitHubTokenTextBox.Text = _settings.GitHubApiToken;

                if (GamePathTextBox != null)
                    GamePathTextBox.Text = _settings.AppsPath;

                if (LinuxWindowsLaunchCommandTextBox != null)
                    LinuxWindowsLaunchCommandTextBox.Text = _settings.LinuxWindowsLaunchCommand;

                if (StartFullscreenCheckBox != null)
                    StartFullscreenCheckBox.IsChecked = _settings.StartFullscreen;

                if (RoundWindowCornersCheckBox != null)
                    RoundWindowCornersCheckBox.IsChecked = _settings.WindowBorderRounding;

                if (EnableGamepadCheckBox != null)
                    EnableGamepadCheckBox.IsChecked = _settings.EnableGamepadInput;

                if (CloseAfterLaunchCheckBox != null)
                    CloseAfterLaunchCheckBox.IsChecked = _settings.CloseAfterLaunch;

                PlatformString = _settings.Platform switch
                {
                    TargetOS.Auto => "Automatic",
                    TargetOS.Windows => "Windows",
                    TargetOS.MacOS => "macOS",
                    TargetOS.LinuxX64 => "Linux x64",
                    TargetOS.LinuxARM64 => "Linux ARM64",
                    _ => "Unknown"
                };

                // Initialize theme
                ThemeColorBrush = new SolidColorBrush(Color.Parse(_settings?.PrimaryColor ?? "#18181b"));
                SecondaryColorBrush = new SolidColorBrush(Color.Parse(_settings?.SecondaryColor ?? "#404040"));
                UpdateThemeColors();
                RefreshCatalogSourcesList();
                RefreshTagDisplayFiltersUI();
                RefreshSidebarFilterSelection();
            }
            finally
            {
                _suppressSettingsUiEvents = false;
            }
        }

        private void RefreshTagDisplayFiltersUI()
        {
            _settings.EnsureInitialized();

            TagDisplayFilters.Clear();
            foreach (var filter in _settings.TagDisplayFilters)
            {
                var isSelected = string.Equals(
                    filter.Id,
                    _settings.ActiveTagDisplayFilterId,
                    StringComparison.OrdinalIgnoreCase);
                TagDisplayFilters.Add(TagDisplayFilterListItem.FromFilter(filter, isSelected));
            }
        }

        private void RefreshSidebarFilterSelection()
        {
            _settings.EnsureInitialized();

            UnhideAllGamesButton?.Classes.Set(
                "selected",
                _settings.ListScope == AppListScope.AllApps);
            HideNonInstalledButton?.Classes.Set(
                "selected",
                _settings.ListScope == AppListScope.InstalledOnly);
        }

        private void RefreshCatalogSourcesList()
        {
            if (_isRefreshingCatalogSources)
                return;

            _isRefreshingCatalogSources = true;
            _suppressCatalogSourceUiEvents = true;
            try
            {
                _catalogViewModel.RefreshSourceList(CatalogSources, _settings);
            }
            finally
            {
                _suppressCatalogSourceUiEvents = false;
                _isRefreshingCatalogSources = false;
            }

            RefreshCatalogBadgeCounts();
            UpdateCatalogSourcesEmptyState();
        }

        private void RefreshCatalogBadgeCounts()
        {
            OnPropertyChanged(nameof(CatalogReviewBadgeCount));
            OnPropertyChanged(nameof(CatalogReviewBadgeVisible));
        }

        private void UpdateCatalogSourcesEmptyState()
        {
            if (this.FindControl<TextBlock>("CatalogSourcesEmptyText") is TextBlock emptyText)
                emptyText.IsVisible = CatalogSources.Count == 0;
        }

        private void LibraryNavButton_Click(object? sender, RoutedEventArgs e) => ShowLibraryView();

        private void AppCatalogNavButton_Click(object? sender, RoutedEventArgs e) =>
            ShowAppCatalogSourcesView();

        private void CatalogReviewBack_Click(object? sender, RoutedEventArgs e) =>
            ShowAppCatalogSourcesView();

        private void ShowLibraryView()
        {
            _mainViewMode = MainViewMode.Library;
            _appCatalogSubView = AppCatalogSubView.Sources;
            UpdateMainViewUi();
        }

        private void ShowAppCatalogSourcesView()
        {
            _mainViewMode = MainViewMode.AppCatalog;
            _appCatalogSubView = AppCatalogSubView.Sources;
            _activeCatalogSyncSource = null;
            CatalogSyncRows.Clear();
            UpdateMainViewUi();
        }

        private void ShowAppCatalogReviewView(AppCatalogSource source)
        {
            _mainViewMode = MainViewMode.AppCatalog;
            _appCatalogSubView = AppCatalogSubView.Review;
            _activeCatalogSyncSource = source;
            UpdateMainViewUi();

            if (this.FindControl<TextBlock>("HeaderTitleText") is TextBlock headerTitle)
                headerTitle.Text = $"Review: {source.Name}";
        }

        private void UpdateMainViewUi()
        {
            var isLibrary = _mainViewMode == MainViewMode.Library;
            var isCatalog = _mainViewMode == MainViewMode.AppCatalog;
            var isReview = isCatalog && _appCatalogSubView == AppCatalogSubView.Review;

            if (this.FindControl<ScrollViewer>("LibraryContentPanel") is ScrollViewer libraryPanel)
                libraryPanel.IsVisible = isLibrary;

            if (this.FindControl<Grid>("CatalogContentPanel") is Grid catalogPanel)
                catalogPanel.IsVisible = isCatalog;

            if (this.FindControl<ScrollViewer>("CatalogSourcesPanel") is ScrollViewer sourcesPanel)
                sourcesPanel.IsVisible = isCatalog && !isReview;

            if (this.FindControl<Grid>("CatalogReviewPanel") is Grid reviewPanel)
                reviewPanel.IsVisible = isReview;

            if (this.FindControl<StackPanel>("LibraryTopBarPanel") is StackPanel libraryTopBar)
                libraryTopBar.IsVisible = isLibrary;

            if (this.FindControl<Button>("CatalogReviewBackButton") is Button backButton)
                backButton.IsVisible = isReview;

            if (this.FindControl<Grid>("LibraryFiltersPanel") is Grid libraryFilters)
                libraryFilters.IsVisible = isLibrary;

            LibraryNavButton?.Classes.Set("selected", isLibrary);
            AppCatalogNavButton?.Classes.Set("selected", isCatalog);

            if (this.FindControl<TextBlock>("HeaderTitleText") is TextBlock headerTitle)
            {
                headerTitle.Text = isLibrary
                    ? "Library"
                    : isReview && _activeCatalogSyncSource != null
                        ? $"Review: {_activeCatalogSyncSource.Name}"
                        : "App Catalog";
            }
        }

        private static readonly (string Tag, CatalogReviewFilter Filter)[] CatalogReviewFilters =
        [
            ("All", CatalogReviewFilter.All),
            ("NeedsReview", CatalogReviewFilter.NeedsReview),
            ("New", CatalogReviewFilter.New),
            ("NotInLibrary", CatalogReviewFilter.NotInLibrary),
            ("Changed", CatalogReviewFilter.Changed),
            ("UpToDate", CatalogReviewFilter.UpToDate),
            ("LocalOnly", CatalogReviewFilter.LocalOnly),
            ("Hidden", CatalogReviewFilter.Hidden),
        ];

        private void CatalogReviewFilter_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string tag })
                return;

            var filter = CatalogReviewFilters.FirstOrDefault(f => f.Tag == tag).Filter;
            _catalogSyncViewModel.ReviewFilter = filter;
            RefreshCatalogReviewFilterButtons(tag);
            ApplyCatalogSyncFilter();
        }

        private void RefreshCatalogReviewFilterButtons(string selectedTag)
        {
            foreach (var (tag, _) in CatalogReviewFilters)
            {
                var buttonName = tag switch
                {
                    "All" => "CatalogFilterAllButton",
                    "NeedsReview" => "CatalogFilterNeedsReviewButton",
                    "New" => "CatalogFilterNewButton",
                    "NotInLibrary" => "CatalogFilterNotInLibraryButton",
                    "Changed" => "CatalogFilterChangedButton",
                    "UpToDate" => "CatalogFilterUpToDateButton",
                    "LocalOnly" => "CatalogFilterLocalOnlyButton",
                    "Hidden" => "CatalogFilterHiddenButton",
                    _ => null,
                };

                if (buttonName != null && this.FindControl<Button>(buttonName) is Button button)
                    button.Classes.Set("selected", tag == selectedTag);
            }

            UpdateCatalogReviewFilterChipLabels();
        }

        private void UpdateCatalogReviewFilterChipLabels()
        {
            if (this.FindControl<Button>("CatalogFilterNeedsReviewButton") is Button needsReviewButton)
            {
                var count = _catalogSyncViewModel.NeedsReviewCount;
                needsReviewButton.Content = count > 0 ? $"Needs review ({count})" : "Needs review";
            }

            if (this.FindControl<Button>("CatalogFilterNotInLibraryButton") is Button notInLibraryButton)
            {
                var count = _catalogSyncViewModel.NotInLibraryCount;
                notInLibraryButton.Content = count > 0 ? $"Not in library ({count})" : "Not in library";
            }

            if (this.FindControl<Button>("CatalogFilterHiddenButton") is Button hiddenButton)
            {
                var count = _catalogSyncViewModel.HiddenCount;
                hiddenButton.Content = count > 0 ? $"Hidden ({count})" : "Hidden";
            }
        }

        private async Task RefreshAllCatalogPendingCountsAsync()
        {
            foreach (var source in _settings.AppCatalogSources.Where(s => s.Enabled))
                await _gameManager.CatalogService.RefreshUpdateAvailableAsync(source);

            RefreshCatalogSourcesList();
        }

        private async void AddCatalogSource_Click(object? sender, RoutedEventArgs e)
        {
            var result = await ShowAddCatalogSourceDialogAsync();
            if (result == null)
                return;

            var (name, location) = result.Value;
            var (apps, version, error) = await _gameManager.CatalogService.TryLoadSourceAsync(_gameManager.HttpClient, location);
            if (error != null)
            {
                await ShowMessageBoxAsync($"Could not load catalog source:\n{error}", "Invalid Source");
                return;
            }

            if (apps.Count == 0)
            {
                await ShowMessageBoxAsync("The catalog source loaded successfully but contains no apps.", "Empty Catalog");
                return;
            }

            var source = _catalogViewModel.CreateSource(name, location);
            string? rawJson = null;
            try
            {
                rawJson = await CatalogLocationReader.Default.ReadAsync(_gameManager.HttpClient, location);
            }
            catch
            {
                // RegisterNewSourceAsync will re-fetch if needed.
            }

            await _gameManager.CatalogService.RegisterNewSourceAsync(_gameManager.HttpClient, source, rawJson);

            _settings.AppCatalogSources.Add(source);
            OnSettingChanged();
            RefreshCatalogSourcesList();

            var versionLabel = string.IsNullOrWhiteSpace(version) ? "unknown" : version;
            await ShowMessageBoxAsync($"Added \"{name}\" ({apps.Count} app(s), v{versionLabel}). Use App Catalog → Review to add apps to your library.", "Source Added");
        }

        private async void RemoveCatalogSource_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string sourceId })
                return;

            var source = _settings.AppCatalogSources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null)
                return;

            var confirmed = await ShowMessageBoxAsync(
                $"Remove catalog source \"{source.Name}\"?\n\nApps already in your local apps.json will stay in your library.",
                "Remove Source",
                true);

            if (!confirmed)
                return;

            _gameManager.CatalogService.DeleteSourceCache(sourceId);
            _settings.AppCatalogSources.RemoveAll(s => s.Id == sourceId);
            OnSettingChanged();
            RefreshCatalogSourcesList();
        }

        private async void CatalogSourceEnabled_Changed(object? sender, RoutedEventArgs e)
        {
            if (_suppressCatalogSourceUiEvents)
                return;

            if (sender is not CheckBox checkBox || checkBox.Tag is not string sourceId)
                return;

            _settings.EnsureInitialized();
            var source = _settings.AppCatalogSources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null)
                return;

            source.Enabled = checkBox.IsChecked == true;
            OnSettingChanged();

            await _gameManager.CatalogService.RefreshUpdateAvailableAsync(source);
            RefreshCatalogSourcesList();

            await _gameManager.LoadGamesAsync();
            ApplySorting();
        }

        private async void RefreshCatalogSources_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                await _gameManager.CatalogService.RefreshAllSourcesAsync(_gameManager.HttpClient, _settings);
                OnSettingChanged();
                _settings = AppSettings.Load();
                _settings.EnsureInitialized();
                RefreshCatalogSourcesList();

                if (_settings.AppCatalogSources.Any(s => s.Enabled && s.UpdateAvailable))
                {
                    await ShowMessageBoxAsync(
                        "Catalog updates are available. Open App Catalog to review and sync apps.",
                        "Updates Available");
                }
                else
                {
                    await ShowMessageBoxAsync("Catalog sources refreshed.", "Refresh Complete");
                }
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to refresh catalog sources: {ex.Message}", "Refresh Error");
            }
        }

        private async Task NotifyCatalogUpdatesIfNeededAsync()
        {
            if (!_settings.LocalFirstCatalogMigrationComplete)
            {
                await RunLocalFirstCatalogMigrationAsync();
                return;
            }

            if (_settings.AppCatalogSources.Any(s => s.Enabled && s.UpdateAvailable))
            {
                var openCatalog = await ShowMessageBoxAsync(
                    "Catalog updates are available. Open App Catalog now to review changes?",
                    "Catalog Updates",
                    true);

                if (openCatalog)
                    await OpenAppCatalogForReviewAsync();
            }
        }

        private async Task OpenAppCatalogForReviewAsync()
        {
            ShowAppCatalogSourcesView();

            var source = _settings.AppCatalogSources
                .Where(s => s.Enabled && s.PendingReviewCount > 0)
                .OrderByDescending(s => s.PendingReviewCount)
                .FirstOrDefault();

            if (source != null)
                await OpenCatalogReviewAsync(source.Id);
        }

        private async Task RunLocalFirstCatalogMigrationAsync()
        {
            AppCatalogService.MigrateLegacyCatalogSources(_settings);

            await ShowFirstRunWelcomeAsync();

            var defaultSource = _settings.AppCatalogSources
                .FirstOrDefault(CommunityCatalogDefaults.IsDefaultSource);

            if (defaultSource != null && defaultSource.Enabled)
                await OpenCatalogReviewAsync(defaultSource.Id, CatalogReviewFilter.New);
            else
                await OpenAppCatalogForReviewAsync();

            _settings.LocalFirstCatalogMigrationComplete = true;
            OnSettingChanged();
        }

        private Task ShowFirstRunWelcomeAsync() =>
            ShowWelcomeMessageBoxAsync(
                CommunityCatalogDefaults.FirstRunWelcomeMessage,
                CommunityCatalogDefaults.FirstRunWelcomeTitle);

        private static string GetCatalogReviewFilterTag(CatalogReviewFilter filter)
        {
            foreach (var (tag, value) in CatalogReviewFilters)
            {
                if (value == filter)
                    return tag;
            }

            return "All";
        }

        private async void ReviewCatalogSource_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string sourceId })
                return;

            await OpenCatalogReviewAsync(sourceId);
        }

        private async Task OpenCatalogReviewAsync(
            string sourceId,
            CatalogReviewFilter initialFilter = CatalogReviewFilter.All)
        {
            var source = _settings.AppCatalogSources.FirstOrDefault(s => s.Id == sourceId);
            if (source == null)
                return;

            await _gameManager.CatalogService.FetchSourceAsync(_gameManager.HttpClient, source);
            OnSettingChanged();

            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var externalApps = await _gameManager.CatalogService.LoadCachedAppsAsync(source.Id);

            _activeCatalogSyncSource = source;
            _catalogSyncViewModel.ReviewFilter = initialFilter;
            _catalogSyncViewModel.Refresh(source, localApps, externalApps);

            RefreshCatalogReviewFilterButtons(GetCatalogReviewFilterTag(initialFilter));
            ApplyCatalogSyncFilter();
            UpdateCatalogSyncBulkButtons();

            ShowAppCatalogReviewView(source);
        }

        private void ApplyCatalogSyncFilter()
        {
            CatalogSyncRows.Clear();
            var isHiddenFilter = _catalogSyncViewModel.ReviewFilter == CatalogReviewFilter.Hidden;
            foreach (var row in _catalogSyncViewModel.GetFilteredRows())
            {
                row.ShowHideButton = !isHiddenFilter &&
                                     _activeCatalogSyncSource != null &&
                                     !CatalogCompareService.IsHiddenFromReview(_activeCatalogSyncSource, row.Repository);
                row.ShowUnhideButton = isHiddenFilter;
                CatalogSyncRows.Add(row);
            }

            if (this.FindControl<TextBlock>("CatalogReviewVersionText") is TextBlock versionText)
                versionText.Text = _catalogSyncViewModel.VersionSummary;

            if (this.FindControl<TextBlock>("CatalogSyncEmptyText") is TextBlock emptyText)
            {
                var showNeedsReviewComplete = _catalogSyncViewModel.ShowNeedsReviewCompleteState;
                emptyText.Text = isHiddenFilter
                    ? "No hidden apps for this source."
                    : "No apps match this filter.";
                emptyText.IsVisible = CatalogSyncRows.Count == 0 && !showNeedsReviewComplete;
            }

            if (this.FindControl<StackPanel>("CatalogSyncNeedsReviewEmptyPanel") is StackPanel needsReviewEmptyPanel)
                needsReviewEmptyPanel.IsVisible = _catalogSyncViewModel.ShowNeedsReviewCompleteState;

            UpdateCatalogReviewFilterChipLabels();
        }

        private void CatalogSyncBackToLibrary_Click(object? sender, RoutedEventArgs e) =>
            ShowLibraryView();

        private void CatalogSyncBackToSources_Click(object? sender, RoutedEventArgs e) =>
            ShowAppCatalogSourcesView();

        private void UpdateCatalogSyncBulkButtons()
        {
            if (this.FindControl<Button>("CatalogSyncAddAllButton") is Button addAllButton)
                addAllButton.IsEnabled = _catalogSyncViewModel.ExternalOnlyCount > 0;

            if (this.FindControl<Button>("CatalogSyncReplaceAllButton") is Button replaceAllButton)
                replaceAllButton.IsEnabled = _catalogSyncViewModel.ChangedCount > 0;

            if (this.FindControl<Button>("CatalogSyncAcknowledgeButton") is Button skipReviewButton)
                skipReviewButton.IsVisible = _catalogSyncViewModel.ShowSkipReviewButton;
        }

        private CatalogSyncRowItem? FindCatalogSyncRow(string repository) =>
            _catalogSyncViewModel.AllRows.FirstOrDefault(r =>
                r.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase));

        private IReadOnlyList<CatalogSyncRowItem> GetActionableCatalogSyncRows() =>
            _catalogSyncViewModel.AllRows
                .Where(r => _activeCatalogSyncSource != null &&
                            CatalogCompareService.IsActionableRow(r, _activeCatalogSyncSource))
                .ToList();

        private async Task ApplyCatalogSyncLocalAppsAsync(List<GameInfo> localApps)
        {
            await _gameManager.CatalogService.SaveLocalAppsAsync(localApps);
            await _gameManager.LoadGamesAsync();
            ApplySorting();

            if (_activeCatalogSyncSource != null)
            {
                await _gameManager.CatalogService.RefreshUpdateAvailableAsync(_activeCatalogSyncSource);
                OnSettingChanged();
                RefreshCatalogSourcesList();
            }

            await RefreshActiveCatalogSyncRowsAsync(localApps);
        }

        private async Task RefreshActiveCatalogSyncRowsAsync(List<GameInfo>? localApps = null)
        {
            if (_activeCatalogSyncSource == null)
                return;

            localApps ??= await _gameManager.CatalogService.LoadLocalAppsAsync();
            var externalApps = await _gameManager.CatalogService.LoadCachedAppsAsync(_activeCatalogSyncSource.Id);
            _catalogSyncViewModel.Refresh(_activeCatalogSyncSource, localApps, externalApps);
            ApplyCatalogSyncFilter();
            UpdateCatalogSyncBulkButtons();
        }

        private async Task AfterCatalogSyncMutationAsync()
        {
            if (_activeCatalogSyncSource == null)
                return;

            await _gameManager.CatalogService.RefreshUpdateAvailableAsync(_activeCatalogSyncSource);
            OnSettingChanged();
            RefreshCatalogSourcesList();
            await RefreshActiveCatalogSyncRowsAsync();
        }

        private async void CatalogSyncAddAll_Click(object? sender, RoutedEventArgs e)
        {
            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var updated = CatalogCompareService.ApplyAddAllExternalOnly(localApps, GetActionableCatalogSyncRows());
            await ApplyCatalogSyncLocalAppsAsync(updated);
        }

        private async void CatalogSyncReplaceAll_Click(object? sender, RoutedEventArgs e)
        {
            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var updated = CatalogCompareService.ApplyReplaceAllChanged(localApps, GetActionableCatalogSyncRows());
            await ApplyCatalogSyncLocalAppsAsync(updated);
        }

        private async void CatalogSyncAcknowledge_Click(object? sender, RoutedEventArgs e)
        {
            if (_activeCatalogSyncSource == null)
                return;

            _gameManager.CatalogService.AcknowledgeSourceVersion(_activeCatalogSyncSource);
            OnSettingChanged();
            RefreshCatalogSourcesList();
            ApplyCatalogSyncFilter();
            UpdateCatalogSyncBulkButtons();
        }

        private async void CatalogSyncRowIgnore_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository } || _activeCatalogSyncSource == null)
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null || !row.CanIgnore)
                return;

            CatalogCompareService.IgnoreChangesForCurrentVersion(_activeCatalogSyncSource, repository);
            await AfterCatalogSyncMutationAsync();
        }

        private async void CatalogSyncRowHide_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository } || _activeCatalogSyncSource == null)
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null)
                return;

            CatalogCompareService.HideFromReview(_activeCatalogSyncSource, repository);
            await AfterCatalogSyncMutationAsync();
        }

        private async void CatalogSyncRowUnhide_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository } || _activeCatalogSyncSource == null)
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null)
                return;

            CatalogCompareService.UnhideFromReview(_activeCatalogSyncSource, repository);
            await AfterCatalogSyncMutationAsync();
        }

        private async void CatalogSyncRowAdd_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository })
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null)
                return;

            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var updated = CatalogCompareService.ApplyRowAdd(localApps, row);
            CatalogCompareService.ClearIgnoredChange(_activeCatalogSyncSource!, row.Repository);
            await ApplyCatalogSyncLocalAppsAsync(updated);
        }

        private async void CatalogSyncRowReplace_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository })
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null)
                return;

            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var updated = CatalogCompareService.ApplyRowReplace(localApps, row);
            CatalogCompareService.ClearIgnoredChange(_activeCatalogSyncSource!, row.Repository);
            await ApplyCatalogSyncLocalAppsAsync(updated);
        }

        private async void CatalogSyncRowMerge_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string repository })
                return;

            var row = FindCatalogSyncRow(repository);
            if (row == null)
                return;

            var localApps = await _gameManager.CatalogService.LoadLocalAppsAsync();
            var updated = CatalogCompareService.ApplyRowMerge(localApps, row);
            CatalogCompareService.ClearIgnoredChange(_activeCatalogSyncSource!, row.Repository);
            await ApplyCatalogSyncLocalAppsAsync(updated);
        }

        private async Task<(string Name, string Location)?> ShowAddCatalogSourceDialogAsync()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null)
                return null;

            var nameBox = new TextBox
            {
                Watermark = "Display name",
                Margin = new Thickness(0, 0, 0, 8),
            };
            var locationBox = new TextBox
            {
                Watermark = "URL or file path to apps.json",
                Margin = new Thickness(0, 0, 0, 8),
            };

            var browseButton = new Button
            {
                Content = "Browse…",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12),
            };

            browseButton.Click += async (_, _) =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select apps.json",
                    FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } },
                    AllowMultiple = false,
                });

                if (files?.Count > 0)
                    locationBox.Text = files[0].Path.LocalPath;
            };

            (string Name, string Location)? result = null;
            var addButton = new Button { Content = "Add", MinWidth = 80 };
            var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };

            var dialog = new Window
            {
                Title = "Add Catalog Source",
                Width = 420,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Name",
                            FontWeight = FontWeight.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4),
                        },
                        nameBox,
                        new TextBlock
                        {
                            Text = "Location",
                            FontWeight = FontWeight.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4),
                        },
                        locationBox,
                        browseButton,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Spacing = 10,
                            Children = { addButton, cancelButton },
                        },
                    },
                },
            };

            addButton.Click += (_, _) =>
            {
                var name = nameBox.Text?.Trim() ?? "";
                var location = locationBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
                    return;

                result = (name, location);
                dialog.Close();
            };

            cancelButton.Click += (_, _) => dialog.Close();

            await dialog.ShowDialog(desktop.MainWindow);
            return result;
        }

        private void OnSettingChanged()
        {
            if (_suppressSettingsUiEvents)
                return;

            try
            {
                AppSettings.Save(_settings);
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to save settings: {ex.Message}", "Save Error");
            }
        }

        private void StartFullscreenCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.StartFullscreen = true;
                OnSettingChanged();
            }
        }

        private void StartFullscreenCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.StartFullscreen = false;
                OnSettingChanged();
            }
        }

        private void RoundWindowCornersCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.WindowBorderRounding = true;
                ApplyRoundedCorners();
                OnPropertyChanged(nameof(WindowBackground));
                OnSettingChanged();
            }
        }

        private void RoundWindowCornersCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.WindowBorderRounding = false;
                ApplyRoundedCorners();
                OnPropertyChanged(nameof(WindowBackground));
                OnSettingChanged();
            }
        }

        private void ShowOSTopBarCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ShowOSTopBar = true;
                OnPropertyChanged(nameof(ExtendClientAreaEnabled));
                OnPropertyChanged(nameof(ChromeHints));
                OnPropertyChanged(nameof(WindowBackground));
                OnSettingChanged();
            }
        }

        private void ShowOSTopBarCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ShowOSTopBar = false;
                OnPropertyChanged(nameof(ExtendClientAreaEnabled));
                OnPropertyChanged(nameof(ChromeHints));
                OnPropertyChanged(nameof(WindowBackground));
                OnSettingChanged();
            }
        }

        private void UpdateGridLayoutVisibility()
        {
            if (_settings == null)
                return;

            var useGrid = _settings.UseGridView;
            var compact = _settings.GridCompactCards;

            if (ClassicGridViewControl != null)
                ClassicGridViewControl.IsVisible = useGrid && !compact;

            if (CompactGridViewControl != null)
                CompactGridViewControl.IsVisible = useGrid && compact;
        }

        private void UseGridViewCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _settings.UseGridView = true;
            UpdateGridLayoutVisibility();
            OnSettingChanged();
        }

        private void UseGridViewCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _settings.UseGridView = false;
            UpdateGridLayoutVisibility();
            OnSettingChanged();
        }

        private void GridCompactCardsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _settings.GridCompactCards = true;
            UpdateGridLayoutVisibility();
            OnSettingChanged();
        }

        private void GridCompactCardsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _settings.GridCompactCards = false;
            UpdateGridLayoutVisibility();
            OnSettingChanged();
        }

        private void SlotSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.SlotSize = (int)e.NewValue;
                OnSettingChanged();
            }
        }

        private void ActionButtonSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ActionButtonSize = (int)e.NewValue;
                OnSettingChanged();
            }
        }

        private void IconOpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.IconOpacity = (float)e.NewValue;
                OnSettingChanged();
            }
        }

        private void IconSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.IconSize = (int)e.NewValue;
                OnSettingChanged();
            }
        }

        private void IconMarginSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.IconMargin = (int)e.NewValue;
                OnSettingChanged();
            }
        }

        private void TextMarginSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.SlotTextMargin = (int)e.NewValue;
                OnSettingChanged();
            }
        }

        private void IconFillCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.IconFill = true;
                OnSettingChanged();
            }
        }

        private void IconFillCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.IconFill = false;
                OnSettingChanged();
            }
        }

        private void PlatformAuto_Click(object sender, RoutedEventArgs e)
        {             
            if (_settings != null)
            {
                _settings.Platform = TargetOS.Auto;
                PlatformString = "Automatic";
                OnSettingChanged();
            }
        }

        private void PlatformWindows_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.Platform = TargetOS.Windows;
                PlatformString = "Windows";
                OnSettingChanged();
            }
        }

        private void PlatformMacOS_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.Platform = TargetOS.MacOS;
                PlatformString = "macOS";
                OnSettingChanged();
            }
        }

        private void PlatformLinuxX64_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.Platform = TargetOS.LinuxX64;
                PlatformString = "Linux x64";
                OnSettingChanged();
            }
        }

        private void PlatformLinuxARM64_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.Platform = TargetOS.LinuxARM64;
                PlatformString = "Linux ARM64";
                OnSettingChanged();
            }
        }

        private async void CheckforUpdates_Click(object sender, RoutedEventArgs e)
        {
            IsCheckingUpdates = true;
            try
            {
                if (_app != null)
                    await _app.CheckForAppUpdatesManually();

                await _gameManager.CheckAllUpdatesAsync();
                ApplySorting();
                RefreshUpdateCheckStatus(DateTime.Now);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to check for updates: {ex.Message}", "Error");
                RefreshUpdateCheckStatus();
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        private string GetLastCheckedText()
        {
            if (_lastUpdateCheckTime == null)
                return string.Empty;

            var timeSince = DateTime.Now - _lastUpdateCheckTime.Value;

            if (timeSince.TotalMinutes < 1)
                return "checked just now";
            if (timeSince.TotalMinutes < 2)
                return "checked 1 minute ago";
            if (timeSince.TotalMinutes < 60)
                return $"checked {timeSince.Minutes} minutes ago";
            if (timeSince.TotalHours < 2)
                return "checked 1 hour ago";
            if (timeSince.TotalHours < 24)
                return $"checked {timeSince.Hours} hours ago";
            if (timeSince.TotalDays < 2)
                return "checked 1 day ago";

            return $"checked {timeSince.Days} days ago";
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;
            if (game != null && !string.IsNullOrEmpty(game.FolderName))
            {
                try
                {
                    string folderPath = game.GetInstallPath(_gameManager.GamesFolder);
                    OpenUrl(folderPath);
                }
                catch (Exception ex)
                {
                    _ = ShowMessageBoxAsync($"Failed to open folder: {ex.Message}", "Action Error");
                }
            }
            else
            {
                _ = ShowMessageBoxAsync("Unable to identify the game folder.", "Action Error");
            }
        }

        private async void ForceUpdate_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                await PersistGameVersionPreferencesAsync(game, null, null);
                await game.ForceUpdateAsync(_gameManager.HttpClient, _gameManager.GamesFolder);
                ApplySorting();
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to force update {game.Name}: {ex.Message}", "Force Update Failed");
            }
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "https://github.com/tgeorgiadis/quiver/";
                OpenUrl(url);
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to open Github link: {ex.Message}", "Action Error");
            }
        }

        private async void LaunchGameMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                await game.PerformActionAsync(_gameManager.HttpClient, _gameManager.GamesFolder, _settings);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to launch {game.Name}: {ex.Message}", "Launch Error");
            }
        }

        private async void LocateExistingInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.CommandParameter is not GameInfo game)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Select existing install folder for {game.Name}",
                AllowMultiple = false
            });

            if (folders == null || folders.Count == 0)
                return;

            var selectedPath = folders[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            {
                await ShowMessageBoxAsync("The selected install folder could not be found.", "Install Folder Not Found");
                return;
            }

            var folderName = Path.GetFileName(selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName))
            {
                await ShowMessageBoxAsync("The selected folder does not have a valid folder name.", "Invalid Folder");
                return;
            }

            game.InstallPath = selectedPath;
            game.FolderName = folderName;

            await PersistGameInstallLocationAsync(game);
            await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder, forceUpdateCheck: true);

            ApplySorting();
            UpdateContinueButtonState();
        }

        private async void UpdateNowMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.CommandParameter is not GameInfo game)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var anchor = ResolveMenuAnchor(menuItem) ?? FindGameMenuAnchor(game);
            if (anchor == null)
            {
                await ShowMessageBoxAsync("Unable to open the update menu for this game.", "Error");
                return;
            }

            await HandleUpdateNowAsync(anchor, game);
        }

        private async void SkipUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.CommandParameter is not GameInfo game)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            await HandleSkipUpdateAsync(game);
        }

        private async void ChangeVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.CommandParameter is not GameInfo game)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var anchor = ResolveMenuAnchor(menuItem) ?? FindGameMenuAnchor(game);
            if (anchor == null)
            {
                await ShowMessageBoxAsync("Unable to open the version menu for this game.", "Error");
                return;
            }

            await HandleChangeVersionAsync(anchor, game);
        }

        private void OpenGitHubPage_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game != null && !string.IsNullOrEmpty(game.Repository))
            {
                try
                {
                    var githubUrl = $"https://github.com/{game.Repository}";
                    OpenUrl(githubUrl);
                }
                catch (Exception ex)
                {
                    _ = ShowMessageBoxAsync($"Failed to open GitHub page: {ex.Message}", "Error");
                }
            }
            else _ = ShowMessageBoxAsync($"Failed to open GitHub page", "Error");
        }

        private async void SetCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var selectedGame = menuItem?.CommandParameter as GameInfo;
            if (selectedGame == null)
            {
                _ = ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            bool hasExistingCustomIcon = !string.IsNullOrEmpty(selectedGame.CustomIconPath);
            string confirmMessage = hasExistingCustomIcon
                ? $"Replace the existing custom icon for {selectedGame.Name}?"
                : $"Set custom icon for {selectedGame.Name}?";

            if (hasExistingCustomIcon)
            {
                var confirmResult = await ShowMessageBoxAsync(confirmMessage, "Confirm Icon Replacement", true);
                if (!confirmResult)
                    return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select Custom Icon for {selectedGame.Name}",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.ico" }
                    },
                    new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG Files") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("WebP Files") { Patterns = new[] { "*.webp" } },
                    new FilePickerFileType("Bitmap Files") { Patterns = new[] { "*.bmp" } },
                    new FilePickerFileType("Icon Files") { Patterns = new[] { "*.ico" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                },
                AllowMultiple = false
            });

            if (files?.Count > 0)
            {
                try
                {
                    var filePath = files[0].Path.LocalPath;
                    selectedGame.SetCustomIcon(filePath, _gameManager.CacheFolder);
                }
                catch (Exception ex)
                {
                    string errorMessage = hasExistingCustomIcon
                        ? $"Failed to replace custom icon: {ex.Message}"
                        : $"Failed to set custom icon: {ex.Message}";
                    _ = ShowMessageBoxAsync(errorMessage, "Error");
                }
            }
        }

        private async void RemoveCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var selectedGame = menuItem?.CommandParameter as GameInfo;
            if (selectedGame == null)
            {
                _ = ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            if (string.IsNullOrEmpty(selectedGame.CustomIconPath))
            {
                _ = ShowMessageBoxAsync($"{selectedGame.Name} is already using the default icon.", "No Custom Icon");
                return;
            }

            var result = await ShowMessageBoxAsync($"Remove custom icon for {selectedGame.Name}?", "Confirm Removal", true);
            if (result)
            {
                try
                {
                    selectedGame.RemoveCustomIcon();
                }
                catch (Exception ex)
                {
                    _ = ShowMessageBoxAsync($"Failed to remove custom icon: {ex.Message}", "Error");
                }
            }
        }

        private async void DeleteGameFromLibrary_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null) return;

            if (game.Status == GameStatus.NotInstalled)
            {
                _ = ShowMessageBoxAsync($"{game.Name} is not installed.", "Nothing to Delete");
                return;
            }

            var result = await ShowMessageBoxAsync(
                $"Are you sure you want to delete {game.Name}?\n\nThis will permanently remove all game files and cannot be undone.",
                "Confirm Deletion",
                true);

            if (result)
            {
                try
                {
                    if (string.IsNullOrEmpty(game.FolderName))
                    {
                        await ShowMessageBoxAsync($"Failed to delete {game.Name}: game folder is not configured.", "Deletion Failed");
                        return;
                    }

                    var gamePath = game.GetInstallPath(_gameManager.GamesFolder);

                    if (Directory.Exists(gamePath))
                    {
                        game.Status = GameStatus.Installing;
                        game.IsLoading = true;

                        await Task.Run(() => Directory.Delete(gamePath, true));

                        game.IsLoading = false;
                        await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder);

                        UpdateContinueButtonState();
                    }
                    else
                    {
                        await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder);
                    }
                }
                catch (Exception ex)
                {
                    game.IsLoading = false;
                    _ = ShowMessageBoxAsync($"Failed to delete {game.Name}: {ex.Message}", "Deletion Failed");
                }
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", $"\"{url}\"");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", $"\"{url}\"");
                }
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to open URL: {ex.Message}", "Error");
            }
        }

        private async Task ShowWelcomeMessageBoxAsync(string message, string title)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var messageBox = new Window
                    {
                        Title = title,
                        Width = 480,
                        Height = 260,
                        CanResize = false,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new StackPanel
                        {
                            Margin = new Thickness(20),
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = message,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 0, 0, 20),
                                },
                                new Button
                                {
                                    Content = "OK",
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    MinWidth = 80,
                                },
                            },
                        },
                    };

                    if (((StackPanel)messageBox.Content).Children[1] is Button okButton)
                        okButton.Click += (_, _) => messageBox.Close();

                    await messageBox.ShowDialog(desktop.MainWindow);
                }
            });
        }

        private async Task ShowMessageBoxAsync(string message, string title)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var messageBox = new Window
                    {
                        Title = title,
                        Width = 400,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new StackPanel
                        {
                            Margin = new Thickness(20),
                            Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) },
                        new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center }
                    }
                        }
                    };
                    if (((StackPanel)messageBox.Content).Children[1] is Button okButton)
                    {
                        okButton.Click += (s, e) => messageBox.Close();
                    }
                    await messageBox.ShowDialog(desktop.MainWindow);
                }
            });
        }

        private void UnhideAllGamesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.ListScope = AppListScope.AllApps;
                OnSettingChanged();
                _gameManager.SetListScope(AppListScope.AllApps, _settings);
                RefreshSidebarFilterSelection();
                UpdateContinueButtonState();
                ApplySorting();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to show all apps: {ex.Message}", "Error");
            }
        }

        private void HideNonInstalledButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.ListScope = AppListScope.InstalledOnly;
                OnSettingChanged();
                _gameManager.SetListScope(AppListScope.InstalledOnly, _settings);
                RefreshSidebarFilterSelection();
                UpdateContinueButtonState();
                ApplySorting();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to filter installed apps: {ex.Message}", "Error");
            }
        }

        private void AddDisplayFilter_Click(object? sender, RoutedEventArgs e)
        {
            ShowDisplayFilterOverlay(null);
        }

        private void ToggleTagDisplayFilter(string filterId)
        {
            if (string.Equals(_settings.ActiveTagDisplayFilterId, filterId, StringComparison.OrdinalIgnoreCase))
                _settings.ActiveTagDisplayFilterId = null;
            else
                _settings.ActiveTagDisplayFilterId = filterId;

            OnSettingChanged();
            _gameManager.ApplyTagDisplayFilter(_settings);
            RefreshTagDisplayFiltersUI();
            RefreshSidebarFilterSelection();
            UpdateContinueButtonState();
            ApplySorting();
        }

        private void TagDisplayFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tagFilterSuppressRowClick)
            {
                _tagFilterSuppressRowClick = false;
                return;
            }

            if (sender is not Button button || button.Tag is not string filterId)
                return;

            ToggleTagDisplayFilter(filterId);
        }

        private void TagDisplayFilterReorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border handle || handle.Tag is not string filterId)
                return;

            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
                return;

            if (TagDisplayFiltersItemsControl == null)
                return;

            _tagFilterSuppressRowClick = true;
            _tagFilterDragId = filterId;
            _tagFilterDragStartY = e.GetPosition(TagDisplayFiltersItemsControl).Y;
            _tagFilterDragActive = false;
            _tagFilterDragPointer = e.Pointer;
            _tagFilterDragRowButton = handle.GetVisualAncestors().OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("display-filter-row"));

            if (_tagFilterDragRowButton != null)
                BeginTagDisplayFilterDragVisuals(_tagFilterDragRowButton);

            TagDisplayFiltersItemsControl.AddHandler(
                InputElement.PointerMovedEvent,
                TagDisplayFilterDragPointerMoved,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TagDisplayFiltersItemsControl.AddHandler(
                InputElement.PointerReleasedEvent,
                TagDisplayFilterDragPointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            e.Pointer.Capture(TagDisplayFiltersItemsControl);
            e.Handled = true;
        }

        private void TagDisplayFilterDragPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_tagFilterDragId == null || TagDisplayFiltersItemsControl == null)
                return;

            if (!e.GetCurrentPoint(TagDisplayFiltersItemsControl).Properties.IsLeftButtonPressed)
                return;

            var currentY = e.GetPosition(TagDisplayFiltersItemsControl).Y;
            if (!_tagFilterDragActive && Math.Abs(currentY - _tagFilterDragStartY) >= 3)
            {
                _tagFilterDragActive = true;
                _tagFilterOrderAtDragStart = _settings.TagDisplayFilters.Select(f => f.Id).ToList();
                if (TryMeasureTagFilterDragMetrics(_tagFilterDragId, out var listTop, out var rowStride))
                {
                    _tagFilterDragListTop = listTop;
                    _tagFilterDragRowStride = rowStride;
                }

                ReapplyTagDisplayFilterDragVisuals();
                _tagFilterDragPointer?.Capture(TagDisplayFiltersItemsControl);
            }

            if (!_tagFilterDragActive)
                return;

            var currentIndex = GetTagDisplayFilterIndex(_tagFilterDragId);
            if (currentIndex < 0 || _tagFilterDragRowStride <= 0)
                return;

            var targetIndex = TagDisplayFilterDragDrop.ResolveInsertIndex(
                TagDisplayFilters.Count,
                currentIndex,
                currentY,
                _tagFilterDragListTop,
                _tagFilterDragRowStride);

            if (targetIndex != currentIndex)
                PreviewMoveTagDisplayFilter(_tagFilterDragId, targetIndex);

            e.Handled = true;
        }

        private void TagDisplayFilterDragPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_tagFilterDragId == null)
                return;

            var didDrag = _tagFilterDragActive;
            var orderAtStart = _tagFilterOrderAtDragStart;

            EndTagFilterDragSession();

            if (e.Pointer.Captured != null)
                e.Pointer.Capture(null);

            if (didDrag)
            {
                if (orderAtStart != null &&
                    !orderAtStart.SequenceEqual(_settings.TagDisplayFilters.Select(f => f.Id)))
                {
                    OnSettingChanged();
                }
            }
            else
            {
                _tagFilterSuppressRowClick = true;
            }

            e.Handled = true;
        }

        private void BeginTagDisplayFilterDragVisuals(Button row)
        {
            row.Classes.Add("dragging");
            row.ZIndex = 10;
            _tagFilterDragRowButton = row;
            SetTagDisplayFilterTooltipsEnabled(false);
        }

        private void SetTagDisplayFilterTooltipsEnabled(bool enabled)
        {
            if (TagDisplayFiltersItemsControl == null)
                return;

            foreach (var row in TagDisplayFiltersItemsControl.GetVisualDescendants()
                         .OfType<Button>()
                         .Where(b => b.Classes.Contains("display-filter-row")))
            {
                ToolTip.SetServiceEnabled(row, enabled);
                if (!enabled)
                    ToolTip.SetIsOpen(row, false);
            }
        }

        private void ReapplyTagDisplayFilterDragVisuals()
        {
            if (_tagFilterDragId == null)
                return;

            var row = GetTagDisplayFilterRow(_tagFilterDragId);
            if (row == null)
                return;

            row.Classes.Add("dragging");
            row.ZIndex = 10;
            _tagFilterDragRowButton = row;
        }

        private Button? GetTagDisplayFilterRow(string filterId)
        {
            if (TagDisplayFiltersItemsControl == null)
                return null;

            return TagDisplayFiltersItemsControl.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(b =>
                    b.Classes.Contains("display-filter-row") &&
                    b.Tag is string id &&
                    string.Equals(id, filterId, StringComparison.OrdinalIgnoreCase));
        }

        private int GetTagDisplayFilterIndex(string? filterId)
        {
            if (filterId == null)
                return -1;

            return _settings.TagDisplayFilters.FindIndex(f =>
                string.Equals(f.Id, filterId, StringComparison.OrdinalIgnoreCase));
        }

        private void PreviewMoveTagDisplayFilter(string filterId, int targetIndex)
        {
            _settings.EnsureInitialized();

            var fromIndex = GetTagDisplayFilterIndex(filterId);
            if (fromIndex < 0 || fromIndex == targetIndex)
                return;

            TagDisplayFilterReorder.Move(_settings.TagDisplayFilters, fromIndex, targetIndex);

            var uiFromIndex = TagDisplayFilters
                .Select((item, index) => new { item, index })
                .FirstOrDefault(x => string.Equals(x.item.Id, filterId, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;

            if (uiFromIndex >= 0)
            {
                var item = TagDisplayFilters[uiFromIndex];
                TagDisplayFilters.RemoveAt(uiFromIndex);
                TagDisplayFilters.Insert(targetIndex, item);
            }

            _tagFilterDragRowButton = GetTagDisplayFilterRow(filterId);
        }

        private void ClearTagDisplayFilterDragVisuals()
        {
            SetTagDisplayFilterTooltipsEnabled(true);

            if (_tagFilterDragId != null)
            {
                var row = GetTagDisplayFilterRow(_tagFilterDragId);
                if (row != null)
                {
                    row.Classes.Remove("dragging");
                    row.ZIndex = 0;
                }
            }

            _tagFilterDragRowButton?.Classes.Remove("dragging");
        }

        private void EndTagFilterDragSession()
        {
            if (TagDisplayFiltersItemsControl != null)
            {
                TagDisplayFiltersItemsControl.RemoveHandler(
                    InputElement.PointerMovedEvent,
                    TagDisplayFilterDragPointerMoved);
                TagDisplayFiltersItemsControl.RemoveHandler(
                    InputElement.PointerReleasedEvent,
                    TagDisplayFilterDragPointerReleased);
            }

            ClearTagDisplayFilterDragVisuals();

            _tagFilterDragId = null;
            _tagFilterDragActive = false;
            _tagFilterDragRowButton = null;
            _tagFilterDragPointer = null;
            _tagFilterOrderAtDragStart = null;
            _tagFilterDragListTop = 0;
            _tagFilterDragRowStride = 0;
            _tagFilterSuppressRowClick = false;
        }

        private bool TryMeasureTagFilterDragMetrics(string filterId, out double listTop, out double rowStride)
        {
            listTop = 0;
            rowStride = 0;

            if (TagDisplayFiltersItemsControl == null)
                return false;

            var row = GetTagDisplayFilterRow(filterId);
            if (row == null)
                return false;

            var transform = row.TransformToVisual(TagDisplayFiltersItemsControl);
            if (transform == null)
                return false;

            var rowHeight = row.Bounds.Height;
            if (rowHeight <= 0)
                return false;

            rowStride = rowHeight + 4;
            var dragIndex = GetTagDisplayFilterIndex(filterId);
            if (dragIndex < 0)
                return false;

            var rowTop = transform.Value.Transform(new Point(0, 0)).Y;
            listTop = rowTop - dragIndex * rowStride;
            return true;
        }

        private void DisplayFilterOverflow_Click(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.Open();
            }
        }

        private static string? GetFilterIdFromSender(object? sender) =>
            sender switch
            {
                Button { Tag: string id } => id,
                MenuItem { Tag: string id } => id,
                _ => null
            };

        private void EditTagDisplayFilter_Click(object? sender, RoutedEventArgs e)
        {
            var filterId = GetFilterIdFromSender(sender);
            if (filterId == null)
                return;

            ShowDisplayFilterOverlay(filterId);
        }

        private void ShowDisplayFilterOverlay(string? filterId)
        {
            _editingDisplayFilterId = filterId;
            _settings.EnsureInitialized();

            if (filterId == null)
            {
                DisplayFilterOverlayTitle.Text = "Add Display Filter";
                DisplayFilterNameTextBox.Text = string.Empty;
                DisplayFilterTagsTextBox.Text = string.Empty;
                DisplayFilterExcludeTagsTextBox.Text = string.Empty;
                SetDisplayFilterMatchModeComboBox(TagFilterMatchMode.Any);
                SetDisplayFilterExcludeMatchModeComboBox(TagFilterMatchMode.Any);
            }
            else
            {
                var filter = _settings.TagDisplayFilters.FirstOrDefault(f => f.Id == filterId);
                if (filter == null)
                    return;

                DisplayFilterOverlayTitle.Text = "Edit Display Filter";
                DisplayFilterNameTextBox.Text = filter.Name;
                DisplayFilterTagsTextBox.Text = TagHelper.FormatTagsForDisplay(filter.Tags);
                DisplayFilterExcludeTagsTextBox.Text = TagHelper.FormatTagsForDisplay(filter.ExcludeTags);
                SetDisplayFilterMatchModeComboBox(filter.MatchMode);
                SetDisplayFilterExcludeMatchModeComboBox(filter.ExcludeMatchMode);
            }

            DisplayFilterOverlay.IsVisible = true;
            DisplayFilterNameTextBox.Focus();
        }

        private void SetDisplayFilterMatchModeComboBox(TagFilterMatchMode matchMode)
        {
            DisplayFilterMatchModeComboBox.SelectedIndex = matchMode == TagFilterMatchMode.All ? 1 : 0;
            UpdateDisplayFilterMatchModeHelpText();
        }

        private TagFilterMatchMode GetSelectedDisplayFilterMatchMode() =>
            DisplayFilterMatchModeComboBox.SelectedIndex == 1
                ? TagFilterMatchMode.All
                : TagFilterMatchMode.Any;

        private void UpdateDisplayFilterMatchModeHelpText()
        {
            DisplayFilterMatchModeHelpText.Text = GetSelectedDisplayFilterMatchMode() == TagFilterMatchMode.All
                ? "Show apps that have every include tag listed. Exclude rules are applied next."
                : "Show apps that have at least one include tag listed. Exclude rules are applied next.";
        }

        private void DisplayFilterMatchModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DisplayFilterMatchModeHelpText == null)
                return;

            UpdateDisplayFilterMatchModeHelpText();
        }

        private void SetDisplayFilterExcludeMatchModeComboBox(TagFilterMatchMode matchMode)
        {
            DisplayFilterExcludeMatchModeComboBox.SelectedIndex = matchMode == TagFilterMatchMode.All ? 1 : 0;
            UpdateDisplayFilterExcludeMatchModeHelpText();
        }

        private TagFilterMatchMode GetSelectedDisplayFilterExcludeMatchMode() =>
            DisplayFilterExcludeMatchModeComboBox.SelectedIndex == 1
                ? TagFilterMatchMode.All
                : TagFilterMatchMode.Any;

        private void UpdateDisplayFilterExcludeMatchModeHelpText()
        {
            DisplayFilterExcludeMatchModeHelpText.Text = GetSelectedDisplayFilterExcludeMatchMode() == TagFilterMatchMode.All
                ? "Hide apps that have every exclude tag listed."
                : "Hide apps that have at least one of these tags.";
        }

        private void DisplayFilterExcludeMatchModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DisplayFilterExcludeMatchModeHelpText == null)
                return;

            UpdateDisplayFilterExcludeMatchModeHelpText();
        }

        private void CancelDisplayFilterEdit_Click(object? sender, RoutedEventArgs e)
        {
            _editingDisplayFilterId = null;
            DisplayFilterOverlay.IsVisible = false;
            DisplayFilterNameTextBox.Text = string.Empty;
            DisplayFilterTagsTextBox.Text = string.Empty;
            DisplayFilterExcludeTagsTextBox.Text = string.Empty;
            SetDisplayFilterMatchModeComboBox(TagFilterMatchMode.Any);
            SetDisplayFilterExcludeMatchModeComboBox(TagFilterMatchMode.Any);
        }

        private void SaveDisplayFilter_Click(object? sender, RoutedEventArgs e)
        {
            var name = DisplayFilterNameTextBox.Text?.Trim();
            var tags = TagHelper.ParseCommaSeparatedTags(DisplayFilterTagsTextBox.Text);
            var excludeTags = TagHelper.ParseCommaSeparatedTags(DisplayFilterExcludeTagsTextBox.Text);

            if (string.IsNullOrWhiteSpace(name))
            {
                _ = ShowMessageBoxAsync("Please enter a filter name.", "Validation Error");
                return;
            }

            if (tags.Count == 0 && excludeTags.Count == 0)
            {
                _ = ShowMessageBoxAsync("Please enter at least one include or exclude tag.", "Validation Error");
                return;
            }

            _settings.EnsureInitialized();

            var duplicate = _settings.TagDisplayFilters.Any(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(f.Id, _editingDisplayFilterId, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                _ = ShowMessageBoxAsync("A filter with this name already exists.", "Duplicate Filter");
                return;
            }

            var isEdit = !string.IsNullOrWhiteSpace(_editingDisplayFilterId);
            var matchMode = GetSelectedDisplayFilterMatchMode();
            var excludeMatchMode = GetSelectedDisplayFilterExcludeMatchMode();
            if (isEdit)
            {
                var filter = _settings.TagDisplayFilters.FirstOrDefault(f => f.Id == _editingDisplayFilterId);
                if (filter == null)
                    return;

                filter.Name = name;
                filter.Tags = tags;
                filter.MatchMode = matchMode;
                filter.ExcludeTags = excludeTags;
                filter.ExcludeMatchMode = excludeMatchMode;
            }
            else
            {
                _settings.TagDisplayFilters.Add(new TagDisplayFilter
                {
                    Name = name,
                    Tags = tags,
                    MatchMode = matchMode,
                    ExcludeTags = excludeTags,
                    ExcludeMatchMode = excludeMatchMode,
                });
            }

            CancelDisplayFilterEdit_Click(null, new RoutedEventArgs());
            OnSettingChanged();
            RefreshTagDisplayFiltersUI();
            RefreshSidebarFilterSelection();

            if (isEdit)
            {
                _gameManager.ApplyTagDisplayFilter(_settings);
                ApplySorting();
            }
        }

        private void DeleteTagDisplayFilter_Click(object? sender, RoutedEventArgs e)
        {
            var filterId = GetFilterIdFromSender(sender);
            if (filterId == null)
                return;

            var filter = _settings.TagDisplayFilters.FirstOrDefault(f => f.Id == filterId);
            if (filter == null)
                return;

            _settings.TagDisplayFilters.Remove(filter);
            if (string.Equals(_settings.ActiveTagDisplayFilterId, filterId, StringComparison.OrdinalIgnoreCase))
                _settings.ActiveTagDisplayFilterId = null;

            OnSettingChanged();
            RefreshTagDisplayFiltersUI();
            RefreshSidebarFilterSelection();
            _gameManager.ApplyTagDisplayFilter(_settings);
            ApplySorting();
        }

        private async Task<string?> PromptForTextAsync(string title, string initialValue)
        {
            var inputBox = new TextBox
            {
                Text = initialValue,
                Width = 320,
                Foreground = Resources["ThemeText"] as IBrush,
                Background = Resources["ThemeBase"] as IBrush,
                BorderBrush = Resources["ThemeBorder"] as IBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
            };

            var dialog = new Window
            {
                Title = title,
                Width = 380,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Resources["ThemeDarker"] as IBrush,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        inputBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children =
                            {
                                new Button { Classes = { "options" }, Content = "Cancel", Tag = false },
                                new Button { Classes = { "options" }, Content = "OK", Tag = true },
                            }
                        }
                    }
                }
            };

            string? result = null;
            var buttonsPanel = (StackPanel)((StackPanel)dialog.Content!).Children[1];
            foreach (var child in buttonsPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Click += (_, _) =>
                    {
                        if (btn.Tag is true)
                            result = inputBox.Text;
                        dialog.Close();
                    };
                }
            }

            await dialog.ShowDialog(this);
            return result;
        }
        private void SortByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            if (SortByComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string sortMode)
            {
                _currentSortBy = sortMode;
                _settings.SortBy = sortMode;
                OnSettingChanged();
                ApplySorting();
            }
        }

        private void ApplySorting()
        {
            if (_gameManager?.Games == null || _gameManager.Games.Count == 0)
            {
                Debug.WriteLine("ApplySorting: No apps to sort");
                return;
            }

            Debug.WriteLine($"ApplySorting: Sorting {_gameManager.Games.Count} apps by {_currentSortBy}");
            _gameGridViewModel.ApplySort(_gameManager.Games, _currentSortBy, _gameManager.GamesFolder);
            Debug.WriteLine("ApplySorting: Completed sorting");
        }

        private DateTime GetLastPlayedTime(GameInfo game) =>
            GameGridViewModel.GetLastPlayedTime(game, _gameManager.GamesFolder);

        private async void HideGame_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                _ = ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                _gameManager.ToggleUserHide(game);
                await _gameManager.LoadGamesAsync();
                ApplySorting();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to hide app: {ex.Message}", "Error");
            }
        }

        private async Task<bool> ShowMessageBoxAsync(string message, string title, bool isQuestion = false)
        {
            if (!isQuestion)
            {
                await ShowMessageBoxAsync(message, title);
                return true;
            }

            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    bool result = false;
                    var messageBox = new Window
                    {
                        Title = title,
                        Width = 450,
                        Height = 170,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new StackPanel
                        {
                            Margin = new Thickness(20),
                            Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                            {
                                new Button
                                {
                                    Content = "Yes",
                                    Margin = new Thickness(0, 0, 10, 0),
                                    MinWidth = 80
                                },
                                new Button
                                {
                                    Content = "No",
                                    MinWidth = 80
                                }
                            }
                        }
                    }
                        }
                    };

                    if (((StackPanel)messageBox.Content).Children[1] is StackPanel buttonPanel &&
                        buttonPanel.Children[0] is Button yesButton &&
                        buttonPanel.Children[1] is Button noButton)
                    {
                        yesButton.Click += (s, e) =>
                        {
                            result = true;
                            messageBox.Close();
                        };

                        noButton.Click += (s, e) =>
                        {
                            result = false;
                            messageBox.Close();
                        };
                    }

                    await messageBox.ShowDialog(desktop.MainWindow);
                    return result;
                }
                return false;
            });
        }
        private void EnableGamepadCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableGamepadInput = true;
                _inputService?.SetGamepadEnabled(true);
                OnSettingChanged();
            }
        }

        private void EnableGamepadCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableGamepadInput = false;
                _inputService?.SetGamepadEnabled(false);
                OnSettingChanged();
            }
        }

        private void CloseAfterLaunchCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.CloseAfterLaunch = true;
                OnPropertyChanged(nameof(CloseAfterLaunch));
                OnSettingChanged();
            }
        }

        private void CloseAfterLaunchCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.CloseAfterLaunch = false;
                OnPropertyChanged(nameof(CloseAfterLaunch));
                OnSettingChanged();
            }
        }

        private void GitHubTokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null && sender is TextBox textBox)
            {
                _settings.GitHubApiToken = textBox.Text ?? string.Empty;
                OnSettingChanged();
            }
        }

        private void BackgroundPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null && sender is TextBox textBox)
            {
                _settings.BackgroundImagePath = textBox.Text ?? string.Empty;
                OnSettingChanged();
            }
        }

        private void LinuxWindowsLaunchCommandTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null && sender is TextBox textBox)
            {
                _settings.LinuxWindowsLaunchCommand = textBox.Text?.Trim() ?? string.Empty;
                OnSettingChanged();
            }
        }

        private System.Threading.CancellationTokenSource? _gamePathUpdateCts;

        private async void GamePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings == null || sender is not TextBox textBox)
                return;

            // Cancel previous update
            var oldCts = _gamePathUpdateCts;
            _gamePathUpdateCts = new System.Threading.CancellationTokenSource();

            // Dispose old token after a delay
            if (oldCts != null)
            {
                var tokenToDispose = oldCts;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    tokenToDispose.Dispose();
                });
            }

            try
            {
                await Task.Delay(500, _gamePathUpdateCts.Token);
                await UpdateGamePath(textBox.Text?.Trim() ?? string.Empty, textBox);
            }
            catch (OperationCanceledException)
            {
                // User is still typing
            }
        }

        private async Task UpdateGamePath(string newPath, TextBox textBox)
        {
            if (_settings.AppsPath == newPath)
                return;

            _settings.AppsPath = newPath;
            OnSettingChanged();

            if (_gameManager == null)
                return;

            if (!string.IsNullOrEmpty(newPath) && !Directory.Exists(newPath))
            {
                var result = await ShowMessageBoxAsync(
                    $"The directory '{newPath}' does not exist. Create it?",
                    "Directory Not Found",
                    true);

                if (!result)
                {
                    textBox.Text = _settings.AppsPath;
                    return;
                }
            }

            try
            {
                await _gameManager.UpdateGamesFolderAsync(_settings.AppsPath);
                await _gameManager.LoadGamesAsync();
                ApplySorting();
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to update apps path: {ex.Message}", "Error");
                textBox.Text = _settings.AppsPath;
            }
        }

        private void CreateGitHubToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string githubTokenUrl = "https://github.com/settings/tokens/new?description=Github-Launcher+Token+for+increased+API+rate+limits";
                OpenUrl(githubTokenUrl);
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to open GitHub token page: {ex.Message}", "Error");
            }
        }

        private void ClearGitHubToken_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.GitHubApiToken = string.Empty;
                if (GitHubTokenTextBox != null)
                    GitHubTokenTextBox.Text = string.Empty;
                OnSettingChanged();
            }
        }

        private async void ClearGamePath_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null)
                return;

            try
            {
                _settings.AppsPath = string.Empty;
                if (GamePathTextBox != null)
                    GamePathTextBox.Text = string.Empty;
                OnSettingChanged();

                if (_gameManager != null)
                {
                    await _gameManager.UpdateGamesFolderAsync(string.Empty);
                    await _gameManager.LoadGamesAsync();
                    ApplySorting();
                }
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to clear games path: {ex.Message}", "Error");
            }
        }

        private async void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Apps Folder",
                    AllowMultiple = false
                });

                if (folders?.Count > 0)
                {
                    var selectedPath = folders[0].Path.LocalPath;

                    if (!Directory.Exists(selectedPath))
                    {
                        _ = ShowMessageBoxAsync("Selected folder does not exist.", "Invalid Selection");
                        return;
                    }

                    if (_settings != null)
                    {
                        _settings.AppsPath = selectedPath;

                        if (GamePathTextBox != null)
                            GamePathTextBox.Text = selectedPath;

                        OnSettingChanged();

                        if (_gameManager != null)
                        {
                            await _gameManager.UpdateGamesFolderAsync(selectedPath);
                            await _gameManager.LoadGamesAsync();
                            ApplySorting();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to select folder: {ex.Message}", "Error");
            }
        }

        private async void ClearIconCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _gameManager.ClearIconCacheAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to clear icon cache: {ex.Message}", "Error");
            }
        }

        private async void SelectBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = StorageProvider;
            var file = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Background Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
            new FilePickerFileType("Image Files")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" }
            }
        }
            });

            if (file.Count > 0)
            {
                var selectedFile = file[0];
                BackgroundImagePath = selectedFile.Path.LocalPath;
                _settings.BackgroundImagePath = BackgroundImagePath;

                AppSettings.Save(_settings);
            }
        }

        private void ClearBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePath = string.Empty;
            _settings.BackgroundImagePath = string.Empty;
            AppSettings.Save(_settings);
        }

        private void BackgroundOpacitySlider_ValueChanged(object sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                BackgroundOpacity = (float)slider.Value;
                _settings.BackgroundOpacity = BackgroundOpacity;
                AppSettings.Save(_settings);
            }
        }

        private async void SelectLauncherMusic_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = StorageProvider;
            var file = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Launcher Music",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
            new FilePickerFileType("Audio Files")
            {
                Patterns = new[] { "*.mp3", "*.wav", "*.ogg", "*.flac", "*.m4a", "*.wma", "*.aac" }
            }
        }
            });

            if (file.Count > 0)
            {
                var selectedFile = file[0];
                LauncherMusicPath = selectedFile.Path.LocalPath;
                _settings.LauncherMusicPath = LauncherMusicPath;
                AppSettings.Save(_settings);

                PlayLauncherMusic(LauncherMusicPath);
            }
        }

        private void ClearLauncherMusic_Click(object sender, RoutedEventArgs e)
        {
            StopLauncherMusic();
            LauncherMusicPath = string.Empty;
            _settings.LauncherMusicPath = string.Empty;
            AppSettings.Save(_settings);
        }

        private string? _editingGameName;
        private string? _editingGameRepository;
        private string? _editingFolderName;
        private GameInfo? _editingGame;

        private Task<List<GameInfo>> LoadGamesFromJsonAsync() =>
            _gameManager.CatalogService.LoadLocalAppsAsync();

        private async Task SaveGamesToJsonAsync(List<GameInfo> appsToSave)
        {
            foreach (var app in appsToSave)
                app.GameManager ??= _gameManager;

            await _gameManager.CatalogService.SaveLocalAppsAsync(appsToSave);
        }

        private async Task SaveGamesToJsonAsync(Dictionary<string, JsonElement> gamesData)
        {
            var imported = _gameManager.CatalogService.ParseAppsFromDictionary(gamesData);
            foreach (var app in imported)
                app.GameManager = _gameManager;

            await _gameManager.CatalogService.SaveLocalAppsAsync(imported);
        }

        private async Task PersistGameVersionPreferencesAsync(GameInfo game, string? preferredVersion, string? skippedUpdateVersion)
        {
            game.SetVersionPreferences(preferredVersion, skippedUpdateVersion);

            var allGames = await LoadGamesFromJsonAsync();
            var matchingGame = allGames.FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(g.Repository) &&
                g.Repository.Equals(game.Repository, StringComparison.OrdinalIgnoreCase));

            if (matchingGame == null)
                return;

            matchingGame.PreferredVersion = game.PreferredVersion;
            matchingGame.SkippedUpdateVersion = game.SkippedUpdateVersion;

            await SaveGamesToJsonAsync(allGames);
        }

        private async Task PersistGameInstallLocationAsync(GameInfo game)
        {
            var allGames = await LoadGamesFromJsonAsync();
            var matchingGame = allGames.FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(g.Repository) &&
                g.Repository.Equals(game.Repository, StringComparison.OrdinalIgnoreCase));

            if (matchingGame == null)
                return;

            matchingGame.FolderName = game.FolderName;
            matchingGame.InstallPath = game.InstallPath;

            await SaveGamesToJsonAsync(allGames);
        }

        private void AddNewEntryButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowEntryFormOverlay(forCreate: true);
        }

        private void ShowEntryFormOverlay(bool forCreate, GameInfo? gameToEdit = null)
        {
            SettingsPanel.IsVisible = false;
            ChangelogPanel.IsVisible = false;

            if (forCreate)
                ClearForm();
            else if (gameToEdit != null)
                OpenEditForm(gameToEdit);

            _isEntryFormOpen = true;
            EntryFormOverlay.IsVisible = true;
        }

        private void CloseEntryFormOverlay()
        {
            _isEntryFormOpen = false;
            EntryFormOverlay.IsVisible = false;
            ClearForm();
        }

        private void OpenEditForm(GameInfo game)
        {
            ClearForm();

            FormTitleText.Text = "Edit App Entry";
            NewGameNameTextBox.Text = game.Name ?? "";
            NewGameRepoTextBox.Text = game.Repository ?? "";
            NewGameRepoTextBox.IsEnabled = false;
            NewGameFolderTextBox.Text = game.FolderName ?? "";
            NewGameIconTextBox.Text = game.GameIconUrl ?? "";
            if (NewGameTagsTextBox != null)
                NewGameTagsTextBox.Text = TagHelper.FormatTagsForDisplay(game.Tags);
            CreateEditButton.Content = "Update Entry";
            CancelButton.IsVisible = true;

            _editingGameName = game.Name;
            _editingGameRepository = game.Repository;
            _editingFolderName = game.FolderName;
            _editingGame = game;
        }

        private void GameNameTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateGameForm();

        private void GameRepoTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateGameForm();

        private void GameFolderTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateGameForm();

        private void GameIconTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateGameForm();

        private void ValidateGameForm()
        {
            if (!_entryFormShowValidation)
            {
                SetValidationStatus("");
                return;
            }

            var name = NewGameNameTextBox?.Text?.Trim();
            var repository = NewGameRepoTextBox?.Text?.Trim();
            var folderName = NewGameFolderTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(name))
                SetValidationStatus("Error: App name is required");
            else if (string.IsNullOrEmpty(repository))
                SetValidationStatus("Error: Repository is required");
            else if (string.IsNullOrEmpty(folderName))
                SetValidationStatus("Error: Folder name is required");
            else if (!Uri.TryCreate(repository, UriKind.Absolute, out var repoUri) ||
                     (repoUri.Scheme != Uri.UriSchemeHttp && repoUri.Scheme != Uri.UriSchemeHttps))
                SetValidationStatus("Warning: Repository should be a valid URL");
            else if (!IsValidFolderName(folderName))
                SetValidationStatus("Warning: Folder name contains invalid characters");
            else
                SetValidationStatus("All fields are valid");
        }

        private void SetValidationStatus(string message)
        {
            if (ValidationStatusText != null)
                ValidationStatusText.Text = message;

            if (ValidationStatusBorder != null)
                ValidationStatusBorder.IsVisible = !string.IsNullOrEmpty(message);
        }

        private static bool IsValidFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return false;

            var invalidChars = Path.GetInvalidFileNameChars();
            if (folderName.IndexOfAny(invalidChars) >= 0)
                return false;

            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            return !reservedNames.Contains(folderName.ToUpper());
        }

        private async void CreateNewEntry_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _entryFormShowValidation = true;

                var name = NewGameNameTextBox?.Text?.Trim();
                var repository = NewGameRepoTextBox?.Text?.Trim();
                var folderName = NewGameFolderTextBox?.Text?.Trim();
                var iconUrl = NewGameIconTextBox?.Text?.Trim();
                var tags = TagHelper.ParseCommaSeparatedTags(NewGameTagsTextBox?.Text);

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(folderName))
                {
                    ValidateGameForm();
                    _ = ShowMessageBoxAsync("Please fill in all required fields (Name, Repository, Folder Name)", "Validation Error");
                    return;
                }

                var games = await LoadGamesFromJsonAsync();

                if (!string.IsNullOrEmpty(_editingGameRepository))
                {
                    var appToUpdate = games.FirstOrDefault(g => g.Repository == _editingGameRepository);
                    if (appToUpdate == null)
                    {
                        _ = ShowMessageBoxAsync("Could not find the app to update.", "Error");
                        return;
                    }

                    if (name != _editingGameName && games.Any(g => g.Name == name))
                    {
                        _ = ShowMessageBoxAsync("An app with this name already exists.", "Duplicate Name");
                        return;
                    }

                    if (appToUpdate.FolderName != folderName && !string.IsNullOrEmpty(appToUpdate.FolderName))
                    {
                        var oldPath = Path.Combine(_settings.AppsPath, appToUpdate.FolderName);
                        var newPath = Path.Combine(_settings.AppsPath, folderName);

                        if (Directory.Exists(oldPath))
                        {
                            try
                            {
                                Directory.Move(oldPath, newPath);
                            }
                            catch (Exception ex)
                            {
                                _ = ShowMessageBoxAsync($"Failed to rename folder {appToUpdate.FolderName}", ex.Message);
                            }
                        }
                    }

                    appToUpdate.Name = name;
                    appToUpdate.FolderName = folderName;
                    appToUpdate.GameIconUrl = iconUrl;
                    appToUpdate.Tags = tags;

                    await SaveGamesToJsonAsync(games);
                    _ = ShowMessageBoxAsync("App entry updated successfully", "App Updated");
                }
                else
                {
                    if (games.Any(g => g.Name == name || g.Repository == repository || g.FolderName == folderName))
                    {
                        _ = ShowMessageBoxAsync("An app with this name, repository, or folder name already exists", "Duplicate App");
                        return;
                    }

                    games.Add(new GameInfo
                    {
                        Name = name,
                        Repository = repository,
                        FolderName = folderName,
                        GameIconUrl = iconUrl,
                        Tags = tags,
                        IsCustom = true,
                        IsExperimental = false
                    });

                    await SaveGamesToJsonAsync(games);
                    _ = ShowMessageBoxAsync("New app entry created successfully", "App Added");
                }

                await _gameManager.LoadGamesAsync();
                ApplySorting();
                CloseEntryFormOverlay();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Error saving app entry: {ex.Message}", "Error");
            }
        }

        private void EditGameEntry_Click(object sender, RoutedEventArgs e)
        {
            var game = (sender as MenuItem)?.CommandParameter as GameInfo;
            if (game == null)
                return;

            ShowEntryFormOverlay(forCreate: false, gameToEdit: game);
        }

        private void EditTagsMenu_Click(object sender, RoutedEventArgs e)
        {
            var game = (sender as MenuItem)?.CommandParameter as GameInfo;
            if (game == null)
                return;

            _editingTagsGame = game;
            TagEditAppNameText.Text = game.Name ?? "Unknown app";
            TagEditTextBox.Text = TagHelper.FormatTagsForDisplay(game.Tags);
            TagEditOverlay.IsVisible = true;
        }

        private void CancelTagEdit_Click(object? sender, RoutedEventArgs e)
        {
            TagEditOverlay.IsVisible = false;
            _editingTagsGame = null;
        }

        private async void SaveTagEdit_Click(object? sender, RoutedEventArgs e)
        {
            if (_editingTagsGame == null)
                return;

            try
            {
                var tags = TagHelper.ParseCommaSeparatedTags(TagEditTextBox?.Text);
                await SaveTagsForGameAsync(_editingTagsGame, tags);
                TagEditOverlay.IsVisible = false;
                _editingTagsGame = null;
                await _gameManager.LoadGamesAsync();
                ApplySorting();
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Failed to save tags: {ex.Message}", "Error");
            }
        }

        private async Task SaveTagsForGameAsync(GameInfo game, List<string> tags)
        {
            game.Tags = tags;

            if (game.IsInLocalAppsJson && !string.IsNullOrWhiteSpace(game.Repository))
            {
                var games = await LoadGamesFromJsonAsync();
                var appToUpdate = games.FirstOrDefault(g =>
                    !string.IsNullOrWhiteSpace(g.Repository) &&
                    g.Repository.Equals(game.Repository, StringComparison.OrdinalIgnoreCase));

                if (appToUpdate != null)
                {
                    appToUpdate.Tags = tags;
                    await SaveGamesToJsonAsync(games);
                }

                if (!string.IsNullOrWhiteSpace(game.Repository))
                    _settings.UserAppTags.Remove(game.Repository);
            }
            else if (!string.IsNullOrWhiteSpace(game.Repository))
            {
                _settings.UserAppTags[game.Repository] = tags;
            }

            OnSettingChanged();
        }

        private void CancelForm_Click(object? sender, RoutedEventArgs e)
        {
            CloseEntryFormOverlay();
        }

        private void ClearForm()
        {
            if (NewGameNameTextBox != null) NewGameNameTextBox.Text = "";
            if (NewGameRepoTextBox != null)
            {
                NewGameRepoTextBox.Text = "";
                NewGameRepoTextBox.IsEnabled = true;
            }
            if (NewGameFolderTextBox != null) NewGameFolderTextBox.Text = "";
            if (NewGameIconTextBox != null) NewGameIconTextBox.Text = "";
            if (NewGameTagsTextBox != null) NewGameTagsTextBox.Text = "";
            if (CreateEditButton != null) CreateEditButton.Content = "Create Entry";
            if (FormTitleText != null) FormTitleText.Text = "Create New Entry";
            if (ValidationStatusText != null) ValidationStatusText.Text = "";

            _entryFormShowValidation = false;
            _editingGameName = null;
            _editingGameRepository = null;
            _editingFolderName = null;
            _editingGame = null;
        }

        private async void RemoveGameEntry_Click(object sender, RoutedEventArgs e)
        {
            var game = (sender as MenuItem)?.CommandParameter as GameInfo;
            if (game == null || string.IsNullOrEmpty(game.Repository) || string.IsNullOrEmpty(game.Name))
                return;

            if (!game.IsInLocalAppsJson)
                return;

            try
            {
                var confirm = await ShowMessageBoxAsync(
                    $"Are you sure you want to remove '{game.Name}' from your apps list?\n\nThis will only remove it from the launcher, not delete your files.",
                    "Confirm Removal",
                    true);

                if (!confirm)
                    return;

                var games = await LoadGamesFromJsonAsync();
                var gameToRemove = games.FirstOrDefault(g =>
                    !string.IsNullOrWhiteSpace(g.Repository) &&
                    g.Repository.Equals(game.Repository, StringComparison.OrdinalIgnoreCase));

                if (gameToRemove == null)
                    return;

                games.Remove(gameToRemove);
                await SaveGamesToJsonAsync(games);
                await _gameManager.LoadGamesAsync();
                ApplySorting();

                _ = ShowMessageBoxAsync($"'{game.Name}' was removed successfully.", "Removed");
            }
            catch (Exception ex)
            {
                _ = ShowMessageBoxAsync($"Error removing app: {ex.Message}", "Error");
            }
        }

        private void MusicVolumeSlider_ValueChanged(object sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                MusicVolume = (float)slider.Value;
                _settings.MusicVolume = MusicVolume;
                AppSettings.Save(_settings);
            }
        }

        private void PlayLauncherMusic(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                StopLauncherMusic();

                // Use runtime detection
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    PlayMusicWindows(path);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    PlayMusicLinux(path);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    PlayMusicMac(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to play launcher music: {ex.Message}");
            }
        }

        private void PlayMusicWindows(string path)
        {
            #if WINDOWS
            try
            {
                _audioFileReader = new AudioFileReader(path);
                _audioFileReader.Volume = MusicVolume;

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioFileReader);

                // Enable looping
                _waveOut.PlaybackStopped += (sender, args) =>
                {
                    if (_audioFileReader != null && _waveOut != null)
                    {
                        _audioFileReader.Position = 0;
                        _waveOut.Play();
                    }
                };

                _waveOut.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NAudio playback failed: {ex.Message}");
            }
        #else
            Debug.WriteLine("Windows audio playback not available on this platform");
        #endif
        }

        private void PlayMusicLinux(string path)
        {
            string[] players = { "ffplay", "mpv", "cvlc", "mplayer" };

            foreach (var player in players)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = player,
                        Arguments = player switch
                        {
                            "ffplay" => $"-nodisp -autoexit -loop 0 -volume {(int)(MusicVolume * 100)} \"{path}\"",
                            "mpv" => $"--no-video --loop=inf --volume={MusicVolume * 100} \"{path}\"",
                            "cvlc" => $"--no-video --loop --volume {(int)(MusicVolume * 512)} \"{path}\"",
                            "mplayer" => $"-loop 0 -volume {(int)(MusicVolume * 100)} \"{path}\"",
                            _ => $"\"{path}\""
                        },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    _musicProcess = Process.Start(psi);
                    if (_musicProcess != null)
                    {
                        _musicProcess.EnableRaisingEvents = true;
                        Debug.WriteLine($"Playing music with {player}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start {player}: {ex.Message}");
                    continue;
                }
            }

            Debug.WriteLine("No suitable audio player found on Linux. Install one of: ffplay, mpv, vlc, mplayer");
        }

        private void PlayMusicMac(string path)
        {
            try
            {
                // afplay volume is 0-255 (0-1 range needs to be converted)
                var volumeValue = MusicVolume * 255f;

                var psi = new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = $"-v {volumeValue} \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _musicProcess = Process.Start(psi);

                if (_musicProcess != null)
                {
                    _musicProcess.EnableRaisingEvents = true;
                    _musicProcess.Exited += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(LauncherMusicPath) && File.Exists(LauncherMusicPath))
                        {
                            try
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (!string.IsNullOrEmpty(LauncherMusicPath))
                                    {
                                        PlayMusicMac(LauncherMusicPath);
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to restart music: {ex.Message}");
                            }
                        }
                    };

                    Debug.WriteLine($"Playing music with afplay at volume {volumeValue}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"afplay failed: {ex.Message}");
            }
        }

        private void StopLauncherMusic()
        {
            try
            {
                #if WINDOWS
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }
                #endif

                if (_musicProcess != null)
                {
                    try
                    {
                        if (!_musicProcess.HasExited)
                        {
                            _musicProcess.Kill();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited, ignore
                    }

                    _musicProcess.Dispose();
                    _musicProcess = null;
                }

                _musicPausedByDeactivation = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop launcher music: {ex.Message}");
            }
        }

        private async Task FadeMusicAsync(float targetVolume, int durationMs)
        {
        #if WINDOWS
            if (_audioFileReader == null)
                return;

        // Cancel any ongoing fade
        _fadeTaskCts?.Cancel();
        _fadeTaskCts = new System.Threading.CancellationTokenSource();
        var token = _fadeTaskCts.Token;

        try
        {
            float currentVolume = _audioFileReader.Volume;
            float targetVol = targetVolume;

            if (Math.Abs(currentVolume - targetVol) < 0.001f)
                return;

            int steps = 20;
            int stepDelay = durationMs / steps;
            float volumeStep = (targetVol - currentVolume) / steps;

            for (int i = 0; i < steps; i++)
            {
                if (token.IsCancellationRequested || _audioFileReader == null)
                    return;

                currentVolume += volumeStep;
                _audioFileReader.Volume = Math.Clamp(currentVolume, 0f, 1f);

                await Task.Delay(stepDelay, token);
            }

                if (_audioFileReader != null && !token.IsCancellationRequested)
                {
                    _audioFileReader.Volume = targetVol;
                }
            }
            catch (OperationCanceledException)
            {
                // Fade was cancelled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during music fade: {ex.Message}");
            }
            #else
                if (targetVolume < 0.01f)
                {
                    if (_musicProcess != null && !_musicProcess.HasExited)
                    {
                        try
                        {
                            _musicProcess.Kill();
                            Debug.WriteLine("Music paused (process killed)");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to pause music: {ex.Message}");
                        }
                    }
                }
                else if (targetVolume > 0.01f)
                {
                    if (_musicProcess == null || _musicProcess.HasExited)
                    {
                        if (!string.IsNullOrEmpty(LauncherMusicPath) && File.Exists(LauncherMusicPath))
                        {
                            PlayLauncherMusic(LauncherMusicPath);
                            Debug.WriteLine("Music resumed");
                        }
                    }
                }
    
                await Task.CompletedTask;
            #endif
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_isProcessingInput || !IsActive)
                return;

            _isProcessingInput = true;

            try
            {
                switch (e.Key)
                {
                    case Key.Space:
                        // Check if a text input control has focus
                        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                        if (focusedElement is TextBox)
                        {
                            // Let space pass through to text input controls
                        }
                        else
                        {
                            // Handle space as confirm action for other controls
                            HandleConfirmAction();
                            e.Handled = true;
                        }
                        break;

                    case Key.LeftShift:
                        HandleCancelAction();
                        e.Handled = true;
                        break;

                    case Key.Escape:
                        HandleCancelAction();
                        e.Handled = true;
                        break;

                    case Key.Up:
                        _inputService?.HandleNavigation(Services.NavigationDirection.Up);
                        e.Handled = true;
                        break;

                    case Key.Down:
                        _inputService?.HandleNavigation(Services.NavigationDirection.Down);
                        e.Handled = true;
                        break;

                    case Key.Left:
                        _inputService?.HandleNavigation(Services.NavigationDirection.Left);
                        e.Handled = true;
                        break;

                    case Key.Right:
                        _inputService?.HandleNavigation(Services.NavigationDirection.Right);
                        e.Handled = true;
                        break;
                }
            }
            finally
            {
                _isProcessingInput = false;
            }
        }

        private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                _inputService?.ResetNavigationTimer();
            }
        }

        private void HandleConfirmAction()
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

            if (focused is MenuItem menuItem)
            {
                // Trigger the menu item click
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            else if (focused is Button button)
            {
                // If button has a context menu, open it
                if (button.ContextMenu != null)
                {
                    button.ContextMenu.PlacementTarget = button;
                    button.ContextMenu.Placement = PlacementMode.Bottom;
                    button.ContextMenu.Open();

                    // Focus first menu item after opening
                    Dispatcher.UIThread.Post(() =>
                    {
                        var firstMenuItem = button.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault();
                        firstMenuItem?.Focus();
                    }, DispatcherPriority.Loaded);
                }
                else
                {
                    // Normal button click
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
            else if (focused is CheckBox checkBox)
            {
                checkBox.IsChecked = !checkBox.IsChecked;
            }
            else if (focused is ToggleButton toggleButton)
            {
                toggleButton.IsChecked = !toggleButton.IsChecked;
            }
        }

        private void HandleCancelAction()
        {
            // First check if any context menu is open and close it
            var allButtons = this.GetVisualDescendants().OfType<Button>();
            foreach (var button in allButtons)
            {
                if (button.ContextMenu?.IsOpen == true)
                {
                    button.ContextMenu.Close();
                    button.Focus(); // Return focus to the button
                    return;
                }
            }

            // Close settings panel if open
            if (CloseSettingsPanel())
                return;

            // Close changelog if open
            if (_isChangelogOpen)
            {
                CloseChangelog();
                return;
            }
        }

        private bool IsInsideSettingsPanel(Control control)
        {
            var parent = control.Parent;
            while (parent != null)
            {
                if (parent == SettingsPanel)
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Unsubscribe from events
            this.Activated -= MainWindow_Activated;
            this.Deactivated -= MainWindow_Deactivated;

            // Stop Launcher Music
            _fadeTaskCts?.Cancel();
            _fadeTaskCts?.Dispose();
            StopLauncherMusic();

            if (_inputService != null)
            {
                _inputService.OnConfirm -= HandleConfirmAction;
                _inputService.OnCancel -= HandleCancelAction;
                _inputService.Dispose();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _fadeTaskCts?.Cancel();
            StopLauncherMusic();
            base.OnClosing(e);
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (!_trackingLaunchedGameProcess)
            {
                _launchedGameOwnsInput = false;
            }

            _inputService?.SetWindowActive(true);
            if (_settings.EnableGamepadInput && !_launchedGameOwnsInput)
            {
                _inputService?.SetGamepadEnabled(true);
            }

            #if WINDOWS
                        _ = FadeMusicAsync(MusicVolume, FADE_DURATION_MS);
            #else
                if (_musicPausedByDeactivation)
                {
                    _musicPausedByDeactivation = false;
                    _ = FadeMusicAsync(MusicVolume, FADE_DURATION_MS);
                }
            #endif
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _inputService?.SetWindowActive(false);
            _inputService?.SetGamepadEnabled(false);

            #if WINDOWS
                        _ = FadeMusicAsync(0f, FADE_DURATION_MS);
            #else
                if (_musicProcess != null && !_musicProcess.HasExited)
                {
                    _musicPausedByDeactivation = true;
                    _ = FadeMusicAsync(0f, FADE_DURATION_MS);
                }
            #endif
        }

        private void SubscribeToGameEvents(GameInfo game)
        {
            // Unsubscribe
            game.GameProcessStarted -= OnGameProcessStarted;
            game.GameProcessStarted += OnGameProcessStarted;
        }

        private void OnGameProcessStarted(Process? process)
        {
            _launchedGameOwnsInput = true;
            _trackingLaunchedGameProcess = process != null;
            _inputService?.SetWindowActive(false);
            _inputService?.SetGamepadEnabled(false);

            if (process == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await process.WaitForExitAsync();

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var currentProcessGroupId = await TryGetProcessGroupIdAsync(Environment.ProcessId);
                        var launchedProcessGroupId = await TryGetProcessGroupIdAsync(process.Id);
                        if (launchedProcessGroupId.HasValue && launchedProcessGroupId != currentProcessGroupId)
                        {
                            while (await HasActiveProcessGroupAsync(launchedProcessGroupId.Value))
                            {
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch { /* process may have already exited */ }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _trackingLaunchedGameProcess = false;
                    _launchedGameOwnsInput = false;

                    if (IsActive && _settings.EnableGamepadInput)
                    {
                        _inputService?.SetGamepadEnabled(true);
                        _inputService?.SetWindowActive(true);
                    }
                });
            });
        }

        private static async Task<int?> TryGetProcessGroupIdAsync(int processId)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ps",
                    Arguments = $"-o pgid= -p {processId}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var psProcess = Process.Start(startInfo);
                if (psProcess == null)
                {
                    return null;
                }

                var output = await psProcess.StandardOutput.ReadToEndAsync();
                await psProcess.WaitForExitAsync();

                if (psProcess.ExitCode != 0)
                {
                    return null;
                }

                var trimmed = output.Trim();
                if (int.TryParse(trimmed, out var processGroupId))
                {
                    return processGroupId;
                }
            }
            catch
            {
            }

            return null;
        }

        private static async Task<bool> HasActiveProcessGroupAsync(int processGroupId)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ps",
                    Arguments = $"-o pid= -g {processGroupId}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var psProcess = Process.Start(startInfo);
                if (psProcess == null)
                {
                    return false;
                }

                var output = await psProcess.StandardOutput.ReadToEndAsync();
                await psProcess.WaitForExitAsync();

                if (psProcess.ExitCode != 0)
                {
                    return false;
                }

                return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => !string.IsNullOrWhiteSpace(line));
            }
            catch
            {
                return false;
            }
        }

        private async void ShowChangelog_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null || string.IsNullOrEmpty(game.Repository))
            {
                await ShowMessageBoxAsync("Unable to retrieve changelog information.", "Error");
                return;
            }

            _currentChangelogGame = game;
            await ShowChangelogAsync(game);
        }

        private async Task ShowChangelogAsync(GameInfo game)
        {
            try
            {
                _isChangelogOpen = true;

                // Show changelog panel
                var changelogPanel = this.FindControl<Border>("ChangelogPanel");
                if (changelogPanel != null)
                {
                    changelogPanel.IsVisible = true;
                }

                // Update header title
                var headerTitle = this.FindControl<TextBlock>("HeaderTitleText");
                if (headerTitle != null)
                {
                    headerTitle.Text = $"{game.Name} - Version {game.LatestVersion ?? "Unknown"}";
                }

                // Update changelog title
                var changelogTitle = this.FindControl<TextBlock>("ChangelogTitle");
                if (changelogTitle != null)
                {
                    changelogTitle.Text = $"{game.Name} Changelog";
                }

                // Fetch and render changelog
                var changelogContent = this.FindControl<ItemsControl>("ChangelogContent");
                if (changelogContent != null)
                {
                    // Show loading placeholder
                    var loadingPanel = new StackPanel();
                    loadingPanel.Children.Add(new TextBlock
                    {
                        Text = "Loading changelog...",
                        Foreground = new SolidColorBrush(Color.Parse("#B8B8B8")),
                        FontSize = 14
                    });
                    changelogContent.ItemsSource = new[] { loadingPanel };
                }

                string changelogText = await FetchChangelogAsync(game.Repository);

                if (changelogContent != null)
                {
                    changelogContent.ItemsSource = ParseMarkdown(changelogText);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to load changelog: {ex.Message}", "Error");
                CloseChangelog();
            }
        }

        private List<Control> ParseMarkdown(string markdown)
        {
            var controls = new List<Control>();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                controls.Add(new SelectableTextBlock
                {
                    Text = "No changelog available.",
                    Foreground = new SolidColorBrush(Color.Parse("#B8B8B8")),
                    FontSize = 14
                });
                return controls;
            }

            var lines = markdown.Split('\n');
            var listItems = new List<string>();
            var codeBlockLines = new List<string>();
            bool inCodeBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Code blocks
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        if (codeBlockLines.Count > 0)
                        {
                            var codeBlock = new Border
                            {
                                Background = new SolidColorBrush(Color.Parse("#1e1e1e")),
                                BorderBrush = new SolidColorBrush(Color.Parse("#2d2d30")),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(12),
                                Margin = new Thickness(0, 8, 0, 8)
                            };
                            codeBlock.Child = new SelectableTextBlock
                            {
                                Text = string.Join("\n", codeBlockLines),
                                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                                FontSize = 13,
                                Foreground = new SolidColorBrush(Color.Parse("#d4d4d4"))
                            };
                            controls.Add(codeBlock);
                            codeBlockLines.Clear();
                        }
                        inCodeBlock = false;
                    }
                    else
                    {
                        FlushListItems(controls, listItems);
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(line);
                    continue;
                }

                // GitHub alerts: > [!NOTE], etc.
                var alertMatch = System.Text.RegularExpressions.Regex.Match(line.TrimStart(),
                    @"^>\s*\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (alertMatch.Success)
                {
                    FlushListItems(controls, listItems);

                    var alertType = alertMatch.Groups[1].Value.ToUpper();
                    var alertContentLines = new List<string>();

                    // Move to the first line of content
                    i++;

                    // Collect all lines belonging to the alert block
                    while (i < lines.Length)
                    {
                        var nextLine = lines[i].TrimEnd('\r');

                        // Stop if the line is empty
                        if (string.IsNullOrWhiteSpace(nextLine))
                        {
                            break;
                        }

                        var trimmedLine = nextLine.TrimStart();

                        if (trimmedLine.StartsWith(">"))
                        {
                            // Standard blockquote line: strip the '>'
                            var content = trimmedLine.Substring(1);
                            if (content.StartsWith(" ")) content = content.Substring(1);
                            alertContentLines.Add(content);
                        }
                        else
                        {
                            // Lazy continuation
                            alertContentLines.Add(trimmedLine);
                        }
                        i++;
                    }

                    // Define colors and icon names
                    var (borderColorHex, iconPath) = alertType switch
                    {
                        "NOTE" => ("#0969da", "markdown_info.png"),
                        "TIP" => ("#1a7f37", "markdown_tip.png"),
                        "IMPORTANT" => ("#8250df", "markdown_important.png"),
                        "WARNING" => ("#9a6700", "markdown_warning.png"),
                        "CAUTION" => ("#d1242f", "markdown_caution.png"),
                        _ => ("#2d2d30", "markdown_info.png")
                    };

                    var alertColor = Color.Parse(borderColorHex);
                    var alertBrush = new SolidColorBrush(alertColor);

                    var alertBorder = new Border
                    {
                        BorderBrush = alertBrush,
                        BorderThickness = new Thickness(4, 0, 0, 0),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(16, 12, 16, 12),
                        Margin = new Thickness(0, 8, 0, 8),
                        Background = new SolidColorBrush(alertColor) { Opacity = 0.05 }
                    };

                    var alertPanel = new StackPanel();

                    // Title row with icon tinted to border color
                    var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

                    try
                    {
                        // Use a Rectangle + OpacityMask to draw the icon in the alert's color
                        var iconRect = new Avalonia.Controls.Shapes.Rectangle
                        {
                            Width = 16,
                            Height = 16,
                            Margin = new Thickness(0, 0, 8, 0),
                            Fill = alertBrush,
                            VerticalAlignment = VerticalAlignment.Center,
                            OpacityMask = new ImageBrush
                            {
                                Source = new Avalonia.Media.Imaging.Bitmap(
                                    Avalonia.Platform.AssetLoader.Open(
                                        new Uri($"avares://Quiver/Assets/{iconPath}")))
                            }
                        };
                        titlePanel.Children.Add(iconRect);
                    }
                    catch (Exception)
                    {
                        // Fallback circle if icon load fails
                        titlePanel.Children.Add(new Avalonia.Controls.Shapes.Ellipse
                        {
                            Width = 8,
                            Height = 8,
                            Fill = alertBrush,
                            Margin = new Thickness(0, 0, 8, 0)
                        });
                    }

                    titlePanel.Children.Add(new SelectableTextBlock
                    {
                        Text = alertType,
                        FontSize = 14,
                        FontWeight = FontWeight.Bold,
                        Foreground = alertBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    alertPanel.Children.Add(titlePanel);

                    // Parse the inner content for markdown (bold, links, etc.)
                    var contentText = string.Join("\n", alertContentLines);
                    var contentBlocks = ParseInlineMarkdown(contentText);
                    foreach (var block in contentBlocks)
                    {
                        block.Margin = new Thickness(0, 2, 0, 2);
                        alertPanel.Children.Add(block);
                    }

                    alertBorder.Child = alertPanel;
                    controls.Add(alertBorder);
                    continue;
                }

                // Headers
                if (line.StartsWith("#"))
                {
                    FlushListItems(controls, listItems);
                    int level = 0;
                    while (level < line.Length && line[level] == '#') level++;
                    var headerText = line.Substring(level).Trim();
                    var fontSize = level switch { 1 => 24, 2 => 20, 3 => 18, 4 => 16, _ => 14 };
                    var fontWeight = level <= 2 ? FontWeight.Bold : FontWeight.SemiBold;

                    controls.Add(new SelectableTextBlock
                    {
                        Text = headerText,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, level == 1 ? 16 : 12, 0, 8)
                    });
                    continue;
                }

                // Lists
                if (line.TrimStart().StartsWith("* ") || line.TrimStart().StartsWith("- "))
                {
                    var itemText = line.TrimStart().Substring(2);
                    listItems.Add("• " + itemText);
                    continue;
                }

                var orderedMatch = System.Text.RegularExpressions.Regex.Match(line.TrimStart(), @"^(\d+)\.\s+(.+)");
                if (orderedMatch.Success)
                {
                    listItems.Add(orderedMatch.Groups[1].Value + ". " + orderedMatch.Groups[2].Value);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("---"))
                {
                    FlushListItems(controls, listItems);
                    if (line.Trim().StartsWith("---"))
                        controls.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#2d2d30")), Margin = new Thickness(0, 12, 0, 12) });
                    continue;
                }

                FlushListItems(controls, listItems);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var blocks = ParseInlineMarkdown(line);
                    foreach (var block in blocks)
                    {
                        block.Margin = new Thickness(0, 0, 0, 8);
                        controls.Add(block);
                    }
                }
            }

            FlushListItems(controls, listItems);
            return controls;
        }

        private List<Control> ParseInlineMarkdown(string text)
        {
            var blocks = new List<Control>();
            var panel = new WrapPanel { Orientation = Orientation.Horizontal };

            int i = 0;
            var currentText = new StringBuilder();

            void FlushText()
            {
                if (currentText.Length > 0)
                {
                    panel.Children.Add(new SelectableTextBlock
                    {
                        Text = currentText.ToString(),
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.Parse("#B8B8B8")),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    });
                    currentText.Clear();
                }
            }

            void AddLineBreak()
            {
                // Add all current panel content to blocks
                if (panel.Children.Count > 0)
                {
                    blocks.Add(panel);
                    panel = new WrapPanel { Orientation = Orientation.Horizontal };
                }
            }

            while (i < text.Length)
            {
                // Check for line breaks
                if (text[i] == '\n' || text[i] == '\r')
                {
                    FlushText();
                    AddLineBreak();

                    // Skip \r\n or \n\r combinations
                    if (i + 1 < text.Length && (text[i + 1] == '\n' || text[i + 1] == '\r') && text[i] != text[i + 1])
                    {
                        i++;
                    }
                    i++;
                    continue;
                }

                // Bold **text**
                if (i < text.Length - 1 && text[i] == '*' && text[i + 1] == '*')
                {
                    FlushText();
                    i += 2;
                    var boldText = new StringBuilder();
                    while (i < text.Length - 1 && !(text[i] == '*' && text[i + 1] == '*')) { boldText.Append(text[i]); i++; }
                    if (i < text.Length - 1) i += 2;
                    panel.Children.Add(new SelectableTextBlock
                    {
                        Text = boldText.ToString(),
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    });
                    continue;
                }

                // Inline code `text`
                if (text[i] == '`')
                {
                    FlushText();
                    i++;
                    var codeText = new StringBuilder();
                    while (i < text.Length && text[i] != '`') { codeText.Append(text[i]); i++; }
                    if (i < text.Length) i++;
                    panel.Children.Add(new SelectableTextBlock
                    {
                        Text = codeText.ToString(),
                        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                        Foreground = new SolidColorBrush(Color.Parse("#d4d4d4")),
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    });
                    continue;
                }

                // Links [text](url)
                if (text[i] == '[')
                {
                    var linkMatch = System.Text.RegularExpressions.Regex.Match(text.Substring(i), @"^\[([^\]]+)\]\(([^\)]+)\)");
                    if (linkMatch.Success)
                    {
                        FlushText();
                        var linkText = linkMatch.Groups[1].Value;
                        var linkUrl = linkMatch.Groups[2].Value;

                        var linkButton = new Button
                        {
                            Content = linkText,
                            Foreground = new SolidColorBrush(Color.Parse("#0969da")),
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(0),
                            Cursor = new Cursor(StandardCursorType.Hand),
                            FontSize = 14,
                            VerticalAlignment = VerticalAlignment.Center,
                            Tag = linkUrl
                        };

                        linkButton.Click += (s, e) =>
                        {
                            if (linkButton.Tag is string url)
                            {
                                try { OpenUrl(url); } catch { }
                            }
                        };

                        panel.Children.Add(linkButton);
                        i += linkMatch.Length;
                        continue;
                    }
                }

                currentText.Append(text[i]);
                i++;
            }

            FlushText();

            if (panel.Children.Count > 0)
            {
                blocks.Add(panel);
            }

            return blocks;
        }

        private void FlushListItems(List<Control> controls, List<string> listItems)
        {
            if (listItems.Count > 0)
            {
                var listPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
                foreach (var item in listItems)
                {
                    var blocks = ParseInlineMarkdown(item);
                    foreach (var block in blocks)
                    {
                        block.Margin = new Thickness(0, 2, 0, 2);
                        listPanel.Children.Add(block);
                    }
                }
                controls.Add(listPanel);
                listItems.Clear();
            }
        }

        private async Task<string> FetchChangelogAsync(string repository)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Quiver");

                if (!string.IsNullOrEmpty(_settings?.GitHubApiToken))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"token {_settings.GitHubApiToken}");
                }

                var url = $"https://api.github.com/repos/{repository}/releases/latest";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return "Failed to fetch changelog from GitHub.";
                }

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("body", out var bodyElement))
                {
                    var body = bodyElement.GetString();
                    if (!string.IsNullOrEmpty(body))
                    {
                        return body;
                    }
                }

                return "No changelog available for this release.";
            }
            catch (Exception ex)
            {
                return $"Error fetching changelog: {ex.Message}";
            }
        }

        private void CloseChangelog()
        {
            _isChangelogOpen = false;
            _currentChangelogGame = null;

            // Hide changelog panel
            var changelogPanel = this.FindControl<Border>("ChangelogPanel");
            if (changelogPanel != null)
            {
                changelogPanel.IsVisible = false;
            }

            // Restore sidebar content
            var sidebarContent = this.FindControl<StackPanel>("SidebarContent");
            if (sidebarContent != null)
            {
                sidebarContent.Width = double.NaN;
            }

            // Restore header title
            var headerTitle = this.FindControl<TextBlock>("HeaderTitleText");
            if (headerTitle != null)
            {
                headerTitle.Text = "Library";
            }
        }

        private async void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                string launcherPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(launcherPath))
                {
                    await ShowMessageBoxAsync("Could not determine launcher location.", "Error");
                    return;
                }

                await Quiver.Services.ShortcutHelper.CreateGameShortcutAsync(
                    game,
                    launcherPath,
                    _gameManager.CacheFolder);

            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to create shortcut: {ex.Message}", "Error");
            }
        }

        private async void AddToSteam_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var game = menuItem?.CommandParameter as GameInfo;

            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                string launcherPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(launcherPath))
                {
                    await ShowMessageBoxAsync("Could not determine launcher location.", "Error");
                    return;
                }

                string resultMessage = Quiver.Services.ShortcutHelper.IsSteamRunning()
                    ? Quiver.Services.ShortcutHelper.QueueGameAddToSteam(game, launcherPath)
                    : Quiver.Services.ShortcutHelper.AddGameToSteam(
                        game,
                        launcherPath,
                        _gameManager.CacheFolder);

                await ShowMessageBoxAsync(resultMessage, "Steam Shortcut");
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to add the game to Steam: {ex.Message}", "Error");
            }
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ApplyRoundedCorners()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                var hwnd = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd != IntPtr.Zero)
                {
                    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                    int DWMWCP_ROUND = 2;

                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref DWMWCP_ROUND, sizeof(int));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply rounded corners: {ex.Message}");
            }
        }

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }

    public class MarkdownBlock
    {
        public string Type { get; set; } = "paragraph";
        public string Content { get; set; } = "";
        public int Level { get; set; } = 0;
        public List<string> Items { get; set; } = new List<string>();
    }

}

