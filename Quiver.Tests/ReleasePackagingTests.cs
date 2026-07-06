using FluentAssertions;

namespace Quiver.Tests;

public class ReleasePackagingTests
{
    [Fact]
    public void Release_workflow_does_not_bundle_apps_json_in_platform_archives()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            ".github", "workflows", "dotnet-desktop.yml"));

        File.Exists(workflowPath).Should().BeTrue($"expected workflow at {workflowPath}");

        var workflow = File.ReadAllText(workflowPath);

        workflow.Should().NotContain("Copy-Item apps.json");
        workflow.Should().NotContain("cp apps.json");
    }

    [Fact]
    public void Repository_includes_apps_json_example_for_documentation()
    {
        var examplePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "apps.json.example"));

        File.Exists(examplePath).Should().BeTrue();
        File.ReadAllText(examplePath).Should().Contain("\"apps\"");
    }
}
