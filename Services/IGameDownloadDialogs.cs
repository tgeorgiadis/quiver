namespace Quiver.Services;

public interface IGameDownloadDialogs
{
    Task<bool> ConfirmDownloadWithoutRunnerAsync();
    Task<bool> ConfirmDownloadWithRunnerAsync();
    Task ShowRateLimitExceededAsync();
    Task ShowErrorAsync(string message, string title);
}

public sealed class AvaloniaGameDownloadDialogs : IGameDownloadDialogs
{
    public static AvaloniaGameDownloadDialogs Instance { get; } = new();

    public Task<bool> ConfirmDownloadWithoutRunnerAsync() =>
        GameDialogService.ShowWineNotFoundWarningAsync();

    public Task<bool> ConfirmDownloadWithRunnerAsync() =>
        GameDialogService.ShowWineDownloadWarningAsync();

    public Task ShowRateLimitExceededAsync() =>
        GameDialogService.ShowRateLimitErrorAsync();

    public Task ShowErrorAsync(string message, string title) =>
        GameDialogService.ShowMessageBoxAsync(message, title);
}

public sealed class HeadlessGameDownloadDialogs : IGameDownloadDialogs
{
    public static HeadlessGameDownloadDialogs Instance { get; } = new();

    public Task<bool> ConfirmDownloadWithoutRunnerAsync() => Task.FromResult(true);

    public Task<bool> ConfirmDownloadWithRunnerAsync() => Task.FromResult(true);

    public Task ShowRateLimitExceededAsync() => Task.CompletedTask;

    public Task ShowErrorAsync(string message, string title) => Task.CompletedTask;
}
