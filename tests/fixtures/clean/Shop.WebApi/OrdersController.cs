using Shop.Domain;

namespace Shop.WebApi;

public class OrdersController
{
    public Order Get(int id) => new() { Id = id };
}
