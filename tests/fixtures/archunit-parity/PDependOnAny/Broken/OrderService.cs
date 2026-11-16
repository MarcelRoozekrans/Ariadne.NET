using Parity.PDependOnAny.Forbidden;
using Parity.PDependOnAny.Target;

namespace Parity.PDependOnAny.Broken;

public class OrderService
{
    public Repository? Store;

    public Mailer? Outbox;
}
