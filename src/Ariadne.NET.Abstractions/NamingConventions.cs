using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class NamingConventions
{
    public string Namespace { get; init; } = "{SolutionName}.{Feature}.{Layer}";
    public string FilePath { get; init; } = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs";
}
