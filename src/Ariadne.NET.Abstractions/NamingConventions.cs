using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class NamingConventions
{
    [NotEmpty] public string Namespace { get; init; } = "{SolutionName}.{Feature}.{Layer}";
    [NotEmpty] public string FilePath { get; init; } = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs";
}
