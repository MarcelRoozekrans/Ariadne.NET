using System;

namespace Parity.PAttributeAreSealed.Clean;

public sealed class NoteAttribute : Attribute
{
    public int Level { get; set; }
}
