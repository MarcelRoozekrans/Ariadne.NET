namespace Ariadne.NET.Abstractions;

public sealed class LayerDefinition
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public IList<string> AllowedDependencies { get; init; } = [];
}
