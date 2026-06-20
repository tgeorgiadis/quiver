using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Quiver.Tests.HeadlessTestApp))]

namespace Quiver.Tests;

public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Quiver.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
