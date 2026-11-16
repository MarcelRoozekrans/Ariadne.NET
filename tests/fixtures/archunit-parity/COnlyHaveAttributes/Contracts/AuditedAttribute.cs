using System;

namespace Parity.COnlyHaveAttributes.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute
{
}
