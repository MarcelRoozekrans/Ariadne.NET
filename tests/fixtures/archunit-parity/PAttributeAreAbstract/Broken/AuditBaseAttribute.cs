using System;
using Parity.PAttributeAreAbstract.Target;

namespace Parity.PAttributeAreAbstract.Broken;

public abstract class AuditBaseAttribute : Attribute
{
    public Repository? Store { get; set; }
}
