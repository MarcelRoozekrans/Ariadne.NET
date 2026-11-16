using Parity.CHaveAttributeWithNamedArguments.Contracts;

namespace Parity.CHaveAttributeWithNamedArguments.Broken;

[Tag("orders", 3, Area = "billing")]
[Tag("orders", 4, Audited = true)]
public class OrderService
{
    public int Total { get; set; }
}
