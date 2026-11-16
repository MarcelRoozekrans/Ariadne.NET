using System;

namespace Parity.POnlyHaveAttributes.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute
{
}
