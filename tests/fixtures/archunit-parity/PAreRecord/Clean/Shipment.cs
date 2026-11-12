using Parity.PAreRecord.Target;

namespace Parity.PAreRecord.Clean;

public record struct Shipment
{
    public Repository? Store { get; set; }
}
