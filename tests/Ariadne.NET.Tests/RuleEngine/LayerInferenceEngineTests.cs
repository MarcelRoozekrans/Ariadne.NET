using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;

namespace Ariadne.NET.Tests.RuleEngine;

public class LayerInferenceEngineTests
{
    private static RulePack CreateTestPack()
    {
        return new RulePack
        {
            Name = "clean-architecture",
            ComponentRoles =
            [
                new ComponentRoleDefinition
                {
                    Name = "Entity",
                    Layer = "Domain",
                    Description = "Domain entity",
                    AlternativeNames = ["DomainEntity", "AggregateRoot"],
                },
                new ComponentRoleDefinition
                {
                    Name = "CommandHandler",
                    Layer = "Application",
                    Description = "Handles commands",
                    AlternativeNames = ["Handler"],
                },
                new ComponentRoleDefinition
                {
                    Name = "Repository",
                    Layer = "Infrastructure",
                    Accessibility = "internal",
                    Description = "Data access repository",
                    AlternativeNames = ["Repo"],
                },
            ],
        };
    }

    [Fact]
    public void Infer_Entity_ReturnsOkWithDomainLayer()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "Entity");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Domain");
        result.RoleName.Should().Be("Entity");
    }

    [Fact]
    public void Infer_CommandHandler_ReturnsOkWithApplicationLayer()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "CommandHandler");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Application");
        result.RoleName.Should().Be("CommandHandler");
    }

    [Fact]
    public void Infer_Repository_ReturnsOkWithInfrastructureLayer()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "Repository");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Infrastructure");
        result.RoleName.Should().Be("Repository");
    }

    [Fact]
    public void Infer_UnknownRole_ReturnsInvalid()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "UnknownRole");

        result.Status.Should().Be(LayerInferenceStatus.Invalid);
        result.Message.Should().Be("Unknown role: UnknownRole");
    }

    [Fact]
    public void Infer_CaseInsensitiveMatch_ReturnsOk()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "entity");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Domain");
        result.RoleName.Should().Be("Entity");
    }

    [Fact]
    public void Infer_AlternativeName_ReturnsOkWithPrimaryRoleName()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "AggregateRoot");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Domain");
        result.RoleName.Should().Be("Entity");
    }

    [Fact]
    public void Infer_AlternativeNameCaseInsensitive_ReturnsOk()
    {
        var engine = new LayerInferenceEngine();
        var result = engine.Infer(CreateTestPack(), "repo");

        result.Status.Should().Be(LayerInferenceStatus.Ok);
        result.Layer.Should().Be("Infrastructure");
        result.RoleName.Should().Be("Repository");
    }

    [Fact]
    public void Infer_AmbiguousAlternativeName_ReturnsAmbiguous()
    {
        var pack = new RulePack
        {
            Name = "ambiguous-pack",
            ComponentRoles =
            [
                new ComponentRoleDefinition
                {
                    Name = "ServiceA",
                    Layer = "Application",
                    AlternativeNames = ["Service"],
                },
                new ComponentRoleDefinition
                {
                    Name = "ServiceB",
                    Layer = "Infrastructure",
                    AlternativeNames = ["Service"],
                },
            ],
        };

        var engine = new LayerInferenceEngine();
        var result = engine.Infer(pack, "Service");

        result.Status.Should().Be(LayerInferenceStatus.Ambiguous);
        result.AmbiguousOptions.Should().HaveCount(2);
    }
}
