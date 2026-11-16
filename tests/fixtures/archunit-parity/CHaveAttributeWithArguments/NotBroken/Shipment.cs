using Parity.CHaveAttributeWithArguments.Contracts;

namespace Parity.CHaveAttributeWithArguments.NotBroken;

[Tag("billing", 1)]
[Tag("shipping", 7)]
public class Shipment
{
    public int Total { get; set; }
}
