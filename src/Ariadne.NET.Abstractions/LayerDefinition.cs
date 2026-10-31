using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class LayerDefinition
{
    [NotEmpty] public required string Name { get; init; }
    public string Description { get; init; } = "";
    public IList<string> AllowedDependencies { get; init; } = [];
}
