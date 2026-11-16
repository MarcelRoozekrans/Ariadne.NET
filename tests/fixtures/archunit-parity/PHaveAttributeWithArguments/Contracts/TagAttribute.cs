using System;

namespace Parity.PHaveAttributeWithArguments.Contracts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TagAttribute : Attribute
{
    public TagAttribute(string name, int order)
    {
        Name = name;
        Order = order;
    }

    public string Name { get; }

    public int Order { get; }

    public string? Area { get; set; }

    public bool Audited { get; set; }
}
