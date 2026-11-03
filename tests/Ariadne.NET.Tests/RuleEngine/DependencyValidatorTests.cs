using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;

namespace Ariadne.NET.Tests.RuleEngine;

public class DependencyValidatorTests
{
    private static RulePack CreateCleanArchitecturePack()
    {
        return new RulePack
        {
            Name = "clean-architecture",
            Layers =
            [
                new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
                new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
                new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
                new LayerDefinition { Name = "WebApi", AllowedDependencies = ["Domain", "Application", "Infrastructure"] },
            ],
        };
    }

    [Fact]
    public void Validate_ApplicationDependingOnDomain_ReturnsNoViolations()
    {
        var validator = new DependencyValidator();
        var pack = CreateCleanArchitecturePack();

        var violations = validator.Validate(pack, "Application", ["Domain"]);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DomainDependingOnInfrastructure_ReturnsViolation()
    {
        var validator = new DependencyValidator();
        var pack = CreateCleanArchitecturePack();

        var violations = validator.Validate(pack, "Domain", ["Infrastructure"]);

        violations.Should().ContainSingle();
        violations[0].SourceLayer.Should().Be("Domain");
        violations[0].TargetLayer.Should().Be("Infrastructure");
        violations[0].Message.Should().Contain("not allowed to depend on");
    }

    [Fact]
    public void Validate_InfrastructureDependingOnApplicationAndDomain_ReturnsNoViolations()
    {
        var validator = new DependencyValidator();
        var pack = CreateCleanArchitecturePack();

        var violations = validator.Validate(pack, "Infrastructure", ["Application", "Domain"]);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnknownLayer_ReturnsViolation()
    {
        var validator = new DependencyValidator();
        var pack = CreateCleanArchitecturePack();

        var violations = validator.Validate(pack, "NonExistent", ["Domain"]);

        violations.Should().ContainSingle();
        violations[0].SourceLayer.Should().Be("NonExistent");
        violations[0].Message.Should().Contain("Unknown layer: NonExistent");
    }
}
