using System;
using Parity.PAttributeAreSealed.Target;

namespace Parity.PAttributeAreSealed.NotBroken;

public class AuditAttribute : Attribute
{
    public Repository? Store { get; set; }
}
