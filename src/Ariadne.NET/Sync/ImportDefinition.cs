namespace Ariadne.NET.Sync;

public sealed class ImportDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Url { get; init; }
    public required string Version { get; init; }
    public string Path { get; init; } = "";
}
