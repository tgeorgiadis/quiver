using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Xunit.Abstractions;

namespace Quiver.Tests;

public class ReleasePackagingTests
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(8);

    private readonly ITestOutputHelper _output;

    public ReleasePackagingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", ".."));

    [Fact]
    public void Release_workflow_does_not_bundle_apps_json_in_platform_archives()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github", "workflows", "dotnet-desktop.yml");

        File.Exists(workflowPath).Should().BeTrue($"expected workflow at {workflowPath}");

        var workflow = File.ReadAllText(workflowPath);

        workflow.Should().NotContain("Copy-Item apps.json");
        workflow.Should().NotContain("cp apps.json");
    }

    [Fact]
    public void Release_workflow_strips_apps_json_before_archiving()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github", "workflows", "dotnet-desktop.yml");
        var workflow = File.ReadAllText(workflowPath);

        workflow.Should().Contain("Remove-Item publish/win-x64/apps.json");
        workflow.Should().Contain("rm -f publish/linux-x64/apps.json publish/linux-arm64/apps.json");
        workflow.Should().Contain("rm -f publish/osx-x64/apps.json");
    }

    [Fact]
    public void Project_does_not_copy_apps_json_to_publish_output()
    {
        var csprojPath = Path.Combine(RepoRoot, "Quiver.csproj");
        var csproj = File.ReadAllText(csprojPath);

        csproj.Should().NotContain(
            "<None Update=\"apps.json\">",
            "apps.json must not be copied to publish output; it is user data created at runtime");
        csproj.Should().Contain(
            "<None Update=\"version.txt\">",
            "version.txt should still ship with the app for update checks");
    }

    [Fact]
    public void Repository_includes_apps_json_example_for_documentation()
    {
        var examplePath = Path.Combine(RepoRoot, "apps.json.example");

        File.Exists(examplePath).Should().BeTrue();
        File.ReadAllText(examplePath).Should().Contain("\"apps\"");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task Publish_output_does_not_include_apps_json()
    {
        var publishDir = Path.Combine(Path.GetTempPath(), "QuiverPublishTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(publishDir);

        Process? process = null;

        try
        {
            var runtimeIdentifier = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx-x64"
                : "linux-x64";

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{Path.Combine(RepoRoot, "Quiver.csproj")}\" -c Release -r {runtimeIdentifier} --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o \"{publishDir}\"",
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _output.WriteLine($"Starting publish (timeout {PublishTimeout.TotalMinutes:F0} minutes)...");
            _output.WriteLine(startInfo.FileName + " " + startInfo.Arguments);

            process = Process.Start(startInfo);
            process.Should().NotBeNull();

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process!.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                stdout.AppendLine(e.Data);
                _output.WriteLine("[stdout] " + e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                stderr.AppendLine(e.Data);
                _output.WriteLine("[stderr] " + e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(PublishTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!process.HasExited)
            {
                throw new TimeoutException(
                    $"dotnet publish did not finish within {PublishTimeout.TotalMinutes:F0} minutes.");
            }

            process.ExitCode.Should().Be(0, because: stderr.ToString());

            File.Exists(Path.Combine(publishDir, "apps.json")).Should().BeFalse(
                "release publish output must not ship a blank apps.json that could wipe user libraries on update");
        }
        finally
        {
            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cleanup after timeout or test failure.
                }
            }

            process?.Dispose();

            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, true);
        }
    }
}
