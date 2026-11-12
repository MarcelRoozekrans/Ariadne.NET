using Parity.PAreAbstract.Target;

namespace Parity.PAreAbstract.Broken;

public abstract record OrderRecord
{
    public Repository? Store { get; init; }
}
