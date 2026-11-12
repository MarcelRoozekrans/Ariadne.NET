using System;

namespace Parity.CAttributeBeAbstract.Clean;

public abstract class TraceBaseAttribute : Attribute
{
    public int Total { get; set; }
}
