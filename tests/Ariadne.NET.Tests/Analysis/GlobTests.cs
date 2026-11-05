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
}
