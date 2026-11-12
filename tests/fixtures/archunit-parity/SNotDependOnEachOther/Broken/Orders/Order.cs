namespace Parity.SNotDependOnEachOther.Broken.Orders;

public class Order
{
    public Parity.SNotDependOnEachOther.Broken.Billing.Invoice? Invoice { get; set; }

    public Parity.SNotDependOnEachOther.Broken.Orders.Internal.Helper? Helper { get; set; }
}
