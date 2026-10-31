using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class ComponentRoleDefinition
{
    [NotEmpty] public required string Name { get; init; }
    [NotEmpty] public required string Layer { get; init; }
    public CSharpTypeKind TypeKind { get; init; } = CSharpTypeKind.Class;
    public bool RequiresInterface { get; init; }
    public string? InterfaceRole { get; init; }
    [NotNull] public string Accessibility { get; init; } = "public";
    public string Description { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<string> AlternativeNames { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => items is null || !items.Any(static i => i is null);
}
