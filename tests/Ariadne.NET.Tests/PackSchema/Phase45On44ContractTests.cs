using Ariadne.NET.Analysis;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

/// <summary>Pins the key that the Phase 4.5 migration guide uses for target vacuity, so a later change cannot drop it unnoticed.</summary>
public sealed class Phase45On44ContractTests
{
    private const string Pack = """
        schema: 2
        name: contract
        layers:
          - name: Agents
            id: C-L-AGENTS
            because: agents
            namespaces: ["Agents.**"]
        rules:
          - id: C-R1
            because: Subagents run through one seam.
            requireTarget: true
            allowOnly:
              from: { layers: [Agents] }
              to: { types: ["Lib.ISubagentRunner"] }
        """;

    [Fact]
    public void Key_phase_4_5_builds_on_loads_and_validates()
    {
        PackSchemaHarness.LoaderAccepts(Pack).Should().BeTrue();
        PackSchemaHarness.SchemaErrors(Pack).Should().BeEmpty();
        var loaded = new YamlRulePackLoader().LoadFromString(Pack);
        loaded.Value.Rules[0].RequireTarget.Should().BeTrue();
        ((int)ViolationKind.VacuousRule).Should().Be(11);
    }
}
