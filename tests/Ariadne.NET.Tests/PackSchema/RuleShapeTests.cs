using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

public class RuleShapeTests
{
    private const string Head = "schema: 2\nname: x\nrules:\n";

    [Theory]
    [InlineData("  - id: R1\n    because: b\n    bogus: 1\n    forbid:\n      from: { namespaces: [A] }\n      to: { namespaces: [B] }\n")]
    [InlineData("  - id: R1\n    because: b\n")]
    [InlineData("  - id: R1\n    because: b\n    forbid:\n      from: { namespaces: [A] }\n      to: { namespaces: [B] }\n    allowOnly:\n      from: { namespaces: [A] }\n      to: { namespaces: [B] }\n")]
    [InlineData("  - id: R1\n    because: b\n    forbid:\n      from: { namespaces: [A], bogus: [C] }\n      to: { namespaces: [B] }\n")]
    [InlineData("  - id: R1\n    because: b\n    forbid:\n      from: { namespaces: [A] }\n      to: { namespaces: [B] }\n      bogus: 1\n")]
    [InlineData("  - id: R1\n    because: b\n    allowEmpty: yes please\n    forbid:\n      from: { namespaces: [A] }\n      to: { namespaces: [B] }\n")]
    public void Malformed_rule_is_rejected_by_schema_and_loader(string rule)
    {
        var yaml = Head + rule;

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeFalse();
        PackSchemaHarness.SchemaErrors(yaml).Should().NotBeEmpty();
    }

    [Fact]
    public void Rule_amendment_in_a_child_pack_is_accepted()
    {
        var yaml = "schema: 2\nname: x\nextends: [builtin://clean-architecture]\nrules:\n  - id: CA-X\n    exceptions:\n      - types: [A.B]\n        because: legacy\n";

        PackSchemaHarness.SchemaErrors(yaml).Should().BeEmpty();
    }
}
