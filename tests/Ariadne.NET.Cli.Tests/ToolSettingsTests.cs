using System.Xml.Linq;
using FluentAssertions;

namespace Ariadne.NET.Cli.Tests;

// Phase 3.4's workflows run `dotnet tool install Ariadne.NET.Cli` and call `ariadne`, so these names are a contract.
public sealed class ToolSettingsTests
{
    [Fact]
    public void Cli_project_is_packed_as_the_ariadne_tool()
    {
        var project = XDocument.Load(FindCliProject());

        Property(project, "PackAsTool").Should().Be("true");
        Property(project, "ToolCommandName").Should().Be("ariadne");
        Property(project, "PackageId").Should().Be("Ariadne.NET.Cli");
    }

    private static string? Property(XDocument project, string name) =>
        project.Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();

    private static string FindCliProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Ariadne.NET.slnx")))
            {
                return Path.Combine(dir.FullName, "src", "Ariadne.NET.Cli", "Ariadne.NET.Cli.csproj");
            }
        }

        throw new InvalidOperationException("The folder holding src/Ariadne.NET.slnx was not found.");
    }
}
