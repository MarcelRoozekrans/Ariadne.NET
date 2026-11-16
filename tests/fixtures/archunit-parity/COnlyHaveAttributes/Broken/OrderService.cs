using Parity.COnlyHaveAttributes.Contracts;
using Parity.COnlyHaveAttributes.Other;

namespace Parity.COnlyHaveAttributes.Broken;

[Audited]
[Tag]
public class OrderService
{
    public int Total { get; set; }
}
