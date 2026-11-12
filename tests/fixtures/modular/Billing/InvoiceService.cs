using Modular.Billing.Public;
using Modular.Orders.Public;

namespace Modular.Billing.Core;

// Uses another module's Public API, which the modular-monolith pack allows.
public sealed class InvoiceService(IOrderLookup orders) : IInvoicing
{
    public decimal Invoice(int orderId) => orders.TotalOf(orderId);
}
