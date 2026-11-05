using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class CodeFeatureRuleDefinition
{
    [NotEmpty] public required string Id { get; init; }
    public required string Description { get; init; }
    public IDictionary<string, object> AppliesTo { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
    public bool Locked { get; init; }
}
