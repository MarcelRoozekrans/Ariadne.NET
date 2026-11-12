using System.Collections.Generic;

namespace Modular.Billing.Infrastructure;

// Covers the pack's Module.Infrastructure layer, so the fixture has no vacuous layer.
public sealed class InvoiceStore
{
    private readonly Dictionary<int, decimal> _amounts = [];

    public void Save(int orderId, decimal amount) => _amounts[orderId] = amount;
}
