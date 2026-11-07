using System.Text.Json;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Workspaces;
using FluentAssertions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Hooks.Tests;

public class StopHookTests
{
    private static readonly RulePack Pack = new() { Name = "p" };

    private static ArchitectureViolation Violation() =>
        new()
        {
            RuleId = "SHOP-RULE",
            Kind = ViolationKind.ForbiddenDependency,
            SourceType = "Shop.A",
            Message = "A depends on B.",
            Because = "Keep the layers apart.",
        };

    private static Result<ArchitectureReport, AriadneError> Report(params ArchitectureViolation[] violations) =>
        Result<ArchitectureReport, AriadneError>.Success(new ArchitectureReport { Violations = violations, Projects = [], TypesChecked = 1 });

    private sealed class FakeRules(Result<RulePack, AriadneError> result) : IProjectRules
    {
        public Result<RulePack, AriadneError> Load(string root) => result;
    }

    private sealed class FakeValidator(Result<ArchitectureReport, AriadneError> result) : ISolutionValidator
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        public Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(RulePack resolvedPack, string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            Path = path;
            return Task.FromResult(result);
        }
    }

    private static Result<RulePack, AriadneError> Loaded() => Result<RulePack, AriadneError>.Success(Pack);

    private static StopHook Create(FakeValidator validator) => Create(validator, Loaded());

    private static StopHook Create(FakeValidator validator, Result<RulePack, AriadneError> rules) =>
        new(new FakeRules(rules), validator);

    private static Task<string?> RunAsync(StopHook hook, string root, string session, bool active = false, TextWriter? warn = null) =>
        hook.RunAsync(new HookInput { SessionId = session, Cwd = root, StopHookActive = active }, warn ?? TextWriter.Null, CancellationToken.None);

    private static TempProject ProjectWithSolution()
    {
        var project = new TempProject();
        File.WriteAllText(Path.Combine(project.Root, "Shop.slnx"), "");
        return project;
    }

    [Fact]
    public void Serves_the_stop_event() => Create(new FakeValidator(Report())).Event.Should().Be("stop");

    [Fact]
    public async Task Without_recorded_edits_the_validator_is_never_called()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        var validator = new FakeValidator(Report(Violation()));

        var json = await RunAsync(Create(validator), project.Root, session.Id).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Edits_and_a_violation_return_a_stop_document_and_clear_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));

        var json = await RunAsync(Create(validator), project.Root, session.Id).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var output = doc.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().Should().Be("Stop");
        output.GetProperty("additionalContext").GetString().Should().Contain("SHOP-RULE").And.Contain("Keep the layers apart.");
        validator.Calls.Should().Be(1);
        validator.Path.Should().Be(Path.Combine(project.Root, "Shop.slnx"));
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Edits_and_a_clean_report_return_null_and_clear_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report());

        var json = await RunAsync(Create(validator), project.Root, session.Id).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(1);
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task An_active_stop_hook_stays_silent_and_clears_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));

        var json = await RunAsync(Create(validator), project.Root, session.Id, active: true).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_non_cs_edits_return_null_without_validating()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/readme.md");
        var validator = new FakeValidator(Report(Violation()));

        var json = await RunAsync(Create(validator), project.Root, session.Id).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task No_solution_warns_once_and_keeps_the_list()
    {
        using var project = new TempProject();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().Contain("solution");
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task A_validator_failure_warns_once_bounded_and_keeps_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var failure = Result<ArchitectureReport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, new string('e', 5000)));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(new FakeValidator(failure)), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        var lines = warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle();
        lines[0].Length.Should().BeLessThan(700);
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task Invalid_rules_warn_once_and_keep_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        var rules = Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "bad yaml"));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator, rules), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().Contain("bad yaml");
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task Missing_rules_are_silent_and_nothing_is_validated()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        var rules = Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, "none"));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator, rules), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Should().BeEmpty();
    }

    [UnixFact]
    public async Task An_unreadable_edit_list_warns_once_and_stays_fail_open()
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            return; // Root reads any file, so the failure cannot be provoked.
        }

        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var file = EditedFiles.PathOf(session.Id);
        File.SetUnixFileMode(file, UnixFileMode.None);
        var validator = new FakeValidator(Report(Violation()));
        using var warn = new StringWriter();

        try
        {
            var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

            json.Should().BeNull();
            validator.Calls.Should().Be(0);
            warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().StartWith("ariadne:");
        }
        finally
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task The_real_validator_names_a_real_rule_id_for_the_broken_daedalus_fixture()
    {
        FixtureProjects.EnsureRestored();
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var rulesFile = FixtureProjects.Path("daedalus", "daedalus.rules.yaml");
        var pack = resolver.Resolve(loader.LoadFromFile(rulesFile).Value, Path.GetDirectoryName(rulesFile)!);
        pack.IsSuccess.Should().BeTrue(pack.IsFailure ? pack.Error.Message : null);
        var hook = new StopHook(new FakeRules(pack), new SolutionValidator(new SolutionLoader(), new ArchitectureValidator()));
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");

        var json = await RunAsync(hook, FixtureProjects.Path("daedalus", "broken"), session.Id).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var text = doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;
        text.Should().StartWith("Ariadne found").And.MatchRegex("Rule (CA|DAE)-[A-Z-]+:");
    }
}
