using System;

namespace Parity.CAttributeBeSealed.Broken;

public class AuditAttribute : Attribute
{
    public int Total { get; set; }
}
