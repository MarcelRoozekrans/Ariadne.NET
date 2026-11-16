using Parity.POnlyHaveAttributes.Contracts;
using Parity.POnlyHaveAttributes.Target;

namespace Parity.POnlyHaveAttributes.Broken;

[Audited]
public class Draft
{
    // Two nullable members, so the compiler puts NullableContextAttribute and NullableAttribute on the class itself.
    // With one, it puts NullableAttribute on the field and the class carries only Audited in IL.
    public string? Title;

    public string? Summary;

    public Repository Store;
}
