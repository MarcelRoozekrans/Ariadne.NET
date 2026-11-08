using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class ComponentRoleDefinition
{
    [NotEmpty] public required string Name { get; init; }
    [NotEmpty] public required string Layer { get; init; }
    [IsInEnum] public CSharpTypeKind TypeKind { get; init; } = CSharpTypeKind.Class;
    public bool RequiresInterface { get; init; }
    public string? InterfaceRole { get; init; }
    [NotNull] public string Accessibility { get; init; } = "public";
    public string Description { get; init; } = "";

    /// <summary>
    /// When <see langword="true"/>, the role is allowed to match no types without being reported as vacuous.
    /// </summary>
    public bool AllowEmpty { get; init; }
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> AlternativeNames { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
