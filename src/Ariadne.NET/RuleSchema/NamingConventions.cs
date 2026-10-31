namespace Ariadne.NET.RuleSchema;

public sealed class NamingConventions
{
    public string Namespace { get; init; } = "{SolutionName}.{Feature}.{Layer}";
    public string FilePath { get; init; } = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs";
}
