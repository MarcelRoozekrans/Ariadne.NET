using Ariadne.NET.Tests.PackSchema;
using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace Ariadne.NET.Tests.Examples;

public class GithubActionsExamplesTests
{
    private const string ToolInstall = "dotnet tool install --global Ariadne.NET.Cli";
    private const string ValidateFlags = "--format sarif --output";

    private static string ExamplesFolder() =>
        Path.Combine(PackSchemaHarness.RepoRoot(), "examples", "github-actions");

    public static TheoryData<string> ExampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(ExamplesFolder(), "*.yml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void Every_example_parses_as_a_workflow_with_a_job_and_steps(string file)
    {
        var root = Load(Path.Combine(ExamplesFolder(), file));

        Get(root, "on").Should().NotBeNull($"{file} needs a trigger");
        Steps(root).Should().NotBeEmpty();
    }

    [Fact]
    public void Tool_example_installs_the_tool_validates_with_sarif_and_uploads_always()
    {
        var root = Load(Path.Combine(ExamplesFolder(), "ariadne-tool.yml"));
        var steps = Steps(root);

        Runs(steps).Should().Contain(ToolInstall);

        var validate = steps.Single(s => Run(s).Contains("ariadne validate", StringComparison.Ordinal));
        Run(validate).Should().Contain(ValidateFlags);
        Scalar(validate, "if").Should().BeNull("the validate step is the one that fails the job");

        var upload = steps.Single(s => (Scalar(s, "uses") ?? "").StartsWith("github/codeql-action/upload-sarif@", StringComparison.Ordinal));
        Scalar(upload, "if").Should().Be("always()");
        steps.IndexOf(upload).Should().BeGreaterThan(steps.IndexOf(validate));

        var permissions = (YamlMappingNode)Get(root, "permissions")!;
        Scalar(permissions, "security-events").Should().Be("write");
        Scalar(permissions, "contents").Should().Be("read");
    }

    [Fact]
    public void Tool_example_restores_before_validating()
    {
        var steps = Steps(Load(Path.Combine(ExamplesFolder(), "ariadne-tool.yml")));

        var restore = steps.FindIndex(s => Run(s).StartsWith("dotnet restore", StringComparison.Ordinal));
        var validate = steps.FindIndex(s => Run(s).Contains("ariadne validate", StringComparison.Ordinal));

        restore.Should().BeGreaterThanOrEqualTo(0);
        restore.Should().BeLessThan(validate);
    }

    [Fact]
    public void Test_example_runs_dotnet_test()
    {
        var steps = Steps(Load(Path.Combine(ExamplesFolder(), "ariadne-test.yml")));

        Runs(steps).Should().Contain(r => r.StartsWith("dotnet test", StringComparison.Ordinal));
    }

    private static YamlMappingNode Load(string path)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(path);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static YamlNode? Get(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    private static string? Scalar(YamlMappingNode map, string key) =>
        (Get(map, key) as YamlScalarNode)?.Value;

    private static string Run(YamlMappingNode step) => Scalar(step, "run") ?? "";

    private static List<YamlMappingNode> Steps(YamlMappingNode workflow)
    {
        var jobs = (YamlMappingNode)Get(workflow, "jobs")!;
        return jobs.Children.Values
            .Cast<YamlMappingNode>()
            .SelectMany(job => ((YamlSequenceNode)Get(job, "steps")!).Children.Cast<YamlMappingNode>())
            .ToList();
    }

    private static List<string> Runs(List<YamlMappingNode> steps) =>
        steps.Select(Run).Where(r => r.Length > 0).ToList();
}
