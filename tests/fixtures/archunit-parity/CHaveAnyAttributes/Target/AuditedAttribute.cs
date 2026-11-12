using System;

namespace Parity.CHaveAnyAttributes.Target;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute
{
}
