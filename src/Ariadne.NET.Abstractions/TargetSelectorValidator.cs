namespace Ariadne.NET.Abstractions;

// The generated constructor wants a validator of the same type for dependsOn and onlyDependsOn.
// This one builds the validator with the stub, so existing call sites keep their shape.
public sealed partial class TargetSelectorValidator
{
    public TargetSelectorValidator(ZeroAlloc.Validation.ValidatorFor<AttributeArgumentMatch> attributeArgumentsValidator)
        : this(attributeArgumentsValidator, DependencySelectorStub.Instance, DependencySelectorStub.Instance)
    {
    }
}
