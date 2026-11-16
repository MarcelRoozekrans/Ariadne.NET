using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class AttributeArgumentMatcherTests
{
    private const string Shared = """
        namespace Shop
        {
            public enum Lifetime { Transient = 1, Singleton = 3 }

            public class Order<T> { }

            [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
            public sealed class TagAttribute : System.Attribute
            {
                public TagAttribute() { }
                public TagAttribute(string value) { }
                public TagAttribute(string value, int number) { }
                public string? Name { get; set; }
                public bool Flag { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class OtherAttribute : System.Attribute
            {
                public OtherAttribute(string value) { }
            }
        }
        """;

    private static INamedTypeSymbol Find(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) ?? throw new InvalidOperationException($"{metadataName} not found.");

    private static TypedConstant Constant(string parameterType, string argument)
    {
        var compilation = TestCompilation.Create(Shared + $$"""

            namespace Shop
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class ValueAttribute : System.Attribute
                {
                    public ValueAttribute({{parameterType}} value) { }
                }

                [Value({{argument}})]
                public class Target { }
            }
            """);

        return Only(Only(Find(compilation, "Shop.Target").GetAttributes()).ConstructorArguments);
    }

    private static T Only<T>(IReadOnlyList<T> items)
    {
        items.Should().HaveCount(1);
        return items[0];
    }

    private static IReadOnlyList<AttributeData> AttributesOf(string applied)
    {
        var compilation = TestCompilation.Create(Shared + $$"""

            namespace Shop
            {
                {{applied}}
                public class Target { }
            }
            """);

        return Find(compilation, "Shop.Target").GetAttributes();
    }

    [Theory]
    [InlineData("string", "\"a\"", "a", true)]
    [InlineData("string", "\"a\"", "A", false)]
    [InlineData("char", "'c'", "c", true)]
    [InlineData("char", "'c'", "cc", false)]
    [InlineData("bool", "true", "True", true)]
    [InlineData("bool", "true", "true", true)]
    [InlineData("bool", "true", "false", false)]
    [InlineData("byte", "2", "2", true)]
    [InlineData("byte", "2", "2.0", true)]
    [InlineData("short", "2", "2", true)]
    [InlineData("short", "2", "2.0", true)]
    [InlineData("int", "2", "2", true)]
    [InlineData("int", "2", "2.0", true)]
    [InlineData("long", "2", "2", true)]
    [InlineData("long", "2", "2.0", true)]
    [InlineData("uint", "2", "2", true)]
    [InlineData("uint", "2", "2.0", true)]
    [InlineData("ulong", "2", "2", true)]
    [InlineData("ulong", "2", "2.0", true)]
    [InlineData("sbyte", "-2", "-2", true)]
    [InlineData("sbyte", "-2", "-2.0", true)]
    [InlineData("ushort", "2", "2", true)]
    [InlineData("ushort", "2", "2.0", true)]
    [InlineData("int", "2", "2.", true)]
    [InlineData("int", "-2", "-2", true)]
    [InlineData("uint", "2", "-2", false)]
    [InlineData("float", "1.5f", "1.5", true)]
    [InlineData("double", "1.5", "1.5", true)]
    [InlineData("double", "2", "2", true)]
    [InlineData("double", "2", "2.0", true)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "Singleton", true)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "Shop.Lifetime.Singleton", true)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "3", true)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "Transient", false)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "Other.Lifetime.Singleton", false)]
    [InlineData("Shop.Lifetime", "Shop.Lifetime.Singleton", "1", false)]
    [InlineData("System.Type", "typeof(Shop.Order<int>)", "Shop.Order", true)]
    [InlineData("System.Type", "typeof(Shop.Order<int>)", "Shop.Order`1", false)]
    [InlineData("int[]", "new[] { 1 }", "1", false)]
    [InlineData("int", "2", "two", false)]
    [InlineData("long", "9007199254740993", "9007199254740992", false)]
    [InlineData("long", "9007199254740993", "9007199254740993", true)]
    [InlineData("double", "1e100", "1e100", true)]
    [InlineData("int", "2", "1e100", false)]
    [InlineData("float", "0.1f", "0.1", true)]
    [InlineData("int", "2", "2.00000000000000000000000000001", false)]
    [InlineData("int", "2", "2e0", false)]
    [InlineData("long", "2", "1e500000000", false)]
    [InlineData("ulong", "18446744073709551615", "18446744073709551615", true)]
    [InlineData("long", "-1", "18446744073709551615", false)]
    [InlineData("bool", "false", "false", true)]
    [InlineData("bool", "false", "FALSE", true)]
    [InlineData("bool", "false", "true", false)]
    [InlineData("float", "1.5f", "abc", false)]
    [InlineData("double", "1.5", "abc", false)]
    [InlineData("object", "null", "1", false)]
    [InlineData("object", "null", "null", false)]
    public void ValueEquals_ComparesByKind(string parameterType, string argument, string text, bool expected)
    {
        var constant = Constant(parameterType, argument);

        AttributeArgumentMatcher.ValueEquals(constant, text, new TypeNames()).Should().Be(expected);
    }

    [Fact]
    public void ValueEquals_ErrorConstant_NeverMatches()
    {
        var constant = BrokenConstant("Missing", "null");

        constant.Kind.Should().Be(TypedConstantKind.Error);
        AttributeArgumentMatcher.ValueEquals(constant, "null", new TypeNames()).Should().BeFalse();
        AttributeArgumentMatcher.ValueEquals(constant, "Missing", new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void ValueEquals_TypeofAnUnresolvedType_NeverMatches()
    {
        var constant = BrokenConstant("System.Type", "typeof(Missing)");

        constant.Kind.Should().Be(TypedConstantKind.Type);
        AttributeArgumentMatcher.ValueEquals(constant, "Missing", new TypeNames()).Should().BeFalse();
    }

    // A compilation with errors, which TestCompilation refuses, so the attribute argument binds to an error.
    private static TypedConstant BrokenConstant(string parameterType, string argument)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            public sealed class ValueAttribute : System.Attribute
            {
                public ValueAttribute({{parameterType}} value) { }
            }

            [Value({{argument}})]
            public class Target { }
            """);
        var compilation = CSharpCompilation.Create(
            "Broken",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return Only(Only(Find(compilation, "Target").GetAttributes()).ConstructorArguments);
    }

    [Fact]
    public void Arguments_match_in_any_position_including_named_values()
    {
        var instance = Only(AttributesOf("""[Tag("a", 2, Name = "n", Flag = true)]"""));

        Matcher(arguments: ["2", "n", "a", "true"]).Matches(instance, new TypeNames()).Should().BeTrue();
        Matcher(arguments: ["a", "b"]).Matches(instance, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void All_values_must_come_from_one_instance()
    {
        var attributes = AttributesOf("""[Tag("a")] [Tag("b")]""");
        var matcher = Matcher(arguments: ["a", "b"]);

        attributes.Should().HaveCount(2);
        attributes.Should().NotContain(instance => matcher.Matches(instance, new TypeNames()));
    }

    [Fact]
    public void Named_requires_name_and_value()
    {
        var instance = Only(AttributesOf("""[Tag("a", Name = "n")]"""));

        Matcher(named: Named(("Name", "n"))).Matches(instance, new TypeNames()).Should().BeTrue();
        Matcher(named: Named(("Name", "a"))).Matches(instance, new TypeNames()).Should().BeFalse();
        Matcher(named: Named(("name", "n"))).Matches(instance, new TypeNames()).Should().BeFalse();
        Matcher(named: Named(("Flag", "false"))).Matches(instance, new TypeNames()).Should().BeFalse();
        Matcher(arguments: ["a"], named: Named(("Name", "n"))).Matches(instance, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void Missing_attribute_glob_matches_any_class()
    {
        var attributes = AttributesOf("""[Tag("a")] [Other("a")]""");
        var matcher = Matcher(arguments: ["a"]);

        attributes.Should().HaveCount(2);
        attributes.Should().OnlyContain(instance => matcher.Matches(instance, new TypeNames()));
    }

    [Fact]
    public void Attribute_glob_must_match_the_class()
    {
        var instance = Only(AttributesOf("""[Tag("a")]"""));

        Matcher("Shop.TagAttribute", arguments: ["a"]).Matches(instance, new TypeNames()).Should().BeTrue();
        Matcher("**.Tag*", arguments: ["a"]).Matches(instance, new TypeNames()).Should().BeTrue();
        Matcher("Shop.OtherAttribute", arguments: ["a"]).Matches(instance, new TypeNames()).Should().BeFalse();
        Matcher("Tag", arguments: ["a"]).Matches(instance, new TypeNames()).Should().BeFalse();
    }

    private static Dictionary<string, string> Named(params (string Name, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);

    private static AttributeArgumentMatcher Matcher(string? attribute = null, string[]? arguments = null, Dictionary<string, string>? named = null) =>
        AttributeArgumentMatcher.From(new AttributeArgumentMatch
        {
            Attribute = attribute,
            Arguments = arguments ?? [],
            Named = named ?? new Dictionary<string, string>(StringComparer.Ordinal),
        });
}
