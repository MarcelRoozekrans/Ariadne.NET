using Ariadne.NET.Analysis;
using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class GlobTests
{
    [Theory]
    [InlineData("**.Domain.**", "Domain", true)]
    [InlineData("**.Domain.**", "Shop.Domain", true)]
    [InlineData("**.Domain.**", "Shop.Domain.Orders", true)]
    [InlineData("**.Domain.**", "Shop.Domains", false)]
    [InlineData("*.Domain", "Shop.Domain", true)]
    [InlineData("*.Domain", "A.B.Domain", false)]
    [InlineData("Microsoft.EntityFrameworkCore*", "Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.EntityFrameworkCore*", "Microsoft.EntityFrameworkCoreX", true)]
    [InlineData("Microsoft.EntityFrameworkCore*", "Microsoft.EntityFrameworkCore.Sql", false)]
    [InlineData("Microsoft.EntityFrameworkCore.**", "Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.EntityFrameworkCore.**", "Microsoft.EntityFrameworkCore.Sql", true)]
    [InlineData("**", "", true)]
    [InlineData("A", "", false)]
    [InlineData("**.I*Repository", "Shop.Domain.IOrderRepository", true)]
    [InlineData("shop.domain", "Shop.Domain", false)]
    public void Dotted_MatchesNames(string pattern, string name, bool expected)
    {
        var glob = Glob.Dotted(pattern);

        glob.IsSuccess.Should().BeTrue();
        glob.Value.IsMatch(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Rag.NET*", "Rag.NET", true)]
    [InlineData("Rag.NET*", "Rag.NET.Abstractions", true)]
    [InlineData("Rag.NET*", "RagNET", false)]
    [InlineData("Daedalus.Agents", "Daedalus.Agents", true)]
    [InlineData("Daedalus.Agents", "Daedalus.Agents.Tests", false)]
    public void Flat_MatchesNames(string pattern, string name, bool expected)
    {
        var glob = Glob.Flat(pattern);

        glob.IsSuccess.Should().BeTrue();
        glob.Value.IsMatch(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("A.**B")]
    [InlineData("")]
    [InlineData("A..B")]
    public void Dotted_InvalidPatterns_Fail(string pattern)
    {
        var glob = Glob.Dotted(pattern);

        glob.IsFailure.Should().BeTrue();
        glob.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Dotted_DoubleStarInsideSegment_FailureMentionsDoubleStar()
    {
        Glob.Dotted("A.**B").Error.Should().Contain("**");
    }

    [Fact]
    public void Flat_EmptyPattern_Fails()
    {
        Glob.Flat("").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Dotted_ConsecutiveDoubleStars_DoNotBlowUpOnNonMatch()
    {
        var glob = Glob.Dotted("**.**.**.**.**.**.X").Value;
        var name = string.Join('.', Enumerable.Repeat("Seg", 200));

        glob.IsMatch(name).Should().BeFalse();
        glob.IsMatch(name + ".X").Should().BeTrue();
    }

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread, see ZeroAlloc-Net/ZeroAlloc.TestHelpers#106;
    // AllocationGate is not referenced in this repo yet.
    [Fact]
    public void IsMatch_AllocatesNothing()
    {
        var glob = Glob.Dotted("**.Domain.**").Value;
        glob.IsMatch("Shop.Domain.Orders").Should().BeTrue();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            glob.IsMatch("Shop.Domain.Orders");
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Fact]
    public void Flat_IsMatch_AllocatesNothing()
    {
        var glob = Glob.Flat("Rag.NET*").Value;
        glob.IsMatch("Rag.NET.Abstractions").Should().BeTrue();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            glob.IsMatch("Rag.NET.Abstractions");
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Theory]
    [InlineData("Newtonsoft.**", "NEWTONSOFT.Json.Bson", true)]
    [InlineData("**.fluent*", "Shop.FLUENTValidation", true)]
    [InlineData("fluentvalidation", "FluentValidation", true)]
    [InlineData("fluentvalidation", "FluentValidations", false)]
    public void DottedIgnoreCase_MatchesWhateverTheCase(string pattern, string name, bool expected)
    {
        var glob = Glob.Dotted(pattern, ignoreCase: true);

        glob.IsSuccess.Should().BeTrue();
        glob.Value.IsMatch(name).Should().Be(expected);
    }

    [Fact]
    public void DottedAndFlat_StayCaseSensitive()
    {
        Glob.Dotted("fluentvalidation").Value.IsMatch("FluentValidation").Should().BeFalse();
        Glob.Dotted("Shop.**").Value.IsMatch("shop.Domain").Should().BeFalse();
        Glob.Flat("shop*").Value.IsMatch("Shop.Domain").Should().BeFalse();
    }
}
