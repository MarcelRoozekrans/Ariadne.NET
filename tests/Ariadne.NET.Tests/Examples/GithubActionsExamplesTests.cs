using Ariadne.NET.Tests.PackSchema;
using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace Ariadne.NET.Tests.Examples;

public class GithubActionsExamplesTests
{
    private const string ToolInstall = "dotnet tool install --global Ariadne.NET.Cli";

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
    public void Tool_example_installs_the_tool_validates_with_sarif_and_uploads_when_the_report_exists()
    {
        var root = Load(Path.Combine(ExamplesFolder(), "ariadne-tool.yml"));
        var steps = Steps(root);

        Runs(steps).Should().Contain(ToolInstall);

        var validate = steps.Single(s => Run(s).Contains("ariadne validate", StringComparison.Ordinal));
        Run(validate).Should().Contain("--format sarif --output");
        Scalar(validate, "if").Should().BeNull("the validate step is the one that fails the job");

        var upload = steps.Single(s => (Scalar(s, "uses") ?? "").StartsWith("github/codeql-action/upload-sarif@", StringComparison.Ordinal));
        Scalar(upload, "if").Should().Contain("!cancelled()").And.Contain("hashFiles('ariadne.sarif')")
            .And.Contain("!(github.event_name == 'push' && github.actor == 'dependabot[bot]')")
            .And.NotContain("pull_request");
        steps.IndexOf(upload).Should().BeGreaterThan(steps.IndexOf(validate));

        var permissions = (YamlMappingNode)Get(root, "permissions")!;
        Scalar(permissions, "security-events").Should().Be("write");
        Scalar(permissions, "contents").Should().Be("read");
        Scalar(permissions, "actions").Should().Be("read");
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

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void Docs_page_contains_each_example_verbatim(string file)
    {
        var example = Normalise(File.ReadAllText(Path.Combine(ExamplesFolder(), file)));
        var docs = Normalise(File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), "docs-site", "docs", "ci-github-actions.md")));

        docs.Should().Contain(example.TrimEnd('\n'), $"the docs page must show {file} exactly");
    }

    [Fact]
    public void Proof_job_in_ci_runs_the_validate_command_line_of_the_examples()
    {
        var exampleValidate = Run(Steps(Load(Path.Combine(ExamplesFolder(), "ariadne-tool.yml")))
            .First(s => Run(s).Contains("ariadne validate", StringComparison.Ordinal)));
        var exampleFlags = FlagsWithoutOutputValue(exampleValidate, "ariadne validate");
        exampleFlags.Should().Contain("--format sarif --output");

        var root = Load(Path.Combine(PackSchemaHarness.RepoRoot(), ".github", "workflows", "ci.yml"));
        var jobs = (YamlMappingNode)Get(root, "jobs")!;
        var job = (YamlMappingNode)Get(jobs, "fail-the-build")!;
        var steps = ((YamlSequenceNode)Get(job, "steps")!).Children.Cast<YamlMappingNode>().ToList();

        var validates = steps.Where(s => Run(s).Contains("ariadne\" validate", StringComparison.Ordinal)).ToList();

        validates.Should().NotBeEmpty();
        validates.Select(v => FlagsWithoutOutputValue(Run(v), "ariadne\" validate"))
            .Should().OnlyContain(flags => flags == exampleFlags);
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    // The flags after the solution path, without the value of the final --output.
    private static string FlagsWithoutOutputValue(string runText, string marker)
    {
        var line = runText.Split('\n').First(l => l.Contains(marker, StringComparison.Ordinal));
        var tokens = line[(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var flagsStart = Array.FindIndex(tokens, t => t.StartsWith("--", StringComparison.Ordinal));
        var flags = tokens[flagsStart..];
        var output = Array.LastIndexOf(flags, "--output");

        output.Should().BeGreaterThanOrEqualTo(0);
        return string.Join(' ', flags[..(output + 1)].Concat(flags[(output + 2)..]));
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
