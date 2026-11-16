using Parity.COnlyHaveAttributes.Contracts;

namespace Parity.COnlyHaveAttributes.Clean;

[Audited]
public class Draft
{
    // Two nullable members, so the compiler puts NullableContextAttribute and NullableAttribute on the class itself.
    // With one, it puts NullableAttribute on the field and the class carries only Audited in IL.
    public string? Title;

    public string? Summary;
}
