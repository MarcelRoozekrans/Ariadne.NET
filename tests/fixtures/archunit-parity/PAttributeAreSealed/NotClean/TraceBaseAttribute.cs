using System;

namespace Parity.PAttributeAreSealed.NotClean;

public abstract class TraceBaseAttribute : Attribute
{
    public int Level { get; set; }
}
