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
    public void References_only_framework_and_allow_listed_assemblies()
    {
        var allowedPackages = new HashSet<string>(StringComparer.Ordinal) { "ZeroAlloc.Results", "ZeroAlloc.Validation" };
        var frameworkNames = new HashSet<string>(StringComparer.Ordinal) { "netstandard", "mscorlib", "System" };

        var unexpected = typeof(RulePack).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System.", StringComparison.Ordinal)
                && !frameworkNames.Contains(n) && !allowedPackages.Contains(n))
            .ToList();

        unexpected.Should().BeEmpty("Abstractions may only reference the framework and {0}", string.Join(", ", allowedPackages));
    }
}
