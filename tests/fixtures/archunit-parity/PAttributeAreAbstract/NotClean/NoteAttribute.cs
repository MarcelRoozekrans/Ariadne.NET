using System;

namespace Parity.PAttributeAreAbstract.NotClean;

public sealed class NoteAttribute : Attribute
{
    public int Level { get; set; }
}
