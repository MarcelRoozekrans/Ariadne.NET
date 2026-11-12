using System;

namespace Parity.CAttributeBeSealed.NotClean;

public abstract class TraceBaseAttribute : Attribute
{
    public int Total { get; set; }
}
