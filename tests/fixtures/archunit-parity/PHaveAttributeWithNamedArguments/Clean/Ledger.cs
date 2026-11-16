using Parity.PHaveAttributeWithNamedArguments.Contracts;

namespace Parity.PHaveAttributeWithNamedArguments.Clean;

[Tag("ledger", 2, Area = "billing", Audited = true)]
public class Ledger
{
    public int Total { get; set; }
}
