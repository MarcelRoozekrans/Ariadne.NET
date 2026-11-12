using Ariadne.NET.Tests.Shared;
using ArchUnitNET.Loader;
using FluentAssertions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ariadne.NET.Parity.Tests;

[Collection(ParityFixtureCollection.Name)]
public sealed class ArchUnitNetSmokeTests
{
    public ArchUnitNetSmokeTests() => FixtureProjects.EnsureParityBuilt();

    [Fact]
    public void ArchUnitNet_loads_a_net10_fixture_and_flags_the_broken_type()
    {
        var output = Path.Combine(FixtureProjects.ParityRoot, "CNotDependOnAny", "bin", "Debug", "net10.0");
        var architecture = new ArchLoader().LoadFilteredDirectory(output, "Parity.CNotDependOnAny.dll", SearchOption.TopDirectoryOnly).Build();
        var rule = Types().That().ResideInNamespace("Parity.CNotDependOnAny.Broken")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.CNotDependOnAny.Target"));

        var failed = rule.Evaluate(architecture).Where(r => !r.Passed).Select(r => r.EvaluatedObjectIdentifier.ToString());

        failed.Should().ContainSingle().Which.Should().Contain("Parity.CNotDependOnAny.Broken.OrderService");
    }
}
