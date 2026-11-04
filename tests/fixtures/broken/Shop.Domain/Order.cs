using Shop.Infrastructure;

namespace Shop.Domain;

public class Order
{
    private readonly SqlStore _store = new SqlStore();

    public void Save() => _store.Save(this);
}
