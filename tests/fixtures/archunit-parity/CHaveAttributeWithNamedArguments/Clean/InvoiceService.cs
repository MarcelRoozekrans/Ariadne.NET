using Parity.CHaveAttributeWithNamedArguments.Contracts;

namespace Parity.CHaveAttributeWithNamedArguments.Clean;

[Tag("invoices", 1, Area = "billing", Audited = true)]
public class InvoiceService
{
    public int Total { get; set; }
}
