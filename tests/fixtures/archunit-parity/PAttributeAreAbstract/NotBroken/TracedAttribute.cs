using System;
using Parity.PAttributeAreAbstract.Target;

namespace Parity.PAttributeAreAbstract.NotBroken;

public sealed class TracedAttribute : Attribute
{
    public Repository? Store { get; set; }
}
