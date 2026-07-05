using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Quiver;

namespace Quiver.Services;

public static class GameDialogService
{
    public static bool IsGitHubRateLimitError(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }
    public static async Task ShowMessageBoxAsync(string message, string title)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var messageBox = new Window
                {
                    Title = title,
                    MinWidth = 420,
                    MaxWidth = 520,
                    MaxHeight = 520,
                    CanResize = true,
                    SizeToContent = SizeToContent.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                var okButton = new Button
                {
                    Content = "OK",
                    Width = 80,
                    Padding = new Thickness(12, 6),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                okButton.Click += (_, _) => messageBox.Close();

                var buttonRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { okButton },
                };

                messageBox.Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    Children =
                    {
                        new ScrollViewer
                        {
                            MaxHeight = 360,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = new TextBlock
                            {
                                Text = message,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 13,
                            },
                        },
                        buttonRow,
                    },
                };

                await messageBox.ShowDialog(desktop.MainWindow);
            }
            else
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {title}");
                Console.ResetColor();
                Console.WriteLine(message);
                Console.WriteLine();
            }
        });
    }

    public static async Task<bool> ShowWineNotFoundWarningAsync()
    {
        var userChoice = false;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null)
            {
                userChoice = true;
                return;
            }

            var messageBox = new Window
            {
                Title = "Windows Runner Not Found",
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "This game requires a Linux Windows-runner to launch, but none was detected.\n\n" +
                                   "Install Wine/Proton or set a custom command in Settings for Bottles or another launcher.\n\n" +
                                   "Do you want to download anyway? The game will not launch without a configured runner.",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20),
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Spacing = 10,
                            Children =
                            {
                                new Button { Content = "Download Anyway", Width = 140 },
                                new Button { Content = "Cancel", Width = 100 },
                            },
                        },
                    },
                },
            };

            if (((StackPanel)messageBox.Content).Children[1] is StackPanel buttonPanel &&
                buttonPanel.Children[0] is Button yesButton &&
                buttonPanel.Children[1] is Button noButton)
            {
                yesButton.Click += (_, _) => { userChoice = true; messageBox.Close(); };
                noButton.Click += (_, _) => { userChoice = false; messageBox.Close(); };
            }

            await messageBox.ShowDialog(desktop.MainWindow);
        });

        return userChoice;
    }

    public static async Task<bool> ShowWineDownloadWarningAsync()
    {
        var userChoice = false;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null)
            {
                userChoice = true;
                return;
            }

            var messageBox = new Window
            {
                Title = "Windows Runner Required",
                Width = 500,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "This game requires a Linux Windows-runner to launch. A compatible runner was detected or configured and will be used.\n\nWant to download?",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20),
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Spacing = 10,
                            Children =
                            {
                                new Button { Content = "Yes", Width = 100 },
                                new Button { Content = "No", Width = 100 },
                            },
                        },
                    },
                },
            };

            if (((StackPanel)messageBox.Content).Children[1] is StackPanel buttonPanel &&
                buttonPanel.Children[0] is Button yesButton &&
                buttonPanel.Children[1] is Button noButton)
            {
                yesButton.Click += (_, _) => { userChoice = true; messageBox.Close(); };
                noButton.Click += (_, _) => { userChoice = false; messageBox.Close(); };
            }

            await messageBox.ShowDialog(desktop.MainWindow);
        });

        return userChoice;
    }

    public static async Task ShowRateLimitErrorAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null)
                return;

            var hyperlinkText = new TextBlock
            {
                Text = "https://github.com/settings/tokens",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                Cursor = new Cursor(StandardCursorType.Hand),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 0, 0),
            };

            hyperlinkText.PointerPressed += (_, _) =>
            {
                try
                {
                    var url = "https://github.com/settings/tokens";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        Process.Start("xdg-open", url);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        Process.Start("open", url);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open URL: {ex.Message}");
                }
            };

            var openSettingsButton = new Button
            {
                Content = "Open Settings",
                MinWidth = 120,
            };

            var closeButton = new Button
            {
                Content = "Close",
                MinWidth = 100,
            };

            var messageBox = new Window
            {
                Title = "Rate Limit Exceeded",
                Width = 600,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 15,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "GitHub API rate limit exceeded.",
                                FontWeight = FontWeight.Bold,
                                FontSize = 16,
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = "GitHub limits anonymous requests to 60 per hour. The limit resets one hour after depletion.",
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = "To avoid this, add a GitHub API token in Settings → Advanced:",
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 10, 0, 0),
                            },
                            new TextBlock
                            {
                                Text = "1. Click the link below to create a token:",
                                TextWrapping = TextWrapping.Wrap,
                            },
                            hyperlinkText,
                            new TextBlock { Text = "2. Click 'Generate new token (classic)'", TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = "3. Give it a name (no special permissions needed)", TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = "4. Click 'Generate token' at the bottom", TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = "5. Copy the token and paste it in Settings → Advanced → GitHub API Token", TextWrapping = TextWrapping.Wrap },
                            new TextBlock
                            {
                                Text = "Do not share your token with anyone!",
                                Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                                FontWeight = FontWeight.Bold,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 10, 0, 0),
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Spacing = 10,
                                Margin = new Thickness(0, 10, 0, 0),
                                Children = { openSettingsButton, closeButton },
                            },
                        },
                    },
                },
            };

            openSettingsButton.Click += (_, _) =>
            {
                messageBox.Close();
                if (desktop.MainWindow is MainWindow mainWindow)
                    mainWindow.OpenGitHubApiTokenSettings();
            };
            closeButton.Click += (_, _) => messageBox.Close();
            await messageBox.ShowDialog(desktop.MainWindow);
        });
    }
}
