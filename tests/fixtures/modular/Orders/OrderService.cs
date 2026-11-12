using Modular.Billing.Core;
using Modular.Orders.Public;

namespace Modular.Orders.Core;

// Uses its own module's Public API, which is allowed, and the Billing module's Core, which is the violation.
public sealed class OrderService(IOrderLookup lookup, InvoiceService invoices)
{
    public decimal Checkout(int orderId) => lookup.TotalOf(orderId) + invoices.Invoice(orderId);
}
