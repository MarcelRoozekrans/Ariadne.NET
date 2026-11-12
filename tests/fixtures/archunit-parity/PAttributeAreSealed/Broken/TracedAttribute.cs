using System;
using Parity.PAttributeAreSealed.Target;

namespace Parity.PAttributeAreSealed.Broken;

public sealed class TracedAttribute : Attribute
{
    public Repository? Store { get; set; }
}
