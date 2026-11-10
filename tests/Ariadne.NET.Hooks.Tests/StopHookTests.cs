using System.Runtime.Versioning;
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

    private sealed class FakeValidator(Result<ArchitectureReport, AriadneError> result, Exception? throws = null) : IPackSetValidator
    {
        public int Calls { get; private set; }

        public string? ValidatedPath { get; private set; }

        public Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            ValidatedPath = path;
            if (throws is not null)
            {
                throw throws;
            }

            return Task.FromResult(result);
        }
    }

    private static StopHook Create(FakeValidator validator) => new(validator, _ => null);

    private static Task<string?> RunAsync(StopHook hook, string root, string session, bool active = false, TextWriter? warn = null) =>
        hook.RunAsync(new HookInput { SessionId = session, Cwd = root, StopHookActive = active }, warn ?? TextWriter.Null, CancellationToken.None);

    private static TempProject ProjectWithSolution()
    {
        var project = new TempProject();
        File.WriteAllText(Path.Combine(project.Root, "Shop.slnx"), "");
        project.WriteRules();
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
    public async Task Stop_hook_blocks_on_a_vacuous_layer()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var vacuous = new ArchitectureViolation
        {
            RuleId = "SHOP-LAYER-DOMAIN",
            Kind = ViolationKind.VacuousLayer,
            SourceType = "Domain",
            Message = "Layer 'Domain' covers no type in the 1 project(s) loaded.",
        };
        var validator = new FakeValidator(Report(vacuous));

        var json = await RunAsync(Create(validator), project.Root, session.Id).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()
            .Should().Contain("[VacuousLayer] Domain").And.Contain("covers no type");
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
        validator.ValidatedPath.Should().Be(Path.Combine(project.Root, "Shop.slnx"));
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task After_a_cd_into_a_subfolder_the_stop_hook_still_gives_feedback_for_the_project_solution()
    {
        using var project = ProjectWithSolution();
        project.WriteRules();
        var below = Path.Combine(project.Root, "src", "Shop.Core");
        Directory.CreateDirectory(below);
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));

        var json = await RunAsync(Create(validator), below, session.Id).ConfigureAwait(true);

        json.Should().NotBeNull().And.Contain("SHOP-RULE");
        validator.ValidatedPath.Should().Be(Path.Combine(project.Root, "Shop.slnx"));
    }

    [Fact]
    public async Task Editing_a_file_twice_is_validated_once()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        var hook = Create(validator);

        var first = await RunAsync(hook, project.Root, session.Id).ConfigureAwait(true);
        var second = await RunAsync(hook, project.Root, session.Id).ConfigureAwait(true);

        first.Should().NotBeNull();
        second.Should().BeNull();
        validator.Calls.Should().Be(1);
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
    public async Task No_packs_and_no_solution_is_silent_and_keeps_the_list()
    {
        using var project = new TempProject();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Should().BeEmpty();
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task No_solution_warns_once_and_keeps_the_list()
    {
        using var project = new TempProject();
        project.WriteRules();
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
    public async Task Invalid_packs_warn_once_and_keep_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Result<ArchitectureReport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "a-pack.yaml: bad yaml")));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(1);
        warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().Contain("bad yaml");
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task Missing_packs_are_silent_and_the_edits_are_kept()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Result<ArchitectureReport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, "none")));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(1);
        warn.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task An_edit_list_that_cannot_be_read_warns_once_and_stays_fail_open()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        using var warn = new StringWriter();

        // An exclusive lock makes the read fail with an IOException on every platform and for every user, root included.
        using var locked = new FileStream(Directory.GetFiles(EditedFiles.SessionFolder(session.Id)).First(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().StartWith("ariadne:");
    }

    [Fact]
    public async Task A_throwing_validator_is_reported_once_bounded_by_the_host_and_keeps_the_list()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(), new InvalidOperationException(new string('x', 30_000)));
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var input = JsonSerializer.Serialize(new { session_id = session.Id, cwd = project.Root });
        var exit = await new HookHost([Create(validator)]).RunAsync("stop", new StringReader(input), stdout, stderr, CancellationToken.None).ConfigureAwait(true);

        exit.Should().Be(0);
        stdout.ToString().Should().BeEmpty();
        var lines = stderr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle();
        lines[0].Length.Should().BeLessThan(700);
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task A_cancelled_validation_propagates_and_keeps_the_list_and_the_host_exits_zero()
    {
        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(), new OperationCanceledException());
        var hook = Create(validator);

        var act = () => RunAsync(hook, project.Root, session.Id);
        await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var input = JsonSerializer.Serialize(new { session_id = session.Id, cwd = project.Root });
        var exit = await new HookHost([hook]).RunAsync("stop", new StringReader(input), stdout, stderr, CancellationToken.None).ConfigureAwait(true);

        exit.Should().Be(0);
        stdout.ToString().Should().BeEmpty();
        stderr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Two_solutions_warn_once_and_keep_the_list()
    {
        using var project = ProjectWithSolution();
        File.WriteAllText(Path.Combine(project.Root, "Other.slnx"), "");
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        var validator = new FakeValidator(Report(Violation()));
        using var warn = new StringWriter();

        var json = await RunAsync(Create(validator), project.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle();
        EditedFiles.Read(session.Id).Should().Equal("/x/A.cs");
    }

    [Fact]
    public async Task The_real_validator_names_a_real_rule_id_for_the_broken_daedalus_fixture()
    {
        FixtureProjects.EnsureRestored();
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var hook = new StopHook(new PackSetValidator(new SolutionLoader(), new ArchitectureValidator(), loader, resolver), _ => null);
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");

        // The hook walks up to the nearest .architecture folder, which for the fixture folder is the repository's own.
        // So the project root is a temp folder with the fixture's pack and a solution that points at the fixture projects.
        using var project = new TempProject();
        File.Copy(FixtureProjects.Path("daedalus", "daedalus.rules.yaml"), Path.Combine(project.Root, ".architecture", "daedalus.yaml"));
        var broken = FixtureProjects.Path("daedalus", "broken");
        var entries = Directory.GetFiles(broken, "*.csproj", SearchOption.AllDirectories)
            .Select(f => $"  <Project Path=\"{Path.GetRelativePath(project.Root, f).Replace('\\', '/')}\" />");
        File.WriteAllText(Path.Combine(project.Root, "Daedalus.slnx"), $"<Solution>\n{string.Join('\n', entries)}\n</Solution>\n");

        var json = await RunAsync(hook, project.Root, session.Id).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var text = doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;
        text.Should().StartWith("Ariadne found 9 architecture violation(s)");
        foreach (var id in new[] { "CA-LAYER-DOMAIN", "DAE-NO-EFCORE-IN-DOMAIN", "DAE-APP-NOT-ON-AGENTS", "DAE-RAGNET-ONLY-IN-AGENTS", "DAE-SUBAGENT-RUNNER",
            "DAE-API-KEEPS-NO-COPY-OF-DTOS", "DAE-AGENTS-NO-STATE-MACHINE", "DAE-APP-NO-VALIDATION-LIB", "DAE-DOMAIN-NO-YAML" })
        {
            text.Should().Contain($"Rule {id}:");
        }
    }

    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    // A read-only session folder lets the records be read but not deleted. A privileged process ignores the mode.
    private static bool CanBlockDeletes => !Environment.IsPrivilegedProcess;

    [UnsupportedOSPlatform("windows")]
    private static void BlockDeletes(string session) =>
        File.SetUnixFileMode(EditedFiles.SessionFolder(session), UnixFileMode.UserRead | UnixFileMode.UserExecute);

    [UnsupportedOSPlatform("windows")]
    private static void AllowDeletes(string session) => File.SetUnixFileMode(EditedFiles.SessionFolder(session), Private);

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_stop_hook_that_cannot_clear_the_edits_after_the_check_warns_and_still_gives_feedback()
    {
        if (!CanBlockDeletes)
        {
            return;
        }

        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        using var warn = new StringWriter();
        string? json;
        BlockDeletes(session.Id);
        try
        {
            json = await RunAsync(Create(new FakeValidator(Report(Violation()))), project.Root, session.Id, warn: warn).ConfigureAwait(true);
        }
        finally
        {
            AllowDeletes(session.Id);
        }

        json.Should().Contain("SHOP-RULE");
        warn.ToString().Should().Contain("could not clear the edited files");
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_second_stop_that_cannot_clear_the_edits_warns_and_stays_silent()
    {
        if (!CanBlockDeletes)
        {
            return;
        }

        using var project = ProjectWithSolution();
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");
        using var warn = new StringWriter();
        var validator = new FakeValidator(Report(Violation()));
        string? json;
        BlockDeletes(session.Id);
        try
        {
            json = await RunAsync(Create(validator), project.Root, session.Id, active: true, warn: warn).ConfigureAwait(true);
        }
        finally
        {
            AllowDeletes(session.Id);
        }

        json.Should().BeNull();
        validator.Calls.Should().Be(0);
        warn.ToString().Should().Contain("could not clear the edited files");
    }

    [Fact]
    public async Task On_twopacks_the_feedback_names_the_broken_pack()
    {
        FixtureProjects.EnsureRestored();
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var twopacks = FixtureProjects.Path("twopacks");
        var hook = new StopHook(new PackSetValidator(new SolutionLoader(), new ArchitectureValidator(), loader, resolver), name => string.Equals(name, "CLAUDE_PROJECT_DIR", StringComparison.Ordinal) ? twopacks : null);
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/x/A.cs");

        // The project root is found through rules.yaml, which twopacks lacks, so the project dir comes from the environment.
        using var outside = new TempProject();
        using var warn = new StringWriter();
        var json = await RunAsync(hook, outside.Root, session.Id, warn: warn).ConfigureAwait(true);

        json.Should().NotBeNull(warn.ToString());
        using var doc = JsonDocument.Parse(json!);
        var text = doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;
        text.Should().Contain("broken-pack").And.Contain("SHOP-DOMAIN-NO-INFRASTRUCTURE").And.NotContain("clean-pack");
    }
}
