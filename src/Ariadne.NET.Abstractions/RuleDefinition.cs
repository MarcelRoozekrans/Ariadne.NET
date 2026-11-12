using System.Text.Json.Serialization;
using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>A dependency rule between two selections of code.</summary>
[Validate]
public sealed class RuleDefinition
{
    [NotEmpty] public required string Id { get; init; }
    [NotEmpty] public required string Because { get; init; }
    public string Description { get; init; } = "";
    [IsInEnum] public required RuleKind Kind { get; init; }
    [NotNull] public required TargetSelector From { get; init; }
    [NotNull] public TargetSelector To { get; init; } = new TargetSelector();
    public RuleRequirement? Require { get; init; }
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Packages { get; init; } = [];
    [NotNull] public TargetSelector Against { get; init; } = new TargetSelector();
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<RuleException> Exceptions { get; init; } = [];
    public bool Locked { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the rule is allowed to match nothing without being reported as vacuous.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowEmpty { get; init; }

    /// <summary>
    /// The slices of a <see cref="RuleKind.Slices"/> rule: namespace patterns with one capture each, <c>(*)</c> for exactly
    /// one segment or <c>(**)</c> for one or more, and whether the slices must be free of cycles or independent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SliceDefinition? Slices { get; init; }

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
