using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

/// <summary>Pins the keys that the Phase 4.5 migration guide uses for slices, so a later change cannot drop them unnoticed.</summary>
public sealed class Phase45On43ContractTests
{
    private const string FreeOfCycles = """
        schema: 2
        name: contract
        rules:
          - id: R1
            because: b
            slices:
              matching: ["Shop.(*)"]
              freeOfCycles: true
        """;

    private const string Independent = """
        schema: 2
        name: contract
        rules:
          - id: R1
            because: b
            slices:
              matching: ["Shop.(**).Core"]
              independent: true
              allow: { namespaces: ["Shop.*.Public.**"] }
        """;

    [Fact]
    public void Keys_phase_4_5_builds_on_load_and_validate()
    {
        foreach (var pack in new[] { FreeOfCycles, Independent })
        {
            PackSchemaHarness.LoaderAccepts(pack).Should().BeTrue();
            PackSchemaHarness.SchemaErrors(pack).Should().BeEmpty();
        }

        ((int)ViolationKind.SliceCycle).Should().Be(14);
        ((int)ViolationKind.SliceDependency).Should().Be(15);
    }
}
