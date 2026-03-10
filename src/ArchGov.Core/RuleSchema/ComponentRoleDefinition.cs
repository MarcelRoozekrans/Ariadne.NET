namespace ArchGov.Core.RuleSchema;

public sealed class ComponentRoleDefinition
{
    public required string Name { get; init; }
    public required string Layer { get; init; }
    public CSharpTypeKind TypeKind { get; init; } = CSharpTypeKind.Class;
    public bool RequiresInterface { get; init; }
    public string? InterfaceRole { get; init; }
    public string Accessibility { get; init; } = "public";
    public string Description { get; init; } = "";
    public List<string> AlternativeNames { get; init; } = [];
}
