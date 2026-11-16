using Parity.CHaveAttributeWithNamedArguments.Contracts;

namespace Parity.CHaveAttributeWithNamedArguments.NotBroken;

[Tag("shipments", 2, Area = "billing")]
[Tag("shipments", 5, Audited = true)]
public class Shipment
{
    public int Total { get; set; }
}
