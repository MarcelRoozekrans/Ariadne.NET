using System;

namespace Parity.CAttributeBeAbstract.Broken;

public sealed class AuditAttribute : Attribute
{
    public int Total { get; set; }
}
