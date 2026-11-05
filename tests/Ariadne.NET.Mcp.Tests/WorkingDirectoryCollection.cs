namespace Ariadne.NET.Mcp.Tests;

/// <summary>
/// Tests that change the process working directory. They run alone, after the parallel tests,
/// because <see cref="RuleResolutionHelper"/> reads <c>.architecture/</c> from the working directory.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkingDirectoryCollection
{
    public const string Name = "Working directory";
}
