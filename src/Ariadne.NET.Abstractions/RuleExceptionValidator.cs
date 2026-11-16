namespace Ariadne.NET.Abstractions;

// The generated constructor wants a validator of TargetSelector for dependsOn and onlyDependsOn.
// The stub keeps the call sites as they were; the [Must] rules on the properties do the check.
public sealed partial class RuleExceptionValidator
{
    public RuleExceptionValidator(ZeroAlloc.Validation.ValidatorFor<AttributeArgumentMatch> attributeArgumentsValidator)
        : this(attributeArgumentsValidator, DependencySelectorStub.Instance, DependencySelectorStub.Instance)
    {
    }
}
