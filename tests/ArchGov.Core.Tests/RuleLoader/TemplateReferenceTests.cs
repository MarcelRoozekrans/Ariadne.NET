using FluentAssertions;
using ArchGov.Core.RuleLoader;

namespace ArchGov.Core.Tests.RuleLoader;

public class TemplateReferenceTests
{
    [Fact]
    public void ParseNames_CompositeTemplate_ShouldReturnEachName()
    {
        var result = TemplateReference.ParseNames("clean-architecture+cqrs");

        result.Should().Equal("clean-architecture", "cqrs");
    }

    [Fact]
    public void ToExtends_SingleTemplate_ShouldReturnOneBuiltinReference()
    {
        var result = TemplateReference.ToExtends("clean-architecture");

        result.Should().Equal("builtin://clean-architecture");
    }

    [Fact]
    public void ToExtends_CompositeTemplate_ShouldReturnBuiltinReferencePerPart()
    {
        var result = TemplateReference.ToExtends("clean-architecture+cqrs");

        result.Should().Equal("builtin://clean-architecture", "builtin://cqrs");
    }

    [Fact]
    public void ToExtends_ShouldTrimPartsAndSkipEmptyOnes()
    {
        var result = TemplateReference.ToExtends(" clean-architecture + +cqrs ");

        result.Should().Equal("builtin://clean-architecture", "builtin://cqrs");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" + ")]
    public void ToExtends_WithoutTemplateNames_ShouldThrow(string template)
    {
        var act = () => TemplateReference.ToExtends(template);

        act.Should().Throw<ArgumentException>();
    }
}
