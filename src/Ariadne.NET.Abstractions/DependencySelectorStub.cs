using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>
/// Stands in for the nested validator of <c>dependsOn</c> and <c>onlyDependsOn</c>. The generator would hand
/// <c>TargetSelectorValidator</c> a validator of its own type for these properties, which cannot be built, so the
/// partial constructors pass this one and <see cref="DependencySelectorCheck"/> does the work in a <c>[Must]</c> rule.
/// </summary>
internal sealed class DependencySelectorStub : ValidatorFor<TargetSelector>
{
    public static DependencySelectorStub Instance { get; } = new();

    public override ValidationResult Validate(TargetSelector instance) => default;
}
