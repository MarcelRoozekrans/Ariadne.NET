using Modular.Billing.Core;
using Modular.Orders.Core;

namespace Modular.Host;

// The host is in no slice, so it may use every module's Core.
public sealed class Startup(OrderService orders, InvoiceService invoices)
{
    public decimal Run(int orderId) => orders.Checkout(orderId) + invoices.Invoice(orderId);
}
