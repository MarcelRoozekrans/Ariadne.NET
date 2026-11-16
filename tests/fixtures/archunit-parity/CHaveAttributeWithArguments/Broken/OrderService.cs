using Parity.CHaveAttributeWithArguments.Contracts;

namespace Parity.CHaveAttributeWithArguments.Broken;

[Tag("billing", 1)]
[Tag("shipping", 7)]
public class OrderService
{
    public int Total { get; set; }
}
