using System;

namespace Parity.CAttributeBeSealed.Clean;

public sealed class TracedAttribute : Attribute
{
    public int Total { get; set; }
}
