using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Parity.Tests;

public sealed class TypeNamesTests
{
    [Theory]
    [InlineData("Ns.Plain", "Ns.Plain")]
    [InlineData("Ns.Outer+Inner", "Ns.Outer.Inner")]
    [InlineData("Ns.Outer.Inner", "Ns.Outer.Inner")]
    [InlineData("Ns.Foo`1", "Ns.Foo`1")]
    [InlineData("Ns.Foo<T>", "Ns.Foo`1")]
    [InlineData("Ns.Map<TKey, TValue>", "Ns.Map`2")]
    [InlineData("Ns.Outer`1+Inner", "Ns.Outer`1.Inner")]
    [InlineData("Ns.Outer<T>.Inner", "Ns.Outer`1.Inner")]
    [InlineData("Ns.Outer<Ns.Foo<int>>.Inner", "Ns.Outer`1.Inner")]
    [InlineData("Ns.Foo`1[[System.Int32, System.Private.CoreLib]]", "Ns.Foo`1")]
    public void Canonical_maps_both_styles_to_one_form(string input, string expected)
        => TypeNames.Canonical(input).Should().Be(expected);
}
