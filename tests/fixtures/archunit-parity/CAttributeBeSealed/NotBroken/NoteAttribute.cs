using System;

namespace Parity.CAttributeBeSealed.NotBroken;

public sealed class NoteAttribute : Attribute
{
    public int Total { get; set; }
}
