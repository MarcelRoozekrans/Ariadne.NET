namespace Ariadne.NET.Abstractions;

/// <summary>
/// Checks the selector under <c>dependsOn</c> and <c>onlyDependsOn</c> by hand: the generated validator does not
/// validate a nested <see cref="TargetSelector"/> property, and one that did would need itself in its constructor.
/// </summary>
internal static class DependencySelectorCheck
{
    /// <summary>The validation message for a bad <c>dependsOn</c> or <c>onlyDependsOn</c>, on a selector and on an exception.</summary>
    public const string Message =
        "{PropertyName} must select something, must not hold members, dependsOn or onlyDependsOn, and must not mix layers with namespace, assembly or type globs.";

    public static bool IsValid(TargetSelector nested)
    {
        if (nested.Members.Count > 0 || nested.DependsOn is not null || nested.OnlyDependsOn is not null)
        {
            return false;
        }

        if (nested.Layers.Count > 0 && (nested.Namespaces.Count > 0 || nested.Assemblies.Count > 0 || nested.Types.Count > 0
                || nested.BaseTypes.Count > 0 || nested.Interfaces.Count > 0 || nested.Attributes.Count > 0
                || nested.AttributeArguments.Count > 0 || nested.OnlyAttributes.Count > 0))
        {
            return false;
        }

        if (nested is
            {
                Layers.Count: 0, Namespaces.Count: 0, Assemblies.Count: 0, Types.Count: 0, BaseTypes.Count: 0, Interfaces.Count: 0,
                Attributes.Count: 0, AttributeArguments.Count: 0, OnlyAttributes.Count: 0, Names.Count: 0, Kinds.Count: 0,
                Accessibility.Count: 0, AssignableTo.Count: 0, Sealed: null, Abstract: null, Static: null, Nested: null,
            })
        {
            return false;
        }

        return new TargetSelectorValidator(new AttributeArgumentMatchValidator()).Validate(nested).IsValid;
    }
}
