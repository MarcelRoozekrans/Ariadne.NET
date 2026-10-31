using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Tests.Abstractions;

public class AbstractionsAssemblyTests
{
    [Fact]
    public void Schema_and_contracts_live_in_the_abstractions_assembly()
    {
        var types = new[]
        {
            typeof(RulePack), typeof(LayerDefinition), typeof(ComponentRoleDefinition),
            typeof(CodeFeatureRuleDefinition), typeof(NamingConventions), typeof(CSharpTypeKind),
            typeof(IRulePackLoader), typeof(IRulePackResolver), typeof(IBuiltinPackRegistry),
        };

        types.Should().AllSatisfy(t =>
        {
            t.Assembly.GetName().Name.Should().Be("Ariadne.NET.Abstractions");
            t.Namespace.Should().Be("Ariadne.NET.Abstractions");
        });
    }

    [Fact]
    public void References_only_framework_assemblies()
    {
        var referenced = typeof(RulePack).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        referenced.Should().OnlyContain(n => n.StartsWith("System", StringComparison.Ordinal)
            || n == "netstandard" || n == "mscorlib" || n == "ZeroAlloc.Results");
    }
}
