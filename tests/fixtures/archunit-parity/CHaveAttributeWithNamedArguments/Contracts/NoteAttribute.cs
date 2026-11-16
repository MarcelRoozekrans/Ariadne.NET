using System;

namespace Parity.CHaveAttributeWithNamedArguments.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NoteAttribute : Attribute
{
    public string? Area { get; set; }
}
