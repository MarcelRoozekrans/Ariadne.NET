using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Checks the module isolation rule of the modular-monolith built-in on the <c>modular</c> fixture solution.</summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class ModularMonolithTests
{
    private const string RuleId = "MM-MODULES-INDEPENDENT";

    private static readonly SolutionValidator Validator = new(new SolutionLoader(), new ArchitectureValidator());

    public ModularMonolithTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task Module_core_may_not_use_another_module_core()
    {
        var report = await ValidateAsync();

        report.SkippedProjects.Should().BeEmpty();
        var violation = report.Violations.Should().ContainSingle(v => string.Equals(v.RuleId, RuleId, StringComparison.Ordinal)).Which;
        violation.Kind.Should().Be(ViolationKind.SliceDependency);
        violation.SourceType.Should().Be("Modular.Orders.Core.OrderService");
        violation.Target.Should().Be("Modular.Billing.Core.InvoiceService");
        violation.Because.Should().Be("A module may use another module only through that module's public API.");
    }

    [Fact]
    public async Task Public_apis_and_the_host_give_no_other_violation()
    {
        var report = await ValidateAsync();

        report.Violations.Select(v => (v.RuleId, v.SourceType, v.Target)).Should().Equal(
            (RuleId, "Modular.Orders.Core.OrderService", "Modular.Billing.Core.InvoiceService"));
    }

    private static async Task<ArchitectureReport> ValidateAsync()
    {
        var loader = new YamlRulePackLoader();
        var loaded = loader.LoadFromFile(FixtureProjects.Path("modular", ".architecture", "rules.yaml"));
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");

        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var resolved = resolver.Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue(resolved.IsFailure ? $"{resolved.Error.Message} {resolved.Error.Detail}" : "");

        var result = await Validator.ValidateAsync(resolved.Value, FixtureProjects.Path("modular", "Modular.slnx")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
