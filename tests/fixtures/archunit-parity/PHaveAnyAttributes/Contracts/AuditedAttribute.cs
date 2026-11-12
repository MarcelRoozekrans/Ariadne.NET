using System;

namespace Parity.PHaveAnyAttributes.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute
{
}
