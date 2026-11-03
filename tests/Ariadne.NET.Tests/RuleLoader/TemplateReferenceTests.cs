using Ariadne.NET.Abstractions;
using FluentAssertions;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.RuleLoader;

public class TemplateReferenceTests
{
    [Fact]
    public void ParseNames_CompositeTemplate_ShouldReturnEachName()
    {
        var result = TemplateReference.ParseNames("clean-architecture+cqrs");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal("clean-architecture", "cqrs");
    }

    [Fact]
    public void ToExtends_SingleTemplate_ShouldReturnOneBuiltinReference()
    {
        var result = TemplateReference.ToExtends("clean-architecture");

        result.Value.Should().Equal("builtin://clean-architecture");
    }

    [Fact]
    public void ToExtends_CompositeTemplate_ShouldReturnBuiltinReferencePerPart()
    {
        var result = TemplateReference.ToExtends("clean-architecture+cqrs");

        result.Value.Should().Equal("builtin://clean-architecture", "builtin://cqrs");
    }

    [Fact]
    public void ToExtends_ShouldTrimPartsAndSkipEmptyOnes()
    {
        var result = TemplateReference.ToExtends(" clean-architecture + +cqrs ");

        result.Value.Should().Equal("builtin://clean-architecture", "builtin://cqrs");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" + ")]
    public void ToExtends_WithoutTemplateNames_ShouldReturnInvalid(string template)
    {
        var result = TemplateReference.ToExtends(template);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain($"'{template}'");
    }
}
