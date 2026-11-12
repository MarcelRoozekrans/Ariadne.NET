using System;

namespace Parity.CAttributeBeAbstract.NotBroken;

public abstract class NoteBaseAttribute : Attribute
{
    public int Total { get; set; }
}
