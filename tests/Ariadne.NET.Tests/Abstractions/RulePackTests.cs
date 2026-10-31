using FluentAssertions;
using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Tests.Abstractions;

public class RulePackTests
{
    [Fact]
    public void RulePack_ShouldHoldLayersAndRoles()
    {
        var pack = new RulePack
        {
            Name = "clean-architecture",
            Version = "1.0.0",
            Author = "platform-team",
            Layers =
            [
                new LayerDefinition { Name = "Domain", Description = "Entities", AllowedDependencies = [] },
                new LayerDefinition { Name = "Application", Description = "Use cases", AllowedDependencies = ["Domain"] }
            ],
            ComponentRoles =
            [
                new ComponentRoleDefinition
                {
                    Name = "Entity",
                    Layer = "Domain",
                    TypeKind = CSharpTypeKind.Class,
                    RequiresInterface = false
                }
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
            }
        };

        pack.Name.Should().Be("clean-architecture");
        pack.Layers.Should().HaveCount(2);
        pack.Layers[1].AllowedDependencies.Should().Contain("Domain");
        pack.ComponentRoles.Should().HaveCount(1);
        pack.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Class);
    }
}
