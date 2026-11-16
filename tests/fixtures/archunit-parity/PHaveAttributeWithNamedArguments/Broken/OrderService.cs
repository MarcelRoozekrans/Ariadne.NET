using Parity.PHaveAttributeWithNamedArguments.Contracts;
using Parity.PHaveAttributeWithNamedArguments.Target;

namespace Parity.PHaveAttributeWithNamedArguments.Broken;

[Tag("orders", 1, Area = "billing", Audited = true)]
public class OrderService
{
    public Repository? Store;
}
