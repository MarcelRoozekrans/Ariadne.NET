namespace Modular.Orders.Public;

public interface IOrderLookup
{
    decimal TotalOf(int orderId);
}
