using Parity.COnlyDependOn.Forbidden;
using Parity.COnlyDependOn.Target;

namespace Parity.COnlyDependOn.Broken;

public class OrderService
{
    public Repository? Store;

    public Mailer? Outbox;
}
