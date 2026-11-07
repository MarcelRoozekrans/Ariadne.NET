using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class LayerProbeParityTests
{
    private static string FixtureRules()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "daedalus", "daedalus.rules.yaml")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "tests", "fixtures", "daedalus", "daedalus.rules.yaml");
    }

    private static RulePack DaedalusPack()
    {
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var user = loader.LoadFromFile(FixtureRules());
        user.IsSuccess.Should().BeTrue();
        var resolved = resolver.Resolve(user.Value, Path.GetDirectoryName(FixtureRules()));
        resolved.IsSuccess.Should().BeTrue();
        return resolved.Value;
    }

    private static INamedTypeSymbol TypeT(string assembly, string source)
    {
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)]);
        return compilation.GetSymbolsWithName("T", SymbolFilter.Type).OfType<INamedTypeSymbol>().First();
    }

    [Fact]
    public void Agrees_with_LayerMap_for_a_range_of_assemblies_and_namespaces()
    {
        var pack = DaedalusPack();
        var map = new LayerMap(pack);
        string[] assemblies = ["Daedalus.Domain", "Daedalus.Application", "Daedalus.Infrastructure", "Daedalus.Api", "Daedalus.Agents", "Rag.NET", "Stub.EntityFrameworkCore", "Other"];
        string[] namespaces =
        [
            "", "Daedalus.Domain", "Daedalus.Application.Planning", "Daedalus.Agents", "Daedalus.Agents.Contracts",
            "Daedalus.Agents.Runtime", "Daedalus.Agents.Runtime.Retrieval", "Daedalus.Agents.RuntimeX", "Microsoft.EntityFrameworkCore", "Rag.NET",
        ];

        var compared = 0;
        foreach (var assembly in assemblies)
        {
            foreach (var ns in namespaces)
            {
                var source = ns.Length == 0 ? "public class T {}" : $"namespace {ns} {{ public class T {{}} }}";
                var type = TypeT(assembly, source);

                LayerProbe.LayersOf(pack, assembly, ns).Should().Equal(map.LayersOf(type), $"{assembly} / '{ns}'");
                compared++;
            }
        }

        compared.Should().Be(assemblies.Length * namespaces.Length);
    }
}
