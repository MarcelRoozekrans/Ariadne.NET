using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

public sealed class RazorGeneratorTests
{
    private const string Razor = "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator";

    [Theory]
    [InlineData("/r/obj/Debug/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/" + Razor + "/Components/Counter_razor.g.cs", "Components/Counter_razor.g.cs")]
    [InlineData(@"C:\r\obj\Debug\net10.0\Microsoft.CodeAnalysis.Razor.Compiler\" + Razor + @"\Components\Counter_razor.g.cs", "Components/Counter_razor.g.cs")]
    [InlineData("/r/obj/Debug/net8.0/Microsoft.NET.Sdk.Razor.SourceGenerators/" + Razor + "/Pages/Index_cshtml.g.cs", "Pages/Index_cshtml.g.cs")]
    [InlineData("/r/obj/Debug/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/" + Razor + "/_Imports_razor.g.cs", "_Imports_razor.g.cs")]
    public void DocumentOfTheRazorGenerator_IsRazor(string filePath, string hintName) =>
        RazorGenerator.IsRazorDocument(filePath, hintName).Should().BeTrue();

    [Theory]
    [InlineData("/r/obj/Debug/net10.0/Gen.Generator/Gen.Generator.RegistrationGenerator/Registrations.cs", "Registrations.cs")]
    [InlineData("/r/obj/Debug/net10.0/Other/Other.Generator/Components/Counter_razor.g.cs", "Components/Counter_razor.g.cs")]
    [InlineData("/r/obj/Debug/net10.0/Other/Other.Generator/" + Razor + "/Counter_razor.g.cs", "" + Razor + "/Counter_razor.g.cs")]
    [InlineData("Counter_razor.g.cs", "Counter_razor.g.cs")]
    [InlineData("", "Counter_razor.g.cs")]
    public void OtherDocument_IsNotRazor(string filePath, string hintName) =>
        RazorGenerator.IsRazorDocument(filePath, hintName).Should().BeFalse();
}
