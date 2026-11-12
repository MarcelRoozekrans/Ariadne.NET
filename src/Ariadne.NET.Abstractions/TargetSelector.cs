using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>Selects code by layer, namespace, assembly, type, base type, interface or attribute.</summary>
[Validate]
public sealed class TargetSelector
{
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Layers { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Namespaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Assemblies { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Types { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> BaseTypes { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Interfaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Attributes { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Members { get; init; } = [];

    /// <summary>Flat globs (<c>Glob.Flat</c>) over the innermost simple name of the type, without arity. Any one may match.</summary>
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Names { get; init; } = [];
    /// <summary>Type kinds the type may have: class, interface, record, struct, record struct, enum or delegate. Any one may match.</summary>
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<CSharpTypeKind> Kinds { get; init; } = [];
    /// <summary>Accessibilities the type may have: <c>public</c>, <c>internal</c>, <c>protected</c>, <c>private</c>, <c>protected internal</c> or <c>private protected</c>. Any one may match.</summary>
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Accessibility { get; init; } = [];
    /// <summary>Type patterns in the <c>types</c> grammar, matched against the type itself, every class in its base chain including <c>System.Object</c>, and every interface it implements. Any one may match.</summary>
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> AssignableTo { get; init; } = [];
    /// <summary><c>true</c> matches a class or record class declared <c>sealed</c> and a static class; <c>false</c> matches everything else. Every other kind is not sealed.</summary>
    public bool? Sealed { get; init; }
    /// <summary><c>true</c> matches a class or record class declared <c>abstract</c>, never a static class; <c>false</c> matches everything else. Every other kind is not abstract.</summary>
    public bool? Abstract { get; init; }
    /// <summary><c>true</c> matches a static class; <c>false</c> matches everything else.</summary>
    public bool? Static { get; init; }
    /// <summary><c>true</c> matches a type declared inside another type; <c>false</c> matches a top-level type.</summary>
    public bool? Nested { get; init; }

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
