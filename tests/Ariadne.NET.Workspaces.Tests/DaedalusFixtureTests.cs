using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Checks the Daedalus-shaped fixture solutions against the schema 2 pack that extends the clean-architecture built-in.</summary>
public sealed class DaedalusFixtureTests
{
    private const string DomainBecause = "The domain is the core of the architecture and depends on no other layer.";
    private const string NoEfCoreBecause = "The domain stays free of persistence frameworks, so it can be tested and reused without a database.";
    private const string NotOnAgentsBecause = "Use cases reach agents only through interfaces they own, so the application never depends on agent code.";
    private const string RagNetBecause = "Retrieval stays inside the agents layer, so no other layer takes a dependency on Rag.NET.";
    private const string SubagentRunnerBecause = "Only the executor uses the subagent runner directly, and the registered composition code wires it.";

    private readonly SolutionValidator _validator = new(new SolutionLoader(), new ArchitectureValidator());

    public DaedalusFixtureTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task Clean_NoViolations()
    {
        var report = await ValidateAsync("clean");

        report.Violations.Should().BeEmpty();
        report.SkippedProjects.Should().BeEmpty();
        report.Projects.Select(p => p.Name).Should().BeEquivalentTo(
            "Daedalus.Domain", "Daedalus.Application", "Daedalus.Infrastructure", "Daedalus.Api", "Daedalus.Agents", "Rag.NET", "Stub.EntityFrameworkCore");
    }

    [Fact]
    public async Task Broken_ExactlyOneViolationPerRule()
    {
        var report = await ValidateAsync("broken");

        report.Violations.Select(v => (v.RuleId, v.Kind, v.Because)).Should().BeEquivalentTo(
            new (string, ViolationKind, string)[]
            {
                ("CA-LAYER-DOMAIN", ViolationKind.DependencyNotAllowed, DomainBecause),
                ("DAE-NO-EFCORE-IN-DOMAIN", ViolationKind.ForbiddenDependency, NoEfCoreBecause),
                ("DAE-APP-NOT-ON-AGENTS", ViolationKind.ForbiddenDependency, NotOnAgentsBecause),
                ("DAE-RAGNET-ONLY-IN-AGENTS", ViolationKind.DependencyNotPermitted, RagNetBecause),
                ("DAE-SUBAGENT-RUNNER", ViolationKind.DependencyNotPermitted, SubagentRunnerBecause),
            });
    }

    private async Task<ArchitectureReport> ValidateAsync(string variant)
    {
        var loader = new YamlRulePackLoader();
        var rulesPath = FixtureProjects.Path("daedalus", "daedalus.rules.yaml");
        var loaded = loader.LoadFromFile(rulesPath);
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");

        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var resolved = resolver.Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue(resolved.IsFailure ? $"{resolved.Error.Message} {resolved.Error.Detail}" : "");

        var result = await _validator.ValidateAsync(resolved.Value, FixtureProjects.Path("daedalus", variant, "Daedalus.slnx")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
