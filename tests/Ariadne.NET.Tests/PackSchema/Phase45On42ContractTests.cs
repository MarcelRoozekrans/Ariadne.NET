using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

/// <summary>Pins the keys that the Phase 4.5 migration guide uses, so a later change cannot drop them unnoticed.</summary>
public sealed class Phase45On42ContractTests
{
    private const string Pack = """
        schema: 2
        name: contract
        rules:
          - id: R1
            because: b
            require:
              for:
                names: ["*Repository"]
                kinds: [class, record]
                accessibility: [public]
                assignableTo: ["System.Object"]
                sealed: false
                abstract: false
                static: false
                nested: false
              is: { names: ["*Repository"] }
              isNot: { types: ["**.Legacy.**"] }
        """;

    [Fact]
    public void Keys_phase_4_5_builds_on_load_and_validate()
    {
        PackSchemaHarness.LoaderAccepts(Pack).Should().BeTrue();
        PackSchemaHarness.SchemaErrors(Pack).Should().BeEmpty();
        ((int)ViolationKind.RequirementNotMet).Should().Be(13);
    }
}
