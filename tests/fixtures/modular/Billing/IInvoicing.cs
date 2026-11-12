namespace Modular.Billing.Public;

public interface IInvoicing
{
    decimal Invoice(int orderId);
}
