namespace Parity.CBeInternal.NotBroken;

public class Order
{
    public int Total { get; set; }

    internal class Line
    {
        public int Quantity { get; set; }
    }
}
