using System.Text.Json.Nodes;
using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

public class PackSchemaHarnessTests
{
    [Fact]
    public void YamlToJson_types_plain_scalars()
    {
        var json = PackSchemaHarness.YamlToJson("""
            schema: 2
            name: shop
            version: "2"
            allowEmpty: true
            quoted: "true"
            """);

        json["schema"]!.GetValueKind().Should().Be(System.Text.Json.JsonValueKind.Number);
        json["schema"]!.GetValue<long>().Should().Be(2);
        json["name"]!.GetValue<string>().Should().Be("shop");
        json["version"]!.GetValue<string>().Should().Be("2");
        json["allowEmpty"]!.GetValueKind().Should().Be(System.Text.Json.JsonValueKind.True);
        json["quoted"]!.GetValue<string>().Should().Be("true");
    }

    [Fact]
    public void YamlToJson_maps_sequences_and_flow_mappings()
    {
        var json = PackSchemaHarness.YamlToJson("""
            layers:
              - name: Domain
                namespaces: [A.B, "C.D"]
            empty: {}
            """);

        json["layers"].Should().BeOfType<JsonArray>();
        json["layers"]![0]!["namespaces"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Equal("A.B", "C.D");
        json["empty"].Should().BeOfType<JsonObject>();
    }

    private const string Minimal = "schema: 2\nname: x\n";

    [Fact]
    public void Schema_accepts_minimal_pack() => AssertBoth(Minimal, accepted: true);

    [Fact]
    public void Schema_rejects_missing_schema_key() => AssertBoth("name: x\n", accepted: false);

    [Fact]
    public void Schema_rejects_schema_1() => AssertBoth("schema: 1\nname: x\n", accepted: false);

    [Fact]
    public void Schema_rejects_unknown_top_level_key() => AssertBoth(Minimal + "bogus: 1\n", accepted: false);

    [Fact]
    public void Schema_accepts_layers_roles_and_naming_conventions() => AssertBoth("""
        schema: 2
        name: daedalus
        version: "1.0.0"
        author: daedalus-team
        extends:
          - builtin://clean-architecture
        layers:
          - name: Domain
            assemblies: [Daedalus.Domain]
          - name: Agents
            id: DAEDALUS-LAYER-AGENTS
            because: Agents orchestrate use cases; they never reach into the API.
            assemblies: [Daedalus.Agents]
            allowedDependencies: [Domain, Application]
        componentRoles:
          - name: Entity
            layer: Domain
            typeKind: Class
        namingConventions:
          namespace: "{SolutionName}.{Feature}.{Layer}"
          filePath: "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
        """, accepted: true);

    private static void AssertBoth(string yaml, bool accepted)
    {
        PackSchemaHarness.LoaderAccepts(yaml).Should().Be(accepted, "the loader is the authority");
        var errors = PackSchemaHarness.SchemaErrors(yaml);
        if (accepted)
        {
            errors.Should().BeEmpty();
        }
        else
        {
            errors.Should().NotBeEmpty();
        }
    }
}
