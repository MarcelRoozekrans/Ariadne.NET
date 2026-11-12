using System;

namespace Parity.PAttributeAreAbstract.Clean;

public abstract class TraceBaseAttribute : Attribute
{
    public int Level { get; set; }
}
