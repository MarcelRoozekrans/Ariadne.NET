namespace Multi.Domain;

public class Order
{
#if NET10_0_OR_GREATER
    public Multi.Infrastructure.SqlStore? Store { get; init; }
#endif
}
