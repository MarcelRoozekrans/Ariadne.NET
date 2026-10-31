using FluentAssertions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.Tests.RuleEngine;

public class RulePackMergerTests
{
    [Fact]
    public void Merge_ShouldCombineLayersFromMultiplePacks()
    {
        var basePack = new RulePack
        {
            Name = "base",
            Layers = [new LayerDefinition { Name = "Domain", AllowedDependencies = [] }]
        };
        var extensionPack = new RulePack
        {
            Name = "extension",
            Layers = [new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([basePack, extensionPack]);

        result.Layers.Should().HaveCount(2);
        result.Layers.Select(l => l.Name).Should().Contain("Domain").And.Contain("Application");
    }

    [Fact]
    public void Merge_ShouldUnionDependenciesForDuplicateLayers()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            Layers = [new LayerDefinition { Name = "Infra", AllowedDependencies = ["Domain"] }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            Layers = [new LayerDefinition { Name = "Infra", AllowedDependencies = ["Application"] }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.Layers.Should().HaveCount(1);
        result.Layers[0].AllowedDependencies.Should().Contain("Domain").And.Contain("Application");
    }

    [Fact]
    public void Merge_ShouldCombineComponentRoles()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.ComponentRoles.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_LaterPackOverridesDuplicateRole()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Record }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.ComponentRoles.Should().HaveCount(1);
        result.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Record);
    }

    [Fact]
    public void Merge_LastNamingConventionsWin()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            NamingConventions = new NamingConventions { Namespace = "old" }
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            NamingConventions = new NamingConventions { Namespace = "new" }
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.NamingConventions.Namespace.Should().Be("new");
    }

    [Fact]
    public void Merge_LockedRuleCannotBeOverridden()
    {
        var pack1 = new RulePack
        {
            Name = "org",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "ARCH-001",
                    Description = "Domain must not depend on Infrastructure",
                    Locked = true,
                },
            ],
        };
        var pack2 = new RulePack
        {
            Name = "team",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "ARCH-001",
                    Description = "Overridden - should be ignored",
                },
            ],
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.CodeFeatureRules.Should().HaveCount(1);
        result.CodeFeatureRules[0].Description.Should().Be("Domain must not depend on Infrastructure");
    }

    [Fact]
    public void MergeWithDiagnostics_LockedRuleOverrideProducesWarning()
    {
        var pack1 = new RulePack
        {
            Name = "org",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "ARCH-001",
                    Description = "Locked rule",
                    Locked = true,
                },
            ],
        };
        var pack2 = new RulePack
        {
            Name = "team",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "ARCH-001",
                    Description = "Override attempt",
                },
            ],
        };

        var merger = new RulePackMerger();
        var result = merger.MergeWithDiagnostics([pack1, pack2]);

        result.MergedPack.CodeFeatureRules[0].Description.Should().Be("Locked rule");
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Message.Should().Contain("ARCH-001");
        result.Warnings[0].Message.Should().Contain("locked");
    }

    [Fact]
    public void Merge_UnlockedRuleCanBeOverridden()
    {
        var pack1 = new RulePack
        {
            Name = "org",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "NAMING-001",
                    Description = "Original",
                    Locked = false,
                },
            ],
        };
        var pack2 = new RulePack
        {
            Name = "team",
            CodeFeatureRules =
            [
                new CodeFeatureRuleDefinition
                {
                    Id = "NAMING-001",
                    Description = "Team override",
                },
            ],
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.CodeFeatureRules[0].Description.Should().Be("Team override");
    }
}
